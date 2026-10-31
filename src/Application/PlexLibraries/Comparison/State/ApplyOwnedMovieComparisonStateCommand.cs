namespace Reaparr.Application;

/// <summary>
/// Projects stored comparison scopes and movie hit rows onto owned-library <see cref="PlexMediaSlimDTO"/> items,
/// marking them <see cref="PlexMediaComparisonState.HigherQuality"/> when any current remote scope has an upgrade hit.
/// </summary>
/// <param name="Items">The overview page items from an owned library. Modified in-place.</param>
/// <param name="OwnedLibraryId">The owned movie Plex library being browsed.</param>
public record ApplyOwnedMovieComparisonStateCommand(List<PlexMediaSlimDTO> Items, int OwnedLibraryId)
    : ICommand<Result>;

public class ApplyOwnedMovieComparisonStateCommandValidator : AbstractValidator<ApplyOwnedMovieComparisonStateCommand>
{
    public ApplyOwnedMovieComparisonStateCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Items).NotNull().WithMessage("Items must not be null.");
        RuleFor(x => x.OwnedLibraryId).GreaterThan(0).WithMessage("OwnedLibraryId must be greater than 0.");
    }
}

public class ApplyOwnedMovieComparisonStateCommandHandler
    : ICommandHandler<ApplyOwnedMovieComparisonStateCommand, Result>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ILogger _log;
    private readonly IScheduler _scheduler;

    public ApplyOwnedMovieComparisonStateCommandHandler(IReaparrDbContext dbContext, ILogger log, IScheduler scheduler)
    {
        _dbContext = dbContext;
        _log = log.ForContext<ApplyOwnedMovieComparisonStateCommandHandler>();
        _scheduler = scheduler;
    }

    public async Task<Result> ExecuteAsync(ApplyOwnedMovieComparisonStateCommand command, CancellationToken ct)
    {
        var items = command.Items;
        if (items.Count == 0)
            return Result.Ok();

        var ownedLibraryExists = await _dbContext.PlexLibraries.AnyAsync(x => x.Id == command.OwnedLibraryId, ct);

        if (!ownedLibraryExists)
        {
            _log.Here()
                .Warning("Owned library {LibraryId} not found for comparison projection", command.OwnedLibraryId);
            return Result.Ok();
        }

        var remoteLibraryIds = await _dbContext
            .PlexLibraries.WhereIsNotOwned()
            .Where(x => x.Type == PlexMediaType.Movie)
            .Select(x => x.Id)
            .ToHashSetAsync(ct);

        if (remoteLibraryIds.Count == 0)
            return Result.Ok();

        var currentRemoteLibraryIds = await _dbContext
            .PlexComparisonScopes.Where(x =>
                x.OwnedPlexLibraryId == command.OwnedLibraryId
                && x.MediaType == PlexMediaType.Movie
                && remoteLibraryIds.Contains(x.RemotePlexLibraryId)
            )
            .Select(x => x.RemotePlexLibraryId)
            .ToHashSetAsync(ct);

        if (currentRemoteLibraryIds.Count == 0)
        {
            var hasActiveJobs = await _scheduler.HasActiveJobs(
                remoteLibraryIds.Select(x => PlexLibraryComparisonJob.GetJobKey(command.OwnedLibraryId, x)),
                ct
            );
            if (hasActiveJobs)
            {
                foreach (var item in items)
                    item.SetComparisonState(PlexMediaComparisonState.Pending);
            }

            return Result.Ok();
        }

        var itemIds = items.Select(x => x.Id).ToHashSet();

        var upgradeIds = await _dbContext
            .PlexMovieComparisons.Where(x =>
                currentRemoteLibraryIds.Contains(x.RemotePlexLibraryId)
                && x.OwnedPlexLibraryId == command.OwnedLibraryId
                && itemIds.Contains(x.OwnedPlexMediaId)
                && x.HitState == PlexMediaComparisonHitState.HigherQuality
            )
            .Select(x => x.OwnedPlexMediaId)
            .Distinct()
            .ToListAsync(ct);

        var upgradeIdSet = upgradeIds.ToHashSet();

        foreach (var item in items)
        {
            item.SetComparisonState(
                upgradeIdSet.Contains(item.Id) ? PlexMediaComparisonState.HigherQuality : PlexMediaComparisonState.Owned
            );
        }

        return Result.Ok();
    }
}
