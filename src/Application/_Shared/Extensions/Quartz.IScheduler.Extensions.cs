using Quartz.Impl.Matchers;

namespace Reaparr.Application;

public static partial class QuartzExtensions
{
    public static async Task<Result<DateTimeOffset>> ExecuteJob<TJob, TPayload>(
        this IScheduler scheduler,
        JobKey jobKey,
        TPayload payload,
        CancellationToken cancellationToken = default,
        DateTimeOffset? executeAt = null
    )
        where TJob : IJob
    {
        if (!Enum.TryParse<JobTypes>(jobKey.Group, out _))
            return Result.Fail(
                "Quartz job group '{JobKeyGroup}' is not a valid {JobTypesName} value",
                jobKey.Group,
                nameof(JobTypes)
            );

        if (await scheduler.CheckExists(jobKey, cancellationToken))
            return Result.Ok(DateTimeOffset.MinValue);

        var job = JobBuilder
            .Create<TJob>()
            .WithIdentity(jobKey)
            .UsingJobData(payload.ToJobDataMap())
            .DisallowConcurrentExecution()
            .RequestRecovery()
            .Build();
        var triggerBuilder = TriggerBuilder.Create().WithIdentity(jobKey.Name, jobKey.Group).ForJob(job);
        triggerBuilder = executeAt.HasValue ? triggerBuilder.StartAt(executeAt.Value) : triggerBuilder.StartNow();
        var trigger = triggerBuilder.WithSimpleSchedule(x => x.WithMisfireHandlingInstructionFireNow()).Build();

        return await Result.Try(async Task<DateTimeOffset> () =>
            await scheduler.ScheduleJob(job, trigger, cancellationToken)
        );
    }

    public static Task<Result> ExecuteJobs<TJob, TPayload>(
        this IScheduler scheduler,
        IReadOnlyCollection<(JobKey JobKey, TPayload Payload)> jobs,
        CancellationToken cancellationToken = default,
        DateTimeOffset? executeAt = null
    )
        where TJob : IJob =>
        scheduler.ExecuteJobs<TJob>(
            jobs.Select(x => (x.JobKey, x.Payload.ToJobDataMap())).ToList(),
            cancellationToken,
            executeAt
        );

    public static async Task<Result> ExecuteJobs<TJob>(
        this IScheduler scheduler,
        IReadOnlyCollection<(JobKey Key, JobDataMap DataMap)> jobs,
        CancellationToken cancellationToken = default,
        DateTimeOffset? executeAt = null
    )
        where TJob : IJob
    {
        var jobsAndTriggers = new Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>>();
        foreach (var (key, dataMap) in jobs)
        {
            if (await scheduler.CheckExists(key, cancellationToken))
                continue;

            var detail = JobBuilder
                .Create<TJob>()
                .WithIdentity(key)
                .UsingJobData(dataMap)
                .DisallowConcurrentExecution()
                .RequestRecovery()
                .Build();
            var triggerBuilder = TriggerBuilder.Create().WithIdentity(key.Name, key.Group).ForJob(detail);
            triggerBuilder = executeAt.HasValue ? triggerBuilder.StartAt(executeAt.Value) : triggerBuilder.StartNow();
            jobsAndTriggers.Add(
                detail,
                [triggerBuilder.WithSimpleSchedule(x => x.WithMisfireHandlingInstructionFireNow()).Build()]
            );
        }

        if (jobsAndTriggers.Count == 0)
            return Result.Ok();

        await scheduler.ScheduleJobs(jobsAndTriggers, replace: false, cancellationToken);
        return Result.Ok();
    }

    public static Task<bool> IsJobRunning(
        this IScheduler scheduler,
        JobKey key,
        CancellationToken cancellationToken = default
    ) => scheduler.IsJobExecuting(key, cancellationToken);

    public static Task<bool> IsQueued(
        this IScheduler scheduler,
        JobKey key,
        CancellationToken cancellationToken = default
    ) => scheduler.IsQueuedInternal(key, cancellationToken);

    public static async Task<bool> IsCancellable(
        this IScheduler scheduler,
        JobKey key,
        CancellationToken cancellationToken = default
    )
    {
        if (!await scheduler.CheckExists(key, cancellationToken))
            return false;

        foreach (var trigger in await scheduler.GetTriggersOfJob(key, cancellationToken))
        {
            if (trigger is ICronTrigger)
                continue;

            var state = await scheduler.GetTriggerState(trigger.Key, cancellationToken);
            if (state is TriggerState.Normal or TriggerState.Blocked or TriggerState.Paused)
                return true;
        }

        return false;
    }

    public static async Task<bool> IsJobExecuting(
        this IScheduler scheduler,
        JobKey key,
        CancellationToken cancellationToken = default
    ) => (await scheduler.GetCurrentlyExecutingJobs(cancellationToken)).Any(x => x.JobDetail.Key.Equals(key));

