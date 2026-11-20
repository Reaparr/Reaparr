namespace Reaparr.Application;

public sealed class AllJobListener : IJobListener
{
    private readonly ILogger _log;
    private readonly IProgressHubService _progressHubService;
    private readonly ITelemetry _telemetry;

    public AllJobListener(ILogger log, IProgressHubService progressHubService, ITelemetry telemetry)
    {
        _log = log.ForContext<AllJobListener>();
        _progressHubService = progressHubService;
        _telemetry = telemetry;
    }

    public string Name => nameof(AllJobListener);

    public ValueTask JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default) =>
        Publish(context, JobStatus.Cancelled);

    public ValueTask JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default) =>
        Publish(context, JobStatus.Started);

    public ValueTask JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException? jobException,
        CancellationToken cancellationToken = default
    )
    {
        var exception = jobException?.InnerException ?? jobException;
        if (
            exception is not null
            && (exception is not OperationCanceledException || !context.CancellationToken.IsCancellationRequested)
        )
        {
            using var scope = _telemetry.BeginJob(TelemetryActivity.BackgroundJob, context.GetTelemetryContext());
            _telemetry.CaptureException(exception, TelemetryActivity.BackgroundJob);
        }

        return Publish(context, BackgroundJobTerminalOutcome.From(context, jobException).Status);
    }

    private async ValueTask Publish(IJobExecutionContext context, JobStatus status)
    {
        var result = await Result.Try(async Task () =>
        {
            var update = new JobStatusUpdate<string>(
                JobStatusUpdateMapper.ToJobType(context.JobDetail.Key.Group),
                status,
                context.GetPayloadAsJson(),
                context.JobDetail.Key.Name,
                context.FireTimeUtc.UtcDateTime
            );
            await _progressHubService.SendJobStatusUpdateAsync(update);

            _log.Here()
                .Verbose(
                    "Published Quartz lifecycle event for {JobName} with status {JobStatus}: {JobUpdate}",
                    context.JobDetail.Key,
                    status,
                    update
                );
        });

        if (result.IsCancelledOrFailed)
        {
            if (!result.IsCancelled)
                result.WithError($"Failed to publish Quartz lifecycle event for {context.JobDetail.Key}");

            result.LogIfFailed();
        }
    }
}
