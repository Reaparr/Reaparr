namespace Reaparr.Application;

public record StartDownloadTaskCommand(Guid DownloadTaskGuid) : ICommand<Result>;

public class StartDownloadTaskCommandValidator : AbstractValidator<StartDownloadTaskCommand>
{
    public StartDownloadTaskCommandValidator()
    {
        RuleFor(x => x.DownloadTaskGuid).NotEmpty();
    }
}

public class StartDownloadTaskCommandHandler : ICommandHandler<StartDownloadTaskCommand, Result>
{
    private static readonly DownloadStatus[] _resumablePausedStatuses =
    [
        DownloadStatus.Paused,
        DownloadStatus.AutoPaused,
        DownloadStatus.MovePaused,
        DownloadStatus.AutoMovePaused,
    ];

    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IDownloadTaskUpdateDispatcher _downloadTaskUpdateDispatcher;
    private readonly IEventPublisher _eventPublisher;
    private readonly IDownloadTaskScheduler _downloadTaskScheduler;
    private readonly IMoveDownloadFileScheduler _moveDownloadFileScheduler;

    public StartDownloadTaskCommandHandler(
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        IDownloadTaskUpdateDispatcher downloadTaskUpdateDispatcher,
        IEventPublisher eventPublisher,
        IDownloadTaskScheduler downloadTaskScheduler,
        IMoveDownloadFileScheduler moveDownloadFileScheduler
    )
    {
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _downloadTaskUpdateDispatcher = downloadTaskUpdateDispatcher;
        _eventPublisher = eventPublisher;
        _downloadTaskScheduler = downloadTaskScheduler;
        _moveDownloadFileScheduler = moveDownloadFileScheduler;
    }

    public async Task<Result> ExecuteAsync(StartDownloadTaskCommand command, CancellationToken cancellationToken)
    {
        var key = await _dbContext.GetDownloadTaskKeyAsync(command.DownloadTaskGuid, cancellationToken);
        if (key is null)
            return ResultExtensions.EntityNotFound(nameof(DownloadTaskGeneric), command.DownloadTaskGuid).LogError();

        var downloadableChildTasks = await _dbContext.GetDownloadableChildTasks(key, cancellationToken);
        if (!downloadableChildTasks.Any())
            return ResultExtensions.IsEmpty(nameof(downloadableChildTasks)).LogWarning();

        var nextDownloadTask = downloadableChildTasks.FirstOrDefault(x =>
            _resumablePausedStatuses.Contains(x.DownloadStatus)
        );
        nextDownloadTask ??= downloadableChildTasks.FirstOrDefault(x =>
            x.DownloadTaskPhase != DownloadTaskPhase.Completed
        );
        nextDownloadTask ??= downloadableChildTasks.First();
        var nextDownloadTaskKey = nextDownloadTask.ToKey();

        if (await _dbContext.IsDownloadsPausedByUser(nextDownloadTaskKey.PlexServerId))
        {
            return Result.Fail("Download tasks cannot be started while the server is paused by the user").LogWarning();
        }

        await _dbContext.CreateDownloadClientLog(
            nextDownloadTaskKey,
            NotificationLevel.Information,
            DownloadStatus.Queued,
            $"Start requested for download task {nextDownloadTaskKey.Id} ({nextDownloadTask.FileName})"
        );

        var queuedWaitingTasks = new List<(DownloadTaskKey Key, DownloadStatus Status)>();

        if (key.Type is DownloadTaskType.TvShow or DownloadTaskType.Season)
        {
            DownloadStatus[] statusesToQueue = nextDownloadTask.DownloadStatus switch
            {
                DownloadStatus.Paused or DownloadStatus.AutoPaused =>
                [
                    DownloadStatus.Paused,
                    DownloadStatus.AutoPaused,
                ],
                DownloadStatus.Stopped => [DownloadStatus.Stopped],
                _ => [],
            };

            foreach (
                var waitingTask in downloadableChildTasks.Where(x =>
                    x.Id != nextDownloadTask.Id && statusesToQueue.Contains(x.DownloadStatus)
                )
            )
            {
                queuedWaitingTasks.Add((waitingTask.ToKey(), waitingTask.DownloadStatus));
                await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                    waitingTask.ToKey(),
                    DownloadStatus.Queued,
                    cancellationToken
                );
            }
        }

