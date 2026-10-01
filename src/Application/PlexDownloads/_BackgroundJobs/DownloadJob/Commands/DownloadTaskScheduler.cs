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
            if (await _scheduler.IsActive(jobKey, cancellationToken))
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

        return (
            await Result.Try(async Task<Result> () =>
            {
                _log.Here().Information("Stopping DownloadClient for DownloadTaskId {DownloadTaskId}", downloadTaskKey);

                var jobKey = DownloadJob.GetJobKey(downloadTaskKey.Id);
                var isRunning = await _scheduler.IsJobRunning(jobKey, cancellationToken);
                var isCancellable = await _scheduler.IsCancellable(jobKey, cancellationToken);
                if (!isRunning && !isCancellable)
                {
                    return Result
                        .Fail(
                            "{DownloadJobName} with {JobKey} cannot be stopped because it is not scheduled",
                            nameof(DownloadJob),
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
                                "Failed to cancel queued {DownloadJobName} with {JobKey}",
                                nameof(DownloadJob),
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
                                "Failed to stop {DownloadTaskGenericName} with id {DownloadTaskKey}",
                                nameof(DownloadTaskGeneric),
                                downloadTaskKey
                            )
                            .LogError();
                    }

                    return Result.Ok();
                }

                if (!waitForCompletion)
                    return Result.Ok();

                var completionResult = await _scheduler.AwaitJobCompletion(jobKey, cancellationToken);
                if (completionResult.IsCancelled || cancellationToken.IsCancellationRequested)
                    return ResultExtensions.TaskIsCancelled(nameof(StopDownloadTaskJob));

                return completionResult.IsSuccess ? Result.Ok() : completionResult;
            })
        ).LogIfFailed();
    }


    public async Task<bool> IsDownloading(
        DownloadTaskKey downloadTaskKey,
        CancellationToken cancellationToken = default)
    {
        var jobKey = DownloadJob.GetJobKey(downloadTaskKey.Id);
        return await _scheduler.IsActive(jobKey, cancellationToken);
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
            if (!await _scheduler.IsActive(jobKey, CancellationToken.None))
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