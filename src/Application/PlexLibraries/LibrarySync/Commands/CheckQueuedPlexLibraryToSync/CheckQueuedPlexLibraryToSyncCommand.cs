namespace Reaparr.Application;

public record CheckQueuedPlexLibraryToSyncCommand : ICommand<Result>;

public class CheckQueuedPlexLibraryToSyncCommandValidator : AbstractValidator<CheckQueuedPlexLibraryToSyncCommand>
{
    public CheckQueuedPlexLibraryToSyncCommandValidator()
    {
        RuleFor(x => x).NotNull();
    }
}

public class CheckQueuedPlexLibraryToSyncCommandHandler : ICommandHandler<CheckQueuedPlexLibraryToSyncCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IScheduler _scheduler;

    public CheckQueuedPlexLibraryToSyncCommandHandler(ILogger log, IReaparrDbContext dbContext, IScheduler scheduler)
    {
        _log = log.ForContext<CheckQueuedPlexLibraryToSyncCommandHandler>();
        _dbContext = dbContext;
        _scheduler = scheduler;
    }

    public async Task<Result> ExecuteAsync(
        CheckQueuedPlexLibraryToSyncCommand command,
        CancellationToken cancellationToken
    )
    {
        // Group queued libraries by server
        var librariesByServer = await _dbContext
            .LibrarySyncJobQueues.Where(x => x.Status == LibrarySyncJobStatus.Queued)
            .GroupBy(x => x.PlexServerId)
            .ToListAsync(cancellationToken: cancellationToken);

        if (!librariesByServer.Any())
        {
            _log.Here().Debug("No queued libraries found to sync the media for");
            return Result.Ok();
        }

        var totalLibraries = librariesByServer.Sum(g => g.Count());
        _log.Here()
            .Debug(
                "Found {Count} queued libraries across {ServerCount} servers to sync",
                totalLibraries,
                librariesByServer.Count
            );

        foreach (var serverGroup in librariesByServer)
        {
            var serverId = serverGroup.Key;
            var isServerOnline = await _dbContext.IsServerOnline(serverId);

            if (!isServerOnline)
            {
                _log.Here()
                    .Warning(
                        "Skipping {Count} library syncs for server {ServerId} because it is offline",
                        serverGroup.Count(),
                        serverId
                    );

                // Mark all queued libraries for this server as waiting for the server to come online
                await _dbContext
                    .LibrarySyncJobQueues.Where(x =>
                        x.PlexServerId == serverId && x.Status == LibrarySyncJobStatus.Queued
                    )
                    .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsServerOffline, true), cancellationToken);

                continue;
            }

            var hasProcessingLibrary = await _dbContext.LibrarySyncJobQueues.AnyAsync(
                x => x.PlexServerId == serverId && x.Status == LibrarySyncJobStatus.Processing,
                cancellationToken
            );

            if (hasProcessingLibrary)
            {
                _log.Here()
                    .Debug(
                        "Skipping queued library syncs for server {ServerId} because another library is already processing",
                        serverId
                    );
                continue;
            }

            await _dbContext
                .LibrarySyncJobQueues.Where(x =>
                    x.PlexServerId == serverId && x.Status == LibrarySyncJobStatus.Queued && x.IsServerOffline
                )
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsServerOffline, false), cancellationToken);

            var nextLibrary = serverGroup.OrderBy(x => x.Priority).First();

            await ScheduleLibrarySyncJob(
                serverId,
                nextLibrary.PlexLibraryId,
                nextLibrary.ForceMediaRefresh,
                cancellationToken
            );
        }

        return Result.Ok();
    }

    private async Task ScheduleLibrarySyncJob(
        int serverId,
        int libraryId,
        bool forceMediaRefresh,
        CancellationToken cancellationToken
    )
    {
        var jobKey = LibrarySyncJob.GetJobKey(serverId, libraryId);

        if (await _scheduler.IsQueued(jobKey, cancellationToken))
        {
            var serverName = await _dbContext.GetPlexServerNameById(serverId);
            var libraryName = await _dbContext.GetPlexLibraryNameById(libraryId);
            _log.Here()
                .Warning(
                    "Library sync job already queued for server: {ServerName} with id: {ServerId}, library {LibraryName} with id: {LibraryId}",
                    serverName,
                    serverId,
                    libraryName,
                    libraryId
                );
            return;
        }

        var claimed = await _dbContext
            .LibrarySyncJobQueues.Where(x =>
                x.PlexServerId == serverId
                && x.PlexLibraryId == libraryId
                && x.Status == LibrarySyncJobStatus.Queued
                && !_dbContext.LibrarySyncJobQueues.Any(other =>
                    other.PlexServerId == x.PlexServerId && other.Status == LibrarySyncJobStatus.Processing
                )
            )
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, LibrarySyncJobStatus.Processing)
                        .SetProperty(x => x.StartedAt, DateTime.UtcNow),
                cancellationToken
            );
        if (claimed != 1)
            return;

        var scheduleResult = await Result.Try(() =>
            _scheduler.ExecuteJob<LibrarySyncJob, LibrarySyncJobPayload>(
                jobKey,
                new LibrarySyncJobPayload(serverId, libraryId, forceMediaRefresh),
                cancellationToken
            )
        );

        // ExecuteJob also succeeds without scheduling when the job key already exists.
        if (scheduleResult.IsCancelledOrFailed || scheduleResult.Value == DateTimeOffset.MinValue)
        {
            await _dbContext
                .LibrarySyncJobQueues.Where(x =>
                    x.PlexServerId == serverId
                    && x.PlexLibraryId == libraryId
                    && x.Status == LibrarySyncJobStatus.Processing
                )
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.Status, LibrarySyncJobStatus.Queued)
                            .SetProperty(x => x.StartedAt, (DateTime?)null),
                    CancellationToken.None
                );
            scheduleResult.LogIfFailed();
            return;
        }

        _log.Here()
            .Debug("Scheduled library sync job for server {ServerId} and library {LibraryId}", serverId, libraryId);
    }
}
