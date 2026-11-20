namespace Reaparr.Application;

/// <summary>
/// Rebuilds all persisted root-media overview snapshots sequentially.
/// </summary>
[DisallowConcurrentExecution]
public sealed class MediaOverviewSnapshotJob : IJob
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IScheduler _scheduler;
    private readonly IMediaOverviewRebuildCoordinator _coordinator;
    private readonly ITelemetry _telemetry;

    public MediaOverviewSnapshotJob(
        ILogger log,
        ICommandExecutor commandExecutor,
        IScheduler scheduler,
        IMediaOverviewRebuildCoordinator coordinator,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<MediaOverviewSnapshotJob>();
        _commandExecutor = commandExecutor;
        _scheduler = scheduler;
        _coordinator = coordinator;
        _telemetry = telemetry;
    }

    public static JobKey GetJobKey() =>
        new(nameof(JobTypes.MediaOverviewSnapshotJob), nameof(JobTypes.MediaOverviewSnapshotJob));

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var telemetryScope = _telemetry.BeginJob(
            TelemetryActivity.MediaOverviewSnapshot,
            context.GetTelemetryContext()
        );

        var scope = _coordinator.BeginRebuild(DateTimeOffset.UtcNow);
        if (scope is null)
        {
            context.SetResult(JobStatus.Completed);
            _log.Here().Debug("Media overview snapshot rebuild skipped because no pending media types were requested");
            return;
        }

        var succeeded = false;
        try
        {
            Result? rebuildResult = null;
            var dispatchResult = await Result.Try(async Task () =>
                rebuildResult = await _commandExecutor.Send(
                    scope.RebuildAll
                        ? new RebuildMediaOverviewCommand()
                        : new RebuildMediaOverviewCommand(scope.MediaTypes),
                    cancellationToken
                )
            );
            var result = dispatchResult.IsCancelledOrFailed ? dispatchResult : rebuildResult!;

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
                _telemetry.CaptureErrors(result, TelemetryActivity.MediaOverviewSnapshot);
                return;
            }

            succeeded = true;
            context.SetResult(JobStatus.Completed);
            _log.Here().Information("Media overview snapshots rebuilt successfully");
        }
        finally
        {
            var followUpScope = _coordinator.CompleteRebuild(succeeded, DateTimeOffset.UtcNow);
            if (followUpScope is not null)
            {
                var triggerResult = await TriggerFollowUpRebuildAsync(cancellationToken);
                triggerResult.LogIfFailed();
                if (triggerResult.IsFailed)
                    _telemetry.CaptureErrors(triggerResult, TelemetryActivity.MediaOverviewSnapshot);
            }
        }
    }

    private async Task<Result> TriggerFollowUpRebuildAsync(CancellationToken cancellationToken)
    {
        var result = await Result.Try(async Task () =>
            await _scheduler.TriggerJob(GetJobKey(), cancellationToken: cancellationToken)
        );
        if (result.IsFailed)
            _coordinator.CancelPendingRebuild();

        return result;
    }
}
