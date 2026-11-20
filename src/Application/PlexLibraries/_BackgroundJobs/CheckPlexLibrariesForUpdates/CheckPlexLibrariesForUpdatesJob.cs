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
    private readonly ITelemetry _telemetry;

    public CheckPlexLibrariesForUpdatesJob(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<CheckPlexLibrariesForUpdatesJob>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    protected JobTypes JobType => JobTypes.CheckPlexLibrariesForUpdatesJob;

    public static JobKey GetJobKey() =>
        new(nameof(JobTypes.CheckPlexLibrariesForUpdatesJob), nameof(JobTypes.CheckPlexLibrariesForUpdatesJob));

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var telemetryScope = _telemetry.BeginJob(
            TelemetryActivity.LibraryUpdateCheck,
            context.GetTelemetryContext()
        );

        _log.Here().Debug("Executing job: {JobName}", nameof(CheckPlexLibrariesForUpdatesJob));

        var result = await Result.Try(
            async Task<Result> () =>
            {
                var enabledServerIds = await _dbContext.PlexServers.Select(x => x.Id).ToListAsync(cancellationToken);

                if (enabledServerIds.Count == 0)
                {
                    _log.Here().Debug("No enabled Plex servers found for automatic library sync");
                    context.SetResult(JobStatus.Completed);
                    return Result.Ok();
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

                    if (refreshResult.IsCancelledOrFailed)
                    {
                        if (refreshResult.IsCancelled)
                            return refreshResult.ToResult();

                        refreshResult.LogIfFailed();
                        _telemetry.CaptureErrors(refreshResult, TelemetryActivity.LibraryUpdateCheck);
                        continue;
                    }

                    serversWithTokenMappings.Add(serverId);
                }

                if (serversWithTokenMappings.Count == 0)
                {
                    _log.Here().Debug("No Plex servers with token mappings found for automatic library sync");
                    context.SetResult(JobStatus.Completed);
                    return Result.Ok();
                }

                var outdatedLibraryIds = await _dbContext
                    .PlexLibraries.Where(x => serversWithTokenMappings.Contains(x.PlexServerId) && x.Outdated)
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken);

                if (outdatedLibraryIds.Count == 0)
                {
                    _log.Here().Debug("No outdated Plex libraries found for automatic sync");
                    context.SetResult(JobStatus.Completed);
                    return Result.Ok();
                }

                _log.Here()
                    .Information(
                        "Queueing {Count} outdated Plex libraries for automatic sync",
                        outdatedLibraryIds.Count
                    );

                var queueResult = await _commandExecutor.Send(
                    new QueueLibrarySyncJobCommand(outdatedLibraryIds, Trigger: TelemetryTrigger.Scheduled),
                    cancellationToken
                );

                if (queueResult.IsCancelledOrFailed)
                    return queueResult;

                context.SetResult(JobStatus.Completed);
                return Result.Ok();
            },
            exception =>
                exception is OperationCanceledException && !cancellationToken.IsCancellationRequested
                    ? new Error(exception.Message).CausedBy(exception)
                    : new ExceptionalError(exception)
        );

        if (result.IsCancelledOrFailed)
        {
            context.SetResult(result.IsCancelled ? JobStatus.Cancelled : JobStatus.Failed, result);
            result.LogIfFailed();
            _telemetry.CaptureErrors(result, TelemetryActivity.LibraryUpdateCheck);
        }
    }
}
