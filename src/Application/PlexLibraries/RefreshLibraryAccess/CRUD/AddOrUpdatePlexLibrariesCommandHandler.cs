namespace Reaparr.Application;

public record AddOrUpdatePlexLibrariesCommand : ICommand<Result<List<PlexLibraryAccessRapport>>>
{
    public required int PlexAccountId { get; init; }

    public required List<PlexLibrary> PlexLibraries { get; init; }
}

public class AddOrUpdatePlexLibrariesValidator : AbstractValidator<AddOrUpdatePlexLibrariesCommand>
{
    public AddOrUpdatePlexLibrariesValidator()
    {
        RuleFor(x => x.PlexAccountId).GreaterThan(0);
        RuleFor(x => x.PlexLibraries).NotEmpty();
        RuleForEach(x => x.PlexLibraries)
            .ChildRules(library =>
            {
                library.RuleFor(x => x.PlexServerId).GreaterThan(0);
                library.RuleFor(x => x.Title).NotEmpty();
                library.RuleFor(x => x.Uuid).NotEmpty();
            });
    }
}

public class AddOrUpdatePlexLibrariesCommandHandler
    : ICommandHandler<AddOrUpdatePlexLibrariesCommand, Result<List<PlexLibraryAccessRapport>>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public AddOrUpdatePlexLibrariesCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor
    )
    {
        _log = log.ForContext<AddOrUpdatePlexLibrariesCommandHandler>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<List<PlexLibraryAccessRapport>>> ExecuteAsync(
        AddOrUpdatePlexLibrariesCommand command,
        CancellationToken cancellationToken
    )
    {
        List<PlexLibraryAccessRapport> rapportList = [];
        List<PlexLibrary> newPlexLibraries = [];
        HashSet<int> changedPlexLibraryIds = [];

        var plexAccountId = command.PlexAccountId;

        var plexAccount = await _dbContext.PlexAccounts.GetAsync(plexAccountId, cancellationToken);

        if (plexAccount is null)
            return ResultExtensions.IsNull(nameof(plexAccount));

        var plexServerLibrariesDict = command
            .PlexLibraries.GroupBy(x => x.PlexServerId)
            .ToDictionary(group => group.Key, group => group.GroupBy(x => x.Uuid).Select(x => x.Last()).ToList());

        foreach (var (_, incomingPlexLibraries) in plexServerLibrariesDict)
        {
            foreach (var incomingPlexLibrary in incomingPlexLibraries)
            {
                var plexLibraryDb = await _dbContext
                    .PlexLibraries.IgnoreIsEnabledFilter()
                    .AsTracking()
                    .FirstOrDefaultAsync(
                        x => x.PlexServerId == incomingPlexLibrary.PlexServerId && x.Uuid == incomingPlexLibrary.Uuid,
                        cancellationToken
                    );

                if (plexLibraryDb is null)
                {
                    _log.Here()
                        .Debug("Adding PlexLibrary {PlexLibraryName} to the database", incomingPlexLibrary.Title);
                    await _dbContext.PlexLibraries.AddAsync(incomingPlexLibrary, cancellationToken);
                    newPlexLibraries.Add(incomingPlexLibrary);
                }
                else
                {
                    _log.Here()
                        .Debug(
                            "Updating PlexLibrary {PlexLibraryName} with id: {PlexLibraryId} in the database",
                            incomingPlexLibrary.Title,
                            plexLibraryDb.Id
                        );

                    incomingPlexLibrary.Id = plexLibraryDb.Id;

                    plexLibraryDb.Title = incomingPlexLibrary.Title;
                    plexLibraryDb.Key = incomingPlexLibrary.Key;
                    plexLibraryDb.CreatedAt = incomingPlexLibrary.CreatedAt;
                    plexLibraryDb.UpdatedAt = incomingPlexLibrary.UpdatedAt;

                    plexLibraryDb.ScannedAt = incomingPlexLibrary.ScannedAt;
                    plexLibraryDb.ContentChangedAt = incomingPlexLibrary.ContentChangedAt;
                    plexLibraryDb.Uuid = incomingPlexLibrary.Uuid;
                    plexLibraryDb.Language = incomingPlexLibrary.Language;

                    var contentChangedAfterLastSync =
                        incomingPlexLibrary.ContentChangedAt != plexLibraryDb.SyncedContentChangedAt;
                    plexLibraryDb.Outdated |= contentChangedAfterLastSync;
                    if (contentChangedAfterLastSync)
                        changedPlexLibraryIds.Add(plexLibraryDb.Id);
                }
            }

            // NOTE: We don't delete libraries here, access can be temporarily suspended due to missing PlexServer access or offline.
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Add or update the PlexAccount, PlexServer and PlexLibrary relationships
        _log.Here()
            .Information(
                "Adding, updating or removing the PlexAccount: {PlexAccountDisplayName} association with PlexLibraries now",
                plexAccount.DisplayName
            );

        foreach (var (plexServerId, incomingPlexLibraries) in plexServerLibrariesDict)
        {
            var currentPlexLibraryAccessList = await _dbContext
                .PlexAccountLibraries.Where(x => x.PlexAccountId == plexAccountId && x.PlexServerId == plexServerId)
                .ToListAsync(cancellationToken);

            // When a server is owned by an account, then mark all libraries as owned as well
            var ownedServerIds = await _dbContext
                .PlexAccountServers.Where(x => x.PlexAccountId == plexAccountId && x.IsServerOwned)
                .Select(x => x.PlexServerId)
                .ToListAsync(cancellationToken);

            var plexServerName = await _dbContext.GetPlexServerNameById(plexServerId);
            var rapport = FindOrCreate(rapportList, plexServerId, plexAccount.DisplayName, plexServerName);

            foreach (var incomingPlexLibrary in incomingPlexLibraries)
            {
                var plexLibraryId = incomingPlexLibrary.Id;

                // Check if this PlexAccount has been associated with the PlexLibrary already
                var hasAccess = currentPlexLibraryAccessList.Any(x =>
                    x.PlexAccountId == plexAccountId
                    && x.PlexLibraryId == plexLibraryId
                    && x.PlexServerId == incomingPlexLibrary.PlexServerId
                );

                if (!hasAccess)
                {
                    // Add entry
                    _log.Here()
                        .Debug(
                            "PlexAccount: {PlexAccountDisplayName} does not have an association with PlexLibrary: {PlexLibraryName} of PlexServer: {PlexServerName} creating one with the authentication token now",
                            plexAccount.DisplayName,
                            incomingPlexLibrary.Name,
                            plexServerName
                        );

                    await _dbContext.PlexAccountLibraries.AddAsync(
                        new PlexAccountLibrary
                        {
                            PlexAccountId = plexAccountId,
                            PlexLibraryId = plexLibraryId,
                            PlexServerId = incomingPlexLibrary.PlexServerId,
                            IsLibraryOwned = ownedServerIds.Contains(incomingPlexLibrary.PlexServerId),
                        },
                        cancellationToken
                    );

                    rapport.AddGranted(plexLibraryId, incomingPlexLibrary.Name);
                }
                else
                {
                    _log.Here()
                        .Debug(
                            "PlexAccount: {PlexAccountDisplayName} already has an association with PlexLibrary: {PlexLibraryName} of PlexServer: {PlexServerName} skipping for now",
                            plexAccount.DisplayName,
                            incomingPlexLibrary.Name,
                            plexServerName
                        );

                    rapport.AddUpdated(plexLibraryId, incomingPlexLibrary.Name);
                }
            }

            // Determine if PlexAccount lost access
            var lostLibraryIds = currentPlexLibraryAccessList
                .Select(x => x.PlexLibraryId)
                .Except(incomingPlexLibraries.Select(x => x.Id))
                .ToList();

            if (lostLibraryIds.Any())
            {
                await _dbContext
                    .PlexAccountLibraries.Where(x =>
                        x.PlexAccountId == plexAccountId
                        && x.PlexServerId == plexServerId
                        && lostLibraryIds.Contains(x.PlexLibraryId)
                    )
                    .ExecuteDeleteAsync(cancellationToken);

                foreach (var lostLibraryId in lostLibraryIds)
                {
                    var libraryName = await _dbContext.GetPlexLibraryNameById(lostLibraryId);
                    rapport.AddRevoked(lostLibraryId, libraryName);
                }
            }
        }

        await AddHistoryEventsAsync(rapportList, plexAccount, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var rapport in rapportList)
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            _log.Here().InformationMsg(rapport.ToString());

        var libraryIdsToSync = newPlexLibraries.Select(x => x.Id).Concat(changedPlexLibraryIds).Distinct().ToList();

        if (libraryIdsToSync.Count > 0)
        {
            var queueResult = await _commandExecutor.Send(
                new QueueLibrarySyncJobCommand(libraryIdsToSync),
                cancellationToken
            );
            if (queueResult.IsFailed)
                return queueResult.ToResult<List<PlexLibraryAccessRapport>>().LogError();
        }

        var newLibraryIds = newPlexLibraries.Select(x => x.Id).ToHashSet();
        var affectedLibraryIds = rapportList
            .SelectMany(x => x.Data)
            .Where(x => x.State is PlexAccessState.Granted or PlexAccessState.Revoked)
            .Select(x => x.PlexLibraryId)
            .Where(x => !newLibraryIds.Contains(x))
            .Distinct()
            .ToList();
        var invalidationResult = await _commandExecutor.Send(
            new InvalidateLibraryComparisonJobsCommand(affectedLibraryIds),
            cancellationToken
        );
        if (invalidationResult.IsFailed)
            return invalidationResult.ToResult<List<PlexLibraryAccessRapport>>().LogError();

        foreach (var libraryId in affectedLibraryIds)
        {
            var comparisonResult = await _commandExecutor.Send(
                new ScheduleAffectedLibraryComparisonJobsCommand(libraryId),
                cancellationToken
            );
            if (comparisonResult.IsFailed)
                return comparisonResult.ToResult<List<PlexLibraryAccessRapport>>().LogError();
        }

        return Result.Ok(rapportList);
    }

    private async Task AddHistoryEventsAsync(
        List<PlexLibraryAccessRapport> rapportList,
        PlexAccount plexAccount,
        CancellationToken cancellationToken
    )
    {
        var refreshRunId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        var updatedRows = rapportList
            .SelectMany(rapport =>
                rapport
                    .Data.Where(x => x.State == PlexAccessState.Updated)
                    .Select(row => new
                    {
                        rapport.PlexServerId,
                        row.PlexLibraryId,
                        row.PlexLibraryName,
                        rapport.PlexServerName,
                    })
            )
            .DistinctBy(x => new { x.PlexServerId, x.PlexLibraryId })
            .ToList();

        var existingHistoryKeys = new HashSet<(int PlexServerId, int PlexLibraryId)>();

        if (updatedRows.Count > 0)
        {
            var updatedServerIds = updatedRows.Select(x => x.PlexServerId).Distinct().ToList();
            var updatedLibraryIds = updatedRows.Select(x => x.PlexLibraryId).Distinct().ToList();

            var existingHistory = await _dbContext
                .PlexLibraryAccessHistoryEvents.Where(x =>
                    x.PlexAccountId == plexAccount.Id
                    && x.PlexServerId.HasValue
                    && x.PlexLibraryId.HasValue
                    && updatedServerIds.Contains(x.PlexServerId.Value)
                    && updatedLibraryIds.Contains(x.PlexLibraryId.Value)
                )
                .Select(x => new { x.PlexServerId, x.PlexLibraryId })
                .ToListAsync(cancellationToken);

            existingHistoryKeys = existingHistory
                .Select(x => (x.PlexServerId!.Value, x.PlexLibraryId!.Value))
                .ToHashSet();
        }

        foreach (var rapport in rapportList)
        {
            foreach (var row in rapport.Data.Where(x => x.State is PlexAccessState.Granted or PlexAccessState.Revoked))
            {
                await _dbContext.PlexLibraryAccessHistoryEvents.AddAsync(
                    new PlexLibraryAccessHistoryEvent
                    {
                        RefreshRunId = refreshRunId,
                        PlexAccountId = plexAccount.Id,
                        PlexAccountNameSnapshot = plexAccount.DisplayName,
                        PlexServerId = rapport.PlexServerId,
                        PlexServerNameSnapshot = rapport.PlexServerName,
                        PlexLibraryId = row.PlexLibraryId,
                        PlexLibraryNameSnapshot = row.PlexLibraryName,
                        State = row.State,
                        CreatedAt = createdAt,
                    },
                    cancellationToken
                );
            }
        }

        foreach (var updatedRow in updatedRows)
        {
            var historyKey = (updatedRow.PlexServerId, updatedRow.PlexLibraryId);
            if (existingHistoryKeys.Contains(historyKey))
                continue;

            await _dbContext.PlexLibraryAccessHistoryEvents.AddAsync(
                new PlexLibraryAccessHistoryEvent
                {
                    RefreshRunId = refreshRunId,
                    PlexAccountId = plexAccount.Id,
                    PlexAccountNameSnapshot = plexAccount.DisplayName,
                    PlexServerId = updatedRow.PlexServerId,
                    PlexServerNameSnapshot = updatedRow.PlexServerName,
                    PlexLibraryId = updatedRow.PlexLibraryId,
                    PlexLibraryNameSnapshot = updatedRow.PlexLibraryName,
                    State = PlexAccessState.Granted,
                    CreatedAt = DateTime.UtcNow,
                },
                cancellationToken
            );
        }
    }

    private static PlexLibraryAccessRapport FindOrCreate(
        List<PlexLibraryAccessRapport> rapportList,
        int plexServerId,
        string plexAccountName,
        string plexServerName
    )
    {
        var x = rapportList.FirstOrDefault(x => x.PlexServerId == plexServerId);
        if (x is not null)
            return x;

        rapportList.Add(new PlexLibraryAccessRapport(plexAccountName, plexServerId, plexServerName));
        return rapportList.Last();
    }
}
