namespace Reaparr.Application;

public record AddOrUpdatePlexAccountServersCommand(int PlexAccountId, List<ServerAccessTokenDTO> ServerAccessTokens)
    : ICommand<Result<RefreshPlexServerAccessRapport>>;

public class AddOrUpdatePlexAccountServersCommandValidator : AbstractValidator<AddOrUpdatePlexAccountServersCommand>
{
    public AddOrUpdatePlexAccountServersCommandValidator()
    {
        RuleFor(x => x.PlexAccountId).GreaterThan(0);
    }
}

public class AddOrUpdatePlexAccountServersCommandHandler
    : ICommandHandler<AddOrUpdatePlexAccountServersCommand, Result<RefreshPlexServerAccessRapport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public AddOrUpdatePlexAccountServersCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor
    )
    {
        _log = log.ForContext<AddOrUpdatePlexAccountServersCommandHandler>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<RefreshPlexServerAccessRapport>> ExecuteAsync(
        AddOrUpdatePlexAccountServersCommand command,
        CancellationToken cancellationToken
    )
    {
        var plexAccountId = command.PlexAccountId;
        var serverAccessTokens = command.ServerAccessTokens;

        // Ensure we don't have any empty access tokens
        foreach (var serverAccessToken in serverAccessTokens)
            if (string.IsNullOrWhiteSpace(serverAccessToken.AccessToken))
            {
                _log.Here()
                    .Error(
                        "Server Access Token was given with an empty access token for machine identifier: {MachineIdentifier}",
                        serverAccessToken.MachineIdentifier
                    );
            }

        serverAccessTokens.RemoveAll(x => string.IsNullOrWhiteSpace(x.AccessToken));

        var plexAccount = await _dbContext.PlexAccounts.GetAsync(plexAccountId, cancellationToken);
        if (plexAccount is null)
            return ResultExtensions.EntityNotFound(nameof(PlexAccount), plexAccountId);

        var rapport = new RefreshPlexServerAccessRapport(plexAccount.Id, plexAccount.DisplayName);

        // Add or update the PlexAccount and PlexServer relationships
        _log.Here().Information("Adding or updating the PlexAccount association with PlexServers now");

        // Fetch all relevant PlexServers in one query
        var machineIdentifiers = serverAccessTokens.Select(x => x.MachineIdentifier).ToList();
        var plexServers = await _dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => machineIdentifiers.Contains(x.MachineIdentifier))
            .ToDictionaryAsync(x => x.MachineIdentifier, cancellationToken);

        var newAccountServers = new List<PlexAccountServer>();
        var accessiblePlexServers = new List<int>();

        foreach (var serverAccessToken in serverAccessTokens)
        {
            if (!plexServers.TryGetValue(serverAccessToken.MachineIdentifier, out var plexServer))
            {
                _log.Here()
                    .Error(
                        "Server Access Token was given for a machine identifier that has no PlexServer in the database: {MachineIdentifier}",
                        serverAccessToken.MachineIdentifier
                    );
                continue;
            }

            accessiblePlexServers.Add(plexServer.Id);

            // Check if this PlexAccount has been associated with the plexServer already
            var plexAccountServer = await _dbContext.PlexAccountServers.FirstOrDefaultAsync(
                x => x.PlexAccountId == plexAccountId && x.PlexServerId == plexServer.Id,
                cancellationToken
            );

            if (plexAccountServer is null)
            {
                _log.Here()
                    .Debug(
                        "PlexAccount {PlexAccountDisplayName} does not have an association with PlexServer: {PlexServerName}, creating one now with the authentication token",
                        plexAccount.DisplayName,
                        plexServer.Name
                    );
                newAccountServers.Add(
                    new PlexAccountServer
                    {
                        PlexAccountId = plexAccountId,
                        PlexServerId = plexServer.Id,
                        AuthToken = serverAccessToken.AccessToken,
                        AuthTokenCreationDate = DateTime.UtcNow,
                        IsServerOwned = serverAccessToken.IsServerOwned,
                    }
                );

                rapport.AddGranted(plexServer.Id, plexServer.Name);
            }
            else
            {
                _log.Here()
                    .Debug(
                        "PlexAccount {PlexAccountDisplayName} already has an association with PlexServer: {PlexServerName}, updating authentication token now",
                        plexAccount.DisplayName,
                        plexServer.Name
                    );
                plexAccountServer.AuthToken = serverAccessToken.AccessToken;
                plexAccountServer.AuthTokenCreationDate = DateTime.UtcNow;
                plexAccountServer.IsServerOwned = serverAccessToken.IsServerOwned;

                rapport.AddUpdated(plexServer.Id, plexServer.Name);
            }
        }

        // Add new associations in bulk
        if (newAccountServers.Any())
            await _dbContext.PlexAccountServers.AddRangeAsync(newAccountServers, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _log.Here().Information("Checking if there are any PlexServers this PlexAccount has no access to anymore");

        // The list of all past and current serverId's the plexAccount has access too
        var removalList = await _dbContext
            .PlexAccountServers.Where(x =>
                x.PlexAccountId == plexAccountId && !accessiblePlexServers.Contains(x.PlexServerId)
            )
            .Include(x => x.PlexAccount)
            .Include(x => x.PlexServer)
            .AsTracking()
            .ToListAsync(cancellationToken);

        if (removalList.Any())
        {
            foreach (var plexAccountServer in removalList)
                _log.Here()
                    .Warning(
                        "PlexAccount {PlexAccountDisplayName} has lost access to {PlexServerName}!",
                        plexAccountServer.PlexAccount!.DisplayName,
                        plexAccountServer.PlexServer!.Name
                    );
            var removalIds = removalList.Select(x => x.PlexServerId).ToList();
            await _dbContext
                .PlexAccountServers.Where(x => removalIds.Contains(x.PlexServerId) && x.PlexAccountId == plexAccountId)
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var plexServerId in removalIds)
            {
                var plexServerName = await _dbContext.GetPlexServerNameById(plexServerId);
                rapport.AddRevoked(plexServerId, plexServerName);
            }
        }
        else
        {
            _log.Here()
                .Information(
                    "No Plex server access for {PlexAccountDisplayName} has been lost",
                    plexAccount.DisplayName
                );
        }

        var rebuildResult = await _commandExecutor.Send(new QueueMediaOverviewRebuildCommand(), cancellationToken);
        if (rebuildResult.IsFailed)
        {
            rebuildResult.LogIfFailed();
            return rebuildResult;
        }

        return Result.Ok(rapport);
    }
}