    public static async Task<Result> CancelJob(
        this IScheduler scheduler,
        JobKey key,
        CancellationToken cancellationToken = default,
        bool waitForCompletion = true
    )
    {
        var executing = await scheduler.IsJobExecuting(key, cancellationToken);
        if (executing)
            await scheduler.Interrupt(key, cancellationToken);
        if (await scheduler.CheckExists(key, cancellationToken))
            await scheduler.DeleteJob(key, cancellationToken);

        if (executing && waitForCompletion && !cancellationToken.IsCancellationRequested)
            await scheduler.AwaitJobCompletion(key, cancellationToken);

        return Result.Ok();
    }

    public static async Task<Result> DeleteBatchJobs(
        this IScheduler scheduler,
        IReadOnlyCollection<JobKey> keys,
        CancellationToken cancellationToken = default
    )
    {
        if (!keys.Any())
            return Result.Ok();

        return await Result.Try(async Task () => await scheduler.DeleteJobs(keys.ToList(), cancellationToken));
    }

    public static async Task<Result> AwaitJobCompletion(
        this IScheduler scheduler,
        JobKey key,
        CancellationToken cancellationToken = default,
        int timeoutSeconds = 30
    )
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        return await Result.Try(async Task<Result> () =>
        {
            while (await scheduler.IsJobExecuting(key, timeout.Token))
                await Task.Delay(200, timeout.Token);

            return Result.Ok();
        });
    }

    public static async Task<IReadOnlyCollection<IJobExecutionContext>> GetActiveJobs(
        this IScheduler scheduler,
        CancellationToken cancellationToken = default
    ) => await scheduler.GetCurrentlyExecutingJobs(cancellationToken);

    public static async Task<bool> IsActive(
        this IScheduler scheduler,
        JobKey key,
        CancellationToken cancellationToken = default
    ) => await scheduler.IsQueued(key, cancellationToken) || await scheduler.IsJobExecuting(key, cancellationToken);

    public static async Task<Result> WaitForJobsToFinish(
        this IScheduler scheduler,
        IEnumerable<JobKey> keys,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        var results = await Task.WhenAll(
            keys.Distinct()
                .Select(key =>
                    scheduler.AwaitJobCompletion(key, cancellationToken, (int)Math.Ceiling(timeout.TotalSeconds))
                )
        );
        return results.FirstOrDefault(x => x.IsFailed) ?? Result.Ok();
    }

    public static async Task<Result> AwaitScheduler(
        this IScheduler scheduler,
        CancellationToken cancellationToken = default
    ) =>
        await Result.Try(async Task<Result> () =>
        {
            var timeoutAt = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < timeoutAt)
            {
                if ((await scheduler.GetCurrentlyExecutingJobs(cancellationToken)).Count == 0)
                    return Result.Ok();

                await Task.Delay(100, cancellationToken);
            }
            return Result.Fail("Timed out waiting for Quartz scheduler jobs to complete");
        });

    public static async Task<IReadOnlyCollection<JobKey>> GetJobKeys(
        this IScheduler scheduler,
        JobTypes jobType,
        CancellationToken cancellationToken = default
    ) => await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(jobType.ToString()), cancellationToken);

    public static async Task<bool> HasActiveJobs(
        this IScheduler scheduler,
        IEnumerable<JobKey> keys,
        CancellationToken cancellationToken = default
    )
    {
        var requestedKeys = keys.ToHashSet();
        if (requestedKeys.Count == 0)
            return false;

        var executingKeys = (await scheduler.GetCurrentlyExecutingJobs(cancellationToken))
            .Select(x => x.JobDetail.Key)
            .ToHashSet();
        var queuedKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken);

        return queuedKeys.Concat(executingKeys).Any(requestedKeys.Contains);
    }

    public static async Task<List<JobStatusUpdate<string>>> GetRunningJobUpdates(
        this IScheduler scheduler,
        CancellationToken cancellationToken = default
    )
    {
        return (await scheduler.GetCurrentlyExecutingJobs(cancellationToken))
            .Select(context => new JobStatusUpdate<string>(
                JobStatusUpdateMapper.ToJobType(context.JobDetail.Key.Group),
                JobStatus.Started,
                context.GetPayloadAsJson(),
                context.JobDetail.Key.Name,
                context.FireTimeUtc.UtcDateTime
            ))
            .ToList();
    }

    private static async Task<bool> IsQueuedInternal(
        this IScheduler scheduler,
        JobKey key,
        CancellationToken cancellationToken
    )
    {
        if (!await scheduler.CheckExists(key, cancellationToken))
            return false;

        foreach (var trigger in await scheduler.GetTriggersOfJob(key, cancellationToken))
        {
            if (trigger is ICronTrigger)
                continue;

            var state = await scheduler.GetTriggerState(trigger.Key, cancellationToken);
            if (state is TriggerState.Normal or TriggerState.Blocked)
                return true;
        }

        return false;
    }
}
