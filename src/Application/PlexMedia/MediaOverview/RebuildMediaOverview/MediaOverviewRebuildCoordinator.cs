namespace Reaparr.Application;

public interface IMediaOverviewRebuildCoordinator
{
    Task<IDisposable> AcquireLibrarySyncLeaseAsync(CancellationToken cancellationToken);

    Task<IDisposable> AcquireRebuildLeaseAsync(CancellationToken cancellationToken);

    bool RequestRebuild(DateTimeOffset requestedAt);

    bool BeginRebuild(DateTimeOffset startedAt);

    bool CompleteRebuild(bool succeeded, DateTimeOffset completedAt);

    void CancelPendingRebuild();
}

public sealed class MediaOverviewRebuildCoordinator : IMediaOverviewRebuildCoordinator
{
    private readonly object _sync = new();
    private static readonly TimeSpan _rebuildCooldown = TimeSpan.FromMinutes(15);
    private TaskCompletionSource _stateChanged = CreateSignal();
    private int _activeSyncCount;
    private int _waitingRebuildCount;
    private bool _rebuildActive;
    private bool _rebuildTriggerPending;
    private bool _rebuildJobActive;
    private bool _rebuildDirty;
    private DateTimeOffset? _lastSuccessfulRebuildAt;

    public async Task<IDisposable> AcquireLibrarySyncLeaseAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task waitTask;
            lock (_sync)
            {
                if (_waitingRebuildCount == 0 && !_rebuildActive)
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
        lock (_sync)
            _waitingRebuildCount++;

        try
        {
            while (true)
            {
                Task waitTask;
                lock (_sync)
                {
                    if (!_rebuildActive && _activeSyncCount == 0)
                    {
                        _waitingRebuildCount--;
                        _rebuildActive = true;
                        return new Lease(ReleaseRebuild);
                    }

                    waitTask = _stateChanged.Task;
                }

                await waitTask.WaitAsync(cancellationToken);
            }
        }
        catch
        {
            lock (_sync)
            {
                _waitingRebuildCount--;
                SignalStateChanged();
            }

            throw;
        }
    }

    public bool RequestRebuild(DateTimeOffset requestedAt)
    {
        lock (_sync)
        {
            if (_rebuildJobActive)
            {
                _rebuildDirty = true;
                return false;
            }

            if (_rebuildTriggerPending || IsWithinCooldown(requestedAt))
                return false;

            _rebuildTriggerPending = true;
            return true;
        }
    }

    public bool BeginRebuild(DateTimeOffset startedAt)
    {
        lock (_sync)
        {
            _rebuildTriggerPending = false;
            if (IsWithinCooldown(startedAt))
                return false;

            _rebuildJobActive = true;
            _rebuildDirty = false;
            return true;
        }
    }

    public bool CompleteRebuild(bool succeeded, DateTimeOffset completedAt)
    {
        lock (_sync)
        {
            _rebuildJobActive = false;
            if (succeeded)
            {
                _lastSuccessfulRebuildAt = completedAt;
                _rebuildDirty = false;
                return false;
            }

            if (!_rebuildDirty || _rebuildTriggerPending)
                return false;

            _rebuildDirty = false;
            _rebuildTriggerPending = true;
            return true;
        }
    }

    public void CancelPendingRebuild()
    {
        lock (_sync)
            _rebuildTriggerPending = false;
    }

    private bool IsWithinCooldown(DateTimeOffset now) =>
        _lastSuccessfulRebuildAt.HasValue && now - _lastSuccessfulRebuildAt.Value < _rebuildCooldown;

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
