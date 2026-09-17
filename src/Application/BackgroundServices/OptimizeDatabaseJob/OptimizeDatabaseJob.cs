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

    public async Task Execute(IJobExecutionContext context)
    {
        var optimizeResult = await _reaparrDbContextDatabase.Optimize(context.CancellationToken);
        optimizeResult.LogIfFailed();

        if (optimizeResult.IsCancelled)
        {
            context.SetResult(JobStatus.Cancelled);
            return;
        }

        if (optimizeResult.IsFailed)
        {
            context.SetResult(JobStatus.Failed);
            return;
        }

        if (optimizeResult.IsSuccess)
        {
            _log.Here().Information("Optimized SQLite query statistics after a large library change");
            context.SetResult(JobStatus.Completed);
        }
    }
}
