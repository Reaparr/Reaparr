namespace Reaparr.Application;

public class DownloadTaskScheduler : IDownloadTaskScheduler
{
    private readonly ILogger _log;
    private readonly IScheduler _scheduler;

    public DownloadTaskScheduler(ILogger log, IScheduler scheduler)
    {
        _log = log.ForContext<DownloadTaskScheduler>();
        _scheduler = scheduler;
    }

    public async Task<Result> StartDownloadTaskJob(
        DownloadTaskKey downloadTaskKey,
        CancellationToken cancellationToken = default
    )
    {
        if (!downloadTaskKey.IsValid)
            return ResultExtensions.IsInvalidId(nameof(DownloadTaskKey), downloadTaskKey.Id).LogWarning();

        return await Result.Try(async Task<Result> () =>
        {
            var jobKey = DownloadJob.GetJobKey(downloadTaskKey.Id);
            if (await _scheduler.IsJobScheduledOrExecuting(jobKey, cancellationToken))
            {
                _log.Here().Debug("{DownloadJobName} with {JobKey} is already scheduled", nameof(DownloadJob), jobKey);
                return Result.Ok();
            }

            if (await _scheduler.CheckExists(jobKey, cancellationToken))
                await _scheduler.DeleteJob(jobKey, cancellationToken);

            var schedulingResult = await _scheduler.ExecuteJob<DownloadJob, DownloadJobPayload>(
                jobKey,
                new DownloadJobPayload(downloadTaskKey),
                cancellationToken
            );

            schedulingResult.LogIfFailed();
            return schedulingResult.ToResult();
        });
    }

    public async Task<Result> StopDownloadTaskJob(
        DownloadTaskKey downloadTaskKey,
        CancellationToken cancellationToken = default,
        bool waitForCompletion = true
    )
    {
        if (!downloadTaskKey.IsValid)
            return ResultExtensions.IsInvalidId(nameof(DownloadTaskKey), downloadTaskKey.Id).LogWarning();

        var jobKey = DownloadJob.GetJobKey(downloadTaskKey.Id);
        _log.Here().Information("Stopping DownloadClient for DownloadTaskId {DownloadTaskId}", downloadTaskKey);

        var cancelResult = await _scheduler.CancelJob(jobKey, cancellationToken, waitForCompletion);
        if (cancelResult.IsCancelled)
            return ResultExtensions.TaskIsCancelled(nameof(StopDownloadTaskJob)).LogWarning();

        if (cancelResult.IsFailed)
            return cancelResult.LogIfFailed();

        return Result.Ok();
    }


    public async Task<bool> IsDownloading(
        DownloadTaskKey downloadTaskKey,
        CancellationToken cancellationToken = default)
    {
        var jobKey = DownloadJob.GetJobKey(downloadTaskKey.Id);
        return await _scheduler.IsJobScheduledOrExecuting(jobKey, cancellationToken);
    }

    public async Task<List<DownloadTaskKey>> GetCurrentlyDownloadingKeysByServer(int plexServerId)
    {
        var keysById = new Dictionary<Guid, DownloadTaskKey>();
        var contexts = await _scheduler.GetCurrentlyExecutingJobs(CancellationToken.None);
        foreach (var context in contexts.Where(x => x.JobDetail.Key.Group == nameof(JobTypes.DownloadJob)))
        {
            var key = context.MergedJobDataMap.GetPayload<DownloadJobPayload>()?.DownloadTaskKey;
            if (key is not null)
                keysById[key.Id] = key;
        }

        var jobKeys = await _scheduler.GetJobKeys(JobTypes.DownloadJob, CancellationToken.None);
        foreach (var jobKey in jobKeys)
        {
            if (!await _scheduler.IsJobScheduledOrExecuting(jobKey, CancellationToken.None))
                continue;

            var key = (await _scheduler.GetJobDetail(jobKey, CancellationToken.None))?.JobDataMap
                .GetPayload<DownloadJobPayload>()
                ?.DownloadTaskKey;
            if (key is not null)
                keysById[key.Id] = key;
        }

        return keysById.Values.Where(x => x.PlexServerId == plexServerId).ToList();
    }

    public async Task<bool> IsServerDownloading(int plexServerId)
    {
        return (await GetCurrentlyDownloadingKeysByServer(plexServerId)).Any(x => x.PlexServerId == plexServerId);
    }
}