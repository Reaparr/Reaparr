namespace Reaparr.Application;

public class CompareTvShowPlexLibraryCommandValidator : AbstractValidator<CompareTvShowPlexLibraryCommand>
{
    public CompareTvShowPlexLibraryCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.OwnedPlexLibraryId).GreaterThan(0);
        RuleFor(x => x.RemotePlexLibraryId).GreaterThan(0).NotEqual(x => x.OwnedPlexLibraryId);
    }
}

public class CompareTvShowPlexLibraryCommandHandler : ICommandHandler<CompareTvShowPlexLibraryCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private const int BATCH_SIZE = 100;

    public CompareTvShowPlexLibraryCommandHandler(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<CompareTvShowPlexLibraryCommandHandler>();
        _dbContext = dbContext;
    }

    public async Task<Result> ExecuteAsync(CompareTvShowPlexLibraryCommand command, CancellationToken cancellationToken)
    {
        return await Result.Try(
            new Func<Task<Result>>(async () =>
            {
                var (ownedLibraryId, remoteLibraryId) = command;

                _log.Here()
                    .Verbose(
                        "Starting TV show comparison: remote library {RemoteLibId} vs owned library {OwnedLibId}",
                        remoteLibraryId,
                        ownedLibraryId
                    );

                var validationResult = await ValidateLibrariesAsync(ownedLibraryId, remoteLibraryId, cancellationToken);

                if (validationResult.IsFailed)
                    return validationResult;

                return await CompareTvShowsAsync(ownedLibraryId, remoteLibraryId, cancellationToken);
            })
        );
    }

    private async Task<Result> ValidateLibrariesAsync(
        int ownedLibraryId,
        int remoteLibraryId,
        CancellationToken cancellationToken
    )
    {
        var libraries = await _dbContext
            .PlexLibraries.Where(x => x.Id == remoteLibraryId || x.Id == ownedLibraryId)
            .SelectOwnership()
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (!libraries.TryGetValue(remoteLibraryId, out var remoteLibrary))
            return Result.Fail("Remote library {RemoteLibraryId} was not found", remoteLibraryId);

        if (!libraries.TryGetValue(ownedLibraryId, out var ownedLibrary))
            return Result.Fail("Owned library {OwnedLibraryId} was not found", ownedLibraryId);

        if (remoteLibrary.IsOwned)
            return Result.Fail(
                "Library {RemoteLibraryId} is owned and cannot be compared as a remote library",
                remoteLibraryId
            );

        if (!ownedLibrary.IsOwned)
            return Result.Fail(
                "Library {OwnedLibraryId} is not owned and cannot be compared as an owned library",
                ownedLibraryId
            );

        if (remoteLibrary.Type != PlexMediaType.TvShow || ownedLibrary.Type != PlexMediaType.TvShow)
            return Result.Fail("Both libraries must be TV show libraries");

        return Result.Ok();
    }

    private async Task<Result> CompareTvShowsAsync(
        int ownedLibraryId,
        int remoteLibraryId,
        CancellationToken cancellationToken
    )
    {
        var now = DateTime.UtcNow;
        var librarySnapshots = await LoadLibrarySnapshotsAsync(
            _dbContext,
            remoteLibraryId,
            ownedLibraryId,
            cancellationToken
        );

        var transactionResult = await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                await EnsureLibrarySnapshotsUnchangedAsync(
                    ctx,
                    remoteLibraryId,
                    ownedLibraryId,
                    librarySnapshots,
                    txCt
                );

                await ctx
                    .PlexTvShowComparisons.Where(x =>
                        x.RemotePlexLibraryId == remoteLibraryId && x.OwnedPlexLibraryId == ownedLibraryId
                    )
                    .ExecuteDeleteAsync(txCt);
                await ctx
                    .PlexSeasonComparisons.Where(x =>
                        x.RemotePlexLibraryId == remoteLibraryId && x.OwnedPlexLibraryId == ownedLibraryId
                    )
                    .ExecuteDeleteAsync(txCt);
                await ctx
                    .PlexEpisodeComparisons.Where(x =>
                        x.RemotePlexLibraryId == remoteLibraryId && x.OwnedPlexLibraryId == ownedLibraryId
                    )
                    .ExecuteDeleteAsync(txCt);

                var showCount = 0;
                var seasonCount = 0;
                var episodeCount = 0;
                var lastRemoteShowId = 0;

                while (true)
                {
                    txCt.ThrowIfCancellationRequested();
                    var remoteShows = await LoadShowBatchAsync(
                        ctx,
                        remoteLibraryId,
                        lastRemoteShowId,
                        BATCH_SIZE,
                        txCt
                    );
                    if (remoteShows.Count == 0)
                        break;

                    var ownedShowCandidates = await LoadOwnedShowCandidatesAsync(
                        ctx,
                        ownedLibraryId,
                        remoteShows,
                        txCt
                    );
                    var batch = await BuildComparisonBatchAsync(
                        ctx,
                        remoteLibraryId,
                        ownedLibraryId,
                        remoteShows,
                        ownedShowCandidates,
                        now,
                        txCt
                    );

                    if (batch.ShowRows.Count > 0)
                        ctx.PlexTvShowComparisons.AddRange(batch.ShowRows);
                    if (batch.SeasonRows.Count > 0)
                        ctx.PlexSeasonComparisons.AddRange(batch.SeasonRows);
                    if (batch.EpisodeRows.Count > 0)
                        ctx.PlexEpisodeComparisons.AddRange(batch.EpisodeRows);

                    if (batch.ShowRows.Count > 0 || batch.SeasonRows.Count > 0 || batch.EpisodeRows.Count > 0)
                        await ctx.SaveChangesAsync(txCt);

                    ctx.ClearChangeTracker();
                    showCount += batch.ShowRows.Count;
                    seasonCount += batch.SeasonRows.Count;
                    episodeCount += batch.EpisodeRows.Count;
                    lastRemoteShowId = remoteShows[^1].Id;
                }

                await EnsureLibrarySnapshotsUnchangedAsync(
                    ctx,
                    remoteLibraryId,
                    ownedLibraryId,
                    librarySnapshots,
                    txCt
                );

                var state = await ctx
                    .PlexComparisonScopes.AsTracking()
                    .SingleOrDefaultAsync(
                        x =>
                            x.RemotePlexLibraryId == remoteLibraryId
                            && x.OwnedPlexLibraryId == ownedLibraryId
                            && x.MediaType == PlexMediaType.TvShow,
                        txCt
                    );
                if (state is null)
                {
                    state = new PlexComparisonState
                    {
                        Id = 0,
                        RemotePlexLibraryId = remoteLibraryId,
                        OwnedPlexLibraryId = ownedLibraryId,
                        MediaType = PlexMediaType.TvShow,
                        CompletedAt = now,
                    };
                    await ctx.PlexComparisonScopes.AddAsync(state, txCt);
                }
                else
                {
                    state.CompletedAt = now;
                }

                await ctx.SaveChangesAsync(txCt);
                return (showCount, seasonCount, episodeCount);
            },
            cancellationToken
        );
        if (transactionResult.IsFailed)
            return transactionResult.ToResult();

        _log.Here()
            .Information(
                "Completed TV comparison: remote library {RemoteLibId} vs owned library {OwnedLibId}, {ShowCount} show hits, {SeasonCount} season hits, {EpisodeCount} episode hits",
                remoteLibraryId,
                ownedLibraryId,
                transactionResult.Value.Item1,
                transactionResult.Value.Item2,
                transactionResult.Value.Item3
            );

        return Result.Ok();
    }

    private static Task<Dictionary<int, long>> LoadLibrarySnapshotsAsync(
        IReaparrDbContext context,
        int remoteLibraryId,
        int ownedLibraryId,
        CancellationToken cancellationToken
    ) =>
        context
            .PlexLibraries.Where(x => x.Id == remoteLibraryId || x.Id == ownedLibraryId)
            .Select(x => new { x.Id, x.ContentChangedAt })
            .ToDictionaryAsync(x => x.Id, x => x.ContentChangedAt, cancellationToken);

    private static async Task EnsureLibrarySnapshotsUnchangedAsync(
        IReaparrDbContext context,
        int remoteLibraryId,
        int ownedLibraryId,
        Dictionary<int, long> expectedSnapshots,
        CancellationToken cancellationToken
    )
    {
        var currentSnapshots = await LoadLibrarySnapshotsAsync(
            context,
            remoteLibraryId,
            ownedLibraryId,
            cancellationToken
        );
        if (
            expectedSnapshots.Count != currentSnapshots.Count
            || expectedSnapshots.Any(x =>
                !currentSnapshots.TryGetValue(x.Key, out var currentSnapshot) || currentSnapshot != x.Value
            )
        )
        {
            throw new InvalidOperationException(
                "A Plex library changed while its comparison snapshot was being processed"
            );
        }
    }

    private static async Task<List<TvShowProjection>> LoadOwnedShowCandidatesAsync(
        IReaparrDbContext context,
        int libraryId,
        IReadOnlyCollection<TvShowProjection> remoteShows,
        CancellationToken cancellationToken
    )
    {
        if (remoteShows.Count == 0)
            return [];

        var tmdbGuids = remoteShows
            .Where(x => x.Guid_TMDB.HasValue)
            .Select(x => x.Guid_TMDB!.Value)
            .Distinct()
            .ToArray();
        var imdbGuids = remoteShows
            .Where(x => !string.IsNullOrEmpty(x.Guid_IMDB))
            .Select(x => x.Guid_IMDB!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var tvdbGuids = remoteShows
            .Where(x => x.Guid_TVDB.HasValue)
            .Select(x => x.Guid_TVDB!.Value)
            .Distinct()
            .ToArray();
        var years = remoteShows.Select(x => x.Year).Distinct().ToArray();
        var searchTitles = remoteShows
            .Select(x => x.SearchTitle)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return await context
            .PlexTvShows.Where(x =>
                x.PlexLibraryId == libraryId
                && (
                    (x.Guid_TMDB.HasValue && tmdbGuids.Contains(x.Guid_TMDB.Value))
                    || (
                        !string.IsNullOrEmpty(x.Guid_IMDB)
                        && imdbGuids.Contains(EF.Functions.Collate(x.Guid_IMDB!, "NOCASE"))
                    )
                    || (x.Guid_TVDB.HasValue && tvdbGuids.Contains(x.Guid_TVDB.Value))
                    || (years.Contains(x.Year) && searchTitles.Contains(x.SearchTitle))
                )
            )
            .OrderBy(x => x.Id)
            .Select(x => new TvShowProjection
            {
                Id = x.Id,
                SearchTitle = x.SearchTitle,
                Year = x.Year,
                Duration = x.Duration,
                Quality = x.Quality,
                Guid_IMDB = x.Guid_IMDB,
                Guid_TMDB = x.Guid_TMDB,
                Guid_TVDB = x.Guid_TVDB,
            })
            .ToListAsync(cancellationToken);
    }

    private static Task<List<TvShowProjection>> LoadShowBatchAsync(
        IReaparrDbContext context,
        int libraryId,
        int lastShowId,
        int batchSize,
        CancellationToken cancellationToken
    ) =>
        context
            .PlexTvShows.Where(x => x.PlexLibraryId == libraryId && x.Id > lastShowId)
            .OrderBy(x => x.Id)
            .Take(batchSize)
            .Select(x => new TvShowProjection
            {
                Id = x.Id,
                SearchTitle = x.SearchTitle,
                Year = x.Year,
                Duration = x.Duration,
                Quality = x.Quality,
                Guid_IMDB = x.Guid_IMDB,
                Guid_TMDB = x.Guid_TMDB,
                Guid_TVDB = x.Guid_TVDB,
            })
            .ToListAsync(cancellationToken);

    private static async Task<List<SeasonProjection>> LoadSeasonsAsync(
        IReaparrDbContext context,
        IReadOnlyCollection<int> showIds,
        CancellationToken cancellationToken
    )
    {
        if (showIds.Count == 0)
            return [];

        return await context
            .PlexTvShowSeason.Where(x => showIds.Contains(x.TvShowId))
            .Select(x => new SeasonProjection
            {
                Id = x.Id,
                TvShowId = x.TvShowId,
                SeasonNumber = x.SeasonNumber,
                Quality = x.Quality,
            })
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<EpisodeProjection>> LoadEpisodesAsync(
        IReaparrDbContext context,
        IReadOnlyCollection<int> seasonIds,
        CancellationToken cancellationToken
    )
    {
        if (seasonIds.Count == 0)
            return [];

        return await context
            .PlexTvShowEpisodes.Where(x => seasonIds.Contains(x.TvShowSeasonId))
            .Select(x => new EpisodeProjection
            {
                Id = x.Id,
                TvShowSeasonId = x.TvShowSeasonId,
                EpisodeNumber = x.EpisodeNumber,
                Quality = x.Quality,
            })
            .ToListAsync(cancellationToken);
    }

    private static async Task<ComparisonBatch> BuildComparisonBatchAsync(
        IReaparrDbContext context,
        int remoteLibraryId,
        int ownedLibraryId,
        IReadOnlyList<TvShowProjection> remoteShows,
        IReadOnlyList<TvShowProjection> ownedShowCandidates,
        DateTime comparedAt,
        CancellationToken cancellationToken
    )
    {
        var showRows = new List<PlexTvShowComparison>();
        var selectedShowMatches = new List<(TvShowProjection Remote, TvShowProjection Owned)>();

        foreach (var remoteShow in remoteShows)
        {
            var showMatch = MatchShow(remoteShow, ownedShowCandidates);
            if (showMatch is null)
                continue;

            var (ownedShow, showMatchType) = showMatch.Value;
            showRows.Add(
                new PlexTvShowComparison
                {
                    Id = 0,
                    RemotePlexLibraryId = remoteLibraryId,
                    OwnedPlexLibraryId = ownedLibraryId,
                    RemotePlexMediaId = remoteShow.Id,
                    OwnedPlexMediaId = ownedShow.Id,
                    HitState = IsHigherQuality(remoteShow.Quality, ownedShow.Quality)
                        ? PlexMediaComparisonHitState.HigherQuality
                        : PlexMediaComparisonHitState.Matched,
                    RemoteQuality = remoteShow.Quality,
                    OwnedQuality = ownedShow.Quality,
                    MatchType = showMatchType,
                    ComparedAt = comparedAt,
                }
            );
            selectedShowMatches.Add((remoteShow, ownedShow));
        }

        if (selectedShowMatches.Count == 0)
            return new ComparisonBatch(showRows, [], []);

        var remoteShowIds = selectedShowMatches.Select(x => x.Remote.Id).ToArray();
        var ownedShowIds = selectedShowMatches.Select(x => x.Owned.Id).Distinct().ToArray();
        var remoteSeasons = await LoadSeasonsAsync(context, remoteShowIds, cancellationToken);
        var ownedSeasons = await LoadSeasonsAsync(context, ownedShowIds, cancellationToken);
        var remoteSeasonsByShow = remoteSeasons.GroupBy(x => x.TvShowId).ToDictionary(g => g.Key, g => g.ToList());
        var ownedSeasonsByShowAndNumber = ownedSeasons
            .GroupBy(x => (x.TvShowId, x.SeasonNumber))
            .ToDictionary(g => g.Key, g => g.ToList());
        var remoteEpisodes = await LoadEpisodesAsync(
            context,
            remoteSeasons.Select(x => x.Id).ToArray(),
            cancellationToken
        );
        var ownedEpisodes = await LoadEpisodesAsync(
            context,
            ownedSeasons.Select(x => x.Id).ToArray(),
            cancellationToken
        );
        var remoteEpisodesBySeason = remoteEpisodes
            .GroupBy(x => x.TvShowSeasonId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var ownedEpisodesBySeasonAndNumber = ownedEpisodes
            .GroupBy(x => (x.TvShowSeasonId, x.EpisodeNumber))
            .ToDictionary(g => g.Key, g => g.ToList());
        var seasonRows = new List<PlexSeasonComparison>();
        var episodeRows = new List<PlexEpisodeComparison>();

        foreach (var (remoteShow, ownedShow) in selectedShowMatches)
        {
            if (!remoteSeasonsByShow.TryGetValue(remoteShow.Id, out var showSeasons))
                continue;

            foreach (var remoteSeason in showSeasons)
            {
                if (
                    !ownedSeasonsByShowAndNumber.TryGetValue(
                        (ownedShow.Id, remoteSeason.SeasonNumber),
                        out var seasonMatches
                    )
                )
                    continue;

                foreach (var ownedSeason in seasonMatches)
                {
                    seasonRows.Add(
                        new PlexSeasonComparison
                        {
                            Id = 0,
                            RemotePlexLibraryId = remoteLibraryId,
                            OwnedPlexLibraryId = ownedLibraryId,
                            RemotePlexMediaId = remoteSeason.Id,
                            OwnedPlexMediaId = ownedSeason.Id,
                            HitState = IsHigherQuality(remoteSeason.Quality, ownedSeason.Quality)
                                ? PlexMediaComparisonHitState.HigherQuality
                                : PlexMediaComparisonHitState.Matched,
                            RemoteQuality = remoteSeason.Quality,
                            OwnedQuality = ownedSeason.Quality,
                            MatchType = PlexMediaComparisonMatchType.ParentAndChildNumbers,
                            ComparedAt = comparedAt,
                        }
                    );

                    if (!remoteEpisodesBySeason.TryGetValue(remoteSeason.Id, out var seasonEpisodes))
                        continue;

                    foreach (var remoteEpisode in seasonEpisodes)
                    {
                        if (
                            !ownedEpisodesBySeasonAndNumber.TryGetValue(
                                (ownedSeason.Id, remoteEpisode.EpisodeNumber),
                                out var episodeMatches
                            )
                        )
                            continue;

                        episodeRows.AddRange(
                            episodeMatches.Select(ownedEpisode => new PlexEpisodeComparison
                            {
                                Id = 0,
                                RemotePlexLibraryId = remoteLibraryId,
                                OwnedPlexLibraryId = ownedLibraryId,
                                RemotePlexMediaId = remoteEpisode.Id,
                                OwnedPlexMediaId = ownedEpisode.Id,
                                HitState = IsHigherQuality(remoteEpisode.Quality, ownedEpisode.Quality)
                                    ? PlexMediaComparisonHitState.HigherQuality
                                    : PlexMediaComparisonHitState.Matched,
                                RemoteQuality = remoteEpisode.Quality,
                                OwnedQuality = ownedEpisode.Quality,
                                MatchType = PlexMediaComparisonMatchType.ParentAndChildNumbers,
                                ComparedAt = comparedAt,
                            })
                        );
                    }
                }
            }
        }

        return new ComparisonBatch(showRows, seasonRows, episodeRows);
    }

    private static (TvShowProjection Owned, PlexMediaComparisonMatchType MatchType)? MatchShow(
        TvShowProjection remote,
        IReadOnlyList<TvShowProjection> ownedShowCandidates
    )
    {
        if (remote.Guid_TMDB.HasValue)
        {
            var tmdbMatch = ownedShowCandidates
                .Where(x => x.Guid_TMDB == remote.Guid_TMDB)
                .OrderBy(x => x.Id)
                .FirstOrDefault();
            if (tmdbMatch is not null)
                return (tmdbMatch, PlexMediaComparisonMatchType.TmdbGuid);
        }

        if (!string.IsNullOrEmpty(remote.Guid_IMDB))
        {
            var imdbMatch = ownedShowCandidates
                .Where(x => string.Equals(x.Guid_IMDB, remote.Guid_IMDB, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Id)
                .FirstOrDefault();
            if (imdbMatch is not null)
                return (imdbMatch, PlexMediaComparisonMatchType.ImdbGuid);
        }

        if (remote.Guid_TVDB.HasValue)
        {
            var tvdbMatch = ownedShowCandidates
                .Where(x => x.Guid_TVDB == remote.Guid_TVDB)
                .OrderBy(x => x.Id)
                .FirstOrDefault();
            if (tvdbMatch is not null)
                return (tvdbMatch, PlexMediaComparisonMatchType.TvdbGuid);
        }

        var titleYearMatches = ownedShowCandidates
            .Where(x =>
                x.Year == remote.Year
                && string.Equals(x.SearchTitle, remote.SearchTitle, StringComparison.OrdinalIgnoreCase)
            )
            .OrderBy(x => x.Id)
            .ToList();
        if (titleYearMatches.Count == 0)
            return null;

        if (remote.Duration > 0)
        {
            var durationMatch = titleYearMatches.FirstOrDefault(x => x.Duration > 0 && x.Duration == remote.Duration);
            if (durationMatch is not null)
                return (durationMatch, PlexMediaComparisonMatchType.NormalizedTitleYearAndDuration);
        }

        return (titleYearMatches[0], PlexMediaComparisonMatchType.NormalizedTitleAndYear);
    }

    private static bool IsHigherQuality(VideoQuality remoteQuality, VideoQuality ownedQuality) =>
        remoteQuality.ToId() > ownedQuality.ToId();

    private sealed record ComparisonBatch(
        List<PlexTvShowComparison> ShowRows,
        List<PlexSeasonComparison> SeasonRows,
        List<PlexEpisodeComparison> EpisodeRows
    );

    private sealed record TvShowProjection
    {
        public required int Id { get; init; }

        public required string SearchTitle { get; init; }

        public required int Year { get; init; }

        public required int Duration { get; init; }

        public required VideoQuality Quality { get; init; }

        // ReSharper disable once InconsistentNaming
        public required string? Guid_IMDB { get; init; }

        // ReSharper disable once InconsistentNaming
        public required int? Guid_TMDB { get; init; }

        // ReSharper disable once InconsistentNaming
        public required int? Guid_TVDB { get; init; }
    }

    private sealed record SeasonProjection
    {
        public required int Id { get; init; }
        public required int TvShowId { get; init; }
        public required int SeasonNumber { get; init; }
        public required VideoQuality Quality { get; init; }
    }

    private sealed record EpisodeProjection
    {
        public required int Id { get; init; }
        public required int TvShowSeasonId { get; init; }
        public required int EpisodeNumber { get; init; }
        public required VideoQuality Quality { get; init; }
    }
}
