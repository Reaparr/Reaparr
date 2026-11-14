namespace Reaparr.Application;

public class CompareMoviePlexLibraryCommandValidator : AbstractValidator<CompareMoviePlexLibraryCommand>
{
    public CompareMoviePlexLibraryCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.OwnedPlexLibraryId).GreaterThan(0);
        RuleFor(x => x.RemotePlexLibraryId).GreaterThan(0).NotEqual(x => x.OwnedPlexLibraryId);
    }
}

public class CompareMoviePlexLibraryCommandHandler : ICommandHandler<CompareMoviePlexLibraryCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ITelemetry _telemetry;

    public CompareMoviePlexLibraryCommandHandler(ILogger log, IReaparrDbContext dbContext, ITelemetry telemetry)
    {
        _log = log.ForContext<CompareMoviePlexLibraryCommandHandler>();
        _dbContext = dbContext;
        _telemetry = telemetry;
    }

    public async Task<Result> ExecuteAsync(CompareMoviePlexLibraryCommand command, CancellationToken cancellationToken)
    {
        using var activity = _telemetry.StartMovieComparison(
            command.OwnedPlexLibraryId,
            command.RemotePlexLibraryId
        );
        return await Result.Try(
            new Func<Task<Result>>(async () =>
            {
                var (ownedLibraryId, remoteLibraryId) = command;

                _log.Here()
                    .Information(
                        "Starting movie comparison: remote library {RemoteLibId} vs owned library {OwnedLibId}",
                        remoteLibraryId,
                        ownedLibraryId
                    );

                var validationResult = await ValidateLibrariesAsync(ownedLibraryId, remoteLibraryId, cancellationToken);

                if (validationResult.IsFailed)
                    return validationResult;

                return await CompareMoviesAsync(ownedLibraryId, remoteLibraryId, cancellationToken);
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

        if (remoteLibrary.Type != PlexMediaType.Movie || ownedLibrary.Type != PlexMediaType.Movie)
            return Result.Fail("Both libraries must be movie libraries");

        return Result.Ok();
    }

    private async Task<Result> CompareMoviesAsync(
        int ownedLibraryId,
        int remoteLibraryId,
        CancellationToken cancellationToken
    )
    {
        var now = DateTime.UtcNow;
        var librarySnapshots = await _dbContext
            .PlexLibraries.Where(x => x.Id == remoteLibraryId || x.Id == ownedLibraryId)
            .Select(x => new { x.Id, x.ContentChangedAt })
            .ToDictionaryAsync(x => x.Id, x => x.ContentChangedAt, cancellationToken);

        // Load remote movies with their quality
        var remoteMovies = await _dbContext
            .PlexMovies.Where(m => m.PlexLibraryId == remoteLibraryId)
            .Select(m => new MovieProjection
            {
                Id = m.Id,
                SearchTitle = m.SearchTitle,
                Year = m.Year,
                Duration = m.Duration,
                VideoQuality = m.VideoQuality,
                Guid_IMDB = m.Guid_IMDB,
                Guid_TMDB = m.Guid_TMDB,
                Guid_TVDB = m.Guid_TVDB,
            })
            .ToListAsync(cancellationToken);
        // Load owned movies with their quality
        var ownedMovies = await _dbContext
            .PlexMovies.Where(m => m.PlexLibraryId == ownedLibraryId)
            .Select(m => new MovieProjection
            {
                Id = m.Id,
                SearchTitle = m.SearchTitle,
                Year = m.Year,
                Duration = m.Duration,
                VideoQuality = m.VideoQuality,
                Guid_IMDB = m.Guid_IMDB,
                Guid_TMDB = m.Guid_TMDB,
                Guid_TVDB = m.Guid_TVDB,
            })
            .ToListAsync(cancellationToken);

        _log.Here()
            .Debug(
                "Loaded {RemoteCount} remote movies and {OwnedCount} owned movies for comparison",
                remoteMovies.Count,
                ownedMovies.Count
            );

        // Build and match the in-memory snapshots before taking SQLite's serialized write lock.
        var ownedByTmdb = ownedMovies
            .Where(x => x.Guid_TMDB.HasValue)
            .GroupBy(x => x.Guid_TMDB!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var ownedByImdb = ownedMovies
            .Where(x => !string.IsNullOrEmpty(x.Guid_IMDB))
            .GroupBy(x => x.Guid_IMDB!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var ownedByTvdb = ownedMovies
            .Where(x => x.Guid_TVDB.HasValue)
            .GroupBy(x => x.Guid_TVDB!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var ownedByTitleYear = ownedMovies
            .GroupBy(GetTitleYearKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var hitRows = new List<PlexMovieComparison>();

        foreach (var remote in remoteMovies)
        {
            var matches = MatchMovie(remote, ownedByTmdb, ownedByImdb, ownedByTvdb, ownedByTitleYear);

            foreach (var (owned, matchType) in matches)
            {
                var remoteQuality = remote.VideoQuality;
                var ownedQuality = owned.VideoQuality;

                hitRows.Add(
                    new PlexMovieComparison
                    {
                        Id = 0,
                        RemotePlexLibraryId = remoteLibraryId,
                        OwnedPlexLibraryId = ownedLibraryId,
                        RemotePlexMediaId = remote.Id,
                        OwnedPlexMediaId = owned.Id,
                        HitState = IsHigherQuality(remoteQuality, ownedQuality)
                            ? PlexMediaComparisonHitState.HigherQuality
                            : PlexMediaComparisonHitState.Matched,
                        RemoteQuality = remoteQuality,
                        OwnedQuality = ownedQuality,
                        MatchType = matchType,
                        ComparedAt = now,
                    }
                );
            }
        }

        _log.Here()
            .Debug(
                "Produced {HitCount} comparison hit rows for {RemoteCount} remote movies",
                hitRows.Count,
                remoteMovies.Count
            );

        var transactionResult = await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                var currentLibrarySnapshots = await ctx
                    .PlexLibraries.Where(x => x.Id == remoteLibraryId || x.Id == ownedLibraryId)
                    .Select(x => new { x.Id, x.ContentChangedAt })
                    .ToDictionaryAsync(x => x.Id, x => x.ContentChangedAt, txCt);

                var librariesChanged =
                    librarySnapshots.Count != currentLibrarySnapshots.Count
                    || librarySnapshots.Any(x =>
                        !currentLibrarySnapshots.TryGetValue(x.Key, out var updatedAt) || updatedAt != x.Value
                    );
                if (librariesChanged)
                    throw new InvalidOperationException(
                        "A Plex library changed while its comparison snapshot was being processed"
                    );

                var state = await ctx
                    .PlexComparisonScopes.AsTracking()
                    .SingleOrDefaultAsync(
                        x =>
                            x.RemotePlexLibraryId == remoteLibraryId
                            && x.OwnedPlexLibraryId == ownedLibraryId
                            && x.MediaType == PlexMediaType.Movie,
                        txCt
                    );

                if (state is null)
                {
                    state = new PlexComparisonState
                    {
                        Id = 0,
                        RemotePlexLibraryId = remoteLibraryId,
                        OwnedPlexLibraryId = ownedLibraryId,
                        MediaType = PlexMediaType.Movie,
                        CompletedAt = now,
                    };

                    await ctx.PlexComparisonScopes.AddAsync(state, txCt);
                }
                else
                {
                    state.CompletedAt = now;
                }

                await ctx
                    .PlexMovieComparisons.Where(x =>
                        x.RemotePlexLibraryId == remoteLibraryId && x.OwnedPlexLibraryId == ownedLibraryId
                    )
                    .ExecuteDeleteAsync(txCt);

                if (hitRows.Count > 0)
                    ctx.PlexMovieComparisons.AddRange(hitRows);

                await ctx.SaveChangesAsync(txCt);

                return hitRows.Count;
            },
            cancellationToken
        );
        if (transactionResult.IsFailed)
            return transactionResult.ToResult();

        _log.Here()
            .Information(
                "Completed comparison for movies: remote library {RemoteLibId} vs owned library {OwnedLibId}, {HitCount} hits",
                remoteLibraryId,
                ownedLibraryId,
                transactionResult.Value
            );

        return Result.Ok();
    }

    private static List<(MovieProjection Owned, PlexMediaComparisonMatchType MatchType)> MatchMovie(
        MovieProjection remote,
        Dictionary<int, List<MovieProjection>> ownedByTmdb,
        Dictionary<string, List<MovieProjection>> ownedByImdb,
        Dictionary<int, List<MovieProjection>> ownedByTvdb,
        Dictionary<string, List<MovieProjection>> ownedByTitleYear
    )
    {
        if (remote.Guid_TMDB.HasValue && ownedByTmdb.TryGetValue(remote.Guid_TMDB.Value, out var tmdbMatches))
            return tmdbMatches.Select(m => (m, PlexMediaComparisonMatchType.TmdbGuid)).ToList();

        if (!string.IsNullOrEmpty(remote.Guid_IMDB) && ownedByImdb.TryGetValue(remote.Guid_IMDB, out var imdbMatches))
            return imdbMatches.Select(m => (m, PlexMediaComparisonMatchType.ImdbGuid)).ToList();

        if (remote.Guid_TVDB.HasValue && ownedByTvdb.TryGetValue(remote.Guid_TVDB.Value, out var tvdbMatches))
            return tvdbMatches.Select(m => (m, PlexMediaComparisonMatchType.TvdbGuid)).ToList();

        if (!ownedByTitleYear.TryGetValue(GetTitleYearKey(remote), out var titleYearMatches))
            return [];

        if (remote.Duration > 0)
        {
            var durationMatches = titleYearMatches.Where(o => o.Duration > 0 && o.Duration == remote.Duration).ToList();

            if (durationMatches.Count > 0)
                return durationMatches
                    .Select(m => (m, PlexMediaComparisonMatchType.NormalizedTitleYearAndDuration))
                    .ToList();
        }

        return titleYearMatches.Select(m => (m, PlexMediaComparisonMatchType.NormalizedTitleAndYear)).ToList();
    }

    private static bool IsHigherQuality(VideoQuality remoteQuality, VideoQuality ownedQuality) =>
        remoteQuality.ToId() > ownedQuality.ToId();

    private static string GetTitleYearKey(MovieProjection movie) => $"{movie.SearchTitle}\u001F{movie.Year}";

    /// <summary>
    /// Projection used for matching — only the fields needed for comparison.
    /// </summary>
    private sealed record MovieProjection
    {
        public required int Id { get; init; }
        public required string SearchTitle { get; init; }
        public required int Year { get; init; }
        public required int Duration { get; init; }
        public required VideoQuality VideoQuality { get; init; }

        // ReSharper disable once InconsistentNaming
        public required string? Guid_IMDB { get; init; }

        // ReSharper disable once InconsistentNaming
        public required int? Guid_TMDB { get; init; }

        // ReSharper disable once InconsistentNaming
        public required int? Guid_TVDB { get; init; }
    }
}
