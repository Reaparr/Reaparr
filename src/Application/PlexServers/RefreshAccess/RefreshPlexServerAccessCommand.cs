namespace Reaparr.Application;

/// <summary>
/// Retrieve the latest accessible <see cref="PlexServer">PlexServers</see> for this <see cref="PlexAccount"/> from the PlexAPI and stores it in the Database.
/// </summary>
/// <param name="PlexAccountId">The id of the <see cref="PlexAccount"/> to check.</param>
public record RefreshPlexServerAccessCommand(int PlexAccountId) : ICommand<Result<RefreshPlexServerAccessRapport>>;

public class RefreshPlexServerAccessCommandValidator : AbstractValidator<RefreshPlexServerAccessCommand>
{
    public RefreshPlexServerAccessCommandValidator()
    {
        RuleFor(x => x.PlexAccountId).GreaterThan(0);
    }
}

public class RefreshPlexServerAccessCommandHandler
    : ICommandHandler<RefreshPlexServerAccessCommand, Result<RefreshPlexServerAccessRapport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly INotificationHubService _notificationHubService;

    public RefreshPlexServerAccessCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        INotificationHubService notificationHubService
    )
    {
        _log = log.ForContext<RefreshPlexServerAccessCommandHandler>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _notificationHubService = notificationHubService;
    }

    public async Task<Result<RefreshPlexServerAccessRapport>> ExecuteAsync(
        RefreshPlexServerAccessCommand command,
        CancellationToken cancellationToken
    )
    {
        var plexAccountId = command.PlexAccountId;

        var plexAccountName = await _dbContext.GetPlexAccountDisplayName(plexAccountId, cancellationToken);

        _log.Here().Debug("Refreshing Plex servers access for PlexAccount: {PlexAccountName}", plexAccountName);

        var result = await _commandExecutor.Send(new GetAccessiblePlexServersCommand(plexAccountId), cancellationToken);

        // If the Plex API returns a 401 Unauthorized error, remove the PlexServerAccess and PlexLibraryAccess
        if (result.HasPlex401UnauthorizedError())
        {
            _log.Here()
                .Warning(
                    "Plex API returned 401 Unauthorized for PlexAccount: {PlexAccountDisplayName}",
                    plexAccountName
                );
            _log.Here()
                .Warning(
                    "Removing PlexServerAccess and LibraryAccess for PlexAccount: {PlexAccountDisplayName}",
                    plexAccountName
                );

            _log.Here()
                .Error("{PlexAccountName} token has been invalidated and has lost Plex Server access", plexAccountName);
            return await RemovePlexAccess(plexAccountId);
        }

        if (result.IsFailed)
            return result.ToResult().LogIfFailed();

        if (!result.Value.Any())
        {
            _log.Here().Warning("No Plex servers found for PlexAccount: {PlexAccountName}", plexAccountName);
            return await RemovePlexAccess(plexAccountId);
        }

        var serverList = result.Value.Select(x => x.PlexServer).ToList();
        var serverAccessTokens = result.Value.Select(x => x.AccessToken).ToList();

        // Add PlexServers and their PlexServerConnections
        var updateResult = await _commandExecutor.Send(
            new AddOrUpdatePlexServersCommand(serverList),
            cancellationToken
        );
        if (updateResult.IsFailed)
            return updateResult.ToResult().LogIfFailed();

        // Add or update the PlexAccount and PlexServer relationships
        var plexServerAccountAccessRapport = await _commandExecutor.Send(
            new AddOrUpdatePlexAccountServersCommand(plexAccountId, serverAccessTokens),
            cancellationToken
        );

        if (plexServerAccountAccessRapport.IsFailed)
            return plexServerAccountAccessRapport.LogIfFailed();

        _log.Here()
            .Information(
                "Successfully refreshed accessible Plex servers for account {PlexAccountDisplayName}",
                plexAccountName
            );

        await _notificationHubService.SendRefreshNotificationAsync([
            RefreshDataType.PlexServer,
            RefreshDataType.PlexServerConnection,
        ]);

        return plexServerAccountAccessRapport;
    }

    /// <summary>
    ///  Remove all PlexServerAccess and LibraryAccess for the given PlexAccount
    /// </summary>
    /// <param name="plexAccountId">The id of the PlexAccount to remove access for</param>
    private async Task<Result<RefreshPlexServerAccessRapport>> RemovePlexAccess(int plexAccountId)
    {
        var plexServers = await _dbContext
            .PlexAccountServers.Include(x => x.PlexServer)
            .Where(x => x.PlexAccountId == plexAccountId)
            .Select(x => new { x.PlexServerId, x.PlexServer!.Name })
            .ToListAsync(CancellationToken.None);
        var affectedLibraryIds = await _dbContext
            .PlexAccountLibraries.Where(x => x.PlexAccountId == plexAccountId)
            .Select(x => x.PlexLibraryId)
            .Distinct()
            .ToListAsync(CancellationToken.None);

        var plexAccountName = await _dbContext.GetPlexAccountDisplayName(plexAccountId, CancellationToken.None);
        var rapport = new RefreshPlexServerAccessRapport(plexAccountId, plexAccountName);

        if (!plexServers.Any() && affectedLibraryIds.Count == 0)
            return Result.Ok(rapport);

        foreach (var plexServer in plexServers)
        {
            rapport.Access.Add(
                new RefreshPlexServerAccessRapportRow(PlexAccessState.Revoked, plexServer.PlexServerId, plexServer.Name)
            );
        }

        await _dbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == plexAccountId)
            .ExecuteDeleteAsync(CancellationToken.None);
        await _dbContext
            .PlexAccountLibraries.Where(x => x.PlexAccountId == plexAccountId)
            .ExecuteDeleteAsync(CancellationToken.None);

        if (affectedLibraryIds.Count > 0)
        {
            var remainingEnabledLibraryIds = await _dbContext
                .PlexAccountLibraries.Where(x =>
                    affectedLibraryIds.Contains(x.PlexLibraryId) && x.PlexAccount!.IsEnabled
                )
                .Select(x => x.PlexLibraryId)
                .Distinct()
                .ToListAsync(CancellationToken.None);

            foreach (var libraryId in affectedLibraryIds.Except(remainingEnabledLibraryIds))
            {
                var cancelResult = await _commandExecutor.Send(
                    new CancelLibrarySyncJobCommand(libraryId),
                    CancellationToken.None
                );
                if (cancelResult.IsCancelledOrFailed)
                    return cancelResult.LogIfFailed().ToResult<RefreshPlexServerAccessRapport>();
            }
        }

        // TODO should return a RefreshPlexLibraryAccessRapport because those get changed with this command as well

        var rebuildResult = await _commandExecutor.Send(new QueueMediaOverviewRebuildCommand(), CancellationToken.None);
        rebuildResult.LogIfFailed();

        return Result.Ok(rapport);
    }
}
