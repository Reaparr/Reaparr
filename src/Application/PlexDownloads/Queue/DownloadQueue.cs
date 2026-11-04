using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Reaparr.Application;

/// <summary>
/// The DownloadQueue is responsible for deciding which downloadTask is handled.
/// </summary>
public class DownloadQueue : IDownloadQueue
{
    /// <summary>
    /// Cooldown applied after a task is picked by the queue. This prevents a fast-failing
    /// task from being selected repeatedly in a tight listener-triggered loop.
    /// </summary>
    private static readonly TimeSpan _retryCooldown = TimeSpan.FromSeconds(60);

    private readonly ILogger _log;
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly IDownloadTaskScheduler _downloadTaskScheduler;
    private readonly TimeProvider _timeProvider;
    private Task? _workerTask;

    private readonly Channel<int> _plexServersToCheckChannel = Channel.CreateUnbounded<int>();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _retryCooldownUntil = new();
    private readonly Dictionary<int, DateTimeOffset> _retryChecksAt = new();

    public DownloadQueue(
        ILogger log,
        IReaparrDbContextFactory dbContextFactory,
        IDownloadTaskScheduler downloadTaskScheduler,
        TimeProvider timeProvider
    )
    {
        _log = log.ForContext<DownloadQueue>();
        _dbContextFactory = dbContextFactory;
        _downloadTaskScheduler = downloadTaskScheduler;
        _timeProvider = timeProvider;
    }

    public bool IsBusy => _plexServersToCheckChannel.Reader.Count > 0;

    public Result Setup() => Setup(CancellationToken.None);

