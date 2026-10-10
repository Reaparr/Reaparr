using System.Collections.Concurrent;
using System.Threading.Channels;
using ByteSizeLib;
using Microsoft.Extensions.Hosting;

namespace Reaparr.Application;

/// <summary>
/// Coordinates download task update ingestion and dispatches optimized patch updates to the front-end.
/// It handles immediate status changes, an immediate first-progress broadcast, and buffered progress updates with a 1-second flush cadence.
/// </summary>
public class DownloadTaskUpdateDispatcher : BackgroundService, IDownloadTaskUpdateDispatcher
{
    private readonly ILogger _log;
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly IDownloadHubService _downloadHubService;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IDownloadTaskScheduler _downloadTaskScheduler;
    private readonly Channel<ImmediatePatchRequest> _statusChannel;
    private readonly ConcurrentDictionary<Guid, BufferedProgressUpdate> _progressByNodeId;
    private readonly ConcurrentDictionary<int, long> _sequenceByServer;
    private readonly ConcurrentDictionary<Guid, ProgressScopeKey> _scopeByNodeId;
    private readonly ConcurrentDictionary<Guid, DownloadStatus> _statusByNodeId;
    private readonly ConcurrentDictionary<Guid, DownloadTaskProgress> _lastProgressLogByNodeId;
    private readonly ConcurrentDictionary<Guid, byte> _seenProgressNodes;
    private readonly Channel<BufferedProgressUpdate> _firstProgressChannel;
    private const long PROGRESS_LOG_DATA_THRESHOLD_BYTES = 1024 * 1024;
    private const decimal PROGRESS_LOG_PERCENTAGE_THRESHOLD = 1m;

