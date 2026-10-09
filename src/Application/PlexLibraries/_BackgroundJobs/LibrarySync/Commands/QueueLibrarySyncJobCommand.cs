namespace Reaparr.Application;

/// <summary>
/// Queues one or more library sync job for the specified Plex library IDs.
/// </summary>
public record QueueLibrarySyncJobCommand(
    List<int> PlexLibraryIds,
    bool ForceLibrarySync = false,
    bool ForceMediaRefresh = false
) : ICommand<Result>;

public class QueueLibrarySyncJobCommandValidator : AbstractValidator<QueueLibrarySyncJobCommand>
{
    public QueueLibrarySyncJobCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.PlexLibraryIds).NotEmpty();
    }
}

public class QueueLibrarySyncJobCommandHandler : ICommandHandler<QueueLibrarySyncJobCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public QueueLibrarySyncJobCommandHandler(ILogger log, IReaparrDbContext dbContext, ICommandExecutor commandExecutor)
    {
        _log = log.ForContext<QueueLibrarySyncJobCommandHandler>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result> ExecuteAsync(QueueLibrarySyncJobCommand command, CancellationToken cancellationToken)
    {
        var libraries = await _dbContext
            .PlexLibraries.Where(x => command.PlexLibraryIds.Contains(x.Id) && x.Type.IsRootType())
            .Select(x => new
            {
                x.Id,
                x.PlexServerId,
                x.Type,
                x.SyncedAt,
            })
            .ToListAsync(cancellationToken);

        if (!libraries.Any())
        {
            _log.Here().Warning("No libraries found for the provided library IDs. Nothing to queue");
            return Result.Ok();
        }

        var eligibleLibraryIds = libraries.Select(x => x.Id).ToHashSet();

        var existingQueues = await _dbContext
            .LibrarySyncJobQueues.Where(x => eligibleLibraryIds.Contains(x.PlexLibraryId))
            .ToListAsync(cancellationToken: cancellationToken);

        var syncBufferCutoff = DateTime.UtcNow.AddHours(-3);

        var unsyncedLibraryIds = libraries.Where(x => x.SyncedAt is null).Select(x => x.Id).ToHashSet();

        // Get IDs of items to reset (completed/failed/cancelled)
        var itemsToReset = existingQueues
            .Where(x =>
                x.Status is LibrarySyncJobStatus.Failed or LibrarySyncJobStatus.Cancelled
                || (
                    x.Status == LibrarySyncJobStatus.Completed
                    && (
                        command.ForceLibrarySync
                        || x.CompletedAt <= syncBufferCutoff
                        || unsyncedLibraryIds.Contains(x.PlexLibraryId)
                    )
                )
            )
            .Select(x => x.PlexLibraryId)
            .ToList();

        var queuedForceRefreshLibraryIds = existingQueues
            .Where(x => x.Status == LibrarySyncJobStatus.Queued)
            .Select(x => x.PlexLibraryId)
            .Concat(itemsToReset)
            .Distinct()
            .ToList();

        // Reset completed/failed items
        if (itemsToReset.Any())
        {
            await _dbContext.LibrarySyncJobQueues.ResetLibrarySyncJobQueue(itemsToReset, token: cancellationToken);
        }

        if (command.ForceMediaRefresh && queuedForceRefreshLibraryIds.Any())
        {
            await _dbContext
                .LibrarySyncJobQueues.Where(x => queuedForceRefreshLibraryIds.Contains(x.PlexLibraryId))
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.ForceMediaRefresh, true), cancellationToken);
        }

        // Get ALL existing library IDs (including those we just reset, which are now Queued)
        var existingLibraryIds = existingQueues.Select(x => x.PlexLibraryId).ToHashSet();

        // Only log warning if there are items that are queued/processing (not just completed/failed)
        var queuedOrProcessingIds = existingQueues
            .Where(x => x.Status is LibrarySyncJobStatus.Queued or LibrarySyncJobStatus.Processing)
            .Select(x => x.PlexLibraryId)
            .ToHashSet();

        if (queuedOrProcessingIds.Any())
        {
            _log.Here()
                .Warning(
                    "{Count} libraries are already queued or processing. They will be skipped",
                    queuedOrProcessingIds.Count
                );
        }

        // Add new items (excluding ALL existing ones, including those we just reset)
        var itemsToAdd = libraries
            .Where(x => !existingLibraryIds.Contains(x.Id))
            .Where(x => command.ForceLibrarySync || x.SyncedAt is null || x.SyncedAt <= syncBufferCutoff)
            .Select(x => new LibrarySyncJobQueue
            {
                PlexLibraryId = x.Id,
                PlexServerId = x.PlexServerId,
                Priority = GetPriority(x.Type),
                Status = LibrarySyncJobStatus.Queued,
                CreatedAt = DateTime.UtcNow,
                ForceMediaRefresh = command.ForceMediaRefresh,
            })
            .ToList();

        if (itemsToAdd.Any())
        {
            await _dbContext.LibrarySyncJobQueues.AddRangeAsync(itemsToAdd, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (itemsToAdd.Any() || itemsToReset.Any())
        {
            _log.Here()
                .Debug(
                    "Queued {Count} libraries for sync. Reset {ResetCount} completed/failed items. Skipped {SkippedCount} already queued/processing",
                    itemsToAdd.Count,
                    itemsToReset.Count,
                    queuedOrProcessingIds.Count
                );
        }

        if (itemsToAdd.Any() || itemsToReset.Any() || queuedOrProcessingIds.Any())
        {
            var checkQueuedResult = await _commandExecutor.Send(
                new CheckQueuedPlexLibraryToSyncCommand(),
                cancellationToken
            );

            return checkQueuedResult.LogIfFailed();
        }

        return Result.Ok();
    }

    private int GetPriority(PlexMediaType type)
    {
        return type switch
        {
            PlexMediaType.Movie => 1,
            PlexMediaType.TvShow => 2,
            _ => 3,
        };
    }
}
