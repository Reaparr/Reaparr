namespace Reaparr.Application;

public class RefreshLibraryAccessValidator : AbstractValidator<RefreshLibraryAccessCommand>
{
    public RefreshLibraryAccessValidator()
    {
        RuleFor(x => x.PlexAccountId).GreaterThan(0);
        RuleFor(x => x.PlexServerId).GreaterThanOrEqualTo(0);
    }
}

public class RefreshLibraryAccessHandler
    : ICommandHandler<RefreshLibraryAccessCommand, Result<PlexLibraryAccessRefreshResponse>>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandDispatcher;

    public RefreshLibraryAccessHandler(
        ILogger log,
        ICommandExecutor commandExecutor,
        IReaparrDbContext dbContext,
        ICommandExecutor commandDispatcher
    )
    {
        _log = log.ForContext<RefreshLibraryAccessHandler>();
        _commandExecutor = commandExecutor;
        _dbContext = dbContext;
        _commandDispatcher = commandDispatcher;
    }

    public async Task<Result<PlexLibraryAccessRefreshResponse>> ExecuteAsync(
        RefreshLibraryAccessCommand command,
        CancellationToken cancellationToken
    )
    {
        var plexAccountId = command.PlexAccountId;
        var plexServerId = command.PlexServerId;

        var plexServers = new List<PlexServer>();

        // Determine the Plex servers to refresh the Plex libraries for
        if (plexServerId == 0)
        {
            var result = await _dbContext.GetAccessiblePlexServers(plexAccountId, cancellationToken);
            if (result.IsFailed)
                return result.ToResult().LogIfFailed();

            plexServers = result.Value;
        }
        else
        {
            var plexServer = await _dbContext
                .PlexServers.IgnoreIsEnabledFilter()
                .GetAsync(plexServerId, cancellationToken);
            if (plexServer is not null)
            {
                if (!plexServer.IsEnabled)
                    return ResultExtensions.ServerIsDisabled(
                        plexServer.Name,
                        plexServer.Id,
                        nameof(RefreshLibraryAccessCommand)
                    );

                plexServers.Add(plexServer);
            }
        }

        if (!plexServers.Any())
        {
            var plexAccountName = await _dbContext.GetPlexAccountDisplayName(plexAccountId, cancellationToken);
            _log.Here().Warning("No accessible Plex servers found for PlexAccount {PlexAccountName}", plexAccountName);
            return Result.Ok(new PlexLibraryAccessRefreshResponse { Reports = [], OfflineServers = [] });
        }

        // Refresh Plex libraries
        var libraryResults = await Task.WhenAll(
            plexServers.Select(async server =>
                (ServerId: server.Id, Result: await RefreshLibrary(server.Id, plexAccountId, cancellationToken))
            )
        );

        var cancelledResults = libraryResults.Where(x => x.Result.IsCancelled).Select(x => x.Result).ToArray();
        if (cancelledResults.Length > 0)
            return Result.Merge(cancelledResults).ToResult();

        if (libraryResults.All(x => x.Result.IsFailed))
            return Result.Merge(libraryResults.Select(x => x.Result).ToArray()).ToResult();

        var plexLibraries = libraryResults.Where(x => x.Result.IsSuccess).SelectMany(x => x.Result.Value).ToList();
        var failedServers = libraryResults
            .Where(x => x.Result.Has504GatewayTimeoutError())
            .Select(x => x.ServerId)
            .ToList();

        var updateResult = await _commandExecutor.Send(
            new AddOrUpdatePlexLibrariesCommand { PlexAccountId = plexAccountId, PlexLibraries = plexLibraries },
            cancellationToken
        );

        return updateResult.IsFailed
            ? updateResult.ToResult<PlexLibraryAccessRefreshResponse>().LogIfFailed()
            : Result.Ok(
                new PlexLibraryAccessRefreshResponse { Reports = updateResult.Value, OfflineServers = failedServers }
            );
    }

    private async Task<Result<List<PlexLibrary>>> RefreshLibrary(
        int plexServerId,
        int plexAccountId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var plexServerName = await _dbContext.GetPlexServerNameById(plexServerId);
            var plexAccountName = await _dbContext.GetPlexAccountDisplayName(plexAccountId, cancellationToken);
            _log.Here()
                .Debug(
                    "Retrieving accessible PlexLibraries for plexServer with name: {PlexServerName} by using Plex account: {PlexAccountName}",
                    plexServerName,
                    plexAccountName
                );

            var libraries = await _commandDispatcher.Send(
                new GetLibrarySectionsCommand(plexServerId, plexAccountId),
                cancellationToken
            );

            if (libraries.IsCancelled)
                return libraries.ToResult();

            if (libraries.IsFailed)
            {
                _log.Here()
                    .Error(
                        "Failed to retrieve libraries for PlexServer {PlexServerName} and PlexAccount {PlexAccountName}",
                        plexServerName,
                        plexAccountName
                    );

                return libraries.ToResult().LogError();
            }

            if (!libraries.Value.Any())
            {
                return _log.Here()
                    .WarningResult(
                        "PlexServer with name {PlexServerName} returned no Plex libraries for Plex account {PlexAccountName}",
                        plexServerName,
                        plexAccountName
                    );
            }

            return libraries;
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e)).LogError();
        }
    }
}