        // Start the download task depending on the phase.
        switch (nextDownloadTask.DownloadTaskPhase)
        {
            case DownloadTaskPhase.None:
            case DownloadTaskPhase.Downloading:
                var downloadPreviousStatus = nextDownloadTask.DownloadStatus;
                var wasPaused = downloadPreviousStatus is DownloadStatus.Paused or DownloadStatus.AutoPaused or DownloadStatus.Stopped;
                if (wasPaused)
                {
                    await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                        nextDownloadTaskKey,
                        DownloadStatus.Queued,
                        cancellationToken
                    );
                }

                if (!await _downloadTaskScheduler.IsDownloading(nextDownloadTaskKey, cancellationToken))
                {
                    var startResult = await _downloadTaskScheduler.StartDownloadTaskJob(
                        nextDownloadTaskKey,
                        cancellationToken
                    );
                    if (startResult.IsFailed)
                    {
                        foreach (var (waitingTaskKey, waitingTaskStatus) in queuedWaitingTasks)
                        {
                            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                                waitingTaskKey,
                                waitingTaskStatus,
                                CancellationToken.None
                            );
                        }

                        if (wasPaused)
                        {
                            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                                nextDownloadTaskKey,
                                downloadPreviousStatus,
                                CancellationToken.None
                            );
                        }

                        return startResult.LogIfFailed();
                    }

                    var activeDownloadKeys = await _downloadTaskScheduler.GetCurrentlyDownloadingKeysByServer(
                        key.PlexServerId
                    );

                    // Avoid pausing the download task that just started.
                    foreach (var downloadKey in activeDownloadKeys.Where(x => x != nextDownloadTaskKey))
                    {
                        var pauseResult = await _commandExecutor.Send(
                            new PauseDownloadTaskCommand(downloadKey.Id),
                            cancellationToken
                        );
                        if (pauseResult.IsFailed)
                            return pauseResult.LogIfFailed();
                    }
                }

                break;

            case DownloadTaskPhase.FileTransfer:
                var previousStatus = nextDownloadTask.DownloadStatus;
                var wasMovePaused = previousStatus is DownloadStatus.MovePaused or DownloadStatus.AutoMovePaused;
                if (wasMovePaused)
                {
                    await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                        nextDownloadTaskKey,
                        DownloadStatus.DownloadFinished,
                        cancellationToken
                    );
                }

                // Multiple merging tasks can be processing at the same time.
                if (!(await _moveDownloadFileScheduler.IsDownloadFileMoving(nextDownloadTaskKey, cancellationToken)))
                {
                    var moveResult = await _moveDownloadFileScheduler.StartMoveDownloadFileJob(
                        nextDownloadTaskKey,
                        cancellationToken
                    );
                    if (moveResult.IsFailed)
                    {
                        if (wasMovePaused)
                        {
                            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                                nextDownloadTaskKey,
                                previousStatus,
                                CancellationToken.None
                            );
                        }

                        return moveResult.LogIfFailed();
                    }
                }

                break;

            case DownloadTaskPhase.Completed:
                return Result.Fail("Download task is already completed and cannot be started again").LogWarning();

            case DownloadTaskPhase.Unknown:
                return Result.Fail("Download task is in an unknown phase and cannot be started").LogError();

            default:
                throw new ArgumentOutOfRangeException(
                    $"{nextDownloadTask.DownloadTaskPhase} is not a valid DownloadTaskPhase enum value"
                );
        }

        await _eventPublisher.PublishAsync(new CheckDownloadQueueEvent(key.PlexServerId), cancellationToken);

        return Result.Ok();
    }
}
