namespace Reaparr.Application;

public record QueueMetadataSyncCommand(int ServerId) : ICommand<Result>;

public class QueueMetadataSyncCommandValidator : AbstractValidator<QueueMetadataSyncCommand>
{
    public QueueMetadataSyncCommandValidator()
    {
        RuleFor(x => x.ServerId).GreaterThan(0);
    }
}

public class QueueMetadataSyncCommandHandler : ICommandHandler<QueueMetadataSyncCommand, Result>
{
    private readonly IScheduler _scheduler;
    private readonly ILogger _log;

    public QueueMetadataSyncCommandHandler(IScheduler scheduler, ILogger log)
    {
        _scheduler = scheduler;
        _log = log.ForContext<QueueMetadataSyncCommandHandler>();
    }

    public async Task<Result> ExecuteAsync(QueueMetadataSyncCommand command, CancellationToken cancellationToken)
    {
        var jobKey = MetadataSyncJob.GetJobKey(command.ServerId);
        if (await _scheduler.IsJobScheduledOrExecuting(jobKey, cancellationToken))
        {
            _log.Here().Debug("MetadataSyncJob already scheduled for server {ServerId}", command.ServerId);
            return Result.Ok();
        }

        var schedulingResult = await _scheduler.ExecuteJob<MetadataSyncJob, MetadataSyncJobPayload>(
            jobKey,
            new MetadataSyncJobPayload(command.ServerId),
            cancellationToken
        );

        if (schedulingResult.IsFailed)
            return schedulingResult.LogError();

        _log.Here().Information("Scheduled MetadataSyncJob for server {ServerId}", command.ServerId);
        return Result.Ok();
    }
}
