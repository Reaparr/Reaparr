namespace Reaparr.Application;

public sealed class SchedulerListener(ILogger log) : ISchedulerListener
{
    private readonly ILogger _log = log.ForContext<SchedulerListener>();

    public string Name => nameof(SchedulerListener);

    public ValueTask SchedulerError(
        IScheduler scheduler,
        SchedulerErrorContext errorContext,
        CancellationToken cancellationToken = default
    )
    {
        _log.Here().Error(errorContext.Exception, "Quartz scheduler error: {Message}", errorContext.Message);
        return ValueTask.CompletedTask;
    }
}
