namespace Reaparr.Application;

public record CancelLibrarySyncJobCommand(int PlexLibraryId) : ICommand<Result>;

public class CancelLibrarySyncJobCommandValidator : AbstractValidator<CancelLibrarySyncJobCommand>
{
    public CancelLibrarySyncJobCommandValidator()
    {
        RuleFor(x => x.PlexLibraryId).GreaterThan(0);
    }
}

public class CancelLibrarySyncJobCommandHandler : ICommandHandler<CancelLibrarySyncJobCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IScheduler _scheduler;
    private readonly INotificationHubService _notificationHubService;
    private readonly ICommandExecutor _commandExecutor;

    public CancelLibrarySyncJobCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        IScheduler scheduler,
        INotificationHubService notificationHubService,
        ICommandExecutor commandExecutor
    )
    {
        _log = log.ForContext<CancelLibrarySyncJobCommandHandler>();
        _dbContext = dbContext;
        _scheduler = scheduler;
        _notificationHubService = notificationHubService;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result> ExecuteAsync(CancelLibrarySyncJobCommand command, CancellationToken cancellationToken)
    {
        var plexLibraryId = command.PlexLibraryId;

        var queueItem = await _dbContext
            .LibrarySyncJobQueues.Where(x => x.PlexLibraryId == plexLibraryId)
            .FirstOrDefaultAsync(cancellationToken);

        if (queueItem is null)
        {
            _log.Here()
                .Warning(
                    "No library sync queue item found for library {PlexLibraryId}, nothing to cancel",
                    plexLibraryId
                );
            return Result.Ok();
        }

        if (IsTerminalStatus(queueItem.Status))
        {
            _log.Here()
                .Warning(
                    "Library sync job for library {PlexLibraryId} has status {Status}, nothing to cancel",
                    plexLibraryId,
                    queueItem.Status
                );
            return Result.Ok();
        }

        var serverId = queueItem.PlexServerId;
        var createdAt = queueItem.CreatedAt;
        var jobKey = LibrarySyncJob.GetJobKey(serverId, plexLibraryId);

        var wasInterrupted = await _scheduler.Interrupt(jobKey, cancellationToken);
        var canReconcileProcessing = queueItem.Status == LibrarySyncJobStatus.Processing || wasInterrupted;
        if (wasInterrupted)
        {
            _log.Here()
                .Information(
                    "Interrupted running library sync job for server {ServerId}, library {PlexLibraryId}",
                    serverId,
                    plexLibraryId
                );
        }
        else
        {
            // Deleting a scheduled job does not interrupt an execution that started in the meantime.
            await _scheduler.DeleteJob(jobKey, cancellationToken);
            var interruptedAfterDelete = await _scheduler.Interrupt(jobKey, cancellationToken);
            canReconcileProcessing |= interruptedAfterDelete;
        }

        var completionResult = await _scheduler.AwaitJobCompletion(jobKey, cancellationToken);
        if (completionResult.IsCancelledOrFailed)
            return completionResult.LogIfFailed();

        // CreatedAt identifies this queue admission; do not cancel a newer request.
        // Only reconcile processing work that was already observed or successfully interrupted.
        // Cancellation has already been accepted; persist and inspect its outcome independently of the request token.
        var cancelled = await _dbContext
            .LibrarySyncJobQueues.Where(x =>
                x.PlexServerId == serverId && x.PlexLibraryId == plexLibraryId && x.CreatedAt == createdAt
            )
            .Where(x =>
                x.Status == LibrarySyncJobStatus.Queued
                || (canReconcileProcessing && x.Status == LibrarySyncJobStatus.Processing)
            )
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, LibrarySyncJobStatus.Cancelled)
                        .SetProperty(x => x.CompletedAt, DateTime.UtcNow)
                        .SetProperty(x => x.ErrorMessage, (string?)null)
                        .SetProperty(x => x.IsServerOffline, false),
                cancellationToken: CancellationToken.None
            );
        if (cancelled != 1)
        {
            var currentStatus = await _dbContext
                .LibrarySyncJobQueues.Where(x => x.PlexServerId == serverId && x.PlexLibraryId == plexLibraryId)
                .Select(x => x.Status)
                .FirstOrDefaultAsync(CancellationToken.None);
            if (IsTerminalStatus(currentStatus))
            {
                _log.Here()
                    .Information(
                        "Library sync job for library {PlexLibraryId} changed to terminal status {Status} while cancelling",
                        plexLibraryId,
                        currentStatus
                    );
                return Result.Ok();
            }

            return Result
                .Fail("Library sync job for library {PlexLibraryId} changed state while cancelling", plexLibraryId)
                .LogError();
        }

        _log.Here()
            .Information(
                "Cancelled library sync job for server {ServerId}, library {PlexLibraryId}",
                serverId,
                plexLibraryId
            );

        using var cleanupTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cleanupToken = cleanupTokenSource.Token;
        var dispatchResult = await Result.Try(() =>
            _commandExecutor.Send(new CheckQueuedPlexLibraryToSyncCommand(), cleanupToken)
        );
        dispatchResult.LogIfFailed();

        var notificationResult = await Result.Try(() =>
            _notificationHubService
                .SendRefreshNotificationAsync([RefreshDataType.PlexLibrarySyncStatus])
                .WaitAsync(cleanupToken)
        );
        notificationResult.LogIfFailed();

        return Result.Ok();
    }

    private static bool IsTerminalStatus(LibrarySyncJobStatus status) =>
        status is LibrarySyncJobStatus.Completed or LibrarySyncJobStatus.Cancelled or LibrarySyncJobStatus.Failed;
}
