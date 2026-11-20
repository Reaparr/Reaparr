namespace Reaparr.Application;

public sealed record BackgroundJobResult(JobStatus Status, string? ErrorSummary = null) : IJobRunReport
{
    JobRunResult IJobRunReport.Result =>
        Status switch
        {
            JobStatus.Failed => JobRunResult.Failed,
            JobStatus.Cancelled => JobRunResult.Cancelled,
            _ => JobRunResult.Succeeded,
        };

    string? IJobRunReport.Summary =>
        Status switch
        {
            JobStatus.Failed => "Job failed",
            JobStatus.Cancelled => "Job was cancelled",
            _ => null,
        };

    IReadOnlyDictionary<string, object?>? IJobRunReport.Metrics => null;

    internal JobStatusUpdate? CronUpdate { get; init; }
}

public sealed record BackgroundJobTerminalOutcome(JobStatus Status, string? ErrorSummary)
{
    public static BackgroundJobTerminalOutcome From(IJobExecutionContext context, JobExecutionException? exception)
    {
        if (context.Result is BackgroundJobResult result)
            return new(result.Status, result.ErrorSummary);

        if (exception is null)
            return new(JobStatus.Completed, null);

        var baseException = exception.GetBaseException();
        return baseException is OperationCanceledException
            ? new(JobStatus.Cancelled, baseException.Message)
            : new(JobStatus.Failed, baseException.Message);
    }
}
