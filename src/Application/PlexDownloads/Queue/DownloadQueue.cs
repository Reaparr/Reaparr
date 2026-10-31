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
    private Task? _workerTask;

    private readonly Channel<int> _plexServersToCheckChannel = Channel.CreateUnbounded<int>();
    private readonly ConcurrentDictionary<Guid, DateTime> _retryCooldownUntil = new();

    public DownloadQueue(
        ILogger log,
        IReaparrDbContextFactory dbContextFactory,
        IDownloadTaskScheduler downloadTaskScheduler
    )
    {
        _log = log.ForContext<DownloadQueue>();
        _dbContextFactory = dbContextFactory;
        _downloadTaskScheduler = downloadTaskScheduler;
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
                    await foreach (
                        var plexServerId in _plexServersToCheckChannel.Reader.ReadAllAsync(cancellationToken)
                    )
                    {
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
        var nextDownloadTaskResult = GetNextDownloadTask(downloadTasks);
        if (nextDownloadTaskResult.IsFailed)
        {
            _log.Here()
                .Information(
                    "There are no available downloadTasks remaining for PlexServer with Id: {PlexServerName}",
                    plexServerName
                );
            return Result.Ok();
        }

        var nextDownloadTask = nextDownloadTaskResult.Value;

        _log.Here()
            .Information(
                "Selected download task {NextDownloadTaskFullTitle} to start as the next task",
                nextDownloadTask.FullTitle
            );

        var schedulingResult = await _downloadTaskScheduler.StartDownloadTaskJob(
            nextDownloadTask.ToKey(),
            cancellationToken
        );
        if (schedulingResult.IsCancelled)
            return schedulingResult.ToResult<DownloadTaskGeneric>().LogWarning();

        if (schedulingResult.IsFailed)
            return schedulingResult.ToResult<DownloadTaskGeneric>().LogIfFailed();

        _retryCooldownUntil[nextDownloadTask.Id] = DateTime.UtcNow + _retryCooldown;
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

    private bool IsInRetryCooldown(DownloadTaskGeneric task)
    {
        if (!_retryCooldownUntil.TryGetValue(task.Id, out var until))
            return false;

        if (DateTime.UtcNow < until)
            return true;

        _retryCooldownUntil.TryRemove(task.Id, out _);
        return false;
    }

    /// <summary>
    /// Determines the next downloadable <see cref="DownloadTaskGeneric"/> to be executed.
    /// </summary>
    /// <param name="downloadTasks"> The list of downloadTasks to check for the next downloadable task.</param>
    /// <returns> The next downloadable <see cref="DownloadTaskGeneric"/> to be executed.</returns>
    internal Result<DownloadTaskGeneric> GetNextDownloadTask(ICollection<DownloadTaskGeneric> downloadTasks)
    {
        var downloadingTask = FindFirstLeafByStatus(downloadTasks, DownloadStatus.Downloading);
        if (downloadingTask is not null)
            return Result.Fail("There is already a downloadTask downloading").LogDebug();

        var autoPausedTask = FindFirstLeafByStatus(downloadTasks, DownloadStatus.AutoPaused, IsInRetryCooldown);
        if (autoPausedTask is not null)
            return Result.Ok(autoPausedTask);

        var serverUnreachableTask = FindFirstLeafByStatus(
            downloadTasks,
            DownloadStatus.ServerUnreachable,
            IsInRetryCooldown
        );
        if (serverUnreachableTask is not null)
            return Result.Ok(serverUnreachableTask);

        var downloadClientErrorTask = FindFirstLeafByStatus(
            downloadTasks,
            DownloadStatus.DownloadClientError,
            IsInRetryCooldown
        );
        if (downloadClientErrorTask is not null)
            return Result.Ok(downloadClientErrorTask);

        var errorTask = FindFirstLeafByStatus(downloadTasks, DownloadStatus.Error, IsInRetryCooldown);
        if (errorTask is not null)
            return Result.Ok(errorTask);

        var queuedTask = FindFirstLeafByStatus(downloadTasks, DownloadStatus.Queued, IsInRetryCooldown);
        if (queuedTask is not null)
            return Result.Ok(queuedTask);

        return Result.Fail("There were no downloadTasks left to download").LogDebug();
    }

    private static DownloadTaskGeneric? FindFirstLeafByStatus(
        IEnumerable<DownloadTaskGeneric> downloadTasks,
        DownloadStatus status,
        Func<DownloadTaskGeneric, bool>? skip = null
    )
    {
        foreach (var downloadTask in downloadTasks)
        {
            if (downloadTask.Children.Any())
            {
                var childTask = FindFirstLeafByStatus(downloadTask.Children, status, skip);
                if (childTask is not null)
                    return childTask;

                continue;
            }

            if (downloadTask.DownloadStatus == status && (skip is null || !skip(downloadTask)))
                return downloadTask;
        }

        return null;
    }
}
