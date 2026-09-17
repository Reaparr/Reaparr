namespace Reaparr.Application;

public sealed class OptimizeDatabaseJob : IJob
{
    private readonly IReaparrDbContextDatabase _reaparrDbContextDatabase;
    private readonly ILogger _log;

    public OptimizeDatabaseJob(ILogger log, IReaparrDbContextDatabase reaparrDbContextDatabase)
    {
        _reaparrDbContextDatabase = reaparrDbContextDatabase;
        _log = log.ForContext<OptimizeDatabaseJob>();
    }

    public static JobKey GetJobKey() => new(nameof(JobTypes.OptimizeDatabaseJob), nameof(JobTypes.OptimizeDatabaseJob));

    public Task Execute(IJobExecutionContext context)
    {
        var optimizeResult = _reaparrDbContextDatabase.Optimize();
        optimizeResult.LogIfFailed();

        if (optimizeResult.IsCancelled)
        {
            context.SetResult(JobStatus.Cancelled);
            return Task.CompletedTask;
        }

        if (optimizeResult.IsFailed)
        {
            context.SetResult(JobStatus.Failed);
            return Task.CompletedTask;
        }

        if (optimizeResult.IsSuccess)
        {
            _log.Here().Information("Optimized SQLite query statistics after a large library change");
            context.SetResult(JobStatus.Completed);
        }

        return Task.CompletedTask;
    }
}