    public Result Setup(CancellationToken cancellationToken)
    {
        if (_workerTask is not null)
            return Result.Ok();

        _workerTask = Task.Run(
            async () =>
            {
                var result = await Result.Try(async Task () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var plexServerId = await ReadNextServerAsync(cancellationToken);

                        var queueResult = await Result.Try(async Task<Result> () =>
                            (await CheckDownloadQueueServer(plexServerId, cancellationToken)).ToResult()
                        );
                        if (queueResult.IsCancelled)
                        {
                            queueResult.LogWarning();
                            return;
                        }

                        if (queueResult.IsFailed)
                            queueResult.LogWarning();
                    }
                });

                if (result.IsCancelled && cancellationToken.IsCancellationRequested)
                    return;

                if (result.IsCancelled)
                    result.LogWarning();
                else if (result.IsFailed)
                    result.LogError();
            },
            CancellationToken.None
        );
        return Result.Ok();
    }

    private async Task<int> ReadNextServerAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var now = _timeProvider.GetUtcNow();
            DateTimeOffset? nextRetryAt = null;
            foreach (var (serverId, retryAt) in _retryChecksAt)
            {
                if (retryAt <= now)
                {
                    _retryChecksAt.Remove(serverId);
                    _plexServersToCheckChannel.Writer.TryWrite(serverId);
                }
                else if (nextRetryAt is null || retryAt < nextRetryAt)
                    nextRetryAt = retryAt;
            }

            if (nextRetryAt is null || _plexServersToCheckChannel.Reader.TryPeek(out _))
                return await _plexServersToCheckChannel.Reader.ReadAsync(cancellationToken);

            using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                var queuedWork = _plexServersToCheckChannel.Reader.WaitToReadAsync(waitCancellation.Token).AsTask();
                var cooldownElapsed = Task.Delay(nextRetryAt.Value - now, _timeProvider, waitCancellation.Token);
                var completed = await Task.WhenAny(queuedWork, cooldownElapsed);
                await completed;
            }
            finally
            {
                await waitCancellation.CancelAsync();
            }
        }
    }

    /// <summary>
    /// Check the DownloadQueue for downloadTasks which can be started.
    /// </summary>
    public Task<Result> CheckDownloadQueue(List<int> plexServerIds) =>
        CheckDownloadQueue(plexServerIds, CancellationToken.None);

    public async Task<Result> CheckDownloadQueue(List<int> plexServerIds, CancellationToken cancellationToken)
    {
        if (!plexServerIds.Any())
            return ResultExtensions.IsEmpty(nameof(plexServerIds)).LogWarning();

        _log.Here()
            .Information(
                "Adding {PlexServerIdsCount} {NameOfPlexServer}s to the DownloadQueue to check for the next download",
                plexServerIds.Count,
                nameof(PlexServer)
            );
        foreach (var plexServerId in plexServerIds)
            await _plexServersToCheckChannel.Writer.WriteAsync(plexServerId, cancellationToken);

        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> CheckDownloadQueueForAllServers(CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateAsync();

        var plexServerIds = await dbContext.PlexServers.AsNoTracking().Select(x => x.Id).ToListAsync(cancellationToken);

        if (!plexServerIds.Any())
            return Result.Ok();

        return await CheckDownloadQueue(plexServerIds, cancellationToken);
    }

    internal async Task<Result<DownloadTaskGeneric>> CheckDownloadQueueServer(
        int plexServerId,
        CancellationToken cancellationToken = default
    )
    {
        if (plexServerId <= 0)
            return ResultExtensions.IsInvalidId(nameof(plexServerId), plexServerId).LogWarning();

        _retryChecksAt.Remove(plexServerId);

        // Create a new DbContext for this operation to avoid threading issues
        using var dbContext = await _dbContextFactory.CreateAsync();

        var plexServerName = await dbContext.GetPlexServerNameById(plexServerId);

        if (await dbContext.IsDownloadsPausedByUser(plexServerId))
        {
            _log.Here()
                .Information(
                    "Skipping download queue check because PlexServer {PlexServerName} is paused by user",
                    plexServerName
                );
            return Result.Ok();
        }

        if (await dbContext.IsServerDisabled(plexServerId))
        {
            _log.Here()
                .Information(
                    "Skipping download queue check because PlexServer {PlexServerName} is disabled",
                    plexServerName
                );
            return Result.Ok();
        }

        // Check if the server is online
        if (!await dbContext.IsServerOnline(plexServerId))
        {
            return _log.Here()
                .WarningResult(
                    "PlexServer with name: {PlexServerName} is not online, cannot continue checking the DownloadQueue to pick the following download",
                    plexServerName
                );
        }

        var downloadTasks = await dbContext.GetAllDownloadTasksByServerAsync(
            plexServerId,
            cancellationToken: cancellationToken
        );

        var activeDownloadTaskKeys = await _downloadTaskScheduler.GetCurrentlyDownloadingKeysByServer(plexServerId);
        if (HasActivePreDownloadTask(downloadTasks, activeDownloadTaskKeys))
        {
            return Result
                .Fail("Cannot select the next download task because server is already downloading one")
                .LogWarning();
        }

        _log.Here()
            .Debug(
                "Checking {NameOfPlexServer}: {PlexServerName} for the next download to start",
                nameof(PlexServer),
                plexServerName
            );
        var nextDownloadTaskResult = GetNextDownloadTask(downloadTasks, out var nextRetryAt);
        if (nextDownloadTaskResult.IsFailed)
        {
            if (nextRetryAt is not null)
                _retryChecksAt[plexServerId] = nextRetryAt.Value;

            _log.Here()
                .Information(
                    "There are no available downloadTasks remaining for PlexServer with Id: {PlexServerName}",
                    plexServerName
                );
            return Result.Ok();
        }

        return await ScheduleDownloadWithCooldownAsync(nextDownloadTaskResult.Value, cancellationToken);
    }

    private async ValueTask<Result<DownloadTaskGeneric>> ScheduleDownloadWithCooldownAsync(
        DownloadTaskGeneric nextDownloadTask,
        CancellationToken cancellationToken
    )
    {
        _log.Here()
            .Information(
                "Selected download task {NextDownloadTaskFullTitle} to start as the next task",
                nextDownloadTask.FullTitle
            );

        var schedulingResult = await _downloadTaskScheduler.StartDownloadTaskJob(
            nextDownloadTask.ToKey(),
            cancellationToken
        );
        if (schedulingResult.IsFailed)
            return schedulingResult.ToResult<DownloadTaskGeneric>().LogIfFailed();

        _retryCooldownUntil[nextDownloadTask.Id] = _timeProvider.GetUtcNow() + _retryCooldown;
        return Result.Ok(nextDownloadTask);
    }

    private static bool HasActivePreDownloadTask(
        ICollection<DownloadTaskGeneric> downloadTasks,
        IReadOnlyCollection<DownloadTaskKey> activeDownloadTaskKeys
    )
    {
        if (activeDownloadTaskKeys.Count == 0)
            return false;

        var activeDownloadTaskIds = activeDownloadTaskKeys.Select(x => x.Id).ToHashSet();
        var matchedTaskIds = new HashSet<Guid>();
        foreach (var downloadTask in downloadTasks)
        {
            if (HasActivePreDownloadTask(downloadTask, activeDownloadTaskIds, matchedTaskIds))
                return true;
        }

        // An active Quartz job without a corresponding persisted task cannot be identified as cleanup.
        return matchedTaskIds.Count != activeDownloadTaskIds.Count;
    }

    private static bool HasActivePreDownloadTask(
        DownloadTaskGeneric downloadTask,
        HashSet<Guid> activeDownloadTaskIds,
        HashSet<Guid> matchedTaskIds
    )
    {
        if (activeDownloadTaskIds.Contains(downloadTask.Id))
        {
            matchedTaskIds.Add(downloadTask.Id);
            if (downloadTask.DownloadStatus != DownloadStatus.DownloadFinished)
                return true;
        }

        foreach (var child in downloadTask.Children)
        {
            if (HasActivePreDownloadTask(child, activeDownloadTaskIds, matchedTaskIds))
                return true;
        }

        return false;
    }

    private bool IsInRetryCooldown(DownloadTaskGeneric task, ref DateTimeOffset? nextRetryAt)
    {
        if (!_retryCooldownUntil.TryGetValue(task.Id, out var until))
            return false;

        if (_timeProvider.GetUtcNow() >= until)
        {
            _retryCooldownUntil.TryRemove(task.Id, out _);
            return false;
        }

        nextRetryAt = nextRetryAt is null || until < nextRetryAt ? until : nextRetryAt;
        return true;
    }

    /// <summary>
    /// Determines the next downloadable <see cref="DownloadTaskGeneric"/> to be executed.
    /// </summary>
    /// <param name="downloadTasks"> The list of downloadTasks to check for the next downloadable task.</param>
    /// <param name="nextRetryAt">The earliest cooldown blocking otherwise eligible work.</param>
    /// <returns> The next downloadable <see cref="DownloadTaskGeneric"/> to be executed.</returns>
    internal Result<DownloadTaskGeneric> GetNextDownloadTask(
        ICollection<DownloadTaskGeneric> downloadTasks,
        out DateTimeOffset? nextRetryAt
    )
    {
        nextRetryAt = null;
        var downloadingTask = FindFirstLeafByStatus(downloadTasks, DownloadStatus.Downloading, ref nextRetryAt, false);
        if (downloadingTask is not null)
            return Result.Fail("There is already a downloadTask downloading").LogDebug();

        var autoPausedTask = FindFirstLeafByStatus(downloadTasks, DownloadStatus.AutoPaused, ref nextRetryAt, true);
        if (autoPausedTask is not null)
            return Result.Ok(autoPausedTask);

        var serverUnreachableTask = FindFirstLeafByStatus(
            downloadTasks,
            DownloadStatus.ServerUnreachable,
            ref nextRetryAt,
            true
        );
        if (serverUnreachableTask is not null)
            return Result.Ok(serverUnreachableTask);

        var downloadClientErrorTask = FindFirstLeafByStatus(
            downloadTasks,
            DownloadStatus.DownloadClientError,
            ref nextRetryAt,
            true
        );
        if (downloadClientErrorTask is not null)
            return Result.Ok(downloadClientErrorTask);

        var errorTask = FindFirstLeafByStatus(downloadTasks, DownloadStatus.Error, ref nextRetryAt, true);
        if (errorTask is not null)
            return Result.Ok(errorTask);

        var queuedTask = FindFirstLeafByStatus(downloadTasks, DownloadStatus.Queued, ref nextRetryAt, true);
        if (queuedTask is not null)
            return Result.Ok(queuedTask);

        return Result.Fail("There were no downloadTasks left to download").LogDebug();
    }

    private DownloadTaskGeneric? FindFirstLeafByStatus(
        IEnumerable<DownloadTaskGeneric> downloadTasks,
        DownloadStatus status,
        ref DateTimeOffset? nextRetryAt,
        bool skipCooldown
    )
    {
        foreach (var downloadTask in downloadTasks)
        {
            if (downloadTask.Children.Any())
            {
                var childTask = FindFirstLeafByStatus(downloadTask.Children, status, ref nextRetryAt, skipCooldown);
                if (childTask is not null)
                    return childTask;

                continue;
            }

            if (
                downloadTask.DownloadStatus == status
                && (!skipCooldown || !IsInRetryCooldown(downloadTask, ref nextRetryAt))
            )
                return downloadTask;
        }

        return null;
    }
}
