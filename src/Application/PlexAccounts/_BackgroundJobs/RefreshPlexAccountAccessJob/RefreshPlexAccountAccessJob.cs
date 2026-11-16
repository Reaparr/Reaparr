namespace Reaparr.Application;

/// <summary>
/// Periodically validates enabled Plex accounts and refreshes their server and library access.
/// </summary>
public class RefreshPlexAccountAccessJob : IJob
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public RefreshPlexAccountAccessJob(ILogger log, ICommandExecutor commandExecutor, ITelemetry telemetry)
    {
        _log = log.ForContext<RefreshPlexAccountAccessJob>();
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    protected JobTypes JobType => JobTypes.RefreshPlexAccountAccessJob;

    protected List<RefreshDataType> RefreshDataTypes =>
        [RefreshDataType.PlexAccount, RefreshDataType.PlexServer, RefreshDataType.PlexLibrary];

    public static JobKey GetJobKey() =>
        new(nameof(JobTypes.RefreshPlexAccountAccessJob), nameof(JobTypes.RefreshPlexAccountAccessJob));

    public async Task Execute(IJobExecutionContext context)
    {
        using var telemetryScope = _telemetry.BeginJob(
            TelemetryActivity.PlexAccessRefresh,
            context.GetTelemetryContext()
        );

        var cancellationToken = context.CancellationToken;
        _log.Here().Debug("Executing job: {JobName}", nameof(RefreshPlexAccountAccessJob));

        var executionResult = await Result.Try(
            async Task<Result<List<RefreshPlexAccountAccessRapportDTO>>> () =>
            {
                var result = await _commandExecutor.Send(
                    new RefreshPlexAccountAccessCommand(Trigger: TelemetryTrigger.Scheduled),
                    cancellationToken
                );

                if (result.IsCancelledOrFailed)
                    return result;

                _log.Here()
                    .Debug(
                        "{JobName} refreshed access for {PlexAccountCount} Plex accounts",
                        nameof(RefreshPlexAccountAccessJob),
                        result.Value.Count
                    );
                return result;
            },
            exception =>
            {
                if (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                    return new Error(exception.Message).CausedBy(exception);

                return new ExceptionalError(exception);
            }
        );

        if (executionResult.IsCancelledOrFailed)
        {
            context.SetResult(executionResult.IsCancelled ? JobStatus.Cancelled : JobStatus.Failed, executionResult);
            executionResult.LogIfFailed();
            _telemetry.CaptureErrors(executionResult, TelemetryActivity.PlexAccessRefresh);
            return;
        }

        context.SetResult(JobStatus.Completed);
    }
}
