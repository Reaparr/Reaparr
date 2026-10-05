namespace Reaparr.Application;

public record AutoPauseActiveDownloadsCommand : ICommand<Result>;

public class AutoPauseActiveDownloadsCommandHandler : ICommandHandler<AutoPauseActiveDownloadsCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IDownloadTaskScheduler _downloadTaskScheduler;
    private readonly IMoveDownloadFileScheduler _moveDownloadFileScheduler;

    public AutoPauseActiveDownloadsCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        IDownloadTaskScheduler downloadTaskScheduler,
        IMoveDownloadFileScheduler moveDownloadFileScheduler
    )
    {
        _log = log.ForContext<AutoPauseActiveDownloadsCommandHandler>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _downloadTaskScheduler = downloadTaskScheduler;
        _moveDownloadFileScheduler = moveDownloadFileScheduler;
    }

    public async Task<Result> ExecuteAsync(AutoPauseActiveDownloadsCommand command, CancellationToken cancellationToken)
    {
        var plexServerIds = await _dbContext
            .PlexServers.AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var totalPaused = 0;
        var pauseFailure = Result.Ok();

        foreach (var plexServerId in plexServerIds)
        {
            for (var pass = 1; pass <= 2; pass++)
            {
                var activeDownloads = await _downloadTaskScheduler.GetCurrentlyDownloadingKeysByServer(plexServerId);
                var activeMoves = await _moveDownloadFileScheduler.GetCurrentlyMovingKeysByServer(plexServerId);

                foreach (var activeKey in activeDownloads.Concat(activeMoves).Distinct())
                {
                    _log.Here()
                        .Information(
                            "Auto-pausing active task {DownloadTaskKey} during shutdown on pass {Pass} for PlexServer {PlexServerId}",
                            activeKey,
                            pass,
                            plexServerId
                        );

                    var pauseResult = await _commandExecutor.Send(
                        new PauseDownloadTaskCommand(activeKey.Id, AutoPause: true),
                        cancellationToken
                    );

                    if (pauseResult.IsCancelled)
                        return pauseResult.LogIfFailed();

                    if (pauseResult.IsFailed)
                    {
                        _log.Here()
                            .Error(
                                "Failed to auto-pause active task {DownloadTaskKey} during shutdown on pass {Pass} for PlexServer {PlexServerId}",
                                activeKey,
                                pass,
                                plexServerId
                            );
                        pauseFailure.WithErrors(pauseResult.Errors);
                        continue;
                    }

                    totalPaused++;
                }
            }
        }

        if (totalPaused > 0)
            _log.Here().Information("Auto-paused {Count} active download or move task(s) during shutdown", totalPaused);

        if (pauseFailure.IsFailed)
            return pauseFailure.LogIfFailed();

        return Result.Ok();
    }
}
