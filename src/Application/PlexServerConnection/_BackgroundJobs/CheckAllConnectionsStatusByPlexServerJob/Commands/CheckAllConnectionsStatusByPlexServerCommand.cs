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

        // Create connection check tasks for all connections
        var connectionTasks = connections.Select(async plexServerConnection =>
            await _commandExecutor.Send(
                new CheckConnectionStatusByIdCommand(plexServerConnection.Id, command.Timeout),
                cancellationToken
            )
        );

        var tasksResult = await Task.WhenAll(connectionTasks);
        var combinedResults = Result.Merge(tasksResult);

        if (tasksResult.Any(x => x.IsCancelled))
            return combinedResults.ToResult();

        if (combinedResults.IsFailed)
            return combinedResults.ToResult().LogError();

        await _notificationHubService.SendRefreshNotificationAsync([RefreshDataType.PlexServerConnection]);

        // Compare previous and current online status
        var currentOnlineStatus = tasksResult.Any(statusResult =>
            statusResult is { IsSuccess: true, ValueOrDefault.IsSuccessful: true }
        );

        if (previousResult != currentOnlineStatus)
        {
            await _eventPublisher.PublishAsync(
                new ServerOnlineStatusChangedNotification(plexServerId, currentOnlineStatus),
                cancellationToken
            );
        }

        if (currentOnlineStatus)
            return Result.Ok(combinedResults.Value.ToList());

        var warningResult = _log.Here()
            .WarningResult(
                "All connections to plex server with name: {PlexServerName} and id: {PlexServerId} failed to connect",
                plexServerName,
                plexServerId
            );
        return Result.Ok(combinedResults.Value.ToList()).WithReasons(warningResult.Reasons);
    }
}
