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

    public CancelLibrarySyncJobCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        IScheduler scheduler,
        INotificationHubService notificationHubService
    )
    {
        _log = log.ForContext<CancelLibrarySyncJobCommandHandler>();
        _dbContext = dbContext;
        _scheduler = scheduler;
        _notificationHubService = notificationHubService;
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

        if (
            queueItem.Status
            is LibrarySyncJobStatus.Completed
                or LibrarySyncJobStatus.Cancelled
                or LibrarySyncJobStatus.Failed
        )
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
        var jobKey = LibrarySyncJob.GetJobKey(serverId, plexLibraryId);

        var wasInterrupted = await _scheduler.Interrupt(jobKey, cancellationToken);
        if (wasInterrupted)
        {
            _log.Here()
                .Information(
                    "Interrupted running library sync job for server {ServerId}, library {PlexLibraryId}",
                    serverId,
                    plexLibraryId
                );

            var completionResult = await _scheduler.AwaitJobCompletion(jobKey, cancellationToken);
            if (completionResult.IsFailed)
                return completionResult;
        }
        else
        {
            // Job is queued but not yet executing — mark it as cancelled directly.
            if (
                !await _scheduler.DeleteJob(jobKey, cancellationToken)
                && await _scheduler.Interrupt(jobKey, cancellationToken)
            )
                return await _scheduler.AwaitJobCompletion(jobKey, cancellationToken);

            var cancelled = await _dbContext
                .LibrarySyncJobQueues.Where(x => x.PlexServerId == serverId && x.PlexLibraryId == plexLibraryId)
                .Where(x => x.Status == LibrarySyncJobStatus.Queued || x.Status == LibrarySyncJobStatus.Processing)
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.Status, LibrarySyncJobStatus.Cancelled)
                            .SetProperty(x => x.CompletedAt, DateTime.UtcNow)
                            .SetProperty(x => x.ErrorMessage, (string?)null)
                            .SetProperty(x => x.IsServerOffline, false),
                    cancellationToken: cancellationToken
                );
            if (cancelled != 1)
                return Result.Fail(
                    "Library sync job for library {PlexLibraryId} changed state while cancelling",
                    plexLibraryId
                );

            _log.Here()
                .Information(
                    "Cancelled queued library sync job for server {ServerId}, library {PlexLibraryId}",
                    serverId,
                    plexLibraryId
                );

            await _notificationHubService.SendRefreshNotificationAsync([RefreshDataType.PlexLibrarySyncStatus]);
        }

        return Result.Ok();
    }
}
