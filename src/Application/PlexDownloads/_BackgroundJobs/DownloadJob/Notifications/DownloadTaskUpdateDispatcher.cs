using System.Collections.Concurrent;
using System.Threading.Channels;
using ByteSizeLib;
using Microsoft.Extensions.Hosting;

namespace Reaparr.Application;

/// <summary>
/// Coordinates download task update ingestion and dispatches optimized patch updates to the front-end.
/// Status changes are immediate; progress is coalesced on a one-second cadence.
/// </summary>
public class DownloadTaskUpdateDispatcher : BackgroundService, IDownloadTaskUpdateDispatcher
{
    private readonly ILogger _log;
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly IDownloadHubService _downloadHubService;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IDownloadTaskScheduler _downloadTaskScheduler;
    private readonly ITelemetry _telemetry;
    private readonly Channel<ImmediatePatchRequest> _statusChannel;
    private readonly ConcurrentDictionary<Guid, BufferedProgressUpdate> _progressByNodeId;
    private readonly ConcurrentDictionary<int, long> _sequenceByServer;
    private readonly SemaphoreSlim _publicationGate;
    private readonly SemaphoreSlim[] _progressGates;
    private readonly ConcurrentDictionary<Guid, byte> _finalizedProgressNodes;
    private readonly ConcurrentDictionary<Guid, ProgressScopeKey> _scopeByNodeId;
    private readonly ConcurrentDictionary<Guid, DownloadStatus> _statusByNodeId;
    private readonly ConcurrentDictionary<Guid, DownloadTaskProgress> _lastProgressLogByNodeId;
    private readonly ConcurrentDictionary<Guid, long> _lifecycleRevisionByNodeId;
    private const long PROGRESS_LOG_DATA_THRESHOLD_BYTES = 1024 * 1024;
    private const decimal PROGRESS_LOG_PERCENTAGE_THRESHOLD = 1m;
    private int _retirementPending;

    private void AdvanceLifecycleRevision(Guid nodeId) =>
        _lifecycleRevisionByNodeId.AddOrUpdate(nodeId, 1, (_, revision) => revision + 1);

    private readonly record struct PersistedProgressLog(
        DownloadTaskKey Key,
        DownloadTaskProgress Progress,
        long LifecycleRevision
    );

