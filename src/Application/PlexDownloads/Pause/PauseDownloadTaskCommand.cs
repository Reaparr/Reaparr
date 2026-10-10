namespace Reaparr.Application;

/// <summary>
/// Pauses and disposes of the PlexDownloadClient executing the <see cref="DownloadTaskGeneric"/> if it is downloading or pauses the FileTransfer
/// </summary>
/// <param name="DownloadTaskGuid">The id of the <see cref="DownloadTaskGeneric"/> to Pause.</param>
/// <returns>If successful a list of the DownloadTasks that were Paused.</returns>
public record PauseDownloadTaskCommand(Guid DownloadTaskGuid, bool AutoPause = false) : ICommand<Result>;

public class PauseDownloadTaskCommandValidator : AbstractValidator<PauseDownloadTaskCommand>
{
    public PauseDownloadTaskCommandValidator()
    {
        RuleFor(x => x.DownloadTaskGuid).NotEmpty();
    }
}

public class PauseDownloadTaskCommandHandler : ICommandHandler<PauseDownloadTaskCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IDownloadTaskScheduler _downloadTaskScheduler;
    private readonly IMoveDownloadFileScheduler _moveDownloadFileScheduler;
    private readonly IDownloadTaskUpdateDispatcher _downloadTaskUpdateDispatcher;

    public PauseDownloadTaskCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        IDownloadTaskScheduler downloadTaskScheduler,
        IMoveDownloadFileScheduler moveDownloadFileScheduler,
        IDownloadTaskUpdateDispatcher downloadTaskUpdateDispatcher
    )
    {
        _log = log.ForContext<PauseDownloadTaskCommandHandler>();
        _dbContext = dbContext;
        _downloadTaskScheduler = downloadTaskScheduler;
        _moveDownloadFileScheduler = moveDownloadFileScheduler;
        _downloadTaskUpdateDispatcher = downloadTaskUpdateDispatcher;
    }

    public async Task<Result> ExecuteAsync(PauseDownloadTaskCommand command, CancellationToken cancellationToken)
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

            if (downloadTask.DownloadTaskPhase == DownloadTaskPhase.Completed)
                continue;

            // A finished move must never be reset — the file is already at the destination.
            // MoveDownloadFileJob will transition it to Completed; pausing here would corrupt progress.
            if (downloadTask.DownloadStatus == DownloadStatus.MoveFinished)
                continue;

            // DownloadFinished is already in the file-transfer phase. Cancel a queued or running move before preserving it as paused.
            if (downloadTask.DownloadStatus == DownloadStatus.DownloadFinished)
            {
                if (await _moveDownloadFileScheduler.IsDownloadFileMoving(downloadTaskKey, cancellationToken))
                {
                    var stopMoveResult = await _moveDownloadFileScheduler.StopMoveDownloadFileJob(
                        downloadTaskKey,
                        cancellationToken
                    );
                    if (stopMoveResult.IsFailed)
                        return stopMoveResult.LogIfFailed();
                }

                await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                    downloadTaskKey,
                    command.AutoPause ? DownloadStatus.AutoMovePaused : DownloadStatus.MovePaused,
                    cancellationToken
                );
                continue;
            }

            _log.Here()
                .Information("Pausing DownloadTask with id {DownloadTaskTitle} from downloading", downloadTask.Title);

            if (downloadTask.DownloadTaskPhase == DownloadTaskPhase.FileTransfer)
            {
                var isMoving = await _moveDownloadFileScheduler.IsDownloadFileMoving(
                    downloadTaskKey,
                    cancellationToken
                );
                if (isMoving)
                {
                    var stopMoveResult = await _moveDownloadFileScheduler.StopMoveDownloadFileJob(
                        downloadTaskKey,
                        cancellationToken
                    );
                    if (stopMoveResult.IsFailed)
                        return stopMoveResult.LogIfFailed();
                }

                var resetMoveProgressResult = await _dbContext.ResetDownloadTaskProgress(
                    downloadTaskKey,
                    command.AutoPause ? DownloadStatus.AutoMovePaused : DownloadStatus.MovePaused,
                    cancellationToken
                );
                if (resetMoveProgressResult.IsFailed)
                    return resetMoveProgressResult.LogIfFailed();

                await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                    downloadTaskKey,
                    command.AutoPause ? DownloadStatus.AutoMovePaused : DownloadStatus.MovePaused,
                    cancellationToken
                );

                continue;
            }

            var isDownloading = await _downloadTaskScheduler.IsDownloading(downloadTaskKey, cancellationToken);
            if (!isDownloading)
            {
                await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                    downloadTaskKey,
                    command.AutoPause ? DownloadStatus.AutoPaused : DownloadStatus.Paused,
                    cancellationToken
                );
                continue;
            }

            var stopResult = await _downloadTaskScheduler.StopDownloadTaskJob(downloadTaskKey, cancellationToken);
            if (stopResult.IsFailed)
                return stopResult.LogIfFailed();

            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                downloadTaskKey,
                command.AutoPause ? DownloadStatus.AutoPaused : DownloadStatus.Paused,
                cancellationToken
            );
        }

        return Result.Ok();
    }
}
