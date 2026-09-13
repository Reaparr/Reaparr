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
    private readonly IMediaOverviewRebuildCoordinator _coordinator;

    public MediaOverviewSnapshotJob(
        ILogger log,
        ICommandExecutor commandExecutor,
        IScheduler scheduler,
        IMediaOverviewRebuildCoordinator coordinator
    )
    {
        _log = log.ForContext<MediaOverviewSnapshotJob>();
        _commandExecutor = commandExecutor;
        _scheduler = scheduler;
        _coordinator = coordinator;
    }

    public static JobKey GetJobKey() =>
        new(nameof(JobTypes.MediaOverviewSnapshotJob), nameof(JobTypes.MediaOverviewSnapshotJob));

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        if (!_coordinator.BeginRebuild(DateTimeOffset.UtcNow))
        {
            context.SetResult(JobStatus.Completed);
            _log.Here()
                .Debug("Media overview snapshot rebuild skipped because a successful rebuild completed recently");
            return;
        }

        var succeeded = false;
        try
        {
            Result? rebuildResult = null;
            var dispatchResult = await Result.Try(async Task () =>
                rebuildResult = await _commandExecutor.Send(new RebuildMediaOverviewCommand(), cancellationToken)
            );
            var result = dispatchResult.IsFailed ? dispatchResult : rebuildResult!;

            if (result.IsCancelled)
            {
                _log.Here().Warning("Media overview snapshot rebuild was cancelled");
                context.SetResult(JobStatus.Cancelled, result);
                return;
            }

            if (result.IsFailed)
            {
                result.LogError();
                _log.Here().Error("Media overview snapshot job failed");
                context.SetResult(JobStatus.Failed, result);
                return;
            }

            succeeded = true;
            context.SetResult(JobStatus.Completed);
            _log.Here().Information("Media overview snapshots rebuilt successfully");
        }
        finally
        {
            if (_coordinator.CompleteRebuild(succeeded, DateTimeOffset.UtcNow))
            {
                var triggerResult = await TriggerFollowUpRebuildAsync(cancellationToken);
                triggerResult.LogIfFailed();
            }
        }
    }

    private async Task<Result> TriggerFollowUpRebuildAsync(CancellationToken cancellationToken)
    {
        var result = await Result.Try(async Task () => await _scheduler.TriggerJob(GetJobKey(), cancellationToken));
        if (result.IsFailed)
            _coordinator.CancelPendingRebuild();

        return result;
    }
}
