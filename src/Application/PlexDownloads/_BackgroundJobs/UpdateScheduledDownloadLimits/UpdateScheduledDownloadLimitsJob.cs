namespace Reaparr.Application;

/// <summary>
/// Dispatches scheduled download limit recalculation.
/// </summary>
[DisallowConcurrentExecution]
public sealed class UpdateScheduledDownloadLimitsJob : IJob
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public UpdateScheduledDownloadLimitsJob(ICommandExecutor commandExecutor, ITelemetry telemetry)
    {
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public static JobKey GetJobKey() =>
        new(nameof(UpdateScheduledDownloadLimitsJob), nameof(UpdateScheduledDownloadLimitsJob));

    public static TriggerKey GetTriggerKey() =>
        new(nameof(UpdateScheduledDownloadLimitsJob), nameof(UpdateScheduledDownloadLimitsJob));

    public static ITrigger CreateTrigger(string timeZoneId) =>
        TriggerBuilder
            .Create()
            .WithIdentity(GetTriggerKey())
            .ForJob(GetJobKey())
            .WithCronSchedule("0 0,30 * * * ?", x =>
                x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(timeZoneId))
                    .WithMisfireInstruction(CronTriggerMisfireInstruction.FireAndProceed)
            )
            .Build();

    /// <inheritdoc />
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var telemetryScope = _telemetry.BeginJob(
            TelemetryActivity.ScheduledDownloadLimits,
            context.GetTelemetryContext()
        );

        var result = await _commandExecutor.Send(new UpdateScheduledDownloadLimitsCommand(), cancellationToken);
        var status =
            result.IsCancelled ? JobStatus.Cancelled
            : result.IsFailed ? JobStatus.Failed
            : JobStatus.Completed;

        _telemetry.CompleteScheduledDownloadLimits(Activity.Current, result);
        if (result.IsCancelledOrFailed)
        {
            result.LogIfFailed();
            _telemetry.CaptureErrors(result, TelemetryActivity.ScheduledDownloadLimits);
        }

        context.SetResult(status, result);
    }
}
