namespace Reaparr.Application;

public sealed record MediaOverviewRebuildScope(bool RebuildAll, IReadOnlyCollection<PlexMediaType> MediaTypes);

public interface IMediaOverviewRebuildCoordinator
{
    Task<IDisposable> AcquireLibrarySyncLeaseAsync(CancellationToken cancellationToken);

    Task<IDisposable> AcquireRebuildLeaseAsync(CancellationToken cancellationToken);

    bool RequestRebuild(PlexMediaType? mediaType, DateTimeOffset requestedAt);

    MediaOverviewRebuildScope? BeginRebuild(DateTimeOffset startedAt);

    MediaOverviewRebuildScope? CompleteRebuild(bool succeeded, DateTimeOffset completedAt);

    void CancelPendingRebuild();
}

public sealed class MediaOverviewRebuildCoordinator : IMediaOverviewRebuildCoordinator
{
    public static readonly IReadOnlyList<PlexMediaType> AllMediaTypes =
    [
        PlexMediaType.Movie,
        PlexMediaType.TvShow,
        PlexMediaType.MusicArtist,
        PlexMediaType.PhotoAlbum,
        PlexMediaType.OtherVideos,
    ];

    private readonly object _sync = new();
    private static readonly TimeSpan _rebuildCooldown = TimeSpan.FromMinutes(15);
    private readonly HashSet<PlexMediaType> _pendingMediaTypes = [];
    private readonly Dictionary<PlexMediaType, DateTimeOffset> _lastSuccessfulRebuildAt = [];
    private TaskCompletionSource _stateChanged = CreateSignal();
    private int _activeSyncCount;
    private bool _rebuildActive;
    private bool _rebuildTriggerPending;
    private bool _rebuildJobActive;
    private bool _pendingAll;
    private MediaOverviewRebuildScope? _activeScope;

    public async Task<IDisposable> AcquireLibrarySyncLeaseAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task waitTask;
            lock (_sync)
            {
                // A waiting rebuild must not stall unrelated servers behind a long-running sync.
                // Only an active rebuild excludes library synchronization.
                if (!_rebuildActive)
                {
                    _activeSyncCount++;
                    return new Lease(ReleaseLibrarySync);
                }

                waitTask = _stateChanged.Task;
            }

            await waitTask.WaitAsync(cancellationToken);
        }
    }

    public async Task<IDisposable> AcquireRebuildLeaseAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task waitTask;
            lock (_sync)
            {
                if (!_rebuildActive && _activeSyncCount == 0)
                {
                    _rebuildActive = true;
                    return new Lease(ReleaseRebuild);
                }

                waitTask = _stateChanged.Task;
            }

            await waitTask.WaitAsync(cancellationToken);
        }
    }

    public bool RequestRebuild(PlexMediaType? mediaType, DateTimeOffset requestedAt)
    {
        lock (_sync)
        {
            if (_rebuildJobActive)
            {
                AddPending(mediaType);
                return false;
            }

            if (HasPendingRequest)
            {
                AddPending(mediaType);
                if (_rebuildTriggerPending)
                    return false;

                _rebuildTriggerPending = true;
                return true;
            }

            if (IsWithinCooldown(mediaType, requestedAt))
                return false;

            AddPending(mediaType);
            _rebuildTriggerPending = true;
            return true;
        }
    }

    public MediaOverviewRebuildScope? BeginRebuild(DateTimeOffset startedAt)
    {
        lock (_sync)
        {
            _ = startedAt;
            if (_rebuildJobActive || !HasPendingRequest)
                return null;

            var scope = CreatePendingScope();
            ClearPendingRequest();
            _rebuildTriggerPending = false;
            _rebuildJobActive = true;
            _activeScope = scope;
            return scope;
        }
    }

    public MediaOverviewRebuildScope? CompleteRebuild(bool succeeded, DateTimeOffset completedAt)
    {
        lock (_sync)
        {
            if (!_rebuildJobActive)
                return null;

            if (succeeded && _activeScope is not null)
            {
                foreach (var mediaType in _activeScope.MediaTypes)
                    _lastSuccessfulRebuildAt[mediaType] = completedAt;
            }

            _rebuildJobActive = false;
            _activeScope = null;
            if (!HasPendingRequest)
                return null;

            _rebuildTriggerPending = true;
            return CreatePendingScope();
        }
    }

    public void CancelPendingRebuild()
    {
        lock (_sync)
            _rebuildTriggerPending = false;
    }

    private bool HasPendingRequest => _pendingAll || _pendingMediaTypes.Count > 0;

    private void AddPending(PlexMediaType? mediaType)
    {
        if (mediaType is null)
        {
            _pendingAll = true;
            _pendingMediaTypes.Clear();
            return;
        }

        if (!_pendingAll)
            _pendingMediaTypes.Add(mediaType.Value);
    }

    private void ClearPendingRequest()
    {
        _pendingAll = false;
        _pendingMediaTypes.Clear();
    }

    private MediaOverviewRebuildScope CreatePendingScope() =>
        _pendingAll
            ? new MediaOverviewRebuildScope(true, AllMediaTypes)
            : new MediaOverviewRebuildScope(false, _pendingMediaTypes.ToArray());

    private bool IsWithinCooldown(PlexMediaType? mediaType, DateTimeOffset now)
    {
        if (mediaType is null)
            return AllMediaTypes.All(type => IsWithinCooldown(type, now));

        return _lastSuccessfulRebuildAt.TryGetValue(mediaType.Value, out var completedAt)
            && now - completedAt < _rebuildCooldown;
    }

    private void ReleaseLibrarySync()
    {
        lock (_sync)
        {
            _activeSyncCount--;
            SignalStateChanged();
        }
    }

    private void ReleaseRebuild()
    {
        lock (_sync)
        {
            _rebuildActive = false;
            SignalStateChanged();
        }
    }

    private void SignalStateChanged()
    {
        _stateChanged.TrySetResult();
        _stateChanged = CreateSignal();
    }

    private static TaskCompletionSource CreateSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class Lease(Action release) : IDisposable
    {
        private Action? _release = release;

        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}
