namespace Reaparr.Application;

public sealed record LibrarySyncJobPayload(int ServerId, int LibraryId, bool ForceMediaRefresh = false);

/// <summary>
/// Quartz job that syncs a single library and chains to the next library if provided.
/// Uses per-server locking to ensure only one library sync runs per server at a time.
/// </summary>
[DisallowConcurrentExecution]
public class LibrarySyncJob : IJob
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly INotificationHubService _notificationHubService;
    private readonly IReaparrDbContext _dbContext;
    private readonly IMediaOverviewRebuildCoordinator _mediaOverviewRebuildCoordinator;
    private int _serverId;
    private int _libraryId;

    public LibrarySyncJob(
        ILogger log,
        ICommandExecutor commandExecutor,
        INotificationHubService notificationHubService,
        IReaparrDbContext dbContext,
        IMediaOverviewRebuildCoordinator mediaOverviewRebuildCoordinator
    )
    {
        _log = log.ForContext<LibrarySyncJob>();
        _commandExecutor = commandExecutor;
        _notificationHubService = notificationHubService;
        _dbContext = dbContext;
        _mediaOverviewRebuildCoordinator = mediaOverviewRebuildCoordinator;
    }

    public static JobKey GetJobKey(int serverId, int libraryId) =>
        new($"{nameof(JobTypes.LibrarySyncJob)}_{serverId}_{libraryId}", nameof(JobTypes.LibrarySyncJob));

    public async Task Execute(IJobExecutionContext context)
    {
        var payloadResult = context.GetRequiredPayload<LibrarySyncJobPayload>();
        if (payloadResult.IsFailed)
        {
            context.SetResult(JobStatus.Failed, payloadResult);
            payloadResult.LogError();
            return;
        }

        var cancellationToken = context.CancellationToken;
        _serverId = payloadResult.Value.ServerId;
        _libraryId = payloadResult.Value.LibraryId;

        _log.Here()
            .Debug(
                "Executing job: {LibrarySyncJobName} for server {ServerId}, library {LibraryId}",
                nameof(LibrarySyncJob),
                _serverId,
                _libraryId
            );
        var queueStatus = await _dbContext
            .LibrarySyncJobQueues.Where(x => x.PlexServerId == _serverId && x.PlexLibraryId == _libraryId)
            .Select(x => x.Status)
            .FirstOrDefaultAsync(cancellationToken);
        if (queueStatus != LibrarySyncJobStatus.Processing)
        {
            context.SetResult(JobStatus.Cancelled);
            _log.Here()
                .Information(
                    "Skipping library sync job for server {ServerId}, library {LibraryId} because its queue status is {QueueStatus}",
                    _serverId,
                    _libraryId,
                    queueStatus
                );
            return;
        }

        // Check if the server is online before starting sync
        var isServerOnline = await _dbContext.IsServerOnline(_serverId);
        if (!isServerOnline)
        {
            var serverName = await _dbContext.GetPlexServerNameById(_serverId);
            _log.Here()
                .Warning(
                    "Server {ServerName} with id {ServerId} is offline, marking queue item and skipping sync",
                    serverName,
                    _serverId
                );
            await UpdateQueueItemAsync(LibrarySyncJobStatus.Queued, isServerOffline: true);
        }
        else
        {
            using var syncLease = await _mediaOverviewRebuildCoordinator.AcquireLibrarySyncLeaseAsync(
                cancellationToken
            );
            await UpdateQueueItemAsync(LibrarySyncJobStatus.Processing);

            var forceMediaRefresh = payloadResult.Value.ForceMediaRefresh;

            // Jobs should swallow exceptions as otherwise Quartz will keep re-executing it
            // https://www.quartz-scheduler.net/documentation/best-practices.html#throwing-exceptions

            // Execute the library sync command
            var result = await Result.Try(() =>
                _commandExecutor.Send(
                    new RefreshLibraryMediaCommand(_libraryId, forceMediaRefresh),
                    context.CancellationToken
                )
            );

            if (result.IsCancelled)
            {
                context.SetResult(JobStatus.Cancelled, result);
                _log.Here()
                    .Information(
                        "{LibrarySyncJobName} for server {ServerId}, library {LibraryId} has been cancelled",
                        nameof(LibrarySyncJob),
                        _serverId,
                        _libraryId
                    );

                // The Quartz job token is already cancelled, so use a short-lived token
                // to persist the cancellation and continue processing the queue.
                using var cleanupTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var cleanupToken = cleanupTokenSource.Token;

                await UpdateQueueItemAsync(LibrarySyncJobStatus.Cancelled);
                await _notificationHubService.SendRefreshNotificationAsync([RefreshDataType.PlexLibrary]);
                await _commandExecutor.Send(new CheckQueuedPlexLibraryToSyncCommand(), cleanupToken);

                return;
            }
            else if (result.IsFailed)
            {
                context.SetResult(JobStatus.Failed, result);
                result.LogError();

                // Check if failure was due to the server being offline (504 Gateway Timeout)
                // TODO make "Server offline" a generic FluentResult check as this can happen in other places as well and we want to handle it consistently across the app
                var isServerOffline = result.ToResult().Has504GatewayTimeoutError();

                _log.Here()
                    .Warning(
                        "Library sync failed for server {ServerId}, library {LibraryId}. Queue item marked as failed. Server offline: {IsServerOffline}",
                        _serverId,
                        _libraryId,
                        isServerOffline
                    );

                await UpdateQueueItemAsync(
                    LibrarySyncJobStatus.Failed,
                    errorMessage: result.Errors.FirstOrDefault()?.Message,
                    isServerOffline: isServerOffline
                );
            }
            else
            {
                _log.Here()
                    .Information(
                        "Successfully synced library {LibraryId} for server {ServerId}",
                        _libraryId,
                        _serverId
                    );

                await UpdateQueueItemAsync(LibrarySyncJobStatus.Completed);
            }
        }

        // Send PlexLibrary refresh notification
        await _notificationHubService.SendRefreshNotificationAsync([RefreshDataType.PlexLibrary]);

        // Schedule the next library from the queue
        await _commandExecutor.Send(new CheckQueuedPlexLibraryToSyncCommand(), cancellationToken);
    }

    private async Task UpdateQueueItemAsync(
        LibrarySyncJobStatus status,
        string? errorMessage = null,
        bool isServerOffline = false
    )
    {
        var query = _dbContext.LibrarySyncJobQueues.Where(x =>
            x.PlexServerId == _serverId && x.PlexLibraryId == _libraryId
        );

        switch (status)
        {
            case LibrarySyncJobStatus.Queued:
                await query.ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.Status, status)
                            .SetProperty(x => x.StartedAt, (DateTime?)null)
                            .SetProperty(x => x.ErrorMessage, (string?)null)
                            .SetProperty(x => x.IsServerOffline, isServerOffline),
                    CancellationToken.None
                );
                break;

            case LibrarySyncJobStatus.Processing:
                await query.ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.Status, status)
                            .SetProperty(x => x.StartedAt, DateTime.UtcNow)
                            .SetProperty(x => x.IsServerOffline, false),
                    CancellationToken.None
                );
                break;

            case LibrarySyncJobStatus.Completed:
                await query.ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.Status, status)
                            .SetProperty(x => x.CompletedAt, DateTime.UtcNow)
                            .SetProperty(x => x.IsServerOffline, false),
                    CancellationToken.None
                );
                break;

            case LibrarySyncJobStatus.Failed:
                await query.ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.Status, status)
                            .SetProperty(x => x.CompletedAt, DateTime.UtcNow)
                            .SetProperty(x => x.ErrorMessage, errorMessage)
                            .SetProperty(x => x.IsServerOffline, isServerOffline),
                    CancellationToken.None
                );
                break;

            case LibrarySyncJobStatus.Cancelled:
                await query.ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.Status, status)
                            .SetProperty(x => x.CompletedAt, DateTime.UtcNow)
                            .SetProperty(x => x.ErrorMessage, (string?)null)
                            .SetProperty(x => x.IsServerOffline, false),
                    CancellationToken.None
                );
                break;

            case LibrarySyncJobStatus.Unknown:
                _log.Here()
                    .Warning(
                        "Attempted to set LibrarySyncJobStatus to Unknown for server {ServerId}, library {LibraryId}",
                        _serverId,
                        _libraryId
                    );
                break;
            default:
                _log.Here()
                    .Warning(
                        "Attempted to set LibrarySyncJobStatus to invalid value {Status} for server {ServerId}, library {LibraryId}",
                        status,
                        _serverId,
                        _libraryId
                    );
                break;
        }

        await _notificationHubService.SendRefreshNotificationAsync([RefreshDataType.PlexLibrarySyncStatus]);
    }
}
