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

        if (await scheduler.Exists(jobKey, cancellationToken))
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
        var trigger = triggerBuilder
            .WithSimpleSchedule(x => x.WithMisfireInstruction(SimpleTriggerMisfireInstruction.FireNow))
            .Build();

        return await Result.Try(async Task<DateTimeOffset> () =>
            await scheduler.ScheduleJob(job, trigger, cancellationToken: cancellationToken)
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
            if (await scheduler.Exists(key, cancellationToken))
                continue;

            dataMap.SetTelemetryContext();

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
                [
                    triggerBuilder
                        .WithSimpleSchedule(x => x.WithMisfireInstruction(SimpleTriggerMisfireInstruction.FireNow))
                        .Build(),
                ]
            );
        }

        if (jobsAndTriggers.Count == 0)
            return Result.Ok();

        await scheduler.ScheduleJobs(jobsAndTriggers, new ScheduleJobOptions { Replace = false }, cancellationToken);
        return Result.Ok();
    }

    /// <summary>
    /// Determines whether the job has a runnable non-cron trigger waiting in Quartz.
    /// </summary>
    public static async Task<bool> IsQueued(
        this IScheduler scheduler,
        JobKey key,
        CancellationToken cancellationToken = default
    )
    {
        if (!await scheduler.Exists(key, cancellationToken))
            return false;

        var skip = 0;
        while (true)
        {
            var page = await scheduler.QueryTriggers(
                new TriggerQuery
                {
                    Job = key,
                    Skip = skip,
                    Take = PagedQuery.DefaultTake,
                },
                cancellationToken
            );

            foreach (var trigger in page.Items)
            {
                if (trigger.TriggerType == "CRON")
                    continue;

                if (trigger.State is TriggerState.Normal or TriggerState.Blocked)
                    return true;
            }

            if (!page.HasMore)
                return false;
            if (page.Items.Count == 0)
                throw new InvalidOperationException(
                    "Quartz returned an empty trigger page while reporting more results"
                );
            skip += page.Items.Count;
        }
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
        if (!await scheduler.Exists(key, cancellationToken))
            return false;

        var skip = 0;
        while (true)
        {
            var page = await scheduler.QueryTriggers(
                new TriggerQuery
                {
                    Job = key,
                    Skip = skip,
                    Take = PagedQuery.DefaultTake,
                },
                cancellationToken
            );

            foreach (var trigger in page.Items)
            {
                if (trigger.TriggerType == "CRON")
                    continue;

                if (trigger.State is TriggerState.Normal or TriggerState.Blocked or TriggerState.Paused)
                    return true;
            }

            if (!page.HasMore)
                return false;
            if (page.Items.Count == 0)
                throw new InvalidOperationException(
                    "Quartz returned an empty trigger page while reporting more results"
                );
            skip += page.Items.Count;
        }
    }

    /// <summary>
    /// Searches the current local-node Quartz firings for the specified job key.
    /// </summary>
    public static async Task<bool> IsJobExecuting(
        this IScheduler scheduler,
        JobKey key,
        CancellationToken cancellationToken = default
    ) => (await scheduler.GetLocalExecutingFireInstances(cancellationToken)).Any(x => x.JobKey == key);

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
    /// Deletes the supplied jobs as a batch and succeeds when no requested key remains afterward.
    /// </summary>
    public static async Task<Result> DeleteBatchJobs(
        this IScheduler scheduler,
        IReadOnlyCollection<JobKey> keys,
        CancellationToken cancellationToken = default
    )
    {
        if (!keys.Any())
            return Result.Ok();

        return await Result.Try(async Task<Result> () =>
        {
            var deletedKeys = await scheduler.DeleteJobs(keys, cancellationToken);
            if (deletedKeys.Count == keys.Distinct().Count())
                return Result.Ok();

            foreach (var key in keys.Except(deletedKeys))
            {
                if (await scheduler.Exists(key, cancellationToken))
                    return Result.Fail("Failed to delete all requested Quartz jobs");
            }

            return Result.Ok();
        });
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
    /// Waits up to thirty seconds for the local Quartz scheduler to have no executing jobs.
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
                if (!(await scheduler.GetLocalExecutingFireInstances(cancellationToken)).Any())
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
    )
    {
        var keys = new List<JobKey>();
        var skip = 0;
        while (true)
        {
            var page = await scheduler.QueryJobs(
                new JobQuery
                {
                    Group = GroupMatcher<JobKey>.GroupEquals(jobType.ToString()),
                    Skip = skip,
                    Take = PagedQuery.DefaultTake,
                },
                cancellationToken
            );
            keys.AddRange(page.Items.Select(x => x.Key));

            if (!page.HasMore)
                return keys;
            if (page.Items.Count == 0)
                throw new InvalidOperationException("Quartz returned an empty job page while reporting more results");
            skip += page.Items.Count;
        }
    }

    /// <summary>
    /// Returns the current local-node Quartz firings.
    /// </summary>
    public static async Task<IReadOnlyCollection<FireInstance>> GetLocalExecutingFireInstances(
        this IScheduler scheduler,
        CancellationToken cancellationToken = default
    )
    {
        var firings = new List<FireInstance>();
        var skip = 0;
        while (true)
        {
            var page = await scheduler.QueryFireInstances(
                new FireInstanceQuery
                {
                    SchedulerInstanceId = scheduler.SchedulerInstanceId,
                    State = FireInstanceState.Executing,
                    Skip = skip,
                    Take = PagedQuery.DefaultTake,
                },
                cancellationToken
            );
            firings.AddRange(page.Items);

            if (!page.HasMore)
                return firings;
            if (page.Items.Count == 0)
                throw new InvalidOperationException(
                    "Quartz returned an empty firing page while reporting more results"
                );
            skip += page.Items.Count;
        }
    }

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

        var runningKeys = (await scheduler.GetLocalExecutingFireInstances(cancellationToken))
            .Where(x => x.JobKey is not null)
            .Select(x => x.JobKey!);
        if (runningKeys.Any(requestedKeys.Contains))
            return true;

        var skip = 0;
        while (true)
        {
            var page = await scheduler.QueryJobs(
                new JobQuery { Skip = skip, Take = PagedQuery.DefaultTake },
                cancellationToken
            );
            if (page.Items.Any(x => requestedKeys.Contains(x.Key)))
                return true;
            if (!page.HasMore)
                return false;
            if (page.Items.Count == 0)
                throw new InvalidOperationException("Quartz returned an empty job page while reporting more results");
            skip += page.Items.Count;
        }
    }

    /// <summary>
    /// Maps currently executing Quartz jobs to started status updates.
    /// </summary>
    public static async Task<List<JobStatusUpdate<string>>> GetRunningJobUpdates(
        this IScheduler scheduler,
        CancellationToken cancellationToken = default
    )
    {
        var firings = await scheduler.GetLocalExecutingFireInstances(cancellationToken);
        var jobKeys = firings.Where(x => x.JobKey is not null).Select(x => x.JobKey!).Distinct().ToArray();
        var details = await scheduler.GetJobDetails(jobKeys, cancellationToken);
        var detailsByKey = details.ToDictionary(x => x.Key);

        return firings
            .Where(x => x.JobKey is not null && detailsByKey.ContainsKey(x.JobKey!))
            .Select(firing => new JobStatusUpdate<string>(
                JobStatusUpdateMapper.ToJobType(firing.JobKey!.Group),
                JobStatus.Started,
                GetStoredPayloadAsJson(detailsByKey[firing.JobKey!]),
                firing.JobKey!.Name,
                firing.FireTimeUtc.UtcDateTime
            ))
            .ToList();
    }

    private static string GetStoredPayloadAsJson(IJobDetail job)
    {
        return JobStatusUpdateMapper.ToJobType(job.Key.Group) switch
        {
            JobTypes.DownloadJob => JsonSerializer.Serialize(
                job.JobDataMap.GetPayload<DownloadJobPayload>(),
                DefaultJsonSerializerOptions.ConfigStandard
            ),
            JobTypes.MoveDownloadFileJob => JsonSerializer.Serialize(
                job.JobDataMap.GetPayload<MoveDownloadFileJobPayload>(),
                DefaultJsonSerializerOptions.ConfigStandard
            ),
            JobTypes.InspectPlexServerJob => JsonSerializer.Serialize(
                job.JobDataMap.GetPayload<InspectPlexServerJobPayload>(),
                DefaultJsonSerializerOptions.ConfigStandard
            ),
            JobTypes.LibrarySyncJob => JsonSerializer.Serialize(
                job.JobDataMap.GetPayload<LibrarySyncJobPayload>(),
                DefaultJsonSerializerOptions.ConfigStandard
            ),
            JobTypes.LibraryComparisonJob => JsonSerializer.Serialize(
                job.JobDataMap.GetPayload<PlexLibraryComparisonJobPayload>(),
                DefaultJsonSerializerOptions.ConfigStandard
            ),
            JobTypes.CheckAllConnectionsStatusByPlexServerJob => "{}",
            JobTypes.MetadataSyncJob
            or JobTypes.CheckForUpdateJob
            or JobTypes.CheckPlexLibrariesForUpdatesJob
            or JobTypes.RefreshPlexAccountAccessJob
            or JobTypes.MediaOverviewSnapshotJob
            or JobTypes.OptimizeDatabaseJob
            or JobTypes.UpdateScheduledDownloadLimitsJob => "{}",
            _ => throw new ArgumentOutOfRangeException(nameof(job), job.Key.Group, "Unknown job type"),
        };
    }
}
