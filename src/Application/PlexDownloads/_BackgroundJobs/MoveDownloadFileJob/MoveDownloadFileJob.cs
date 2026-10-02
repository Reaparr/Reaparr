namespace Reaparr.Application;

public sealed record MoveDownloadFileJobPayload(DownloadTaskKey DownloadTaskKey);

[DisallowConcurrentExecution]
public class MoveDownloadFileJob : IJob
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IReaparrDbContext _dbContext;
    private readonly IDownloadTaskUpdateDispatcher _downloadTaskUpdateDispatcher;
    private readonly IMoveDownloadFileQueue _moveDownloadFileQueue;

    public MoveDownloadFileJob(
        ILogger log,
        ICommandExecutor commandExecutor,
        IReaparrDbContext dbContext,
        IDownloadTaskUpdateDispatcher downloadTaskUpdateDispatcher,
        IMoveDownloadFileQueue moveDownloadFileQueue
    )
    {
        _log = log.ForContext<MoveDownloadFileJob>();
        _commandExecutor = commandExecutor;
        _dbContext = dbContext;
        _downloadTaskUpdateDispatcher = downloadTaskUpdateDispatcher;
        _moveDownloadFileQueue = moveDownloadFileQueue;
    }

    public static JobKey GetJobKey(Guid id) => new($"{nameof(MoveDownloadFileJob)}_{id}", nameof(MoveDownloadFileJob));

    public async Task Execute(IJobExecutionContext context)
    {
        var payloadResult = context.GetRequiredPayload<MoveDownloadFileJobPayload>();
        if (payloadResult.IsFailed)
        {
            context.SetResult(JobStatus.Failed, payloadResult);
            payloadResult.LogError();
            return;
        }

        // Jobs should swallow exceptions as otherwise Quartz will keep re-executing it
        // https://www.quartz-scheduler.net/documentation/best-practices.html#throwing-exceptions
        var ct = context.CancellationToken;
        var downloadTaskKey = payloadResult.Value.DownloadTaskKey;

        async Task QueueNextAsync()
        {
            var queueResult = await _moveDownloadFileQueue.CheckMoveDownloadFileJobQueue(ct);
            if (queueResult.IsCancelled)
            {
                queueResult.LogWarning();
                return;
            }

            if (queueResult.IsFailed)
                queueResult.LogError();
        }

        var executionResult = await Result.Try(async Task () =>
        {
            _log.Here()
                .Information(
                    "Executing job: {NameOfMoveDownloadJob} for {NameOfFileTaskId} with id: {FileTaskId}",
                    nameof(MoveDownloadFileJob),
                    nameof(downloadTaskKey),
                    downloadTaskKey.Id
                );

            var currentTask = await _dbContext.GetDownloadTaskFileAsync(downloadTaskKey, ct);
            if (currentTask is null)
            {
                ResultExtensions.EntityNotFound(nameof(DownloadTaskGeneric), downloadTaskKey.Id).LogWarning();
                return;
            }

            if (
                currentTask.DownloadStatus
                is not (DownloadStatus.DownloadFinished or DownloadStatus.MoveError or DownloadStatus.MoveFinished)
            )
            {
                _log.Here()
                    .Warning(
                        "Skipping move job for {DownloadTaskKey} because status {DownloadStatus} is not authorized to move",
                        downloadTaskKey,
                        currentTask.DownloadStatus
                    );
                await QueueNextAsync();
                return;
            }

            var moveResult = await Result.Try(() =>
                _commandExecutor.Send(new MoveDownloadFileFromFileTaskCommand(downloadTaskKey), ct)
            );

            if (moveResult.IsCancelled)
            {
                _log.Here()
                    .Warning(
                        "{NameOfMoveDownloadJob} for {NameOfFileTaskId} with id: {FileTaskId} was cancelled",
                        nameof(MoveDownloadFileJob),
                        nameof(downloadTaskKey),
                        downloadTaskKey.Id
                    );
                return;
            }

            if (moveResult.IsFailed)
            {
                _log.Here().Error("Failed to move all files for {DownloadTaskKey}", downloadTaskKey);
                await QueueNextAsync();
                return;
            }

            var downloadTaskResult = await Result.Try(() =>
                _dbContext.GetDownloadTaskFileAsync(downloadTaskKey, CancellationToken.None)
            );

            if (downloadTaskResult.IsFailed)
            {
                downloadTaskResult.LogError();
                await QueueNextAsync();
                return;
            }

            var downloadTask = downloadTaskResult.Value;
            if (downloadTask is null)
            {
                ResultExtensions.EntityNotFound(nameof(DownloadTaskGeneric), downloadTaskKey.Id).LogError();
                await QueueNextAsync();
                return;
            }

            if (downloadTask.DownloadStatus is DownloadStatus.MoveFinished)
            {
                await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                    downloadTaskKey,
                    DownloadStatus.Completed,
                    CancellationToken.None
                );

                // Clean up the Download task folders
                var cleanupResult = await Result.Try(() =>
                    _commandExecutor.Send(
                        new CleanUpDownloadTaskFoldersCommand(downloadTaskKey),
                        CancellationToken.None
                    )
                );
                if (cleanupResult.IsCancelled)
                {
                    _log.Here()
                        .Warning(
                            "{JobName} for {DownloadTaskKey} was cancelled",
                            nameof(MoveDownloadFileJob),
                            downloadTaskKey
                        );
                    await QueueNextAsync();
                    return;
                }

                if (cleanupResult.IsFailed)
                {
                    cleanupResult.LogError();
                    await QueueNextAsync();
                    return;
                }
            }

            await QueueNextAsync();
        });

        if (executionResult.IsCancelled)
        {
            context.SetResult(JobStatus.Cancelled, executionResult);
            executionResult.LogWarning();
        }
        else if (executionResult.IsFailed)
        {
            context.SetResult(JobStatus.Failed, executionResult);
            _log.Here()
                .Error(
                    "Unexpected error in {JobName} for {DownloadTaskKey}",
                    nameof(MoveDownloadFileJob),
                    downloadTaskKey
                );

            executionResult.LogError();
            await QueueNextAsync();
        }
    }
}
