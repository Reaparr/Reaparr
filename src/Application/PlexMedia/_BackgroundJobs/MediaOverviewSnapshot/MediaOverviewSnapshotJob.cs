namespace Reaparr.Application;

/// <summary>
/// Rebuilds both persisted root-media overview snapshots sequentially.
/// </summary>
[DisallowConcurrentExecution]
public sealed class MediaOverviewSnapshotJob : IJob
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IScheduler _scheduler;

    public MediaOverviewSnapshotJob(ILogger log, ICommandExecutor commandExecutor, IScheduler scheduler)
    {
        _log = log.ForContext<MediaOverviewSnapshotJob>();
        _commandExecutor = commandExecutor;
        _scheduler = scheduler;
    }

    public static JobKey GetJobKey() =>
        new(nameof(JobTypes.MediaOverviewSnapshotJob), nameof(JobTypes.MediaOverviewSnapshotJob));

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var result = await _commandExecutor.Send(new RebuildMediaOverviewCommand(), cancellationToken);

        result.LogIfFailed();

        if (result.IsCancelled)
        {
            _log.Here().Warning("Media overview snapshot rebuild was cancelled");
            context.SetResult(JobStatus.Cancelled, result);
            return;
        }

        if (result.IsFailed)
        {
            _log.Here().Error("Media overview snapshot job failed");
            context.SetResult(JobStatus.Failed, result);
            return;
        }

        context.SetResult(JobStatus.Completed);
        _log.Here().Information("Media overview snapshots rebuilt successfully");

        var scheduleResult = await ScheduleNextRunAsync(cancellationToken);
        scheduleResult.LogIfFailed();
    }

    private async Task<Result> ScheduleNextRunAsync(CancellationToken cancellationToken)
    {
        var trigger = TriggerBuilder
            .Create()
            .WithIdentity(GetJobKey().Name, GetJobKey().Group)
            .ForJob(GetJobKey())
            .StartAt(DateTimeOffset.UtcNow.AddHours(1))
            .WithSimpleSchedule(x => x.WithMisfireHandlingInstructionFireNow())
            .Build();
        return await Result.Try(async Task () =>
            await _scheduler.RescheduleJob(trigger.Key, trigger, cancellationToken)
        );
    }
}
