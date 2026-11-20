namespace Reaparr.Application;

/// <summary>
/// Checks every <see cref="PlexServerConnection"/> in parallel of a <see cref="PlexServer"/> whether it connects or not
/// and then stores that <see cref="PlexServerStatus"/> in the database.
/// </summary>
/// <param name="PlexServerId">The id of the <see cref="PlexServer" /> to check the connections for.</param>
/// <returns>Returns successful result if any connection connected.</returns>
public record CheckAllConnectionsStatusByPlexServerCommand(int PlexServerId, int Timeout = 10)
    : ICommand<Result<List<PlexServerStatus>>>;

public class CheckAllConnectionsStatusByPlexServerValidator
    : AbstractValidator<CheckAllConnectionsStatusByPlexServerCommand>
{
    public CheckAllConnectionsStatusByPlexServerValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
        RuleFor(x => x.Timeout).GreaterThan(0);
    }
}

public class CheckAllConnectionsStatusByPlexServerHandler
    : ICommandHandler<CheckAllConnectionsStatusByPlexServerCommand, Result<List<PlexServerStatus>>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IEventPublisher _eventPublisher;
    private readonly INotificationHubService _notificationHubService;

    public CheckAllConnectionsStatusByPlexServerHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        IEventPublisher eventPublisher,
        INotificationHubService notificationHubService
    )
    {
        _log = log.ForContext<CheckAllConnectionsStatusByPlexServerHandler>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _eventPublisher = eventPublisher;
        _notificationHubService = notificationHubService;
    }

    public async Task<Result<List<PlexServerStatus>>> ExecuteAsync(
        CheckAllConnectionsStatusByPlexServerCommand command,
        CancellationToken cancellationToken
    )
    {
        var plexServerId = command.PlexServerId;

        var plexServer = await _dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Include(x => x.PlexServerConnections)
            .GetAsync(plexServerId, cancellationToken);

        if (plexServer == null)
            return ResultExtensions.EntityNotFound(nameof(plexServerId), plexServerId).LogError();

        var plexServerName = await _dbContext.GetPlexServerNameById(plexServerId);
        if (!plexServer.IsEnabled)
        {
            return ResultExtensions
                .ServerIsDisabled(plexServerName, plexServerId, nameof(CheckAllConnectionsStatusByPlexServerCommand))
                .LogError();
        }

        var connections = plexServer.PlexServerConnections.ToList();
        if (!connections.Any())
        {
            return _log.Here().ErrorResult("No connections found for the plex server {PlexServerName}", plexServerName);
        }

        var previousResult = await _dbContext.IsServerOnline(plexServerId);

        // Retain the snapshotted connection id with each result so a result for a removed
        // connection can be reconciled without affecting results for surviving connections.
        var connectionTasks = connections.Select(async plexServerConnection =>
            (
                ConnectionId: plexServerConnection.Id,
                Result: await _commandExecutor.Send(
                    new CheckConnectionStatusByIdCommand(plexServerConnection.Id, command.Timeout),
                    cancellationToken
                )
            )
        );

        var connectionResults = await Task.WhenAll(connectionTasks);
        var tasksResult = connectionResults.Select(x => x.Result).ToArray();
        var combinedResults = Result.Merge(tasksResult);

        if (tasksResult.Any(x => x.IsCancelled))
            return combinedResults.ToResult();

        var notFoundResults = connectionResults
            .Where(x => x.Result.IsFailed && x.Result.Errors.Count == 1 && x.Result.Has404NotFoundError())
            .ToArray();
        List<PlexServerConnection>? reconciledConnections = null;
        if (notFoundResults.Length > 0)
        {
            reconciledConnections = await _dbContext
                .PlexServerConnections.AsNoTracking()
                .Include(x => x.LatestConnectionStatus)
                .Where(x => x.PlexServerId == plexServerId)
                .ToListAsync(cancellationToken);

            var currentConnectionIds = reconciledConnections.Select(x => x.Id).ToHashSet();
            var removedConnectionIds = connectionResults
                .Where(x => !currentConnectionIds.Contains(x.ConnectionId))
                .Select(x => x.ConnectionId)
                .ToHashSet();
            connectionResults = connectionResults.Where(x => !removedConnectionIds.Contains(x.ConnectionId)).ToArray();
            combinedResults = Result.Merge(connectionResults.Select(x => x.Result).ToArray());
        }

        if (combinedResults.IsFailed)
            return combinedResults.ToResult().LogError();

        var statuses = connectionResults.Select(x => x.Result.ValueOrDefault).OfType<PlexServerStatus>().ToList();

        if (reconciledConnections is not null)
        {
            var snapshottedConnectionIds = connections.Select(x => x.Id).ToHashSet();
            statuses.AddRange(
                reconciledConnections
                    .Where(x => !snapshottedConnectionIds.Contains(x.Id))
                    .Select(x => x.LatestConnectionStatus)
                    .OfType<PlexServerStatus>()
            );
        }

        await _notificationHubService.SendRefreshNotificationAsync([RefreshDataType.PlexServerConnection]);

        // Compare previous and current online status. If every snapshotted connection was
        // removed, there was no current probe to justify publishing an offline transition.
        var currentOnlineStatus = statuses.Any(status => status.IsSuccessful);
        var hasCurrentProbeResult = connectionResults.Any(x => x.Result.IsSuccess);

        if (hasCurrentProbeResult && previousResult != currentOnlineStatus)
        {
            await _eventPublisher.PublishAsync(
                new ServerOnlineStatusChangedNotification(plexServerId, currentOnlineStatus),
                Mode.WaitForAll,
                cancellationToken
            );
        }

        if (currentOnlineStatus || !hasCurrentProbeResult)
            return Result.Ok(statuses);

        var warningResult = _log.Here()
            .WarningResult(
                "All connections to plex server with name: {PlexServerName} and id: {PlexServerId} failed to connect",
                plexServerName,
                plexServerId
            );
        return Result.Ok(statuses).WithReasons(warningResult.Reasons);
    }
}
