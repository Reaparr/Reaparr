namespace Reaparr.Application;

public class StopDownloadTaskCommandValidator : AbstractValidator<StopDownloadTaskCommand>
{
    public StopDownloadTaskCommandValidator()
    {
        RuleFor(x => x.DownloadTaskGuid).NotEmpty();
    }
}

public class StopDownloadTaskCommandHandler : ICommandHandler<StopDownloadTaskCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IDownloadTaskUpdateDispatcher _downloadTaskUpdateDispatcher;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IDownloadTaskScheduler _downloadTaskScheduler;
    private readonly IMoveDownloadFileScheduler _moveDownloadFileScheduler;

    public StopDownloadTaskCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        IDownloadTaskUpdateDispatcher downloadTaskUpdateDispatcher,
        ICommandExecutor commandExecutor,
        IDownloadTaskScheduler downloadTaskScheduler,
        IMoveDownloadFileScheduler moveDownloadFileScheduler
    )
    {
        _log = log.ForContext<StopDownloadTaskCommandHandler>();
        _dbContext = dbContext;
        _downloadTaskUpdateDispatcher = downloadTaskUpdateDispatcher;
        _commandExecutor = commandExecutor;
        _downloadTaskScheduler = downloadTaskScheduler;
        _moveDownloadFileScheduler = moveDownloadFileScheduler;
    }

    public async Task<Result> ExecuteAsync(StopDownloadTaskCommand command, CancellationToken cancellationToken)
    {
        var key = await _dbContext.GetDownloadTaskKeyAsync(command.DownloadTaskGuid, cancellationToken);
        if (key is null)
            return ResultExtensions.EntityNotFound(nameof(DownloadTaskGeneric), command.DownloadTaskGuid).LogError();

        var downloadTasks = await _dbContext.GetDownloadableChildTaskKeys(key, cancellationToken);
        var filesById = (await _dbContext.GetDownloadTaskFilesAsync(downloadTasks, cancellationToken)).ToDictionary(x =>
            x.Id
        );

        foreach (var downloadTaskKey in downloadTasks)
        {
            if (!filesById.TryGetValue(downloadTaskKey.Id, out var downloadTask))
            {
                ResultExtensions.EntityNotFound(nameof(DownloadTaskGeneric), downloadTaskKey.Id).LogError();
                continue;
            }

            var isDownloading = await _downloadTaskScheduler.IsDownloading(downloadTaskKey, cancellationToken);
            var isMoving = await _moveDownloadFileScheduler.IsDownloadFileMoving(downloadTaskKey, cancellationToken);

            // Multi-child parents stop only active children; queued siblings retain their files and state.
            // Single-item and file selections stop their selected files regardless of activity.
            if (key.Type.IsParent() && !isDownloading && !isMoving)
            {
                await _dbContext.CreateDownloadClientLog(
                    downloadTaskKey,
                    NotificationLevel.Debug,
                    downloadTask.DownloadStatus,
                    $"Stop requested but skipped because task is not active (status: {downloadTask.DownloadStatus})"
                );
                continue;
            }

            _log.Here().Information("Stopping {DownloadTaskFullTitle}", downloadTask.FullTitle);

            if (isDownloading)
            {
                var stopResult = await _downloadTaskScheduler.StopDownloadTaskJob(downloadTaskKey, cancellationToken);
                if (stopResult.IsFailed)
                {
                    // At most one download task runs per server; if stopping it fails there is
                    // nothing left to stop safely, so abort.
                    return stopResult.LogIfFailed();
                }
            }

            if (isMoving)
            {
                var stopMoveResult = await _moveDownloadFileScheduler.StopMoveDownloadFileJob(
                    downloadTaskKey,
                    cancellationToken
                );
                if (stopMoveResult.IsFailed)
                    return stopMoveResult.LogIfFailed();
            }

            // Only delete the download file when NOT in the completed phase (file is already
            // at its destination directory in that case).
            if (command.DeleteFiles && downloadTask.DownloadTaskPhase != DownloadTaskPhase.Completed)
            {
                _log.Here()
                    .Debug("Deleting partially downloaded files of {DownloadTaskFullTitle}", downloadTask.FullTitle);

                var deleteFilesResult = await _commandExecutor.Send(
                    new DeleteDownloadTaskFilesCommand([downloadTaskKey]),
                    cancellationToken
                );
                if (deleteFilesResult.IsFailed)
                    return deleteFilesResult.LogIfFailed();
            }

            _log.Here()
                .Debug(
                    "Resetting download progress for {Guid} ({DownloadTaskFileName})",
                    downloadTaskKey.Id,
                    downloadTask.FileName
                );

            var resetResult = await _dbContext.ResetDownloadTaskProgress(
                downloadTaskKey,
                DownloadStatus.Stopped,
                cancellationToken
            );
            if (resetResult.IsFailed)
                return resetResult.LogIfFailed();
            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                downloadTaskKey,
                DownloadStatus.Stopped,
                cancellationToken
            );
        }

        return Result.Ok();
    }
}
