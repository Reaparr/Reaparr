namespace Reaparr.Application;

public record GetTvShowMediaComparisonDetailsCommand(int PlexMediaId) : ICommand<Result<PlexMediaComparisonDetailsDTO>>;

public class GetTvShowMediaComparisonDetailsCommandValidator : AbstractValidator<GetTvShowMediaComparisonDetailsCommand>
{
    public GetTvShowMediaComparisonDetailsCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.PlexMediaId).GreaterThan(0);
    }
}

public class GetTvShowMediaComparisonDetailsCommandHandler
    : ICommandHandler<GetTvShowMediaComparisonDetailsCommand, Result<PlexMediaComparisonDetailsDTO>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly IScheduler _scheduler;
    private int _rowId;

    public GetTvShowMediaComparisonDetailsCommandHandler(IReaparrDbContext dbContext, IScheduler scheduler)
    {
        _dbContext = dbContext;
        _scheduler = scheduler;
    }

    public async Task<Result<PlexMediaComparisonDetailsDTO>> ExecuteAsync(
        GetTvShowMediaComparisonDetailsCommand command,
        CancellationToken ct
    )
    {
        var tvShow = await _dbContext.PlexTvShows.SingleOrDefaultAsync(x => x.Id == command.PlexMediaId, ct);
        if (tvShow is null)
            return ResultExtensions.EntityNotFound(nameof(PlexTvShow), command.PlexMediaId).LogError();

        var isOwned = await _dbContext.PlexLibraries.WhereIsOwned().AnyAsync(x => x.Id == tvShow.PlexLibraryId, ct);
        var currentLibraryIds = isOwned
            ? await _dbContext.GetCurrentRemoteLibraryIds(tvShow.PlexLibraryId, PlexMediaType.TvShow, ct)
            : await _dbContext.GetCurrentOwnedLibraryIds(tvShow.PlexLibraryId, PlexMediaType.TvShow, ct);
        PlexMediaComparisonState state;
        List<ComparisonDetailsRow> rows;
        if (currentLibraryIds.Count == 0)
        {
            state = await _dbContext.GetUncomparedComparisonStateAsync(
                _scheduler,
                tvShow.PlexLibraryId,
                isOwned,
                PlexMediaType.TvShow,
                ct
            );
            rows = [];
        }
        else
            (state, rows) = isOwned
                ? await GetOwnedTvShowRowsAsync(tvShow, currentLibraryIds, ct)
                : await GetRemoteTvShowRowsAsync(tvShow, currentLibraryIds, ct);

        return Result.Ok(
            new PlexMediaComparisonDetailsDTO
            {
                PlexMediaId = tvShow.Id,
                Type = PlexMediaType.TvShow,
                State = state,
                Rows = PlexMediaComparisonDetailsMapper.ToDtoRows(rows),
            }
        );
    }

    private async Task<(PlexMediaComparisonState State, List<ComparisonDetailsRow> Rows)> GetRemoteTvShowRowsAsync(
        PlexTvShow tvShow,
        HashSet<int> currentOwnedLibraryIds,
        CancellationToken ct
    )
    {
        var remoteSeasonIds = _dbContext
            .PlexTvShowSeason.Where(x => x.PlexLibraryId == tvShow.PlexLibraryId && x.TvShowId == tvShow.Id)
            .Select(x => x.Id);
        var episodes = await _dbContext
            .PlexTvShowEpisodes.Include(x => x.MediaDataList)
            .Include(x => x.TvShowSeason)
            .Where(x => x.PlexLibraryId == tvShow.PlexLibraryId && remoteSeasonIds.Contains(x.TvShowSeasonId))
            .OrderBy(x => x.TvShowSeason == null ? 0 : x.TvShowSeason.SeasonNumber)
            .ThenBy(x => x.EpisodeNumber)
            .ToListAsync(ct);

        var episodeIds = episodes.Select(x => x.Id).ToHashSet();
        var hits = await _dbContext
            .PlexEpisodeComparisons.Where(x =>
                x.RemotePlexLibraryId == tvShow.PlexLibraryId
                && currentOwnedLibraryIds.Contains(x.OwnedPlexLibraryId)
                && episodeIds.Contains(x.RemotePlexMediaId)
            )
            .ToListAsync(ct);

        var ownedEpisodeIds = hits.Select(x => x.OwnedPlexMediaId).ToHashSet();
        var ownedEpisodes = await _dbContext
            .PlexTvShowEpisodes.Include(x => x.MediaDataList)
            .Where(x => ownedEpisodeIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        var remoteServerIds = await GetPlexServerIdsByLibraryIdAsync(
            hits.Select(x => x.RemotePlexLibraryId).Append(tvShow.PlexLibraryId),
            ct
        );

        var showHits = await _dbContext
            .PlexTvShowComparisons.Where(x =>
                x.RemotePlexLibraryId == tvShow.PlexLibraryId
                && x.RemotePlexMediaId == tvShow.Id
                && currentOwnedLibraryIds.Contains(x.OwnedPlexLibraryId)
            )
            .ToListAsync(ct);
        return BuildRemoteTvRows(episodes, hits, ownedEpisodes, remoteServerIds, showHits);
    }

    private async Task<(PlexMediaComparisonState State, List<ComparisonDetailsRow> Rows)> GetOwnedTvShowRowsAsync(
        PlexTvShow tvShow,
        HashSet<int> currentRemoteLibraryIds,
        CancellationToken ct
    )
    {
        var showHits = await _dbContext
            .PlexTvShowComparisons.Where(x =>
                currentRemoteLibraryIds.Contains(x.RemotePlexLibraryId)
                && x.OwnedPlexLibraryId == tvShow.PlexLibraryId
                && x.OwnedPlexMediaId == tvShow.Id
            )
            .ToListAsync(ct);

        var remoteShowIds = showHits.Select(x => x.RemotePlexMediaId).ToHashSet();
        if (remoteShowIds.Count == 0)
            return (PlexMediaComparisonState.Owned, []);

        var remoteSeasonIds = _dbContext
            .PlexTvShowSeason.Where(x =>
                currentRemoteLibraryIds.Contains(x.PlexLibraryId) && remoteShowIds.Contains(x.TvShowId)
            )
            .Select(x => x.Id);
        var remoteEpisodes = await _dbContext
            .PlexTvShowEpisodes.Include(x => x.MediaDataList)
            .Include(x => x.TvShowSeason)
            .Where(x => currentRemoteLibraryIds.Contains(x.PlexLibraryId) && remoteSeasonIds.Contains(x.TvShowSeasonId))
            .OrderBy(x => x.TvShowSeason == null ? 0 : x.TvShowSeason.SeasonNumber)
            .ThenBy(x => x.EpisodeNumber)
            .ToListAsync(ct);

        var remoteEpisodeIds = remoteEpisodes.Select(x => x.Id).ToHashSet();
        var hits = await _dbContext
            .PlexEpisodeComparisons.Where(x =>
                currentRemoteLibraryIds.Contains(x.RemotePlexLibraryId)
                && x.OwnedPlexLibraryId == tvShow.PlexLibraryId
                && remoteEpisodeIds.Contains(x.RemotePlexMediaId)
            )
            .ToListAsync(ct);

        var ownedEpisodeIds = hits.Select(x => x.OwnedPlexMediaId).ToHashSet();
        var ownedEpisodes = await _dbContext
            .PlexTvShowEpisodes.Include(x => x.MediaDataList)
            .Where(x => ownedEpisodeIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        var remoteServerIds = await GetPlexServerIdsByLibraryIdAsync(
            hits.Select(x => x.RemotePlexLibraryId).Concat(showHits.Select(x => x.RemotePlexLibraryId)),
            ct
        );

        return BuildRemoteTvRows(remoteEpisodes, hits, ownedEpisodes, remoteServerIds, showHits);
    }

    private (PlexMediaComparisonState State, List<ComparisonDetailsRow> Rows) BuildRemoteTvRows(
        List<PlexTvShowEpisode> episodes,
        List<PlexEpisodeComparison> hits,
        Dictionary<int, PlexTvShowEpisode> ownedEpisodes,
        Dictionary<int, int> remoteServerIds,
        List<PlexTvShowComparison> showHits
    )
    {
        var hitLookup = hits.GroupBy(x => x.RemotePlexMediaId).ToDictionary(x => x.Key, x => x.ToList());
        var childRows = new List<ComparisonDetailsRow>();

        foreach (var episode in episodes)
        {
            if (!hitLookup.TryGetValue(episode.Id, out var episodeHits))
            {
                childRows.Add(
                    PlexMediaComparisonDetailsMapper.ToComparisonDetailsRow(
                        rowId: ++_rowId,
                        parentRowId: null,
                        level: 1,
                        plexMediaId: episode.Id,
                        type: PlexMediaType.Episode,
                        title: episode.Title,
                        state: PlexMediaComparisonState.Missing,
                        remoteQuality: episode.VideoQuality != VideoQuality.Unknown
                            ? episode.VideoQuality
                            : episode
                                .MediaDataList.Select(x => x.VideoQuality)
                                .OrderByDescending(x => x.ToId())
                                .FirstOrDefault(),
                        ownedQuality: VideoQuality.None,
                        remoteLocation: episode
                            .MediaDataList.Select(x => x.GetFileName)
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                            ?? string.Empty,
                        ownedLocation: string.Empty,
                        remotePlexLibraryId: episode.PlexLibraryId,
                        remotePlexServerId: remoteServerIds.GetValueOrDefault(episode.PlexLibraryId)
                    )
                );
                continue;
            }

            foreach (var hit in episodeHits.Where(x => x.HitState == PlexMediaComparisonHitState.HigherQuality))
            {
                ownedEpisodes.TryGetValue(hit.OwnedPlexMediaId, out var ownedEpisode);
                childRows.Add(
                    PlexMediaComparisonDetailsMapper.ToComparisonDetailsRow(
                        rowId: ++_rowId,
                        parentRowId: null,
                        level: 1,
                        plexMediaId: episode.Id,
                        type: PlexMediaType.Episode,
                        title: episode.Title,
                        state: PlexMediaComparisonState.HigherQuality,
                        remoteQuality: hit.RemoteQuality != VideoQuality.Unknown
                            ? hit.RemoteQuality
                            : episode
                                .MediaDataList.Select(x => x.VideoQuality)
                                .OrderByDescending(x => x.ToId())
                                .FirstOrDefault(),
                        ownedQuality: ownedEpisode is null || hit.OwnedQuality != VideoQuality.Unknown
                            ? hit.OwnedQuality
                            : ownedEpisode
                                .MediaDataList.Select(x => x.VideoQuality)
                                .OrderByDescending(x => x.ToId())
                                .FirstOrDefault(),
                        remoteLocation: episode
                            .MediaDataList.Select(x => x.GetFileName)
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                            ?? string.Empty,
                        ownedLocation: ownedEpisode
                            ?.MediaDataList.Select(x => x.GetFileName)
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                            ?? string.Empty,
                        remotePlexLibraryId: hit.RemotePlexLibraryId,
                        remotePlexServerId: remoteServerIds.GetValueOrDefault(hit.RemotePlexLibraryId)
                    )
                );
            }
        }

        var hasMissing = childRows.Any(x => x.ComparisonId == PlexMediaComparisonState.Missing.ToComparisonId());
        var hasMatches = showHits.Count > 0 || hits.Count > 0;
        var hasHigherQuality =
            showHits.Any(x => x.HitState == PlexMediaComparisonHitState.HigherQuality)
            || childRows.Any(x => x.ComparisonId == PlexMediaComparisonState.HigherQuality.ToComparisonId());
        var state = (hasMissing, hasMatches, hasHigherQuality) switch
        {
            (true, true, true) => PlexMediaComparisonState.PartialAndHigherQuality,
            (true, true, false) => PlexMediaComparisonState.Partial,
            (_, false, _) => PlexMediaComparisonState.Missing,
            (false, _, true) => PlexMediaComparisonState.HigherQuality,
            _ => PlexMediaComparisonState.Owned,
        };
        return (state, AddSeasonParents(episodes, childRows));
    }

    private List<ComparisonDetailsRow> AddSeasonParents(
        List<PlexTvShowEpisode> episodes,
        List<ComparisonDetailsRow> childRows
    )
    {
        if (childRows.Count == 0)
            return [];

        var episodeLookup = episodes.ToDictionary(x => x.Id);
        var missingEpisodeIds = childRows
            .Where(x => x.ComparisonId == PlexMediaComparisonState.Missing.ToComparisonId())
            .Select(x => x.PlexMediaId)
            .ToHashSet();
        var result = new List<ComparisonDetailsRow>();
        foreach (
            var seasonGroup in childRows
                .GroupBy(x =>
                {
                    var episode = episodeLookup.GetValueOrDefault(x.PlexMediaId);
                    var seasonNumber = episode?.TvShowSeason?.SeasonNumber ?? -1;
                    return (
                        SeasonNumber: seasonNumber,
                        UnknownSeasonId: seasonNumber < 0 ? episode?.TvShowSeasonId ?? 0 : 0
                    );
                })
                .OrderBy(x => x.Key.SeasonNumber < 0)
                .ThenBy(x => x.Key.SeasonNumber)
                .ThenBy(x => x.Key.UnknownSeasonId)
        )
        {
            var rows = seasonGroup
                .GroupBy(x =>
                {
                    var episodeNumber = episodeLookup.GetValueOrDefault(x.PlexMediaId)?.EpisodeNumber ?? -1;
                    return (EpisodeNumber: episodeNumber, UnknownEpisodeId: episodeNumber < 0 ? x.PlexMediaId : 0);
                })
                .OrderBy(x => x.Key.EpisodeNumber < 0)
                .ThenBy(x => x.Key.EpisodeNumber)
                .ThenBy(x => x.First().Title)
                .ThenBy(x => x.Key.UnknownEpisodeId)
                .Select(x =>
                    x.OrderByDescending(y => y.RemoteQuality.ToId())
                        .ThenBy(y => y.RemotePlexLibraryId)
                        .ThenBy(y => y.RemotePlexServerId)
                        .ThenBy(y => y.PlexMediaId)
                        .First()
                )
                .ToList();
            var sourceRow = rows.FirstOrDefault(x => x.RemotePlexLibraryId > 0) ?? rows.First();
            var seasonRowId = ++_rowId;
            var seasonState = PlexMediaComparisonDetailsMapper.ToParentState(rows);
            if (
                seasonState == PlexMediaComparisonState.Missing
                && episodes.Any(episode =>
                    !missingEpisodeIds.Contains(episode.Id)
                    && (episode.TvShowSeason?.SeasonNumber ?? -1) == seasonGroup.Key.SeasonNumber
                    && (seasonGroup.Key.SeasonNumber >= 0 || episode.TvShowSeasonId == seasonGroup.Key.UnknownSeasonId)
                )
            )
                seasonState = PlexMediaComparisonState.Partial;
            result.Add(
                new ComparisonDetailsRow
                {
                    RowId = seasonRowId,
                    ParentRowId = null,
                    Level = 0,
                    PlexMediaId = episodeLookup.GetValueOrDefault(sourceRow.PlexMediaId)?.TvShowSeasonId ?? 0,
                    Type = PlexMediaType.Season,
                    Title =
                        seasonGroup.Key.SeasonNumber < 0 ? "Unknown Season" : $"Season {seasonGroup.Key.SeasonNumber}",
                    ComparisonId = seasonState.ToComparisonId(),
                    IsActionable = true,
                    RemoteQuality = PlexMediaComparisonDetailsMapper.GetHighestQuality(
                        rows.Select(x => x.RemoteQuality)
                    ),
                    OwnedQuality = PlexMediaComparisonDetailsMapper.GetHighestQuality(rows.Select(x => x.OwnedQuality)),
                    RemotePlexLibraryId = sourceRow.RemotePlexLibraryId,
                    RemotePlexServerId = sourceRow.RemotePlexServerId,
                }
            );

            result.AddRange(rows.Select(x => x with { ParentRowId = seasonRowId }));
        }

        return result;
    }

    private async Task<Dictionary<int, int>> GetPlexServerIdsByLibraryIdAsync(
        IEnumerable<int> plexLibraryIds,
        CancellationToken ct
    )
    {
        var ids = plexLibraryIds.ToHashSet();
        return await _dbContext
            .PlexLibraries.Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.PlexServerId, ct);
    }
}
