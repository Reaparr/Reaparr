using Quartz.Impl.Matchers;

namespace Reaparr.Application;

public static partial class QuartzExtensions
{
    /// <summary>
    /// Schedules one recoverable, non-concurrent Quartz job unless its key already exists.
    /// </summary>
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

    /// <summary>
    /// Converts typed payloads into job data maps before scheduling the requested jobs.
    /// </summary>
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

    /// <summary>
    /// Schedules each missing job with a recoverable, non-concurrent one-shot trigger.
    /// </summary>
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


    /// <summary>
    /// Determines whether the specified job is currently executing.
    /// </summary>
    public static Task<bool> IsJobRunning(
        this IScheduler scheduler,
        JobKey key,
        CancellationToken cancellationToken = default
    ) => scheduler.IsJobExecuting(key, cancellationToken);

    /// <summary>
    /// Determines whether the job has a runnable non-cron trigger waiting in Quartz.
    /// </summary>
    public static async Task<bool> IsQueued(
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
            if (state is TriggerState.Normal or TriggerState.Blocked)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the job has a non-cron trigger that can be deleted, including a paused trigger.
    /// </summary>
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

    /// <summary>
    /// Searches Quartz's current execution contexts for the specified job key.
    /// </summary>
    public static async Task<bool> IsJobExecuting(
        this IScheduler scheduler,
        JobKey key,
        CancellationToken cancellationToken = default
    ) => (await scheduler.GetCurrentlyExecutingJobs(cancellationToken)).Any(x => x.JobDetail.Key.Equals(key));

    /// <summary>
    /// Cancels a queued or running job and returns failure when Quartz cannot prove the job was cancelled.
    /// </summary>
    public static async Task<Result> CancelJob(
        this IScheduler scheduler,
        JobKey key,
        CancellationToken cancellationToken = default,
        bool waitForCompletion = true
    )
    {
        var isRunning = await scheduler.IsJobExecuting(key, cancellationToken);
        var isCancellable = await scheduler.IsCancellable(key, cancellationToken);
        if (!isRunning && !isCancellable)
            return Result.Fail("Quartz job {JobKey} is not scheduled", key);

        if (isCancellable)
        {
            var deleted = await scheduler.DeleteJob(key, cancellationToken);
            if (!deleted && await scheduler.IsCancellable(key, cancellationToken))
                return Result.Fail("Failed to cancel queued Quartz job {JobKey}", key);
        }

        if (!isRunning)
            isRunning = await scheduler.IsJobExecuting(key, cancellationToken);

        if (!isRunning)
            return Result.Ok();

        var interrupted = await scheduler.Interrupt(key, cancellationToken);
        if (!interrupted)
        {
            if (await scheduler.IsJobExecuting(key, cancellationToken))
                return Result.Fail("Failed to interrupt running Quartz job {JobKey}", key);

            return Result.Ok();
        }

        if (!waitForCompletion)
            return Result.Ok();

        var completionResult = await scheduler.AwaitJobCompletion(key, cancellationToken);
        if (completionResult.IsCancelled || cancellationToken.IsCancellationRequested)
            return ResultExtensions.TaskIsCancelled(nameof(CancelJob));

        return completionResult.IsSuccess ? Result.Ok() : completionResult;
    }

    /// <summary>
    /// Deletes the supplied jobs as a batch and succeeds immediately when no keys are supplied.
    /// </summary>
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

    /// <summary>
    /// Waits for a job to stop executing until the caller cancels or the configured timeout expires.
    /// </summary>
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

    /// <summary>
    /// Returns Quartz execution contexts for jobs that are currently running.
    /// </summary>
    public static async Task<IReadOnlyCollection<IJobExecutionContext>> GetActiveJobs(
        this IScheduler scheduler,
        CancellationToken cancellationToken = default
    ) => await scheduler.GetCurrentlyExecutingJobs(cancellationToken);

    /// <summary>
    /// Determines whether a job is runnable in Quartz or is currently executing.
    /// </summary>
    public static async Task<bool> IsJobScheduledOrExecuting(
        this IScheduler scheduler,
        JobKey key,
        CancellationToken cancellationToken = default
    ) => await scheduler.IsQueued(key, cancellationToken) || await scheduler.IsJobExecuting(key, cancellationToken);

    /// <summary>
    /// Waits for all distinct job keys to finish and returns the first wait failure.
    /// </summary>
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

    /// <summary>
    /// Waits up to thirty seconds for Quartz to have no executing jobs.
    /// </summary>
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

    /// <summary>
    /// Returns the stored Quartz job keys in the group represented by the specified job type.
    /// </summary>
    public static async Task<IReadOnlyCollection<JobKey>> GetJobKeys(
        this IScheduler scheduler,
        JobTypes jobType,
        CancellationToken cancellationToken = default
    ) => await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(jobType.ToString()), cancellationToken);

    /// <summary>
    /// Determines whether any supplied key is stored by Quartz or currently executing.
    /// </summary>
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

    /// <summary>
    /// Maps currently executing Quartz jobs to started status updates.
    /// </summary>
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
}
