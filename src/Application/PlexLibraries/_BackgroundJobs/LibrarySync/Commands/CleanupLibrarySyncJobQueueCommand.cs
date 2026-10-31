namespace Reaparr.Application;

/// <summary>
/// This command removes completed queue items, re-queues failed and interrupted processing items, and
/// removes persisted library sync Quartz jobs left over from an interrupted application instance.
/// It should only be run once during startup, before Quartz background jobs are started.
/// </summary>
public record CleanupLibrarySyncJobQueueCommand : ICommand<Result>;

public class CleanupLibrarySyncJobQueueCommandValidator : AbstractValidator<CleanupLibrarySyncJobQueueCommand>
{
    public CleanupLibrarySyncJobQueueCommandValidator()
    {
        RuleFor(x => x).NotNull();
    }
}

public class CleanupLibrarySyncJobQueueCommandHandler : ICommandHandler<CleanupLibrarySyncJobQueueCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly INotificationHubService _notificationHubService;
    private readonly IScheduler _scheduler;

    public CleanupLibrarySyncJobQueueCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        INotificationHubService notificationHubService,
        IScheduler scheduler
    )
    {
        _log = log.ForContext<CleanupLibrarySyncJobQueueCommandHandler>();
        _dbContext = dbContext;
        _notificationHubService = notificationHubService;
        _scheduler = scheduler;
    }

    public async Task<Result> ExecuteAsync(
        CleanupLibrarySyncJobQueueCommand command,
        CancellationToken cancellationToken
    )
    {
        // Remove stale Quartz jobs before re-queuing work so existing job keys cannot suppress scheduling.
        var schedulerCleanupResult = await Result.Try(async Task<Result> () =>
        {
            var staleJobKeys = await _scheduler.GetJobKeys(JobTypes.LibrarySyncJob, cancellationToken);
            if (staleJobKeys.Count == 0)
                return Result.Ok();

            var deleted = await _scheduler.DeleteJobs(staleJobKeys.ToList(), cancellationToken);
            return deleted ? Result.Ok() : Result.Fail("Failed to delete persisted library sync Quartz jobs");
        });
        if (schedulerCleanupResult.IsCancelledOrFailed)
            return schedulerCleanupResult.LogIfFailed();

        var deletedCompletedCount = await _dbContext
            .LibrarySyncJobQueues.Where(x => x.Status == LibrarySyncJobStatus.Completed)
            .ExecuteDeleteAsync(cancellationToken: cancellationToken);

        var requeuedFailedCount = await _dbContext
            .LibrarySyncJobQueues.Where(x => x.Status == LibrarySyncJobStatus.Failed)
            .ResetJobsToQueuedAsync(cancellationToken);

        var requeuedProcessingCount = await _dbContext
            .LibrarySyncJobQueues.Where(x => x.Status == LibrarySyncJobStatus.Processing)
            .ResetJobsToQueuedAsync(cancellationToken);

        await _notificationHubService.SendRefreshNotificationAsync([RefreshDataType.PlexLibrarySyncStatus]);

        _log.Here()
            .Debug(
                "Cleaned up library sync job queue: deleted {DeletedCompletedCount} completed items, re-queued {RequeuedFailedCount} failed items and {RequeuedProcessingCount} interrupted processing items",
                deletedCompletedCount,
                requeuedFailedCount,
                requeuedProcessingCount
            );

        return Result.Ok();
    }
}
