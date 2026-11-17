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
    private readonly ITelemetry _telemetry;
    private int _serverId;
    private int _libraryId;

    public LibrarySyncJob(
        ILogger log,
        ICommandExecutor commandExecutor,
        INotificationHubService notificationHubService,
        IReaparrDbContext dbContext,
        IMediaOverviewRebuildCoordinator mediaOverviewRebuildCoordinator,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<LibrarySyncJob>();
        _commandExecutor = commandExecutor;
        _notificationHubService = notificationHubService;
        _dbContext = dbContext;
        _mediaOverviewRebuildCoordinator = mediaOverviewRebuildCoordinator;
        _telemetry = telemetry;
    }

    public static JobKey GetJobKey(int serverId, int libraryId) =>
        new($"{nameof(JobTypes.LibrarySyncJob)}_{serverId}_{libraryId}", nameof(JobTypes.LibrarySyncJob));

    public async Task Execute(IJobExecutionContext context)
    {
        using var telemetryScope = _telemetry.BeginJob(TelemetryActivity.LibrarySync, context.GetTelemetryContext());

        var payloadResult = context.GetRequiredPayload<LibrarySyncJobPayload>();
        if (payloadResult.IsCancelledOrFailed)
        {
            context.SetResult(payloadResult.IsCancelled ? JobStatus.Cancelled : JobStatus.Failed, payloadResult);
            payloadResult.LogIfFailed();
            _telemetry.CaptureErrors(payloadResult, TelemetryActivity.LibrarySync);
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
        LibrarySyncJobQueue? queueItem = null;

        // Jobs should swallow exceptions as otherwise Quartz will keep re-executing it
        // https://www.quartz-scheduler.net/documentation/best-practices.html#throwing-exceptions
        var result = await Result.Try(
            async Task<Result> () =>
            {
                queueItem = await _dbContext
                    .LibrarySyncJobQueues.Include(x => x.PlexLibrary)
                    .Where(x => x.PlexServerId == _serverId && x.PlexLibraryId == _libraryId)
                    .FirstOrDefaultAsync(CancellationToken.None);
                var queueStatus = queueItem?.Status ?? LibrarySyncJobStatus.Unknown;
                if (queueStatus != LibrarySyncJobStatus.Processing || queueItem?.PlexLibrary?.IsEnabled != true)
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

                cancellationToken.ThrowIfCancellationRequested();

                // Check if the server is online before starting sync.
                isServerOffline = !await _dbContext.IsServerOnline(_serverId);
                if (isServerOffline)
                {
                    _log.Here()
                        .Warning("Server {ServerId} is offline, marking queue item and skipping sync", _serverId);
                    return Result.Ok();
                }

                using var syncLease = await _mediaOverviewRebuildCoordinator.AcquireLibrarySyncLeaseAsync(
                    cancellationToken
                );
                var claimed = await UpdateQueueItemAsync(LibrarySyncJobStatus.Processing);
                if (claimed != 1)
                {
                    shouldDispatch = false;
                    context.SetResult(JobStatus.Cancelled);
                    return Result.Ok();
                }

                return (
                    await _commandExecutor.Send(
                        new RefreshLibraryMediaCommand(_libraryId, payloadResult.Value.ForceMediaRefresh),
                        cancellationToken
                    )
                ).ToResult();
            },
            exception =>
                exception is OperationCanceledException && !cancellationToken.IsCancellationRequested
                    ? new Error(exception.Message).CausedBy(exception)
                    : new ExceptionalError(exception)
        );
        if (!shouldDispatch)
            return;

        LibrarySyncJobStatus status;
        if (result.IsCancelledOrFailed)
        {
            if (result.IsCancelled)
            {
                result.LogIfFailed();
                status = LibrarySyncJobStatus.Cancelled;
                context.SetResult(JobStatus.Cancelled, result);
                _telemetry.CaptureErrors(result, TelemetryActivity.LibrarySync);
            }
            else if (result.Has504GatewayTimeoutError())
            {
                result.LogWarning();
                isServerOffline = true;
                status = LibrarySyncJobStatus.Queued;
                context.SetResult(JobStatus.Completed, result);
            }
            else
            {
                result.LogIfFailed();
                status = LibrarySyncJobStatus.Failed;
                context.SetResult(JobStatus.Failed, result);
                _telemetry.CaptureErrors(result, TelemetryActivity.LibrarySync);
            }
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
                errorMessage: status == LibrarySyncJobStatus.Failed ? result.Errors.FirstOrDefault()?.Message : null,
                isServerOffline: isServerOffline
            )
        );
        persistenceResult.LogIfFailed();
        var persisted = persistenceResult.IsSuccess && persistenceResult.Value == 1;
        if (persistenceResult.IsFailed)
        {
            context.SetResult(JobStatus.Failed, persistenceResult);
            _telemetry.CaptureErrors(persistenceResult, TelemetryActivity.LibrarySync);
        }

        if (
            persisted
            && queueItem is not null
            && status
                is (LibrarySyncJobStatus.Completed or LibrarySyncJobStatus.Failed or LibrarySyncJobStatus.Cancelled)
        )
        {
            _telemetry.LibrarySyncFinished(
                queueItem,
                queueItem.PlexLibrary?.Type,
                payloadResult.Value.ForceMediaRefresh,
                status,
                isServerOffline
            );
        }

        // Deliver refresh notifications even when the Quartz job token has been cancelled.
        using var cleanupTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cleanupToken = cleanupTokenSource.Token;
        var notificationResult = await Result.Try(() =>
            _notificationHubService
                .SendRefreshNotificationAsync([RefreshDataType.PlexLibrary, RefreshDataType.PlexLibrarySyncStatus])
                .WaitAsync(cleanupToken)
        );
        notificationResult.LogIfFailed();
    }

    private async Task<int> UpdateQueueItemAsync(
        LibrarySyncJobStatus status,
        string? errorMessage = null,
        bool isServerOffline = false
    )
    {
        var query = _dbContext.LibrarySyncJobQueues.Where(x =>
            x.PlexServerId == _serverId && x.PlexLibraryId == _libraryId && x.Status == LibrarySyncJobStatus.Processing
        );

        return status switch
        {
            LibrarySyncJobStatus.Queued => await query.ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, status)
                        .SetProperty(x => x.StartedAt, (DateTime?)null)
                        .SetProperty(x => x.ErrorMessage, (string?)null)
                        .SetProperty(x => x.IsServerOffline, isServerOffline),
                CancellationToken.None
            ),
            LibrarySyncJobStatus.Processing => await query.ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, status)
                        .SetProperty(x => x.StartedAt, DateTime.UtcNow)
                        .SetProperty(x => x.IsServerOffline, false),
                CancellationToken.None
            ),
            LibrarySyncJobStatus.Completed => await query.ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, status)
                        .SetProperty(x => x.CompletedAt, DateTime.UtcNow)
                        .SetProperty(x => x.IsServerOffline, false),
                CancellationToken.None
            ),
            LibrarySyncJobStatus.Failed => await query.ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, status)
                        .SetProperty(x => x.CompletedAt, DateTime.UtcNow)
                        .SetProperty(x => x.ErrorMessage, errorMessage)
                        .SetProperty(x => x.IsServerOffline, isServerOffline),
                CancellationToken.None
            ),
            LibrarySyncJobStatus.Cancelled => await query.ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, status)
                        .SetProperty(x => x.CompletedAt, DateTime.UtcNow)
                        .SetProperty(x => x.ErrorMessage, (string?)null)
                        .SetProperty(x => x.IsServerOffline, false),
                CancellationToken.None
            ),
            _ => 0,
        };
    }
}
