namespace Reaparr.Application;

/// <summary>
/// Schedules comparisons for every compatible remote-to-owned library pair affected by one library.
/// </summary>
/// <param name="PlexLibraryId">
/// The library whose sync, ownership, or access change should refresh comparison cache rows.
/// </param>
public record ScheduleAffectedLibraryComparisonJobsCommand(int PlexLibraryId) : ICommand<Result>;

/// <summary>
/// Validates requests to discover comparison pairs affected by one library.
/// </summary>
public class ScheduleAffectedLibraryComparisonJobsCommandValidator
    : AbstractValidator<ScheduleAffectedLibraryComparisonJobsCommand>
{
    public ScheduleAffectedLibraryComparisonJobsCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.PlexLibraryId).GreaterThan(0);
    }
}

/// <summary>
/// Discovers compatible remote-to-owned library pairs for one changed library and schedules each affected comparison.
/// </summary>
public class ScheduleAffectedLibraryComparisonJobsCommandHandler
    : ICommandHandler<ScheduleAffectedLibraryComparisonJobsCommand, Result>
{
    private static readonly TimeSpan _comparisonJobDelay = TimeSpan.FromMinutes(2);

    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IScheduler _scheduler;

    public ScheduleAffectedLibraryComparisonJobsCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        IScheduler scheduler
    )
    {
        _log = log.ForContext<ScheduleAffectedLibraryComparisonJobsCommandHandler>();
        _dbContext = dbContext;
        _scheduler = scheduler;
    }

    public async Task<Result> ExecuteAsync(
        ScheduleAffectedLibraryComparisonJobsCommand command,
        CancellationToken cancellationToken
    )
    {
        var sourceLibrary = await _dbContext
            .PlexLibraries.Where(x => x.Id == command.PlexLibraryId)
            .SelectOwnership()
            .SingleOrDefaultAsync(cancellationToken);

        if (sourceLibrary is null)
            return Result.Fail("Library {PlexLibraryId} was not found or was disabled", command.PlexLibraryId);

        if (!sourceLibrary.Type.SupportsComparison())
            return Result.Ok();

        var targetLibraries = await _dbContext
            .PlexLibraries.Where(x => x.Id != sourceLibrary.Id && x.Type == sourceLibrary.Type)
            .SelectOwnership()
            .ToListAsync(cancellationToken);

        // Comparison always flows remote-to-owned, regardless of which side changed.
        var pairs = (
            sourceLibrary.IsOwned
                ? targetLibraries
                    .Where(x => !x.IsOwned)
                    .Select(x => (OwnedLibraryId: sourceLibrary.Id, RemoteLibraryId: x.Id))
                : targetLibraries
                    .Where(x => x.IsOwned)
                    .Select(x => (OwnedLibraryId: x.Id, RemoteLibraryId: sourceLibrary.Id))
        ).ToList();

        if (!pairs.Any())
            return Result.Ok();

        // Resolve active comparison keys once before dispatching child commands, so repeated updates do not spam
        // schedule requests for pairs that are already queued or running.
        var activeComparisonJobKeys = (await _scheduler.GetJobKeys(JobTypes.LibraryComparisonJob, cancellationToken))
            .Select(x => x)
            .ToHashSet();

        pairs = pairs
            .Where(pair =>
                !activeComparisonJobKeys.Contains(
                    PlexLibraryComparisonJob.GetJobKey(pair.OwnedLibraryId, pair.RemoteLibraryId)
                )
            )
            .ToList();

        var comparisonJobs = pairs
            .Select(pair =>
                (
                    PlexLibraryComparisonJob.GetJobKey(pair.OwnedLibraryId, pair.RemoteLibraryId),
                    new PlexLibraryComparisonJobPayload(pair.OwnedLibraryId, pair.RemoteLibraryId)
                )
            )
            .ToList();

        if (comparisonJobs.Count == 0)
            return Result.Ok();

        var schedulingResult = await _scheduler.ExecuteJobs<PlexLibraryComparisonJob, PlexLibraryComparisonJobPayload>(
            comparisonJobs,
            cancellationToken,
            DateTimeOffset.UtcNow.Add(_comparisonJobDelay)
        );

        if (schedulingResult.IsFailed)
            return schedulingResult.ToResult().LogError();

        _log.Here()
            .Debug(
                "Scheduled {ScheduledCount} library comparison jobs affected by library {PlexLibraryId}",
                comparisonJobs.Count,
                sourceLibrary.Id
            );

        return Result.Ok();
    }
}
