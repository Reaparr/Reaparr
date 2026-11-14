namespace Reaparr.Application;

/// <summary>
/// Periodically validates enabled Plex accounts and refreshes their server and library access.
/// </summary>
public class RefreshPlexAccountAccessJob : IJob
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;
    public RefreshPlexAccountAccessJob(
        ILogger log,
        ICommandExecutor commandExecutor,
        ITelemetry telemetry
    )
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

    public Task Execute(IJobExecutionContext context)
    {
        return _telemetry.RunJobAsync(
            TelemetryActivity.PlexAccessRefresh,
            context.GetTelemetryContext(),
            async () =>
            {
                try
                {
                    var cancellationToken = context.CancellationToken;
                    _log.Here().Debug("Executing job: {JobName}", nameof(RefreshPlexAccountAccessJob));

                    var result = await _commandExecutor.Send(
                        new RefreshPlexAccountAccessCommand(Trigger: TelemetryTrigger.Scheduled),
                        cancellationToken
                    );

                    if (result.IsCancelledOrFailed)
                    {
                        context.SetResult(
                            result.IsCancelled ? JobStatus.Cancelled : JobStatus.Failed,
                            result
                        );
                        result.LogIfFailed();
                        _telemetry.CaptureErrors(result, TelemetryActivity.PlexAccessRefresh);
                        return;
                    }

                    _log.Here()
                        .Debug(
                            "{JobName} refreshed access for {PlexAccountCount} Plex accounts",
                            nameof(RefreshPlexAccountAccessJob),
                            result.Value.Count
                        );
                    context.SetResult(JobStatus.Completed);
                }
                catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                {
                    var cancellationResult = ResultExtensions.TaskIsCancelled(nameof(RefreshPlexAccountAccessJob));
                    context.SetResult(JobStatus.Cancelled, cancellationResult);
                    cancellationResult.LogIfFailed();
                }
                catch (Exception exception)
                {
                    var failureResult = Result.Fail(new ExceptionalError(exception));
                    context.SetResult(JobStatus.Failed, failureResult);
                    failureResult.LogIfFailed();
                    _telemetry.CaptureException(exception, TelemetryActivity.PlexAccessRefresh);
                }
            }
        );
    }
}
