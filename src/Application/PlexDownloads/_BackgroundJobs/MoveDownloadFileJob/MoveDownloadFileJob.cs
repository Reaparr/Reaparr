namespace Reaparr.Application;

public sealed record MoveDownloadFileJobPayload(DownloadTaskKey DownloadTaskKey);

[DisallowConcurrentExecution]
public class MoveDownloadFileJob : IJob
{
    internal const string RetryCountKey = "MoveRetryCount";
    internal const int MaxRetryCount = 5;

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

    private static int GetRetryCount(IJobExecutionContext context)
    {
        if (
            !context.MergedJobDataMap.TryGetValue(RetryCountKey, out var value)
            || !int.TryParse(value?.ToString(), out var retryCount)
        )
            return 0;

        return Math.Max(retryCount, 0);
    }

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
            // An individual job cancellation must not cancel continuation of unrelated waiting moves.
            // The queue itself refuses new work once host shutdown begins.
            var queueToken = ct.IsCancellationRequested ? CancellationToken.None : ct;
            var queueResult = await Result.Try(() => _moveDownloadFileQueue.CheckMoveDownloadFileJobQueue(queueToken));
            queueResult.LogIfFailed();
        }

        var executionResult = await Result.Try(async Task<Result> () =>
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
                await QueueNextAsync();
                return Result.Ok();
            }

            if (
                currentTask.DownloadStatus
                is not (
                    DownloadStatus.DownloadFinished
                    or DownloadStatus.MovePaused
                    or DownloadStatus.AutoMovePaused
                    or DownloadStatus.MoveError
                    or DownloadStatus.MoveFinished
                )
            )
            {
                _log.Here()
                    .Warning(
                        "Skipping move job for {DownloadTaskKey} because status {DownloadStatus} is not authorized to move",
                        downloadTaskKey,
                        currentTask.DownloadStatus
                    );
                await QueueNextAsync();
                return Result.Ok();
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
                var status = await _dbContext.GetDownloadStatusAsync(downloadTaskKey);
                if (
                    status
                    is null
                        or DownloadStatus.MovePaused
                        or DownloadStatus.AutoMovePaused
                        or DownloadStatus.Stopped
                        or DownloadStatus.Completed
                )
                    await QueueNextAsync();
                return moveResult;
            }

            if (moveResult.IsFailed)
            {
                _log.Here().Error("Failed to move all files for {DownloadTaskKey}", downloadTaskKey);
                var retryCount = GetRetryCount(context);
                if (retryCount >= MaxRetryCount)
                    return moveResult;

                // The current key is still executing. A separate one-shot trigger survives this firing
                // and Quartz's non-concurrency guard holds it until this execution has been released.
                var nextRetryCount = retryCount + 1;
                var retryResult = await Result.Try(async Task () =>
                {
                    var jobKey = GetJobKey(downloadTaskKey.Id);
                    var triggerKey = new TriggerKey($"{jobKey.Name}_Retry_{context.FireInstanceId}", jobKey.Group);
                    if (await context.Scheduler.CheckExists(triggerKey, ct))
                        return;

                    var retryTrigger = TriggerBuilder
                        .Create()
                        .WithIdentity(triggerKey)
                        .ForJob(jobKey)
                        .UsingJobData(RetryCountKey, nextRetryCount)
                        .StartAt(DateTimeOffset.UtcNow.AddMinutes(1))
                        .WithSimpleSchedule(x => x.WithMisfireHandlingInstructionFireNow())
                        .Build();
                    await context.Scheduler.ScheduleJob(retryTrigger, ct);
                });
                retryResult.LogIfFailed();
                return moveResult;
            }

            var downloadTaskResult = await Result.Try(() =>
                _dbContext.GetDownloadTaskFileAsync(downloadTaskKey, CancellationToken.None)
            );

            if (downloadTaskResult.IsFailed)
            {
                downloadTaskResult.LogError();
                return downloadTaskResult.ToResult();
            }

            var downloadTask = downloadTaskResult.Value;
            if (downloadTask is null)
            {
                return ResultExtensions.EntityNotFound(nameof(DownloadTaskGeneric), downloadTaskKey.Id).LogError();
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
                    return cleanupResult;
                }

                if (cleanupResult.IsFailed)
                {
                    cleanupResult.LogError();
                    return cleanupResult;
                }
            }

            await QueueNextAsync();
            return Result.Ok();
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
