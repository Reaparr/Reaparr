using Quartz.Impl.Matchers;

namespace Reaparr.Application;

public interface IBackgroundJobsSetup : ISetupAsync, IStopAsync;

public sealed class BackgroundJobsSetup : IBackgroundJobsSetup
{
    private readonly IScheduler _scheduler;
    private readonly IAppRuntimeInfo _appRuntimeInfo;
    private readonly AllJobListener _allJobListener;
    private readonly DownloadJobListener _downloadJobListener;
    private readonly LibrarySyncJobListener _librarySyncJobListener;
    private readonly SchedulerListener _schedulerListener;

    public BackgroundJobsSetup(
        IScheduler scheduler,
        IAppRuntimeInfo appRuntimeInfo,
        AllJobListener allJobListener,
        DownloadJobListener downloadJobListener,
        LibrarySyncJobListener librarySyncJobListener,
        SchedulerListener schedulerListener
    )
    {
        _scheduler = scheduler;
        _appRuntimeInfo = appRuntimeInfo;
        _allJobListener = allJobListener;
        _downloadJobListener = downloadJobListener;
        _librarySyncJobListener = librarySyncJobListener;
        _schedulerListener = schedulerListener;
    }

    public async Task<Result> SetupAsync(CancellationToken cancellationToken = default)
    {
        return await Result.Try(async Task () =>
        {
            RegisterListeners();

            if (_appRuntimeInfo.IsIntegrationTestMode)
            {
                await _scheduler.Start(cancellationToken);
                return;
            }

            await SetupRecurringJobs(cancellationToken);

            await _scheduler.Start(cancellationToken);

            await TriggerRecurringJobs(cancellationToken);
        });
    }

    private void RegisterListeners()
    {
        _scheduler.ListenerManager.AddJobListener(_allJobListener, EverythingMatcher<JobKey>.AllJobs());
        _scheduler.ListenerManager.AddJobListener(
            _downloadJobListener,
            GroupMatcher<JobKey>.GroupEquals(nameof(JobTypes.DownloadJob))
        );
        _scheduler.ListenerManager.AddJobListener(
            _librarySyncJobListener,
            GroupMatcher<JobKey>.GroupEquals(nameof(JobTypes.LibrarySyncJob))
        );
        _scheduler.ListenerManager.AddSchedulerListener(_schedulerListener);
    }

