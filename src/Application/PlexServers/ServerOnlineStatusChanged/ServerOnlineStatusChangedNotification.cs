namespace Reaparr.Application;

public record ServerOnlineStatusChangedNotification : IEvent
{
    public ServerOnlineStatusChangedNotification(int plexServerId, bool isOnline)
    {
        PlexServerId = plexServerId;
        IsOnline = isOnline;
    }

    public int PlexServerId { get; }

    public bool IsOnline { get; }
}

public class ServerOnlineStatusChangedHandler : IEventHandler<ServerOnlineStatusChangedNotification>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly IDownloadQueue _downloadQueue;
    private readonly ICommandExecutor _commandExecutor;

    public ServerOnlineStatusChangedHandler(
        ILogger log,
        IReaparrDbContextFactory dbContextFactory,
        IDownloadQueue downloadQueue,
        ICommandExecutor commandExecutor
    )
    {
        _log = log.ForContext<ServerOnlineStatusChangedHandler>();
        _dbContextFactory = dbContextFactory;
        _downloadQueue = downloadQueue;
        _commandExecutor = commandExecutor;
    }

    public async Task HandleAsync(
        ServerOnlineStatusChangedNotification notification,
        CancellationToken cancellationToken
    )
    {
        var rebuildResult = await _commandExecutor.Send(
            new QueueMediaOverviewRebuildCommand(),
            cancellationToken
        );
        rebuildResult.LogIfFailed();

        if (notification.IsOnline)
        {
            // Create a new DbContext for this operation to avoid threading issues
            using var dbContext = await _dbContextFactory.CreateAsync();
            var plexServerName = await dbContext.GetPlexServerNameById(notification.PlexServerId);
            _log.Here()
                .Information(
                    "Server {PlexServerName} came online, checking DownloadQueue and LibrarySyncQueue to resume",
                    plexServerName
                );

            await _downloadQueue.CheckDownloadQueue([notification.PlexServerId], cancellationToken);
            await _commandExecutor.Send(
                new ResetFailedLibrarySyncJobsCommand(notification.PlexServerId),
                cancellationToken
            );
        }
    }
}
