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
        var shouldDispatch = true;
        var isServerOffline = false;
        // Jobs should swallow exceptions as otherwise Quartz will keep re-executing it
        // https://www.quartz-scheduler.net/documentation/best-practices.html#throwing-exceptions
        var result = await Result.Try(async Task<Result> () =>
        {
            var queueStatus = await _dbContext
                .LibrarySyncJobQueues.Where(x => x.PlexServerId == _serverId && x.PlexLibraryId == _libraryId)
                .Select(x => x.Status)
                .FirstOrDefaultAsync(cancellationToken);
            if (queueStatus != LibrarySyncJobStatus.Processing)
            {
                shouldDispatch = false;
                context.SetResult(JobStatus.Cancelled);
                _log.Here()
                    .Information(
                        "Skipping library sync job for server {ServerId}, library {LibraryId} because its queue status is {QueueStatus}",
                        _serverId,
                        _libraryId,
                        queueStatus
                    );
                return Result.Ok();
            }

            // Check if the server is online before starting sync
            isServerOffline = !await _dbContext.IsServerOnline(_serverId);
            if (isServerOffline)
            {
                _log.Here().Warning("Server {ServerId} is offline, marking queue item and skipping sync", _serverId);
                return Result.Ok();
            }

            using var syncLease = await _mediaOverviewRebuildCoordinator.AcquireLibrarySyncLeaseAsync(
                cancellationToken
            );
            await UpdateQueueItemAsync(LibrarySyncJobStatus.Processing);

            // Execute the library sync command
            var refreshResult = await _commandExecutor.Send(
                new RefreshLibraryMediaCommand(_libraryId, payloadResult.Value.ForceMediaRefresh),
                cancellationToken
            );
            return refreshResult.ToResult();
        });

        if (!shouldDispatch)
            return;

        LibrarySyncJobStatus status;
        if (result.IsCancelled)
        {
            result.LogWarning();
            status = LibrarySyncJobStatus.Cancelled;
            context.SetResult(JobStatus.Cancelled, result);
        }
        else if (result.IsFailed)
        {
            result.LogError();
            status = LibrarySyncJobStatus.Failed;
            // Check if failure was due to the server being offline (504 Gateway Timeout)
            // TODO make "Server offline" a generic FluentResult check as this can happen in other places as well and we want to handle it consistently across the app
            isServerOffline = result.Has504GatewayTimeoutError();
            context.SetResult(JobStatus.Failed, result);
        }
        else
        {
            status = isServerOffline ? LibrarySyncJobStatus.Queued : LibrarySyncJobStatus.Completed;
            context.SetResult(JobStatus.Completed);
            if (!isServerOffline)
                _log.Here()
                    .Information(
                        "Successfully synced library {LibraryId} for server {ServerId}",
                        _libraryId,
                        _serverId
                    );
        }

        // Persist the outcome even when cancellation occurred before media synchronization began.
        // The Quartz job token may already be cancelled, so persist the outcome independently.
        var persistenceResult = await Result.Try(() =>
            UpdateQueueItemAsync(
                status,
                errorMessage: status == LibrarySyncJobStatus.Failed ? result.Errors.First().Message : null,
                isServerOffline: isServerOffline
            )
        );
        persistenceResult.LogIfFailed();
        if (persistenceResult.IsFailed)
            context.SetResult(JobStatus.Failed, persistenceResult);

        // Deliver refresh notifications even when the Quartz job token has been cancelled.
        using var cleanupTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cleanupToken = cleanupTokenSource.Token;

        // Send PlexLibrary refresh notification
        var notificationResult = await Result.Try(() =>
            _notificationHubService
                .SendRefreshNotificationAsync([RefreshDataType.PlexLibrary, RefreshDataType.PlexLibrarySyncStatus])
                .WaitAsync(cleanupToken)
        );
        notificationResult.LogIfFailed();
    }

    private async Task UpdateQueueItemAsync(
        LibrarySyncJobStatus status,
        string? errorMessage = null,
        bool isServerOffline = false
    )
    {
        var query = _dbContext.LibrarySyncJobQueues.Where(x =>
            x.PlexServerId == _serverId && x.PlexLibraryId == _libraryId && x.Status == LibrarySyncJobStatus.Processing
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
    }
}