    private async Task SetupRecurringJobs(CancellationToken cancellationToken)
    {
        await _scheduler.DeleteJob(CheckAllConnectionsStatusByPlexServerJob.GetJobKey(), cancellationToken);
        await _scheduler.DeleteJob(CheckPlexLibrariesForUpdatesJob.GetJobKey(), cancellationToken);
        await _scheduler.DeleteJob(RefreshPlexAccountAccessJob.GetJobKey(), cancellationToken);
        await _scheduler.DeleteJob(CheckForUpdateJob.GetJobKey(), cancellationToken);
        await _scheduler.DeleteJob(MediaOverviewSnapshotJob.GetJobKey(), cancellationToken);

        {
            var jobKey = CheckAllConnectionsStatusByPlexServerJob.GetJobKey();
            var job = JobBuilder
                .Create<CheckAllConnectionsStatusByPlexServerJob>()
                .WithIdentity(jobKey)
                .DisallowConcurrentExecution()
                .StoreDurably()
                .RequestRecovery()
                .Build();
            var trigger = TriggerBuilder
                .Create()
                .WithIdentity(jobKey.Name, jobKey.Group)
                .ForJob(jobKey)
                .WithCronSchedule(
                    "0 0/10 * * * ?", // Every 10 minutes
                    x => x.InTimeZone(TimeZoneInfo.Utc).WithMisfireHandlingInstructionDoNothing()
                )
                .Build();

            await _scheduler.ScheduleJob(job, trigger, cancellationToken);
        }

        {
            var jobKey = CheckPlexLibrariesForUpdatesJob.GetJobKey();
            var job = JobBuilder
                .Create<CheckPlexLibrariesForUpdatesJob>()
                .WithIdentity(jobKey)
                .DisallowConcurrentExecution()
                .StoreDurably()
                .RequestRecovery()
                .Build();
            var trigger = TriggerBuilder
                .Create()
                .WithIdentity(jobKey.Name, jobKey.Group)
                .ForJob(jobKey)
                .WithCronSchedule(
                    "0 0 0/3 * * ?", // Every 3 hours
                    x => x.InTimeZone(TimeZoneInfo.Utc).WithMisfireHandlingInstructionDoNothing()
                )
                .Build();

            await _scheduler.ScheduleJob(job, trigger, cancellationToken);
        }

        {
            var jobKey = RefreshPlexAccountAccessJob.GetJobKey();
            var job = JobBuilder
                .Create<RefreshPlexAccountAccessJob>()
                .WithIdentity(jobKey)
                .DisallowConcurrentExecution()
                .StoreDurably()
                .RequestRecovery()
                .Build();
            var trigger = TriggerBuilder
                .Create()
                .WithIdentity(jobKey.Name, jobKey.Group)
                .ForJob(jobKey)
                .WithCronSchedule(
                    "0 0 0/6 * * ?", // Every 6 hours
                    x => x.InTimeZone(TimeZoneInfo.Utc).WithMisfireHandlingInstructionDoNothing()
                )
                .Build();

            await _scheduler.ScheduleJob(job, trigger, cancellationToken);
        }

        {
            var jobKey = CheckForUpdateJob.GetJobKey();
            var job = JobBuilder
                .Create<CheckForUpdateJob>()
                .WithIdentity(jobKey)
                .DisallowConcurrentExecution()
                .StoreDurably()
                .RequestRecovery()
                .Build();
            var trigger = TriggerBuilder
                .Create()
                .WithIdentity(jobKey.Name, jobKey.Group)
                .ForJob(jobKey)
                .WithCronSchedule(
                    "0 0 * * * ?", // Every hour
                    x => x.InTimeZone(TimeZoneInfo.Utc).WithMisfireHandlingInstructionDoNothing()
                )
                .Build();

            await _scheduler.ScheduleJob(job, trigger, cancellationToken);
        }
        {
            var jobKey = MediaOverviewSnapshotJob.GetJobKey();
            var job = JobBuilder
                .Create<MediaOverviewSnapshotJob>()
                .WithIdentity(jobKey)
                .DisallowConcurrentExecution()
                .StoreDurably()
                .RequestRecovery()
                .Build();

            var trigger = TriggerBuilder
                .Create()
                .WithIdentity(jobKey.Name, jobKey.Group)
                .ForJob(jobKey)
                .StartNow()
                .WithCronSchedule(
                    "0 0 * * * ?", // Every hour
                    x => x.InTimeZone(TimeZoneInfo.Utc).WithMisfireHandlingInstructionDoNothing()
                )
                .Build();

            await _scheduler.ScheduleJob(job, trigger, cancellationToken);
        }
    }

    private async Task TriggerRecurringJobs(CancellationToken cancellationToken)
    {
        await _scheduler.TriggerJob(CheckAllConnectionsStatusByPlexServerJob.GetJobKey(), cancellationToken);
        await _scheduler.TriggerJob(CheckPlexLibrariesForUpdatesJob.GetJobKey(), cancellationToken);
        await _scheduler.TriggerJob(RefreshPlexAccountAccessJob.GetJobKey(), cancellationToken);
        await _scheduler.TriggerJob(CheckForUpdateJob.GetJobKey(), cancellationToken);
        await _scheduler.TriggerJob(MediaOverviewSnapshotJob.GetJobKey(), cancellationToken);
    }
    public async Task<Result> StopAsync(CancellationToken cancellationToken = default)
    {
        return await Result.Try(async Task () =>
        {
            using var shutdownTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            shutdownTimeout.CancelAfter(TimeSpan.FromSeconds(30));
            await _scheduler.Standby(shutdownTimeout.Token);

            foreach (var context in await _scheduler.GetCurrentlyExecutingJobs(shutdownTimeout.Token))
                await _scheduler.Interrupt(context.FireInstanceId, shutdownTimeout.Token);

            await _scheduler.Shutdown(waitForJobsToComplete: true, shutdownTimeout.Token);
        });
    }
}
