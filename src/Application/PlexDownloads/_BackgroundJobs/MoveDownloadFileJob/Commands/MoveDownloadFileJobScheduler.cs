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
        if (await _scheduler.IsJobScheduledOrExecuting(jobKey, cancellationToken))
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
        var cancelResult = await _scheduler.CancelJob(jobKey, cancellationToken);
        if (cancelResult.IsCancelled)
            return ResultExtensions.TaskIsCancelled(nameof(StopMoveDownloadFileJob)).LogWarning();

        if (cancelResult.IsFailed)
            return cancelResult.LogIfFailed();

        return Result.Ok();
    }

    public async Task<bool>
        IsDownloadFileMoving(DownloadTaskKey downloadTaskKey, CancellationToken cancellationToken) =>
        await _scheduler.IsJobScheduledOrExecuting(MoveDownloadFileJob.GetJobKey(downloadTaskKey.Id), cancellationToken);

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
            if (!await _scheduler.IsJobScheduledOrExecuting(jobKey, CancellationToken.None))
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