namespace Reaparr.Application;

/// <summary>
/// Periodically checks whether Plex libraries changed and queues full library syncs only when needed.
/// </summary>
[DisallowConcurrentExecution]
public class CheckPlexLibrariesForUpdatesJob : IJob
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public CheckPlexLibrariesForUpdatesJob(ILogger log, IReaparrDbContext dbContext, ICommandExecutor commandExecutor)
    {
        _log = log.ForContext<CheckPlexLibrariesForUpdatesJob>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    protected JobTypes JobType => JobTypes.CheckPlexLibrariesForUpdatesJob;

    public static JobKey GetJobKey() =>
        new(nameof(JobTypes.CheckPlexLibrariesForUpdatesJob), nameof(JobTypes.CheckPlexLibrariesForUpdatesJob));

    public async Task Execute(IJobExecutionContext context)
    {
        _log.Here().Debug("Executing job: {JobName}", nameof(CheckPlexLibrariesForUpdatesJob));

        var cancellationToken = context.CancellationToken;

        var enabledServerIds = await _dbContext.PlexServers.Select(x => x.Id).ToListAsync(cancellationToken);

        if (enabledServerIds.Count == 0)
        {
            _log.Here().Debug("No enabled Plex servers found for automatic library sync");
            context.SetResult(JobStatus.Completed);
            return;
        }

        var accountMappings = await _dbContext
            .PlexAccountServers.Where(x => enabledServerIds.Contains(x.PlexServerId))
            .GroupBy(x => x.PlexServerId)
            .Select(x => new { PlexServerId = x.Key, PlexAccountId = x.Min(y => y.PlexAccountId) })
            .ToDictionaryAsync(x => x.PlexServerId, x => x.PlexAccountId, cancellationToken);

        var serversWithTokenMappings = new List<int>();
        foreach (var serverId in enabledServerIds)
        {
            if (!accountMappings.TryGetValue(serverId, out var accountId) || accountId <= 0)
            {
                _log.Here()
                    .Warning(
                        "No Plex account-server token mapping found for PlexServer with id {PlexServerId}; skipping refresh",
                        serverId
                    );
                continue;
            }

            var refreshResult = await _commandExecutor.Send(
                new RefreshLibraryAccessCommand(accountId, serverId),
                cancellationToken
            );

            if (refreshResult.IsCancelled)
            {
                context.SetResult(JobStatus.Cancelled, refreshResult);
                refreshResult.LogWarning();
                return;
            }

            if (refreshResult.IsFailed)
            {
                refreshResult.ToResult().LogError();
                continue;
            }

            serversWithTokenMappings.Add(serverId);
        }

        if (serversWithTokenMappings.Count == 0)
        {
            _log.Here().Debug("No Plex servers with token mappings found for automatic library sync");
            context.SetResult(JobStatus.Completed);
            return;
        }

        var outdatedLibraryIds = await _dbContext
            .PlexLibraries.Where(x => serversWithTokenMappings.Contains(x.PlexServerId) && x.Outdated)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (outdatedLibraryIds.Count == 0)
        {
            _log.Here().Debug("No outdated Plex libraries found for automatic sync");
            context.SetResult(JobStatus.Completed);
            return;
        }

        _log.Here()
            .Information("Queueing {Count} outdated Plex libraries for automatic sync", outdatedLibraryIds.Count);

        var queueResult = await _commandExecutor.Send(
            new QueueLibrarySyncJobCommand(outdatedLibraryIds),
            cancellationToken
        );

        if (queueResult.IsCancelled)
        {
            context.SetResult(JobStatus.Cancelled, queueResult);
            queueResult.LogWarning();
            return;
        }

        if (queueResult.IsFailed)
        {
            context.SetResult(JobStatus.Failed, queueResult);
            queueResult.LogError();
            return;
        }

        context.SetResult(JobStatus.Completed);
    }
}