    /// <summary>
    /// Creates a new dispatcher instance.
    /// </summary>
    public DownloadTaskUpdateDispatcher(
        ILogger log,
        IReaparrDbContextFactory dbContextFactory,
        IDownloadHubService downloadHubService,
        ICommandExecutor commandExecutor,
        IDownloadTaskScheduler downloadTaskScheduler
    )
    {
        _log = log.ForContext<DownloadTaskUpdateDispatcher>();
        _dbContextFactory = dbContextFactory;
        _downloadHubService = downloadHubService;
        _commandExecutor = commandExecutor;
        _downloadTaskScheduler = downloadTaskScheduler;

        _statusChannel = Channel.CreateUnbounded<ImmediatePatchRequest>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
        );
        _progressByNodeId = new ConcurrentDictionary<Guid, BufferedProgressUpdate>();
        _sequenceByServer = new ConcurrentDictionary<int, long>();
        _scopeByNodeId = new ConcurrentDictionary<Guid, ProgressScopeKey>();
        _statusByNodeId = new ConcurrentDictionary<Guid, DownloadStatus>();
        _lastProgressLogByNodeId = new ConcurrentDictionary<Guid, DownloadTaskProgress>();
        _seenProgressNodes = new ConcurrentDictionary<Guid, byte>();
        _firstProgressChannel = Channel.CreateUnbounded<BufferedProgressUpdate>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
        );
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
        CancellationToken cancellationToken
    )
    {
        var result = await Result.Try(async Task () =>
        {
            _statusByNodeId[key.Id] = newStatus;

            if (newStatus is DownloadStatus.Deleted)
            {
                var sequence = _sequenceByServer.AddOrUpdate(key.PlexServerId, 1, (_, current) => current + 1);
                await _downloadHubService.SendDownloadPatchAsync(
                    key.PlexServerId,
                    sequence,
                    upserts: [],
                    deletedIds: [key.Id],
                    cancellationToken
                );
                return;
            }

            using var dbContext = await _dbContextFactory.CreateAsync();
            var currentStatus = await dbContext.GetDownloadStatusAsync(key);
            var hasStatusChanged = currentStatus != newStatus;

            if (hasStatusChanged)
            {
                if (newStatus is DownloadStatus.Paused or DownloadStatus.AutoPaused)
                    await PersistBufferedProgressBeforePauseAsync(dbContext, key, cancellationToken);

                await dbContext.SetDownloadStatus(key, newStatus);
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

            var changedParentKeys = await dbContext.DetermineDownloadStatus(key, cancellationToken);
            var rootKey = await dbContext.GetRootDownloadTaskKeyAsync(key, cancellationToken: cancellationToken);
            if (rootKey is null)
                return;

            var changedNodeIds = changedParentKeys.Select(x => x.Id).Append(key.Id).Distinct().ToList();
            if (hasStatusChanged || changedParentKeys.Count > 0)
            {
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
    public void OnProgressUpdated(
        DownloadTaskKey key,
        DownloadTaskProgress progress,
        DirectDownloadSnapshot? snapshot = null
    )
    {
        if (
            _statusByNodeId.GetValueOrDefault(key.Id)
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

        // On the first progress event for this node, bypass the periodic flush so the
        // front-end receives data immediately instead of waiting up to 1 second.
        if (_seenProgressNodes.TryAdd(key.Id, 0))
            _firstProgressChannel.Writer.TryWrite(update);
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
        var immediateProgressTask = ProcessImmediateProgressAsync(stoppingToken);
        var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        var result = await Result.Try(async Task () =>
        {
            while (await periodicTimer.WaitForNextTickAsync(stoppingToken))
            {
                await FlushProgressAsync(stoppingToken);
            }
        });

        if (!result.IsCancelled && result.IsFailed)
            result.LogError();

        periodicTimer.Dispose();
        _statusChannel.Writer.TryComplete();
        _firstProgressChannel.Writer.TryComplete();
        await immediateStatusTask;
        await immediateProgressTask;
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
    /// Persists and immediately broadcasts the first progress event for each node, bypassing the periodic flush cadence.
    /// </summary>
    private async Task ProcessImmediateProgressAsync(CancellationToken stoppingToken)
    {
        var result = await Result.Try(async Task () =>
        {
            await foreach (var update in _firstProgressChannel.Reader.ReadAllAsync(stoppingToken))
            {
                if (update.Progress is null)
                    continue;

                var flushResult = await TryProcessImmediateProgressAsync(update, stoppingToken);

                if (flushResult.IsFailed)
                    flushResult.LogError();
            }
        });

        if (result.IsCancelled && !stoppingToken.IsCancellationRequested)
            result.LogWarning();
        else if (result.IsFailed && !result.IsCancelled)
            result.LogError();
    }

    /// <summary>
    /// Flushes buffered progress updates, persists the latest values, and emits changed-node + ancestor patches.
    /// </summary>
    private async Task FlushProgressAsync(CancellationToken cancellationToken)
    {
        var nodeSnapshot = _progressByNodeId.Values.ToList();
        if (nodeSnapshot.Count == 0)
            return;

        using var dbContext = await _dbContextFactory.CreateAsync();
        var changedNodeIdsByScope = new Dictionary<ProgressScopeKey, HashSet<Guid>>();
        var bufferedEntriesByScope = new Dictionary<ProgressScopeKey, List<BufferedProgressUpdate>>();

        foreach (var bufferedProgress in nodeSnapshot)
        {
            if (!_progressByNodeId.TryRemove(bufferedProgress.NodeId, out var latestBufferedProgress))
                continue;

            var flushResult = await TryFlushBufferedProgressAsync(
                dbContext,
                latestBufferedProgress,
                changedNodeIdsByScope,
                bufferedEntriesByScope,
                cancellationToken
            );

            if (flushResult.IsFailed)
            {
                _log.Here()
                    .Error(
                        "Failed to flush buffered progress for {DownloadTaskKey}. Requeuing latest value",
                        latestBufferedProgress.Key
                    );
                flushResult.LogError();
                _progressByNodeId.AddOrUpdate(
                    latestBufferedProgress.NodeId,
                    _ => latestBufferedProgress,
                    (_, _) => latestBufferedProgress
                );
            }
        }

        foreach (var pair in changedNodeIdsByScope)
        {
            var sendResult = await SendPatchAsync(pair.Key, pair.Value.ToList(), cancellationToken, dbContext);
            if (sendResult.IsFailed)
            {
                sendResult.LogError();

                foreach (var bufferedProgress in bufferedEntriesByScope[pair.Key])
                {
                    _progressByNodeId.AddOrUpdate(
                        bufferedProgress.NodeId,
                        _ => bufferedProgress,
                        (_, _) => bufferedProgress
                    );
                }
            }
        }
    }

    private async Task<Result> TryProcessImmediateProgressAsync(
        BufferedProgressUpdate update,
        CancellationToken stoppingToken
    )
    {
        return await Result.Try(async Task () =>
        {
            using var dbContext = await _dbContextFactory.CreateAsync();

            if (
                !_progressByNodeId.TryGetValue(update.NodeId, out var effectiveUpdate)
                || effectiveUpdate.Progress is null
            )
                return;

            await dbContext.UpdateDownloadProgress(
                effectiveUpdate.Key,
                effectiveUpdate.Progress,
                effectiveUpdate.Snapshot,
                stoppingToken
            );

            var scope = await ResolveScopeAsync(dbContext, effectiveUpdate.Key, stoppingToken);
            if (scope is null)
                return;

            var sendResult = await SendPatchAsync(scope, [effectiveUpdate.NodeId], stoppingToken, dbContext);
            if (sendResult.IsFailed)
                sendResult.LogError();
        });
    }

    private async Task<Result> TryFlushBufferedProgressAsync(
        IReaparrDbContext dbContext,
        BufferedProgressUpdate bufferedProgress,
        Dictionary<ProgressScopeKey, HashSet<Guid>> changedNodeIdsByScope,
        Dictionary<ProgressScopeKey, List<BufferedProgressUpdate>> bufferedEntriesByScope,
        CancellationToken cancellationToken
    )
    {
        return await Result.Try(async Task () =>
        {
            if (bufferedProgress.Progress is not null)
            {
                await dbContext.UpdateDownloadProgress(
                    bufferedProgress.Key,
                    bufferedProgress.Progress,
                    bufferedProgress.Snapshot,
                    cancellationToken
                );

                await LogProgressJourneyAsync(
                    dbContext,
                    bufferedProgress.Key,
                    bufferedProgress.Progress,
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

            if (!bufferedEntriesByScope.TryGetValue(scope, out var bufferedEntries))
            {
                bufferedEntries = [];
                bufferedEntriesByScope[scope] = bufferedEntries;
            }

            bufferedEntries.Add(bufferedProgress);
        });
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

    private async Task<Result> SendPatchWithContextAsync(
        IReaparrDbContext dbContext,
        ProgressScopeKey scope,
        IReadOnlyCollection<Guid> changedNodeIds,
        CancellationToken cancellationToken
    )
    {
        var result = await Result.Try(async Task () =>
        {
            var rootTasks = await GetDownloadProgressRootTasksAsync(dbContext, [scope.RootKey], cancellationToken);
            var rootTask = rootTasks.FirstOrDefault();
            if (rootTask is null)
                return;

            var allNodes = Flatten([rootTask]).ToDictionary(x => x.Id);
            var patchNodeIds = ResolvePatchNodeIds(changedNodeIds, allNodes);

            var upserts = patchNodeIds
                .Select(id => allNodes[id])
                .Select(node => new DownloadPatchDTO
                {
                    Id = node.Id,
                    ParentId = node.ParentId,
                    Status = node.DownloadStatus,
                    Percentage = node.Percentage,
                    DataReceived = node.DataReceived,
                    DataTotal = node.DataTotal,
                    DownloadSpeed = node.Speed,
                    TimeRemaining = node.TimeRemaining,
                })
                .ToList();

            if (upserts.Count == 0)
                return;

            var sequence = _sequenceByServer.AddOrUpdate(scope.PlexServerId, 1, (_, current) => current + 1);
            await _downloadHubService.SendDownloadPatchAsync(
                scope.PlexServerId,
                sequence,
                upserts,
                deletedIds: null,
                cancellationToken
            );
        });

        return result;
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

    private async Task LogProgressJourneyAsync(
        IReaparrDbContext dbContext,
        DownloadTaskKey key,
        DownloadTaskProgress progress,
        CancellationToken cancellationToken
    )
    {
        if (!ShouldPersistProgressDebug(key.Id, progress))
            return;

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

    private async Task PersistBufferedProgressBeforePauseAsync(
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

        await LogProgressJourneyAsync(dbContext, bufferedProgress.Key, progress, cancellationToken);

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
            _lastProgressLogByNodeId.TryRemove(nodeId, out _);
            _seenProgressNodes.TryRemove(nodeId, out _);
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
            DownloadTaskType.MovieData or DownloadTaskType.MoviePart => await dbContext
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
            DownloadTaskType.EpisodeData or DownloadTaskType.EpisodePart => await dbContext
                .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == key.Id)
                .Select(x => x.FileName)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.PhotoAlbum => await dbContext
                .DownloadTaskPhotoAlbums.Where(x => x.Id == key.Id)
                .Select(x => x.Title)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.PhotoImage => await dbContext
                .DownloadTaskPhotoImages.Where(x => x.Id == key.Id)
                .Select(x => x.Title)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.PhotoData or DownloadTaskType.PhotoPart => await dbContext
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
            DownloadTaskType.MusicTrackData or DownloadTaskType.MusicTrackPart => await dbContext
                .DownloadTaskMusicTrackFiles.Where(x => x.Id == key.Id)
                .Select(x => x.FileName)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.OtherVideo => await dbContext
                .DownloadTaskOtherVideos.Where(x => x.Id == key.Id)
                .Select(x => x.Title)
                .FirstOrDefaultAsync(cancellationToken),
            DownloadTaskType.OtherVideoData or DownloadTaskType.OtherVideoPart => await dbContext
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
        var photoAlbumRootIds = rootKeys
            .Where(x => x.Type == DownloadTaskType.PhotoAlbum)
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
                + photoAlbumRootIds.Count
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
                generic.Calculate();
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
                generic.Calculate();
                rootTasks.Add(generic);
            }
        }

        if (photoAlbumRootIds.Count > 0)
        {
            var photoRoots = await dbContext
                .DownloadTaskPhotoAlbums.AsSplitQuery()
                .Where(x => photoAlbumRootIds.Contains(x.Id))
                .Include(x => x.Children)
                    .ThenInclude(x => x.Children)
                .ToListAsync(cancellationToken);
            foreach (var album in photoRoots)
            {
                var generic = album.ToGeneric();
                generic.Calculate();
                rootTasks.Add(generic);
            }
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
                generic.Calculate();
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
                generic.Calculate();
                rootTasks.Add(generic);
            }
        }

        return rootTasks;
    }
}
