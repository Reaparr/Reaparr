namespace Reaparr.Application;

/// <summary>
/// This command removes completed queue items, re-queues failed queue items, and cancels processing items left over from an interrupted application instance.
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

    public CleanupLibrarySyncJobQueueCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        INotificationHubService notificationHubService
    )
    {
        _log = log.ForContext<CleanupLibrarySyncJobQueueCommandHandler>();
        _dbContext = dbContext;
        _notificationHubService = notificationHubService;
    }

    public async Task<Result> ExecuteAsync(
        CleanupLibrarySyncJobQueueCommand command,
        CancellationToken cancellationToken
    )
    {
        var deletedCompletedCount = await _dbContext
            .LibrarySyncJobQueues.Where(x => x.Status == LibrarySyncJobStatus.Completed)
            .ExecuteDeleteAsync(cancellationToken: cancellationToken);

        var requeuedFailedCount = await _dbContext
            .LibrarySyncJobQueues.Where(x => x.Status == LibrarySyncJobStatus.Failed)
            .ResetJobsToQueuedAsync(cancellationToken);

        var cancelledProcessingCount = await _dbContext
            .LibrarySyncJobQueues.Where(x => x.Status == LibrarySyncJobStatus.Processing)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, LibrarySyncJobStatus.Cancelled)
                        .SetProperty(x => x.CompletedAt, DateTime.UtcNow)
                        .SetProperty(x => x.ErrorMessage, (string?)null)
                        .SetProperty(x => x.IsServerOffline, false),
                cancellationToken
            );

        await _notificationHubService.SendRefreshNotificationAsync([RefreshDataType.PlexLibrarySyncStatus]);

        _log.Here()
            .Debug(
                "Cleaned up library sync job queue: deleted {DeletedCompletedCount} completed items, re-queued {RequeuedFailedCount} failed items, and cancelled {CancelledProcessingCount} processing items",
                deletedCompletedCount,
                requeuedFailedCount,
                cancelledProcessingCount
            );

        return Result.Ok();
    }
}
