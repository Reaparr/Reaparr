namespace Reaparr.Application;

public interface IMediaOverviewRebuildCoordinator
{
    Task<IDisposable> AcquireLibrarySyncLeaseAsync(CancellationToken cancellationToken);

    Task<IDisposable> AcquireRebuildLeaseAsync(CancellationToken cancellationToken);
}

public sealed class MediaOverviewRebuildCoordinator : IMediaOverviewRebuildCoordinator
{
    private readonly object _sync = new();
    private TaskCompletionSource _stateChanged = CreateSignal();
    private int _activeSyncCount;
    private int _waitingRebuildCount;
    private bool _rebuildActive;

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

    private static TaskCompletionSource CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class Lease(Action release) : IDisposable
    {
        private Action? _release = release;

        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}