    /// <summary>
    /// Creates a new dispatcher instance.
    /// </summary>
    public DownloadTaskUpdateDispatcher(
        ILogger log,
        IReaparrDbContextFactory dbContextFactory,
        IDownloadHubService downloadHubService,
        ICommandExecutor commandExecutor,
        IDownloadTaskScheduler downloadTaskScheduler,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<DownloadTaskUpdateDispatcher>();
        _dbContextFactory = dbContextFactory;
        _downloadHubService = downloadHubService;
        _commandExecutor = commandExecutor;
        _downloadTaskScheduler = downloadTaskScheduler;
        _telemetry = telemetry;
        _statusChannel = Channel.CreateUnbounded<ImmediatePatchRequest>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
        );
        _progressByNodeId = new ConcurrentDictionary<Guid, BufferedProgressUpdate>();
        // Fixed stripes stay alive while queued users finish, without retaining a gate per historical download.
        _progressGates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
        _finalizedProgressNodes = new ConcurrentDictionary<Guid, byte>();
        _sequenceByServer = new ConcurrentDictionary<int, long>();
        _publicationGate = new SemaphoreSlim(1, 1);
        _scopeByNodeId = new ConcurrentDictionary<Guid, ProgressScopeKey>();
        _statusByNodeId = new ConcurrentDictionary<Guid, DownloadStatus>();
        _lastProgressLogByNodeId = new ConcurrentDictionary<Guid, DownloadTaskProgress>();
        _lifecycleRevisionByNodeId = new ConcurrentDictionary<Guid, long>();
    }

    /// <inheritdoc />
    public async Task<DownloadSnapshotDTO> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        // Patches read committed data before taking a sequence. Capture the watermark before
        // reading so the client replays any patches that race with this snapshot.
        var watermark = _sequenceByServer.ToDictionary(x => x.Key, x => x.Value);
        using var dbContext = await _dbContextFactory.CreateAsync();
        var downloadTasks = await dbContext.GetAllDownloadTasksByServerAsync(cancellationToken: cancellationToken);

        return new DownloadSnapshotDTO
        {
            Downloads = downloadTasks.ToServerDownloadProgressDTOList(),
            PatchSequenceByServer = watermark,
        };
    }

    /// <inheritdoc />
    public async Task OnStatusChangedAsync(
        DownloadTaskKey key,
        DownloadStatus newStatus,
        CancellationToken cancellationToken = default
    ) => await OnStatusChangedAsync(key, newStatus, null, cancellationToken);

    /// <inheritdoc />
    public async Task OnStatusChangedAsync(
        DownloadTaskKey key,
        DownloadStatus newStatus,
        Result? errorResult,
        CancellationToken cancellationToken = default
    )
    {
        var result = await Result.Try(async Task () =>
        {
            if (newStatus is DownloadStatus.Deleted)
            {
                var retirement = await Result.Try(RetireDeletedNodesAsync);
                if (retirement.IsFailed)
                {
                    Interlocked.Exchange(ref _retirementPending, 1);
                    retirement.LogError();
                }
                await PublishDeletionPatchAsync(key, cancellationToken);
                return;
            }

            var statusGate = GetProgressGate(key.Id);
            await statusGate.WaitAsync(cancellationToken);
            try
            {
                using var dbContext = await _dbContextFactory.CreateAsync();
                var currentStatus = await dbContext.GetDownloadStatusAsync(key);
                if (currentStatus is null)
                {
                    RetireNode(key.Id);
                    return;
                }
                var hasStatusChanged = currentStatus != newStatus;
                _statusByNodeId[key.Id] = newStatus;

                if (hasStatusChanged)
                {
                    try
                    {
                        if (
                            newStatus
                            is DownloadStatus.Paused
                                or DownloadStatus.AutoPaused
                                or DownloadStatus.DownloadFinished
                        )
                            await PersistBufferedProgressAsync(dbContext, key, cancellationToken);

                        await dbContext.SetDownloadStatus(key, newStatus);
                    }
                    catch
                    {
                        _statusByNodeId[key.Id] = currentStatus.Value;
                        throw;
                    }
                    AdvanceLifecycleRevision(key.Id);
                    await LogStatusChangeAsync(dbContext, key, newStatus, cancellationToken);
                    ResetProgressJourneyTrackingIfNeeded(key.Id, newStatus);

                    if (newStatus is DownloadStatus.Paused or DownloadStatus.AutoPaused)
                        await dbContext.ClearDownloadSpeed(key, cancellationToken);
                }
                else
                {
                    await dbContext.CreateDownloadClientLog(
                        key,
                        NotificationLevel.Debug,
                        newStatus,
                        $"Status change request ignored because task is already in status: {newStatus}"
                    );
                }

                if (
                    currentStatus == DownloadStatus.Downloading
                    || newStatus == DownloadStatus.Downloading
                        && await _downloadTaskScheduler.IsServerDownloading(key.PlexServerId)
                )
                {
                    var refresh = await _commandExecutor.Send(
                        new UpdateScheduledDownloadLimitsCommand(),
                        CancellationToken.None
                    );
                    refresh.LogIfFailed();
                }
                var changedParentStatuses = await dbContext.DetermineDownloadStatus(key, cancellationToken);
                var rootKey = await dbContext.GetRootDownloadTaskKeyAsync(key, cancellationToken: cancellationToken);
                if (rootKey is null)
                    return;

                var changedNodeIds = changedParentStatuses.Select(x => x.Key.Id).Append(key.Id).Distinct().ToList();
                if (hasStatusChanged || changedParentStatuses.Count > 0)
                {
                    var rootStatus = changedParentStatuses.FirstOrDefault(x => x.Key == rootKey);
                    if (rootStatus.Key is null && hasStatusChanged && key == rootKey)
                        rootStatus = (rootKey, newStatus);

                    if (rootStatus.Key is not null)
                        _telemetry.DownloadLifecycleChanged(rootStatus.Key, rootStatus.Status);
                    await _statusChannel.Writer.WriteAsync(
                        new ImmediatePatchRequest(ProgressScopeKey.From(rootKey), changedNodeIds),
                        cancellationToken
                    );
                }

                var scope = ProgressScopeKey.From(rootKey);
                _scopeByNodeId[key.Id] = scope;
                _progressByNodeId.AddOrUpdate(
                    key.Id,
                    _ => BufferedProgressUpdate.FromStatus(key),
                    (_, current) => current with { NodeId = key.Id, Key = key }
                );

                if (errorResult is not null)
                {
                    await dbContext.CreateDownloadClientLog(
                        key,
                        NotificationLevel.Error,
                        newStatus,
                        errorResult.ToString()
                    );
                }
            }
            finally
            {
                statusGate.Release();
            }
        });

        if (result.IsCancelled)
        {
            result.LogWarning();
            return;
        }

        if (result.IsFailed)
            result.LogError();
    }

    /// <inheritdoc />
    public async Task<Result> PersistProgressAsync(
        DownloadTaskKey key,
        DownloadTaskProgress progress,
        DirectDownloadSnapshot? snapshot = null,
        CancellationToken cancellationToken = default
    )
    {
        // Mark the node before waiting for its gate so buffered work that is already queued
        // cannot write an older estimate after this terminal value.
        _finalizedProgressNodes[key.Id] = 0;

        var progressGate = GetProgressGate(key.Id);
        await progressGate.WaitAsync(cancellationToken);
        try
        {
            _progressByNodeId.TryRemove(key.Id, out _);

            return await Result.Try(async Task<Result> () =>
            {
                using var dbContext = await _dbContextFactory.CreateAsync();
                if (await dbContext.GetDownloadStatusAsync(key) is null)
                {
                    RetireNode(key.Id);
                    return Result.Ok();
                }
                await dbContext.UpdateDownloadProgress(key, progress, snapshot, cancellationToken);

                var scope = await ResolveScopeAsync(dbContext, key, cancellationToken);
                if (scope is null)
                    return Result.Ok();

                return await SendPatchAsync(scope, [key.Id], cancellationToken, dbContext);
            });
        }
        finally
        {
            progressGate.Release();
        }
    }

    /// <inheritdoc />
    public void OnProgressUpdated(
        DownloadTaskKey key,
        DownloadTaskProgress progress,
        DirectDownloadSnapshot? snapshot = null
    )
    {
        if (
            _finalizedProgressNodes.ContainsKey(key.Id)
            || _statusByNodeId.GetValueOrDefault(key.Id)
                is DownloadStatus.Paused
                    or DownloadStatus.AutoPaused
                    or DownloadStatus.Deleted
        )
            return;

        var update = new BufferedProgressUpdate
        {
            NodeId = key.Id,
            Key = key,
            Progress = progress,
            Snapshot = snapshot,
        };

        _progressByNodeId.AddOrUpdate(key.Id, _ => update, (_, _) => update);
        if (_finalizedProgressNodes.ContainsKey(key.Id))
            _progressByNodeId.TryRemove(key.Id, out _);
    }

    /// <inheritdoc />
    public void NotifyFileTransferProgress(DownloadTaskKey key)
    {
        _progressByNodeId.AddOrUpdate(
            key.Id,
            _ => BufferedProgressUpdate.FromStatus(key),
            (_, current) => current with { NodeId = key.Id, Key = key }
        );
    }

    /// <summary>
    /// Runs the background processing loop for immediate status patches and periodic progress flushes.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var immediateStatusTask = ProcessStatusQueueAsync(stoppingToken);
        var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        var result = await Result.Try(async Task () =>
        {
            while (await periodicTimer.WaitForNextTickAsync(stoppingToken))
            {
                if (Interlocked.Exchange(ref _retirementPending, 0) != 0)
                {
                    var retirement = await Result.Try(RetireDeletedNodesAsync);
                    if (retirement.IsFailed)
                    {
                        Interlocked.Exchange(ref _retirementPending, 1);
                        retirement.LogError();
                    }
                }
                await FlushProgressAsync(stoppingToken);
            }
        });

        if (!result.IsCancelled && result.IsFailed)
            result.LogError();

        periodicTimer.Dispose();
        _statusChannel.Writer.TryComplete();
        await immediateStatusTask;
    }

    /// <summary>
    /// Reads immediate status patch requests from the channel and dispatches them to clients.
    /// </summary>
    private async Task ProcessStatusQueueAsync(CancellationToken stoppingToken)
    {
        var result = await Result.Try(async Task () =>
        {
            await foreach (var request in _statusChannel.Reader.ReadAllAsync(stoppingToken))
            {
                var changedByScope = new Dictionary<ProgressScopeKey, HashSet<Guid>>();
                MergeImmediatePatchRequest(changedByScope, request);

                while (_statusChannel.Reader.TryRead(out var queuedRequest))
                    MergeImmediatePatchRequest(changedByScope, queuedRequest);

                foreach (var pair in changedByScope)
                {
                    var sendResult = await SendPatchAsync(pair.Key, pair.Value.ToList(), stoppingToken);
                    if (sendResult.IsFailed)
                        sendResult.LogError();
                }
            }
        });

        if (result.IsCancelled && !stoppingToken.IsCancellationRequested)
            result.LogWarning();
        else if (result.IsFailed && !result.IsCancelled)
            result.LogError();
    }

    private static void MergeImmediatePatchRequest(
        Dictionary<ProgressScopeKey, HashSet<Guid>> changedByScope,
        ImmediatePatchRequest patchRequest
    )
    {
        if (!changedByScope.TryGetValue(patchRequest.Scope, out var changedIds))
        {
            changedIds = [];
            changedByScope[patchRequest.Scope] = changedIds;
        }

        foreach (var changedId in patchRequest.ChangedNodeIds)
            changedIds.Add(changedId);
    }

    /// <summary>
    /// Flushes buffered progress updates, persists the latest values, and emits changed-node + ancestor patches.
    /// </summary>
    private async Task FlushProgressAsync(CancellationToken cancellationToken)
    {
        var nodeSnapshot = _progressByNodeId.Values;
        if (nodeSnapshot.Count == 0)
            return;

        var changedNodeIdsByScope = new Dictionary<ProgressScopeKey, HashSet<Guid>>();
        var persistedUpdates = new List<BufferedProgressUpdate>(nodeSnapshot.Count);
        var persistedLogs = new List<PersistedProgressLog>(nodeSnapshot.Count);
        using var dbContext = await _dbContextFactory.CreateAsync();
        foreach (var bufferedProgress in nodeSnapshot)
        {
            var flushResult = await TryFlushBufferedProgressAsync(
                dbContext,
                bufferedProgress,
                changedNodeIdsByScope,
                persistedUpdates,
                persistedLogs,
                cancellationToken
            );

            if (flushResult.IsFailed)
            {
                _log.Here()
                    .Error(
                        "Failed to flush buffered progress for {DownloadTaskKey}. Requeuing latest value",
                        bufferedProgress.Key
                    );
                flushResult.LogError();
            }
        }

        if (changedNodeIdsByScope.Count == 0)
            return;

        var sendResult = await SendPatchesAsync(dbContext, changedNodeIdsByScope, cancellationToken);
        if (sendResult.IsFailed)
        {
            sendResult.LogIfFailed();
            foreach (var update in persistedUpdates)
                _progressByNodeId.TryAdd(update.NodeId, BufferedProgressUpdate.FromStatus(update.Key));
        }

        // Journey logging must not delay the patches for this batch.
        foreach (var update in persistedLogs)
        {
            var gate = GetProgressGate(update.Key.Id);
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (_lifecycleRevisionByNodeId.GetValueOrDefault(update.Key.Id) != update.LifecycleRevision)
                    continue;
                var result = await Result.Try(async Task () =>
                {
                    // Log tables generate IDs independently, so log inserts cannot share a tracker.
                    using var logContext = await _dbContextFactory.CreateAsync();
                    if (
                        await logContext.GetDownloadStatusAsync(update.Key) == DownloadStatus.Downloading
                        && ShouldPersistProgressDebug(update.Key.Id, update.Progress)
                    )
                        await WriteProgressJourneyLogAsync(logContext, update.Key, update.Progress, cancellationToken);
                });
                result.LogIfFailed();
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private async Task<Result> TryFlushBufferedProgressAsync(
        IReaparrDbContext dbContext,
        BufferedProgressUpdate bufferedProgress,
        Dictionary<ProgressScopeKey, HashSet<Guid>> changedNodeIdsByScope,
        List<BufferedProgressUpdate> persistedUpdates,
        List<PersistedProgressLog> persistedLogs,
        CancellationToken cancellationToken
    )
    {
        var progressGate = GetProgressGate(bufferedProgress.NodeId);
        await progressGate.WaitAsync(cancellationToken);
        try
        {
            BufferedProgressUpdate? claimedProgress = null;
            var result = await Result.Try(async Task () =>
            {
                if (!_progressByNodeId.TryRemove(bufferedProgress.NodeId, out var latestBufferedProgress))
                    return;
                claimedProgress = latestBufferedProgress;
                bufferedProgress = latestBufferedProgress;
                if (
                    _finalizedProgressNodes.ContainsKey(bufferedProgress.NodeId)
                    && bufferedProgress.Progress is not null
                )
                    return;
                if (await dbContext.GetDownloadStatusAsync(bufferedProgress.Key) is null)
                {
                    RetireNode(bufferedProgress.NodeId);
                    return;
                }

                if (
                    bufferedProgress.Progress is not null
                    && _statusByNodeId.GetValueOrDefault(bufferedProgress.NodeId)
                        is DownloadStatus.Paused
                            or DownloadStatus.AutoPaused
                )
                    return;
                if (bufferedProgress.Progress is not null)
                {
                    await dbContext.UpdateDownloadProgress(
                        bufferedProgress.Key,
                        bufferedProgress.Progress,
                        bufferedProgress.Snapshot,
                        cancellationToken
                    );
                }

                var scope = await ResolveScopeAsync(dbContext, bufferedProgress.Key, cancellationToken);
                if (scope is null)
                    return;

                if (!changedNodeIdsByScope.TryGetValue(scope, out var changedNodeIds))
                {
                    changedNodeIds = [];
                    changedNodeIdsByScope[scope] = changedNodeIds;
                }

                changedNodeIds.Add(bufferedProgress.NodeId);

                persistedUpdates.Add(bufferedProgress);
                if (bufferedProgress.Progress is { } persistedProgress)
                    persistedLogs.Add(
                        new PersistedProgressLog(
                            bufferedProgress.Key,
                            persistedProgress,
                            _lifecycleRevisionByNodeId.GetValueOrDefault(bufferedProgress.NodeId)
                        )
                    );
            });
            if (result.IsFailed && claimedProgress is not null)
            {
                _progressByNodeId.AddOrUpdate(claimedProgress.NodeId, _ => claimedProgress, (_, current) => current);
            }
            return result;
        }
        finally
        {
            progressGate.Release();
        }
    }

    private SemaphoreSlim GetProgressGate(Guid nodeId) =>
        _progressGates[(int)((uint)nodeId.GetHashCode() % (uint)_progressGates.Length)];

    private async Task RetireDeletedNodesAsync()
    {
        var candidates = _scopeByNodeId
            .Keys.Concat(_statusByNodeId.Keys)
            .Concat(_finalizedProgressNodes.Keys)
            .Concat(_lastProgressLogByNodeId.Keys)
            .Concat(_progressByNodeId.Keys)
            .Distinct()
            .ToArray();
        using var dbContext = await _dbContextFactory.CreateAsync();
        var existingIds = new HashSet<Guid>();
        foreach (var batch in candidates.Chunk(500))
            existingIds.UnionWith(
                (await dbContext.GetDownloadTaskKeysAsync(batch, CancellationToken.None)).Select(existing =>
                    existing.Id
                )
            );
        foreach (var nodeId in candidates.Where(id => !existingIds.Contains(id)))
        {
            var gate = GetProgressGate(nodeId);
            await gate.WaitAsync(CancellationToken.None);
            try
            {
                RetireNode(nodeId);
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private void RetireNode(Guid nodeId)
    {
        _progressByNodeId.TryRemove(nodeId, out _);
        _scopeByNodeId.TryRemove(nodeId, out _);
        _statusByNodeId.TryRemove(nodeId, out _);
        _finalizedProgressNodes.TryRemove(nodeId, out _);
        _lastProgressLogByNodeId.TryRemove(nodeId, out _);
        _lifecycleRevisionByNodeId.TryRemove(nodeId, out _);
    }

    private async Task PublishDeletionPatchAsync(DownloadTaskKey key, CancellationToken cancellationToken)
    {
        // Deletion shares the publication gate with upserts to preserve delivery order.
        await _publicationGate.WaitAsync(cancellationToken);
        try
        {
            var sequence = _sequenceByServer.AddOrUpdate(key.PlexServerId, 1, (_, current) => current + 1);
            await _downloadHubService.SendDownloadPatchAsync(
                key.PlexServerId,
                sequence,
                upserts: [],
                deletedIds: [key.Id],
                cancellationToken
            );
        }
        finally
        {
            _publicationGate.Release();
        }
    }

    /// <summary>
    /// Builds and sends a status patch containing changed nodes and impacted ancestors.
    /// </summary>
    private async Task<Result> SendPatchAsync(
        ProgressScopeKey scope,
        IReadOnlyCollection<Guid> changedNodeIds,
        CancellationToken cancellationToken,
        IReaparrDbContext? dbContext = null
    )
    {
        if (changedNodeIds.Count == 0)
            return Result.Ok();

        if (dbContext is not null)
            return await SendPatchWithContextAsync(dbContext, scope, changedNodeIds, cancellationToken);

        using var ownedContext = await _dbContextFactory.CreateAsync();
        return await SendPatchWithContextAsync(ownedContext, scope, changedNodeIds, cancellationToken);
    }

    private Task<Result> SendPatchWithContextAsync(
        IReaparrDbContext dbContext,
        ProgressScopeKey scope,
        IReadOnlyCollection<Guid> changedNodeIds,
        CancellationToken cancellationToken
    ) => SendPatchesAsync(dbContext, new() { [scope] = [.. changedNodeIds] }, cancellationToken);

    private async Task<Result> SendPatchesAsync(
        IReaparrDbContext dbContext,
        Dictionary<ProgressScopeKey, HashSet<Guid>> changedNodeIdsByScope,
        CancellationToken cancellationToken
    )
    {
        await _publicationGate.WaitAsync(cancellationToken);
        try
        {
            // The per-node gate, when present, is acquired by the caller before this
            // publication gate. This method only reads/maps the current root and awaits
            // its hub send; status/progress database writes remain outside this global
            // publication gate so unrelated mutations do not serialize behind clients.
            var result = await Result.Try(async Task () =>
            {
                var roots = await GetDownloadProgressRootTasksAsync(
                    dbContext,
                    changedNodeIdsByScope.Keys.Select(scope => scope.RootKey).ToArray(),
                    cancellationToken
                );
                var upsertsByServer = new Dictionary<int, List<DownloadPatchDTO>>();
                foreach (var root in roots)
                {
                    var changedNodeIds = changedNodeIdsByScope[ProgressScopeKey.From(root.ToKey())];
                    var allNodes = Flatten([root]).ToDictionary(node => node.Id);
                    var patchNodeIds = ResolvePatchNodeIds(changedNodeIds, allNodes);
                    if (patchNodeIds.Count == 0)
                        continue;

                    if (!upsertsByServer.TryGetValue(root.PlexServerId, out var upserts))
                        upsertsByServer.Add(root.PlexServerId, upserts = []);

                    foreach (var id in patchNodeIds)
                    {
                        var node = allNodes[id];
                        upserts.Add(
                            new DownloadPatchDTO
                            {
                                Id = node.Id,
                                ParentId = node.ParentId,
                                Status = node.DownloadStatus,
                                Percentage = node.Percentage,
                                DataReceived = node.DataReceived,
                                DataTotal = node.DataTotal,
                                DownloadSpeed = node.Speed,
                                TimeRemaining = node.TimeRemaining,
                            }
                        );
                    }
                }

                foreach (var (serverId, upserts) in upsertsByServer)
                {
                    var sequence = _sequenceByServer.AddOrUpdate(serverId, 1, (_, current) => current + 1);
                    await _downloadHubService.SendDownloadPatchAsync(
                        serverId,
                        sequence,
                        upserts,
                        deletedIds: null,
                        cancellationToken
                    );
                }
            });

            return result;
        }
        finally
        {
            _publicationGate.Release();
        }
    }

    /// <summary>
    /// Resolves changed nodes and all ancestor nodes that must be updated in the client tree.
    /// </summary>
    private static IReadOnlyCollection<Guid> ResolvePatchNodeIds(
        IReadOnlyCollection<Guid> changedNodeIds,
        IReadOnlyDictionary<Guid, DownloadTaskGeneric> allNodes
    )
    {
        var patchNodeIds = new HashSet<Guid>();

        foreach (var changedNodeId in changedNodeIds)
        {
            if (!allNodes.TryGetValue(changedNodeId, out var node))
                continue;

            patchNodeIds.Add(node.Id);

            var parentId = node.ParentId;
            while (parentId != Guid.Empty && allNodes.TryGetValue(parentId, out var parentNode))
            {
                patchNodeIds.Add(parentNode.Id);
                parentId = parentNode.ParentId;
            }
        }

        return patchNodeIds;
    }

    /// <summary>
    /// Flattens a hierarchical download task collection into a depth-first sequence.
    /// </summary>
    private static IEnumerable<DownloadTaskGeneric> Flatten(IEnumerable<DownloadTaskGeneric> tasks)
    {
        foreach (var task in tasks)
        {
            yield return task;

            if (task.Children.Count == 0)
                continue;

            foreach (var child in Flatten(task.Children))
                yield return child;
        }
    }

    private async Task<ProgressScopeKey?> ResolveScopeAsync(
        IReaparrDbContext dbContext,
        DownloadTaskKey key,
        CancellationToken cancellationToken
    )
    {
        if (_scopeByNodeId.TryGetValue(key.Id, out var cachedScope))
            return cachedScope;

        var rootKey = await dbContext.GetRootDownloadTaskKeyAsync(key, cancellationToken: cancellationToken);
        if (rootKey is null)
            return null;

        var resolvedScope = ProgressScopeKey.From(rootKey);
        _scopeByNodeId[key.Id] = resolvedScope;
        return resolvedScope;
    }

    private async Task LogStatusChangeAsync(
        IReaparrDbContext dbContext,
        DownloadTaskKey key,
        DownloadStatus status,
        CancellationToken cancellationToken
    )
    {
        var mediaFileName = await GetTaskDisplayNameAsync(dbContext, key, cancellationToken) ?? key.Id.ToString();

        _log.Here()
            .InformationMsg(
                "DownloadTask {DownloadTaskId} ({MediaFileName}) transitioning to {NewStatus}",
                key.Id,
                mediaFileName,
                status
            );

        await dbContext.CreateDownloadClientLog(
            key,
            status.ToNotificationLevel(),
            status,
            $"Download {mediaFileName} transitioned to status: {status}"
        );
    }

    private async Task WriteProgressJourneyLogAsync(
        IReaparrDbContext dbContext,
        DownloadTaskKey key,
        DownloadTaskProgress progress,
        CancellationToken cancellationToken
    )
    {
        var mediaFileName = await GetTaskDisplayNameAsync(dbContext, key, cancellationToken) ?? key.Id.ToString();

        var progressMsg = _log.Here()
            .DebugMsg(
                "[DownloadTaskProgress {MediaFileName} - {Percentage}% - {Speed} - {DataReceived} / {DataTotal} - {TimeRemaining}]",
                mediaFileName,
                progress.Percentage.ToString("F2"),
                DataFormat.FormatSpeedString(progress.DownloadSpeed),
                ByteSize.FromBytes(progress.DataReceived).ToString("MB"),
                ByteSize.FromBytes(progress.DataTotal).ToString("MB"),
                TimeSpan.FromSeconds(progress.TimeRemaining).ToFormattedString()
            );

        await dbContext.CreateDownloadClientLog(key, NotificationLevel.Debug, DownloadStatus.Downloading, progressMsg);
    }

    private bool ShouldPersistProgressDebug(Guid nodeId, DownloadTaskProgress progress)
    {
        if (!_lastProgressLogByNodeId.TryGetValue(nodeId, out var previous))
        {
            _lastProgressLogByNodeId[nodeId] = progress;
            return true;
        }

        var dataDelta = Math.Abs(progress.DataReceived - previous.DataReceived);
        var percentageDelta = Math.Abs(progress.Percentage - previous.Percentage);
        var shouldLog =
            dataDelta >= PROGRESS_LOG_DATA_THRESHOLD_BYTES || percentageDelta >= PROGRESS_LOG_PERCENTAGE_THRESHOLD;

        if (shouldLog)
            _lastProgressLogByNodeId[nodeId] = progress;

        return shouldLog;
    }

    private async Task PersistBufferedProgressAsync(
        IReaparrDbContext dbContext,
        DownloadTaskKey key,
        CancellationToken cancellationToken
    )
    {
        if (!_progressByNodeId.TryGetValue(key.Id, out var bufferedProgress) || bufferedProgress.Progress is null)
            return;

        var progress = bufferedProgress.Progress!;

        await dbContext.UpdateDownloadProgress(
            bufferedProgress.Key,
            progress,
            bufferedProgress.Snapshot,
            cancellationToken
        );

        if (ShouldPersistProgressDebug(key.Id, progress))
            await WriteProgressJourneyLogAsync(dbContext, key, progress, cancellationToken);

        _progressByNodeId.AddOrUpdate(
            key.Id,
            _ => BufferedProgressUpdate.FromStatus(key),
            (_, _) => BufferedProgressUpdate.FromStatus(key)
        );
    }

    private void ResetProgressJourneyTrackingIfNeeded(Guid nodeId, DownloadStatus newStatus)
    {
        if (newStatus is DownloadStatus.Queued or DownloadStatus.Restarting or DownloadStatus.Downloading)
        {
            _finalizedProgressNodes.TryRemove(nodeId, out _);
            _lastProgressLogByNodeId.TryRemove(nodeId, out _);
        }
    }

    private static async Task<string?> GetTaskDisplayNameAsync(
        IReaparrDbContext dbContext,
        DownloadTaskKey key,
        CancellationToken cancellationToken
    )
    {
        return key.Type switch
        {
            DownloadTaskType.Movie => await dbContext
                .DownloadTaskMovie.Where(x => x.Id == key.Id)
                .Select(x => x.Title)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.MovieData => await dbContext
                .DownloadTaskMovieFile.Where(x => x.Id == key.Id)
                .Select(x => x.FileName)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.TvShow => await dbContext
                .DownloadTaskTvShow.Where(x => x.Id == key.Id)
                .Select(x => x.Title)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.Season => await dbContext
                .DownloadTaskTvShowSeason.Where(x => x.Id == key.Id)
                .Select(x => x.Title)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.Episode => await dbContext
                .DownloadTaskTvShowEpisode.Where(x => x.Id == key.Id)
                .Select(x => x.Title)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.EpisodeData => await dbContext
                .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == key.Id)
                .Select(x => x.FileName)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.PhotoFolder => await dbContext
                .DownloadTaskPhotoFolders.Where(x => x.Id == key.Id)
                .Select(x => x.Title)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.PhotoImage => await dbContext
                .DownloadTaskPhotoImageFiles.Where(x => x.Id == key.Id)
                .Select(x => x.FileName)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.MusicArtist => await dbContext
                .DownloadTaskMusicArtists.Where(x => x.Id == key.Id)
                .Select(x => x.Title)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.MusicAlbum => await dbContext
                .DownloadTaskMusicAlbums.Where(x => x.Id == key.Id)
                .Select(x => x.Title)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.MusicTrack => await dbContext
                .DownloadTaskMusicTracks.Where(x => x.Id == key.Id)
                .Select(x => x.Title)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.MusicTrackData => await dbContext
                .DownloadTaskMusicTrackFiles.Where(x => x.Id == key.Id)
                .Select(x => x.FileName)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.OtherVideo => await dbContext
                .DownloadTaskOtherVideos.Where(x => x.Id == key.Id)
                .Select(x => x.Title)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.OtherVideoData => await dbContext
                .DownloadTaskOtherVideoFiles.Where(x => x.Id == key.Id)
                .Select(x => x.FileName)
                .FirstOrDefaultAsync(cancellationToken),
            _ => null,
        };
    }

    private static async Task<List<DownloadTaskGeneric>> GetDownloadProgressRootTasksAsync(
        IReaparrDbContext dbContext,
        IReadOnlyCollection<DownloadTaskKey> rootKeys,
        CancellationToken cancellationToken
    )
    {
        if (rootKeys.Count == 0)
            return [];

        var movieRootIds = rootKeys.Where(x => x.Type == DownloadTaskType.Movie).Select(x => x.Id).Distinct().ToList();
        var tvShowRootIds = rootKeys
            .Where(x => x.Type == DownloadTaskType.TvShow)
            .Select(x => x.Id)
            .Distinct()
            .ToList();
        var photoFolderRootIds = rootKeys
            .Where(x => x.Type == DownloadTaskType.PhotoFolder)
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        var musicArtistRootIds = rootKeys
            .Where(x => x.Type == DownloadTaskType.MusicArtist)
            .Select(x => x.Id)
            .Distinct()
            .ToList();
        var otherVideoRootIds = rootKeys
            .Where(x => x.Type == DownloadTaskType.OtherVideo)
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        var rootTasks = new List<DownloadTaskGeneric>(
            movieRootIds.Count
                + tvShowRootIds.Count
                + photoFolderRootIds.Count
                + musicArtistRootIds.Count
                + otherVideoRootIds.Count
        );

        if (movieRootIds.Count > 0)
        {
            var movieRoots = await dbContext
                .DownloadTaskMovie.AsNoTracking()
                .AsSplitQuery()
                .Where(x => movieRootIds.Contains(x.Id))
                .Include(x => x.Children)
                .ToListAsync(cancellationToken);

            foreach (var movie in movieRoots)
            {
                var generic = movie.ToGeneric();
                rootTasks.Add(generic);
            }
        }

        if (tvShowRootIds.Count > 0)
        {
            var tvShowRoots = await dbContext
                .DownloadTaskTvShow.AsNoTracking()
                .AsSplitQuery()
                .Where(x => tvShowRootIds.Contains(x.Id))
                .Include(x => x.Children)
                    .ThenInclude(x => x.Children)
                        .ThenInclude(x => x.Children)
                .ToListAsync(cancellationToken);

            foreach (var show in tvShowRoots)
            {
                var generic = show.ToGeneric();
                rootTasks.Add(generic);
            }
        }

        foreach (var rootKey in rootKeys.Where(x => x.Type == DownloadTaskType.PhotoFolder).DistinctBy(x => x.Id))
        {
            var generic = await dbContext.GetDownloadTaskAsync(rootKey, cancellationToken);
            if (generic is null)
                continue;

            generic.Calculate();
            rootTasks.Add(generic);
        }
        if (musicArtistRootIds.Count > 0)
        {
            var musicRoots = await dbContext
                .DownloadTaskMusicArtists.AsSplitQuery()
                .Where(x => musicArtistRootIds.Contains(x.Id))
                .Include(x => x.Children)
                    .ThenInclude(x => x.Children)
                        .ThenInclude(x => x.Children)
                .ToListAsync(cancellationToken);
            foreach (var artist in musicRoots)
            {
                var generic = artist.ToGeneric();
                rootTasks.Add(generic);
            }
        }

        if (otherVideoRootIds.Count > 0)
        {
            var otherRoots = await dbContext
                .DownloadTaskOtherVideos.AsSplitQuery()
                .Where(x => otherVideoRootIds.Contains(x.Id))
                .Include(x => x.Children)
                .ToListAsync(cancellationToken);
            foreach (var video in otherRoots)
            {
                var generic = video.ToGeneric();
                rootTasks.Add(generic);
            }
        }

        return rootTasks;
    }
}
