namespace Reaparr.Application;

/// <summary>
/// Projects stored comparison scopes and TV show/season/episode hit rows onto remote-library
/// <see cref="PlexMediaSlimDTO"/> items, setting <see cref="PlexMediaSlimDTO.ComparisonId"/> per item in-place.
/// </summary>
/// <param name="Items">The overview page items from a remote library. Modified in-place.</param>
/// <param name="RemoteLibraryId">The remote TV show Plex library being browsed.</param>
public record ApplyRemoteTvShowComparisonStateCommand(List<PlexMediaSlimDTO> Items, int RemoteLibraryId)
    : ICommand<Result>;

public class ApplyRemoteTvShowComparisonStateCommandValidator
    : AbstractValidator<ApplyRemoteTvShowComparisonStateCommand>
{
    public ApplyRemoteTvShowComparisonStateCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Items).NotNull().WithMessage("Items must not be null.");
        RuleFor(x => x.RemoteLibraryId).GreaterThan(0).WithMessage("RemoteLibraryId must be greater than 0.");
    }
}

public class ApplyRemoteTvShowComparisonStateCommandHandler
    : ICommandHandler<ApplyRemoteTvShowComparisonStateCommand, Result>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ILogger _log;
    private readonly IScheduler _scheduler;

    public ApplyRemoteTvShowComparisonStateCommandHandler(
        IReaparrDbContext dbContext,
        ILogger log,
        IScheduler scheduler
    )
    {
        _dbContext = dbContext;
        _log = log.ForContext<ApplyRemoteTvShowComparisonStateCommandHandler>();
        _scheduler = scheduler;
    }

    public async Task<Result> ExecuteAsync(ApplyRemoteTvShowComparisonStateCommand command, CancellationToken ct)
    {
        var items = command.Items;
        if (items.Count == 0)
            return Result.Ok();

        var remoteLibraryExists = await _dbContext.PlexLibraries.AnyAsync(x => x.Id == command.RemoteLibraryId, ct);

        if (!remoteLibraryExists)
        {
            _log.Here()
                .Warning("Remote library {LibraryId} not found for TV comparison projection", command.RemoteLibraryId);
            return Result.Ok();
        }

        var ownedLibraryIds = await _dbContext
            .PlexLibraries.WhereIsOwned()
            .Where(x => x.Type == PlexMediaType.TvShow)
            .Select(x => x.Id)
            .ToHashSetAsync(ct);

        if (ownedLibraryIds.Count == 0)
            return Result.Ok();

        var currentOwnedLibraryIds = await _dbContext
            .PlexComparisonScopes.Where(x =>
                x.RemotePlexLibraryId == command.RemoteLibraryId
                && x.MediaType == PlexMediaType.TvShow
                && ownedLibraryIds.Contains(x.OwnedPlexLibraryId)
            )
            .Select(x => x.OwnedPlexLibraryId)
            .ToHashSetAsync(ct);

        if (currentOwnedLibraryIds.Count == 0)
        {
            var hasActiveJobs = await _scheduler.HasActiveJobs(
                ownedLibraryIds.Select(x => PlexLibraryComparisonJob.GetJobKey(x, command.RemoteLibraryId)),
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

        var showHits = await _dbContext
            .PlexTvShowComparisons.Where(x =>
                x.RemotePlexLibraryId == command.RemoteLibraryId
                && currentOwnedLibraryIds.Contains(x.OwnedPlexLibraryId)
                && itemIds.Contains(x.RemotePlexMediaId)
            )
            .Select(x => new
            {
                x.RemotePlexMediaId,
                x.OwnedPlexLibraryId,
                x.HitState,
            })
            .ToListAsync(ct);

        var showHitLookup = showHits.GroupBy(x => x.RemotePlexMediaId).ToDictionary(g => g.Key, g => g.ToList());

        var episodeHits = await (
            from comparison in _dbContext.PlexEpisodeComparisons
            join episode in _dbContext.PlexTvShowEpisodes on comparison.RemotePlexMediaId equals episode.Id
            where
                comparison.RemotePlexLibraryId == command.RemoteLibraryId
                && currentOwnedLibraryIds.Contains(comparison.OwnedPlexLibraryId)
                && itemIds.Contains(episode.TvShowId)
            select new
            {
                episode.TvShowId,
                comparison.RemotePlexMediaId,
                comparison.HitState,
            }
        ).ToListAsync(ct);

        var episodeHitLookup = episodeHits
            .GroupBy(x => x.TvShowId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    MatchedCount = g.Select(x => x.RemotePlexMediaId).Distinct().Count(),
                    HigherQualityCount = g.Count(x => x.HitState == PlexMediaComparisonHitState.HigherQuality),
                }
            );

        for (var i = 0; i < items.Count; i++)
        {
            var showId = items[i].Id;
            var hasShowHit = showHitLookup.TryGetValue(showId, out var showHitsForItem);

            if (!hasShowHit)
            {
                items[i].SetComparisonState(PlexMediaComparisonState.Missing);
                continue;
            }

            var showHigherQualityCount = showHitsForItem!.Count(x =>
                x.HitState == PlexMediaComparisonHitState.HigherQuality
            );

            var remoteEpisodeCount = items[i].GrandChildCount;
            episodeHitLookup.TryGetValue(showId, out var episodeHitSummary);
            var matchedEpisodeCount = episodeHitSummary?.MatchedCount ?? 0;
            var hasPartialMissingChildren = remoteEpisodeCount > 0 && matchedEpisodeCount < remoteEpisodeCount;
            var totalHigherQuality = showHigherQualityCount + (episodeHitSummary?.HigherQualityCount ?? 0);

            items[i]
                .SetComparisonState(
                    (hasPartialMissingChildren, totalHigherQuality > 0) switch
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
