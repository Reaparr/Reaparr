namespace Reaparr.Application;

public sealed record QueueMediaOverviewRebuildCommand : ICommand<Result>;

public sealed class QueueMediaOverviewRebuildCommandValidator : AbstractValidator<QueueMediaOverviewRebuildCommand>
{
    public QueueMediaOverviewRebuildCommandValidator() => RuleFor(x => x).NotNull();
}

public sealed class QueueMediaOverviewRebuildCommandHandler : ICommandHandler<QueueMediaOverviewRebuildCommand, Result>
{
    private readonly IScheduler _scheduler;

    public QueueMediaOverviewRebuildCommandHandler(IScheduler scheduler) => _scheduler = scheduler;

    public async Task<Result> ExecuteAsync(
        QueueMediaOverviewRebuildCommand command,
        CancellationToken cancellationToken
    )
    {
        return await Result.Try(async Task () =>
            await _scheduler.TriggerJob(MediaOverviewSnapshotJob.GetJobKey(), cancellationToken)
        );
    }
}
