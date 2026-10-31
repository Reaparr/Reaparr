namespace Reaparr.Application;

/// <summary>
/// Projects stored comparison scopes and TV show hit rows onto owned-library <see cref="PlexMediaSlimDTO"/> items,
/// marking them <see cref="PlexMediaComparisonState.HigherQuality"/> when any current remote scope has an upgrade hit.
/// </summary>
/// <param name="Items">The overview page items from an owned library. Modified in-place.</param>
/// <param name="OwnedLibraryId">The owned TV show Plex library being browsed.</param>
public record ApplyOwnedTvShowComparisonStateCommand(List<PlexMediaSlimDTO> Items, int OwnedLibraryId)
    : ICommand<Result>;

public class ApplyOwnedTvShowComparisonStateCommandValidator : AbstractValidator<ApplyOwnedTvShowComparisonStateCommand>
{
    public ApplyOwnedTvShowComparisonStateCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Items).NotNull().WithMessage("Items must not be null.");
        RuleFor(x => x.OwnedLibraryId).GreaterThan(0).WithMessage("OwnedLibraryId must be greater than 0.");
    }
}

public class ApplyOwnedTvShowComparisonStateCommandHandler
    : ICommandHandler<ApplyOwnedTvShowComparisonStateCommand, Result>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ILogger _log;
    private readonly IScheduler _scheduler;

    public ApplyOwnedTvShowComparisonStateCommandHandler(IReaparrDbContext dbContext, ILogger log, IScheduler scheduler)
    {
        _dbContext = dbContext;
        _log = log.ForContext<ApplyOwnedTvShowComparisonStateCommandHandler>();
        _scheduler = scheduler;
    }

    public async Task<Result> ExecuteAsync(ApplyOwnedTvShowComparisonStateCommand command, CancellationToken ct)
    {
        var items = command.Items;
        if (items.Count == 0)
            return Result.Ok();

        var ownedLibraryExists = await _dbContext.PlexLibraries.AnyAsync(x => x.Id == command.OwnedLibraryId, ct);

        if (!ownedLibraryExists)
        {
            _log.Here()
                .Warning("Owned TV library {LibraryId} not found for comparison projection", command.OwnedLibraryId);
            return Result.Ok();
        }

        var remoteLibraryIds = await _dbContext
            .PlexLibraries.WhereIsNotOwned()
            .Where(x => x.Type == PlexMediaType.TvShow)
            .Select(x => x.Id)
            .ToHashSetAsync(ct);

        if (remoteLibraryIds.Count == 0)
            return Result.Ok();

        var currentRemoteLibraryIds = await _dbContext
            .PlexComparisonScopes.Where(x =>
                x.OwnedPlexLibraryId == command.OwnedLibraryId
                && x.MediaType == PlexMediaType.TvShow
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
                foreach (var t in items)
                    t.SetComparisonState(PlexMediaComparisonState.Pending);
            }

            return Result.Ok();
        }

        var itemIds = items.Select(x => x.Id).ToHashSet();

        var showHits = await (
            from comparison in _dbContext.PlexTvShowComparisons
            join remoteTvShow in _dbContext.PlexTvShows on comparison.RemotePlexMediaId equals remoteTvShow.Id
            where
                currentRemoteLibraryIds.Contains(comparison.RemotePlexLibraryId)
                && comparison.OwnedPlexLibraryId == command.OwnedLibraryId
                && itemIds.Contains(comparison.OwnedPlexMediaId)
            select new
            {
                comparison.OwnedPlexMediaId,
                comparison.HitState,
                RemoteEpisodeCount = remoteTvShow.GrandChildCount,
            }
        ).ToListAsync(ct);

        var showUpgradeIdSet = showHits
            .Where(x => x.HitState == PlexMediaComparisonHitState.HigherQuality)
            .Select(x => x.OwnedPlexMediaId)
            .ToHashSet();

        var episodeHits = await (
            from comparison in _dbContext.PlexEpisodeComparisons
            join episode in _dbContext.PlexTvShowEpisodes on comparison.OwnedPlexMediaId equals episode.Id
            where
                currentRemoteLibraryIds.Contains(comparison.RemotePlexLibraryId)
                && comparison.OwnedPlexLibraryId == command.OwnedLibraryId
                && itemIds.Contains(episode.TvShowId)
            select new
            {
                episode.TvShowId,
                comparison.RemotePlexMediaId,
                comparison.OwnedPlexMediaId,
                comparison.HitState,
            }
        ).ToListAsync(ct);

        var episodeHitLookup = episodeHits
            .GroupBy(x => x.TvShowId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    MatchedCount = g.Select(x => x.OwnedPlexMediaId).Distinct().Count(),
                    HigherQualityCount = g.Count(x => x.HitState == PlexMediaComparisonHitState.HigherQuality),
                }
            );

        var remoteEpisodeCountByOwnedShow = showHits
            .GroupBy(x => x.OwnedPlexMediaId)
            .ToDictionary(x => x.Key, x => x.Max(y => y.RemoteEpisodeCount));

        foreach (var item in items)
        {
            var showId = item.Id;
            remoteEpisodeCountByOwnedShow.TryGetValue(showId, out var remoteEpisodeCount);
            episodeHitLookup.TryGetValue(showId, out var episodeHitSummary);
            var matchedEpisodeCount = episodeHitSummary?.MatchedCount ?? 0;
            var hasPartialMissingChildren = remoteEpisodeCount > 0 && matchedEpisodeCount < remoteEpisodeCount;
            var hasHigherQuality = showUpgradeIdSet.Contains(showId) || episodeHitSummary?.HigherQualityCount > 0;

            item.SetComparisonState(
                (hasPartialMissingChildren, hasHigherQuality) switch
                {
                    (true, true) => PlexMediaComparisonState.PartialAndHigherQuality,
                    (true, false) => PlexMediaComparisonState.Partial,
                    (false, true) => PlexMediaComparisonState.HigherQuality,
                    _ => PlexMediaComparisonState.Owned,
                }
            );
        }

        return Result.Ok();
    }
}
