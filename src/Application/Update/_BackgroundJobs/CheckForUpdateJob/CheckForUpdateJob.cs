namespace Reaparr.Application;

/// <summary>
/// Dispatches <see cref="CheckForUpdatesCommand"/> on a recurring schedule.
/// Publishes update availability after background checks, keeping HTTP checks free of notification feedback.
/// </summary>
[DisallowConcurrentExecution]
public class CheckForUpdateJob : IJob
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly ILogger _log;
    private readonly INotificationHubService _notificationHubService;
    private readonly ITelemetry _telemetry;

    public static JobKey GetJobKey() => new(nameof(CheckForUpdateJob), nameof(CheckForUpdateJob));

    public CheckForUpdateJob(
        ILogger log,
        ICommandExecutor commandExecutor,
        INotificationHubService notificationHubService,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<CheckForUpdateJob>();
        _commandExecutor = commandExecutor;
        _notificationHubService = notificationHubService;
        _telemetry = telemetry;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var telemetryScope = _telemetry.BeginJob(TelemetryActivity.UpdateCheck, context.GetTelemetryContext());

        _log.Here().Debug("Executing job: {JobName}", nameof(CheckForUpdateJob));

        // Jobs must not throw — Quartz would otherwise keep re-executing.
        var result = await Result.Try(async Task<Result<AppUpdateCheckResult>> () =>
        {
            var checkResult = await _commandExecutor.Send(new CheckForUpdatesCommand(), context.CancellationToken);
            if (checkResult.IsCancelledOrFailed)
                return checkResult;

            if (checkResult.Value.IsUpdateAvailable)
                await _notificationHubService.SendRefreshNotificationAsync(RefreshDataType.UpdateAvailable);

            return checkResult;
        });

        if (result.IsCancelled)
        {
            context.SetResult(JobStatus.Cancelled, result);
            result.LogWarning();
        }
        else if (result.IsFailed)
        {
            context.SetResult(JobStatus.Failed, result);
            result.LogError();
            _telemetry.CaptureErrors(result, TelemetryActivity.UpdateCheck);
        }
    }
}
