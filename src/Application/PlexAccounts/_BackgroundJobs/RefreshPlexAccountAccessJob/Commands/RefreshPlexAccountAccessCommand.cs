namespace Reaparr.Application;

/// <summary>
/// Validates enabled Plex accounts and refreshes their accessible Plex servers and libraries.
/// Pass a Plex account id to refresh only that account, or zero to refresh all enabled accounts.
/// </summary>
public record RefreshPlexAccountAccessCommand(int PlexAccountId = 0, TelemetryTrigger Trigger = TelemetryTrigger.Manual)
    : ICommand<Result<List<RefreshPlexAccountAccessRapportDTO>>>;

public class RefreshPlexAccountAccessCommandValidator : AbstractValidator<RefreshPlexAccountAccessCommand>
{
    public RefreshPlexAccountAccessCommandValidator()
    {
        RuleFor(x => x.PlexAccountId).GreaterThanOrEqualTo(0);
    }
}

public class RefreshPlexAccountAccessCommandHandler
    : ICommandHandler<RefreshPlexAccountAccessCommand, Result<List<RefreshPlexAccountAccessRapportDTO>>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly INotificationHubService _notificationHubService;
    private readonly ITelemetry _telemetry;

    public RefreshPlexAccountAccessCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        INotificationHubService notificationHubService,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<RefreshPlexAccountAccessCommandHandler>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _notificationHubService = notificationHubService;
        _telemetry = telemetry;
    }

    public async Task<Result<List<RefreshPlexAccountAccessRapportDTO>>> ExecuteAsync(
        RefreshPlexAccountAccessCommand command,
        CancellationToken cancellationToken
    )
    {
        var plexAccountsQuery = _dbContext.PlexAccounts.AsTracking().AsQueryable();
        plexAccountsQuery =
            command.PlexAccountId > 0
                ? plexAccountsQuery.Where(x => x.Id == command.PlexAccountId)
                : plexAccountsQuery.Where(x => x.IsEnabled);

        var plexAccounts = await plexAccountsQuery.ToListAsync(cancellationToken);
        if (!plexAccounts.Any())
        {
            _log.Here().Warning("No enabled Plex accounts found to refresh Plex access");
            return Result.Ok(new List<RefreshPlexAccountAccessRapportDTO>());
        }

        var rapports = new List<RefreshPlexAccountAccessRapportDTO>();
        var errors = new List<IError>();
        foreach (var plexAccount in plexAccounts)
        {
            var validationResult = await _commandExecutor.Send(
                new ValidatePlexTokenCommand(plexAccount.GetAuthToken),
                cancellationToken
            );

            if (validationResult.IsCancelled)
            {
                _telemetry.PlexAccessRefreshValidationFinished(plexAccount.Id, command.Trigger, validationResult);
                return validationResult.ToResult();
            }

            if (validationResult.HasPlex401UnauthorizedError())
            {
                plexAccount.IsValidated = false;
                plexAccount.ValidatedAt = null;
                await _dbContext.SaveChangesAsync(cancellationToken);
                _log.Here()
                    .Warning(
                        "Plex account {PlexAccountName} has an invalid token; marking it invalid and revoking access",
                        plexAccount.DisplayName
                    );

                var revokeResult = await RevokeAllAccess(plexAccount, cancellationToken);
                if (revokeResult.IsCancelled)
                {
                    _telemetry.PlexAccessRefreshFinished(plexAccount.Id, command.Trigger, revokeResult);
                    return revokeResult.ToResult();
                }
                if (revokeResult.IsFailed)
                {
                    revokeResult.LogError();
                    errors.AddRange(revokeResult.Errors);
                    _telemetry.PlexAccessRefreshFinished(plexAccount.Id, command.Trigger, revokeResult);
                    continue;
                }

                _telemetry.PlexAccessRefreshFinished(plexAccount.Id, command.Trigger, revokeResult, revokeResult.Value);
                rapports.Add(revokeResult.Value);
                continue;
            }

            if (validationResult.IsFailed)
            {
                validationResult.LogError();
                errors.AddRange(validationResult.Errors);
                _telemetry.PlexAccessRefreshValidationFinished(plexAccount.Id, command.Trigger, validationResult);
                continue;
            }

            plexAccount.IsValidated = true;
            plexAccount.ValidatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var serverAccessResult = await _commandExecutor.Send(
                new RefreshPlexServerAccessCommand(plexAccount.Id),
                cancellationToken
            );

            if (serverAccessResult.IsCancelled)
            {
                _telemetry.PlexAccessRefreshFinished(plexAccount.Id, command.Trigger, serverAccessResult);
                return serverAccessResult.ToResult();
            }

            if (serverAccessResult.IsFailed)
            {
                serverAccessResult.LogError();
                errors.AddRange(serverAccessResult.Errors);
                _telemetry.PlexAccessRefreshFinished(plexAccount.Id, command.Trigger, serverAccessResult);
                continue;
            }

            var serverAccessRapport = serverAccessResult.Value;
            var lostServerIds = serverAccessRapport
                .Access.Where(x => x.State == PlexAccessState.Revoked)
                .Select(x => x.PlexServerId)
                .ToList();

            var lostLibraryIds = await _dbContext
                .PlexAccountLibraries.Where(x =>
                    x.PlexAccountId == plexAccount.Id && lostServerIds.Contains(x.PlexServerId)
                )
                .Select(x => x.PlexLibraryId)
                .Distinct()
                .ToListAsync(cancellationToken);

            await _dbContext
                .PlexAccountLibraries.Where(x =>
                    x.PlexAccountId == plexAccount.Id && lostServerIds.Contains(x.PlexServerId)
                )
                .ExecuteDeleteAsync(cancellationToken);

            var cancelResult = await CancelSyncJobsWithoutEnabledAccess(lostLibraryIds, cancellationToken);
            if (cancelResult.IsCancelledOrFailed)
            {
                _telemetry.PlexAccessRefreshFinished(plexAccount.Id, command.Trigger, cancelResult);
                return cancelResult.LogIfFailed().ToResult<List<RefreshPlexAccountAccessRapportDTO>>();
            }

            var libraryAccessResult = await _commandExecutor.Send(
                new RefreshLibraryAccessCommand(plexAccount.Id),
                cancellationToken
            );

            if (libraryAccessResult.IsCancelled)
            {
                _telemetry.PlexAccessRefreshFinished(plexAccount.Id, command.Trigger, libraryAccessResult);
                return libraryAccessResult.ToResult();
            }

            if (libraryAccessResult.IsFailed)
            {
                libraryAccessResult.LogError();
                errors.AddRange(libraryAccessResult.Errors);
                _telemetry.PlexAccessRefreshFinished(plexAccount.Id, command.Trigger, libraryAccessResult);
                continue;
            }

            var rapport = ToDTO(serverAccessRapport, libraryAccessResult.Value);
            _telemetry.PlexAccessRefreshFinished(plexAccount.Id, command.Trigger, libraryAccessResult, rapport);
            rapports.Add(rapport);
        }

        await _notificationHubService.SendRefreshNotificationAsync([
            RefreshDataType.PlexAccount,
            RefreshDataType.PlexServer,
            RefreshDataType.PlexServerConnection,
            RefreshDataType.PlexLibrary,
        ]);

        var rebuildResult = await _commandExecutor.Send(new QueueMediaOverviewRebuildCommand(), cancellationToken);
        rebuildResult.LogIfFailed();

        return Result.Ok(rapports).WithErrors(errors);
    }

    private async Task<Result<RefreshPlexAccountAccessRapportDTO>> RevokeAllAccess(
        PlexAccount plexAccount,
        CancellationToken cancellationToken
    )
    {
        var plexServers = await _dbContext
            .PlexAccountServers.Include(x => x.PlexServer)
            .Where(x => x.PlexAccountId == plexAccount.Id)
            .Select(x => new { x.PlexServerId, x.PlexServer!.Name })
            .ToListAsync(cancellationToken);

        var serverAccessRapport = new RefreshPlexServerAccessRapport(plexAccount.Id, plexAccount.DisplayName);
        serverAccessRapport.Access.AddRange(
            plexServers.Select(x => new RefreshPlexServerAccessRapportRow(
                PlexAccessState.Revoked,
                x.PlexServerId,
                x.Name
            ))
        );

        var libraryAccess = await BuildRevokedLibraryAccessResponse(
            plexAccount,
            serverAccessRapport,
            cancellationToken
        );

        var transactionResult = await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                await ctx.BulkDeleteByIdsAsync(
                    libraryAccess.LostServerAccess,
                    (db, lostServerIds) =>
                        db.PlexAccountLibraries.Where(x =>
                            x.PlexAccountId == plexAccount.Id && lostServerIds.Contains(x.PlexServerId)
                        ),
                    txCt
                );
                await ctx.PlexAccountServers.Where(x => x.PlexAccountId == plexAccount.Id).ExecuteDeleteAsync(txCt);
            },
            cancellationToken
        );
        if (transactionResult.IsCancelledOrFailed)
            return transactionResult.LogIfFailed().ToResult<RefreshPlexAccountAccessRapportDTO>();

        var cancelResult = await CancelSyncJobsWithoutEnabledAccess(
            libraryAccess.AffectedLibraryIds,
            cancellationToken
        );
        if (cancelResult.IsCancelledOrFailed)
            return cancelResult.LogIfFailed().ToResult<RefreshPlexAccountAccessRapportDTO>();

        return Result.Ok(ToDTO(serverAccessRapport, libraryAccess.Response));
    }

    private async Task<Result> CancelSyncJobsWithoutEnabledAccess(
        IReadOnlyCollection<int> affectedLibraryIds,
        CancellationToken cancellationToken
    )
    {
        if (affectedLibraryIds.Count == 0)
            return Result.Ok();

        var remainingEnabledLibraryIds = await _dbContext
            .PlexAccountLibraries.Where(x => affectedLibraryIds.Contains(x.PlexLibraryId) && x.PlexAccount!.IsEnabled)
            .Select(x => x.PlexLibraryId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var libraryId in affectedLibraryIds.Except(remainingEnabledLibraryIds))
        {
            var cancelResult = await _commandExecutor.Send(
                new CancelLibrarySyncJobCommand(libraryId),
                cancellationToken
            );
            if (cancelResult.IsCancelledOrFailed)
                return cancelResult.LogIfFailed();
        }
        return Result.Ok();
    }

    private async Task<(
        PlexLibraryAccessRefreshResponse Response,
        List<int> AffectedLibraryIds,
        List<int> LostServerAccess
    )> BuildRevokedLibraryAccessResponse(
        PlexAccount plexAccount,
        RefreshPlexServerAccessRapport serverAccessRapport,
        CancellationToken cancellationToken
    )
    {
        var lostServerAccess = serverAccessRapport
            .Access.Where(x => x.State == PlexAccessState.Revoked)
            .Select(x => x.PlexServerId)
            .ToList();

        var lostLibraryAccess = await _dbContext
            .PlexAccountLibraries.IgnoreQueryFilters()
            .Include(x => x.PlexLibrary)
            .Include(x => x.PlexServer)
            .Where(x => x.PlexAccountId == plexAccount.Id && lostServerAccess.Contains(x.PlexServerId))
            .ToListAsync(cancellationToken);

        var response = new PlexLibraryAccessRefreshResponse
        {
            Reports = lostLibraryAccess
                .Select(x =>
                    new PlexLibraryAccessRapport(
                        plexAccount.DisplayName,
                        x.PlexServerId,
                        x.PlexServer!.Name
                    ).AddRevoked(x.PlexLibraryId, x.PlexLibrary!.Name)
                )
                .ToList(),
            OfflineServers = [],
        };

        var affectedLibraryIds = lostLibraryAccess.Select(x => x.PlexLibraryId).Distinct().ToList();
        return (response, affectedLibraryIds, lostServerAccess);
    }

    private static RefreshPlexAccountAccessRapportDTO ToDTO(
        RefreshPlexServerAccessRapport serverAccessRapport,
        PlexLibraryAccessRefreshResponse libraryAccessRapport
    ) =>
        new(serverAccessRapport.PlexAccountId, serverAccessRapport.PlexAccountName)
        {
            Access = serverAccessRapport
                .Access.Select(x => new PlexServerAccessRapportDTO
                {
                    State = x.State,
                    PlexServerId = x.PlexServerId,
                    PlexServerName = x.PlexServerName,
                    IsServerOffline = libraryAccessRapport.OfflineServers.Contains(x.PlexServerId),
                    LibraryAccess = libraryAccessRapport
                        .Reports.Where(y => y.PlexServerId == x.PlexServerId)
                        .SelectMany(y => y.Data)
                        .Select(y => new PlexLibraryAccessRapportDTO
                        {
                            PlexLibraryName = y.PlexLibraryName,
                            PlexServerId = y.PlexServerId,
                            State = y.State,
                            PlexLibraryId = y.PlexLibraryId,
                        })
                        .ToList(),
                })
                .ToList(),
        };
}
