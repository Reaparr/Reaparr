namespace Reaparr.Application;

public sealed record QueueMediaOverviewRebuildCommand(PlexMediaType? MediaType = null) : ICommand<Result>;

public sealed class QueueMediaOverviewRebuildCommandValidator : AbstractValidator<QueueMediaOverviewRebuildCommand>
{
    public QueueMediaOverviewRebuildCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.MediaType)
            .Must(type => type is null || MediaOverviewRebuildCoordinator.AllMediaTypes.Contains(type.Value));
    }
}

public sealed class QueueMediaOverviewRebuildCommandHandler : ICommandHandler<QueueMediaOverviewRebuildCommand, Result>
{
    private readonly IScheduler _scheduler;
    private readonly IMediaOverviewRebuildCoordinator _coordinator;

    public QueueMediaOverviewRebuildCommandHandler(IScheduler scheduler, IMediaOverviewRebuildCoordinator coordinator)
    {
        _scheduler = scheduler;
        _coordinator = coordinator;
    }

    public async Task<Result> ExecuteAsync(
        QueueMediaOverviewRebuildCommand command,
        CancellationToken cancellationToken
    )
    {
        if (!_coordinator.RequestRebuild(command.MediaType, DateTimeOffset.UtcNow))
            return Result.Ok();

        var result = await Result.Try(async Task () =>
            await _scheduler.TriggerJob(MediaOverviewSnapshotJob.GetJobKey(), cancellationToken)
        );
        if (result.IsFailed)
            _coordinator.CancelPendingRebuild();

        return result;
    }
}
