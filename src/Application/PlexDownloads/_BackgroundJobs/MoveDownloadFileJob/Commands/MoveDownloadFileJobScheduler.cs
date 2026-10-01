namespace Reaparr.Application;

public class MoveDownloadFileJobScheduler : IMoveDownloadFileScheduler
{
    private readonly ILogger _log;
    private readonly IScheduler _scheduler;

    public MoveDownloadFileJobScheduler(ILogger log, IScheduler scheduler)
    {
        _log = log.ForContext<MoveDownloadFileJobScheduler>();
        _scheduler = scheduler;
    }

    /// <summary>
    /// Should only be called by the <see cref="MoveDownloadFileJobQueue"/> to start a new <see cref="MoveDownloadFileJob"/>.
    /// </summary>
    public async Task<Result> StartMoveDownloadFileJob(
        DownloadTaskKey downloadTaskKey,
        CancellationToken cancellationToken
    )
    {
        if (!downloadTaskKey.IsValid)
            return ResultExtensions.IsInvalidId(nameof(DownloadTaskKey), downloadTaskKey.Id).LogWarning();

        var jobKey = MoveDownloadFileJob.GetJobKey(downloadTaskKey.Id);
        if (await _scheduler.IsActive(jobKey, cancellationToken))
        {
            _log.Here()
                .Debug("{MoveDownloadFileJobName} with {JobKey} is already scheduled", nameof(MoveDownloadFileJob), jobKey);
            return Result.Ok();
        }

        if (await _scheduler.CheckExists(jobKey, cancellationToken))
            await _scheduler.DeleteJob(jobKey, cancellationToken);

        var schedulingResult = await _scheduler.ExecuteJob<MoveDownloadFileJob, MoveDownloadFileJobPayload>(
            jobKey,
            new MoveDownloadFileJobPayload(downloadTaskKey),
            cancellationToken
        );
        schedulingResult.LogIfFailed();
        return schedulingResult.ToResult();
    }

    public async Task<Result> StopMoveDownloadFileJob(
        DownloadTaskKey downloadTaskKey,
        CancellationToken cancellationToken
    )
    {
        if (!downloadTaskKey.IsValid)
            return ResultExtensions.IsInvalidId(nameof(DownloadTaskKey), downloadTaskKey.Id).LogWarning();

        _log.Here()
            .Information(
                "Stopping MoveDownloadJob for {NameOfDownloadFileTask} with id: {FileTaskId}",
                nameof(DownloadTaskKey),
                downloadTaskKey.Id
            );

        var jobKey = MoveDownloadFileJob.GetJobKey(downloadTaskKey.Id);
        var isRunning = await _scheduler.IsJobRunning(jobKey, cancellationToken);
        var isCancellable = await _scheduler.IsCancellable(jobKey, cancellationToken);
        if (!isRunning && !isCancellable)
        {
            return Result
                .Fail(
                    "{MoveDownloadFileJobName} with {JobKey} cannot be stopped because it is not scheduled",
                    nameof(MoveDownloadFileJob),
                    jobKey
                )
                .LogWarning();
        }

        if (isCancellable)
        {
            var deleted = await _scheduler.DeleteJob(jobKey, cancellationToken);
            if (!deleted && await _scheduler.IsCancellable(jobKey, cancellationToken))
            {
                return Result
                    .Fail(
                        "Failed to cancel queued {MoveDownloadFileJobName} with {JobKey}",
                        nameof(MoveDownloadFileJob),
                        jobKey
                    )
                    .LogError();
            }
        }

        if (!isRunning)
            isRunning = await _scheduler.IsJobRunning(jobKey, cancellationToken);

        if (!isRunning)
            return Result.Ok();

        var interrupted = await _scheduler.Interrupt(jobKey, cancellationToken);
        if (!interrupted)
        {
            if (await _scheduler.IsJobRunning(jobKey, cancellationToken))
            {
                return Result
                    .Fail(
                        "Failed to stop {DownloadTaskKeyName} with id {Guid}",
                        nameof(DownloadTaskKey),
                        downloadTaskKey.Id
                    )
                    .LogError();
            }

            return Result.Ok();
        }

        var completionResult = await _scheduler.AwaitJobCompletion(jobKey, cancellationToken);
        if (completionResult.IsCancelled || cancellationToken.IsCancellationRequested)
            return ResultExtensions.TaskIsCancelled(nameof(StopMoveDownloadFileJob));

        return completionResult.IsSuccess ? Result.Ok() : completionResult;
    }

    public async Task<bool>
        IsDownloadFileMoving(DownloadTaskKey downloadTaskKey, CancellationToken cancellationToken) =>
        await _scheduler.IsActive(MoveDownloadFileJob.GetJobKey(downloadTaskKey.Id), cancellationToken);

    public async Task<bool> IsAnyMoveDownloadFileJobRunning() => (await _scheduler.GetCurrentlyExecutingJobs()).Any(x =>
        x.JobDetail.Key.Group == nameof(JobTypes.MoveDownloadFileJob)
    );

    public async Task<List<DownloadTaskKey>> GetCurrentlyMovingKeysByServer(int plexServerId)
    {
        var keysById = new Dictionary<Guid, DownloadTaskKey>();
        var contexts = await _scheduler.GetCurrentlyExecutingJobs(CancellationToken.None);
        foreach (var context in contexts.Where(x => x.JobDetail.Key.Group == nameof(JobTypes.MoveDownloadFileJob)))
        {
            var key = context.MergedJobDataMap.GetPayload<MoveDownloadFileJobPayload>()?.DownloadTaskKey;
            if (key is not null)
                keysById[key.Id] = key;
        }

        var jobKeys = await _scheduler.GetJobKeys(JobTypes.MoveDownloadFileJob, CancellationToken.None);
        foreach (var jobKey in jobKeys)
        {
            if (!await _scheduler.IsActive(jobKey, CancellationToken.None))
                continue;

            var key = (await _scheduler.GetJobDetail(jobKey, CancellationToken.None))?.JobDataMap
                .GetPayload<MoveDownloadFileJobPayload>()
                ?.DownloadTaskKey;
            if (key is not null)
                keysById[key.Id] = key;
        }

        return keysById.Values.Where(x => x.PlexServerId == plexServerId).ToList();
    }
}