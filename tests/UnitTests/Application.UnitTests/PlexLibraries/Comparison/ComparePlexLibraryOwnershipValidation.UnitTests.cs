namespace Reaparr.Application.UnitTests;

public class CompareMoviePlexLibraryCommandOwnershipUnitTests : BaseCommandUnitTest<CompareMoviePlexLibraryCommand>
{
    [Test]
    public async Task ShouldTreatOwnedOverrideFalseAsRemote_WhenAccountLibraryStillOwned()
    {
        // Arrange
        await SetupDatabase(
            32,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareMoviePlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var scope = await IDbContext.PlexComparisonScopes.SingleAsync(
            x =>
                x.RemotePlexLibraryId == remoteLibrary.Id
                && x.OwnedPlexLibraryId == ownedLibrary.Id
                && x.MediaType == PlexMediaType.Movie,
            CancellationToken
        );
        scope.RemotePlexLibraryId.ShouldBe(remoteLibrary.Id);
        scope.OwnedPlexLibraryId.ShouldBe(ownedLibrary.Id);
    }

    [Test]
    public async Task ShouldCreateHitRowsForEveryOwnedMovieWithSameTmdbGuid_WhenMultipleCandidatesExist()
    {
        // Arrange
        await SetupDatabase(
            36,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.MovieCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var remoteMovies = await GetLibraryMoviesAsync(remoteLibrary.Id);
        var remoteMovie = remoteMovies[0];
        var ownedMovies = await GetLibraryMoviesAsync(ownedLibrary.Id);
        await UpdateMovieMatchFieldsAsync(
            remoteMovies[1].Id,
            "remote no match",
            1984,
            99,
            VideoQuality.FullHD,
            tmdbGuid: 9999
        );
        await UpdateMovieMatchFieldsAsync(
            remoteMovie.Id,
            "tmdb remote",
            2026,
            120,
            VideoQuality.UHD_4K,
            tmdbGuid: 4242
        );
        await UpdateMovieMatchFieldsAsync(
            ownedMovies[0].Id,
            "tmdb owned one",
            2020,
            90,
            VideoQuality.HD,
            tmdbGuid: 4242
        );
        await UpdateMovieMatchFieldsAsync(
            ownedMovies[1].Id,
            "tmdb owned two",
            2021,
            100,
            VideoQuality.FullHD,
            tmdbGuid: 4242
        );

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareMoviePlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var hits = await IDbContext
            .PlexMovieComparisons.Where(x =>
                x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id
            )
            .OrderBy(x => x.OwnedPlexMediaId)
            .ToListAsync(CancellationToken);
        hits.Count.ShouldBe(2);
        hits.Select(x => x.OwnedPlexMediaId).ToHashSet().SetEquals(ownedMovies.Select(x => x.Id)).ShouldBeTrue();
        hits.ShouldAllBe(x => x.RemotePlexMediaId == remoteMovie.Id);
        hits.ShouldAllBe(x => x.MatchType == PlexMediaComparisonMatchType.TmdbGuid);
        hits.ShouldAllBe(x => x.HitState == PlexMediaComparisonHitState.HigherQuality);
    }

    [Test]
    public async Task ShouldPreferTmdbGuidOverImdbAndTitleFallback_WhenMultipleMatchLayersExist()
    {
        // Arrange
        await SetupDatabase(
            37,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.MovieCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var remoteMovie = (await GetLibraryMoviesAsync(remoteLibrary.Id))[0];
        var ownedMovies = await GetLibraryMoviesAsync(ownedLibrary.Id);
        await UpdateMovieMatchFieldsAsync(
            remoteMovie.Id,
            "same title",
            2001,
            120,
            VideoQuality.UHD_4K,
            tmdbGuid: 111,
            imdbGuid: "tt-priority"
        );
        await UpdateMovieMatchFieldsAsync(
            ownedMovies[0].Id,
            "different title",
            1999,
            80,
            VideoQuality.HD,
            tmdbGuid: 111
        );
        await UpdateMovieMatchFieldsAsync(
            ownedMovies[1].Id,
            "same title",
            2001,
            120,
            VideoQuality.HD,
            tmdbGuid: 222,
            imdbGuid: "tt-priority"
        );

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareMoviePlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var hit = await IDbContext.PlexMovieComparisons.SingleAsync(
            x => x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id,
            CancellationToken
        );
        hit.RemotePlexMediaId.ShouldBe(remoteMovie.Id);
        hit.OwnedPlexMediaId.ShouldBe(ownedMovies[0].Id);
        hit.MatchType.ShouldBe(PlexMediaComparisonMatchType.TmdbGuid);
    }

    [Test]
    public async Task ShouldPreferTitleYearDurationOverTitleYear_WhenDurationMatchExists()
    {
        // Arrange
        await SetupDatabase(
            38,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.MovieCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var remoteMovie = (await GetLibraryMoviesAsync(remoteLibrary.Id))[0];
        var ownedMovies = await GetLibraryMoviesAsync(ownedLibrary.Id);
        await UpdateMovieMatchFieldsAsync(remoteMovie.Id, "duration title", 1995, 123, VideoQuality.FullHD);
        await UpdateMovieMatchFieldsAsync(ownedMovies[0].Id, "duration title", 1995, 123, VideoQuality.FullHD);
        await UpdateMovieMatchFieldsAsync(ownedMovies[1].Id, "duration title", 1995, 456, VideoQuality.FullHD);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareMoviePlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var hit = await IDbContext.PlexMovieComparisons.SingleAsync(
            x => x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id,
            CancellationToken
        );
        hit.OwnedPlexMediaId.ShouldBe(ownedMovies[0].Id);
        hit.MatchType.ShouldBe(PlexMediaComparisonMatchType.NormalizedTitleYearAndDuration);
        hit.HitState.ShouldBe(PlexMediaComparisonHitState.Matched);
    }

    [Test]
    public async Task ShouldFallbackToTitleYear_WhenRemoteDurationIsZero()
    {
        // Arrange
        await SetupDatabase(
            39,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.MovieCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var remoteMovie = (await GetLibraryMoviesAsync(remoteLibrary.Id))[0];
        var ownedMovie = (await GetLibraryMoviesAsync(ownedLibrary.Id))[0];
        await UpdateMovieMatchFieldsAsync(remoteMovie.Id, "fallback title", 2003, 0, VideoQuality.FullHD);
        await UpdateMovieMatchFieldsAsync(ownedMovie.Id, "fallback title", 2003, 321, VideoQuality.FullHD);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareMoviePlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var hit = await IDbContext.PlexMovieComparisons.SingleAsync(
            x => x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id,
            CancellationToken
        );
        hit.RemotePlexMediaId.ShouldBe(remoteMovie.Id);
        hit.OwnedPlexMediaId.ShouldBe(ownedMovie.Id);
        hit.MatchType.ShouldBe(PlexMediaComparisonMatchType.NormalizedTitleAndYear);
    }

    [Test]
    public async Task ShouldCreateHitRowsForEveryOwnedMovieWithSameTitleYearDuration_WhenMultipleCandidatesExist()
    {
        // Arrange
        await SetupDatabase(
            88,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.MovieCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var remoteMovies = await GetLibraryMoviesAsync(remoteLibrary.Id);
        var remoteMovie = remoteMovies[0];
        var ownedMovies = await GetLibraryMoviesAsync(ownedLibrary.Id);
        await UpdateMovieMatchFieldsAsync(
            remoteMovies[1].Id,
            "remote no match",
            1984,
            99,
            VideoQuality.FullHD,
            tmdbGuid: 9999
        );
        await UpdateMovieMatchFieldsAsync(remoteMovie.Id, "same movie duration", 2026, 120, VideoQuality.UHD_4K);
        await UpdateMovieMatchFieldsAsync(ownedMovies[0].Id, "same movie duration", 2026, 120, VideoQuality.HD);
        await UpdateMovieMatchFieldsAsync(ownedMovies[1].Id, "same movie duration", 2026, 120, VideoQuality.FullHD);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareMoviePlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var hits = await IDbContext
            .PlexMovieComparisons.Where(x =>
                x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id
            )
            .OrderBy(x => x.OwnedPlexMediaId)
            .ToListAsync(CancellationToken);
        hits.Count.ShouldBe(2);
        hits.Select(x => x.OwnedPlexMediaId).ToHashSet().SetEquals(ownedMovies.Select(x => x.Id)).ShouldBeTrue();
        hits.ShouldAllBe(x => x.RemotePlexMediaId == remoteMovie.Id);
        hits.ShouldAllBe(x => x.MatchType == PlexMediaComparisonMatchType.NormalizedTitleYearAndDuration);
        hits.ShouldAllBe(x => x.HitState == PlexMediaComparisonHitState.HigherQuality);
    }

    [Test]
    public async Task ShouldDeleteOldMovieHits_WhenRerunFindsNoMatches()
    {
        // Arrange
        await SetupDatabase(
            40,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.MovieCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var remoteMovie = (await GetLibraryMoviesAsync(remoteLibrary.Id))[0];
        var ownedMovie = (await GetLibraryMoviesAsync(ownedLibrary.Id))[0];
        dbContext.PlexMovieComparisons.Add(
            CreateMovieComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteMovie.Id,
                ownedMovie.Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await UpdateMovieMatchFieldsAsync(remoteMovie.Id, "remote only", 2001, 120, VideoQuality.FullHD);
        await UpdateMovieMatchFieldsAsync(ownedMovie.Id, "owned only", 2002, 121, VideoQuality.FullHD);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareMoviePlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var hitCount = await IDbContext.PlexMovieComparisons.CountAsync(
            x => x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id,
            CancellationToken
        );
        hitCount.ShouldBe(0);
        var scope = await IDbContext.PlexComparisonScopes.SingleAsync(
            x =>
                x.RemotePlexLibraryId == remoteLibrary.Id
                && x.OwnedPlexLibraryId == ownedLibrary.Id
                && x.MediaType == PlexMediaType.Movie,
            CancellationToken
        );
        scope.CompletedAt.ShouldBeGreaterThan(default(DateTime));
    }

    [Test]
    public async Task ShouldUpdateExistingComparisonScopeSnapshots_WhenScopeAlreadyExists()
    {
        // Arrange
        await SetupDatabase(
            33,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        const long remoteContentChangedAt = 1721582655L;
        const long ownedContentChangedAt = 1721570913L;
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryContentChangedAtAsync(remoteLibrary.Id, remoteContentChangedAt);
        await SetLibraryContentChangedAtAsync(ownedLibrary.Id, ownedContentChangedAt);
        dbContext.PlexComparisonScopes.Add(
            new PlexComparisonState
            {
                Id = 0,
                RemotePlexLibraryId = remoteLibrary.Id,
                OwnedPlexLibraryId = ownedLibrary.Id,
                MediaType = PlexMediaType.Movie,
                CompletedAt = new DateTime(2026, 7, 20, 22, 19, 16, DateTimeKind.Utc),
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareMoviePlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        await IDbContext.PlexComparisonScopes.SingleAsync(
            x =>
                x.RemotePlexLibraryId == remoteLibrary.Id
                && x.OwnedPlexLibraryId == ownedLibrary.Id
                && x.MediaType == PlexMediaType.Movie,
            CancellationToken
        );
    }

    private async Task<List<PlexMovie>> GetLibraryMoviesAsync(int plexLibraryId) =>
        await IDbContext
            .PlexMovies.Where(x => x.PlexLibraryId == plexLibraryId)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

    private async Task UpdateMovieMatchFieldsAsync(
        int plexMovieId,
        string searchTitle,
        int year,
        int duration,
        VideoQuality quality,
        int? tmdbGuid = null,
        string? imdbGuid = null,
        int? tvdbGuid = null
    )
    {
        await IDbContext
            .PlexMovies.Where(x => x.Id == plexMovieId)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.SearchTitle, searchTitle)
                        .SetProperty(y => y.Year, year)
                        .SetProperty(y => y.Duration, duration)
                        .SetProperty(y => y.Quality, quality)
                        .SetProperty(y => y.Guid_TMDB, tmdbGuid)
                        .SetProperty(y => y.Guid_IMDB, imdbGuid)
                        .SetProperty(y => y.Guid_TVDB, tvdbGuid),
                CancellationToken
            );
    }
}

public class CompareTvShowPlexLibraryCommandOwnershipUnitTests : BaseCommandUnitTest<CompareTvShowPlexLibraryCommand>
{
    [Test]
    public async Task ShouldTreatOwnedOverrideFalseAsRemote_WhenAccountLibraryStillOwned()
    {
        // Arrange
        await SetupDatabase(
            35,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var scope = await IDbContext.PlexComparisonScopes.SingleAsync(
            x =>
                x.RemotePlexLibraryId == remoteLibrary.Id
                && x.OwnedPlexLibraryId == ownedLibrary.Id
                && x.MediaType == PlexMediaType.TvShow,
            CancellationToken
        );
        scope.RemotePlexLibraryId.ShouldBe(remoteLibrary.Id);
        scope.OwnedPlexLibraryId.ShouldBe(ownedLibrary.Id);
    }

    [Test]
    public async Task ShouldCreateOneHitRowForTheFirstOwnedTvShow_WhenMultipleTmdbCandidatesExist()
    {
        // Arrange
        await SetupDatabase(
            86,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var remoteShows = await GetLibraryTvShowsAsync(remoteLibrary.Id);
        var remoteShow = remoteShows[0];
        var ownedShows = await GetLibraryTvShowsAsync(ownedLibrary.Id);
        var remoteSeason = (await GetLibrarySeasonsAsync(remoteLibrary.Id)).Single(x => x.TvShowId == remoteShow.Id);
        var ownedSeasons = await GetLibrarySeasonsAsync(ownedLibrary.Id);
        var remoteEpisode = (await GetLibraryEpisodesAsync(remoteLibrary.Id)).Single(x => x.TvShowId == remoteShow.Id);
        var ownedEpisodes = await GetLibraryEpisodesAsync(ownedLibrary.Id);
        await UpdateTvShowMatchFieldsAsync(
            remoteShows[1].Id,
            "remote no match",
            1984,
            99,
            VideoQuality.FullHD,
            tmdbGuid: 9999
        );
        await UpdateTvShowMatchFieldsAsync(
            remoteShow.Id,
            "tmdb remote show",
            2026,
            120,
            VideoQuality.UHD_4K,
            tmdbGuid: 5151
        );
        await UpdateTvShowMatchFieldsAsync(
            ownedShows[0].Id,
            "tmdb owned show one",
            2020,
            90,
            VideoQuality.HD,
            tmdbGuid: 5151
        );
        await UpdateTvShowMatchFieldsAsync(
            ownedShows[1].Id,
            "tmdb owned show two",
            2021,
            100,
            VideoQuality.FullHD,
            tmdbGuid: 5151
        );

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var hits = await IDbContext
            .PlexTvShowComparisons.Where(x =>
                x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id
            )
            .OrderBy(x => x.OwnedPlexMediaId)
            .ToListAsync(CancellationToken);
        hits.Count.ShouldBe(1);
        hits[0].OwnedPlexMediaId.ShouldBe(ownedShows[0].Id);
        hits.ShouldAllBe(x => x.RemotePlexMediaId == remoteShow.Id);
        hits.ShouldAllBe(x => x.MatchType == PlexMediaComparisonMatchType.TmdbGuid);
        hits.ShouldAllBe(x => x.HitState == PlexMediaComparisonHitState.HigherQuality);
        var seasonHits = await IDbContext
            .PlexSeasonComparisons.Where(x =>
                x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id
            )
            .ToListAsync(CancellationToken);
        var episodeHits = await IDbContext
            .PlexEpisodeComparisons.Where(x =>
                x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id
            )
            .ToListAsync(CancellationToken);
        var ownedSeason = ownedSeasons.Single(x => x.TvShowId == ownedShows[0].Id);
        seasonHits.Count.ShouldBe(1);
        seasonHits.ShouldAllBe(x =>
            x.RemotePlexMediaId == remoteSeason.Id
            && x.OwnedPlexMediaId == ownedSeason.Id
            && x.MatchType == PlexMediaComparisonMatchType.ParentAndChildNumbers
            && x.HitState == PlexMediaComparisonHitState.Matched
        );
        var ownedEpisode = ownedEpisodes.Single(x => x.TvShowId == ownedShows[0].Id);
        episodeHits.Count.ShouldBe(1);
        episodeHits.ShouldAllBe(x =>
            x.RemotePlexMediaId == remoteEpisode.Id
            && x.OwnedPlexMediaId == ownedEpisode.Id
            && x.MatchType == PlexMediaComparisonMatchType.ParentAndChildNumbers
            && x.HitState == PlexMediaComparisonHitState.Matched
        );
    }

    [Test]
    [Arguments(PlexMediaComparisonMatchType.TmdbGuid)]
    [Arguments(PlexMediaComparisonMatchType.ImdbGuid)]
    [Arguments(PlexMediaComparisonMatchType.TvdbGuid)]
    [Arguments(PlexMediaComparisonMatchType.NormalizedTitleYearAndDuration)]
    public async Task ShouldPreferHigherPriorityMatch_WhenLowerPriorityCandidateHasSmallerId(
        PlexMediaComparisonMatchType expectedMatchType
    )
    {
        // Arrange
        await SetupDatabase(
            41,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var remoteShows = await GetLibraryTvShowsAsync(remoteLibrary.Id);
        var ownedShows = await GetLibraryTvShowsAsync(ownedLibrary.Id);
        await UpdateTvShowMatchFieldsAsync(
            remoteShows[0].Id, "priority show", 2026, 120, VideoQuality.FullHD,
            tmdbGuid: 111, imdbGuid: "tt-tv-priority", tvdbGuid: 333
        );
        await UpdateTvShowMatchFieldsAsync(
            remoteShows[1].Id, "remote no match", 1984, 99, VideoQuality.FullHD
        );
        await UpdateTvShowMatchFieldsAsync(
            ownedShows[0].Id, "priority show", 2026, 60, VideoQuality.UHD_4K,
            imdbGuid: expectedMatchType == PlexMediaComparisonMatchType.TmdbGuid ? "tt-tv-priority" : null,
            tvdbGuid: expectedMatchType == PlexMediaComparisonMatchType.ImdbGuid ? 333 : null
        );
        await UpdateTvShowMatchFieldsAsync(
            ownedShows[1].Id,
            expectedMatchType == PlexMediaComparisonMatchType.NormalizedTitleYearAndDuration
                ? "PRIORITY SHOW" : "different show",
            expectedMatchType == PlexMediaComparisonMatchType.NormalizedTitleYearAndDuration ? 2026 : 1999,
            120,
            VideoQuality.HD,
            tmdbGuid: expectedMatchType == PlexMediaComparisonMatchType.TmdbGuid ? 111 : null,
            imdbGuid: expectedMatchType == PlexMediaComparisonMatchType.ImdbGuid ? "TT-TV-PRIORITY" : null,
            tvdbGuid: expectedMatchType == PlexMediaComparisonMatchType.TvdbGuid ? 333 : null
        );
        var remoteSeason = (await GetLibrarySeasonsAsync(remoteLibrary.Id))
            .Single(x => x.TvShowId == remoteShows[0].Id);
        var ownedSeason = (await GetLibrarySeasonsAsync(ownedLibrary.Id))
            .Single(x => x.TvShowId == ownedShows[1].Id);
        var remoteEpisode = (await GetLibraryEpisodesAsync(remoteLibrary.Id))
            .Single(x => x.TvShowSeasonId == remoteSeason.Id);
        var ownedEpisode = (await GetLibraryEpisodesAsync(ownedLibrary.Id))
            .Single(x => x.TvShowSeasonId == ownedSeason.Id);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var hit = await dbContext.PlexTvShowComparisons.SingleAsync(CancellationToken);
        hit.RemotePlexMediaId.ShouldBe(remoteShows[0].Id);
        hit.OwnedPlexMediaId.ShouldBe(ownedShows[1].Id);
        hit.RemotePlexLibraryId.ShouldBe(remoteLibrary.Id);
        hit.OwnedPlexLibraryId.ShouldBe(ownedLibrary.Id);
        hit.MatchType.ShouldBe(expectedMatchType);
        hit.HitState.ShouldBe(PlexMediaComparisonHitState.HigherQuality);
        hit.RemoteQuality.ShouldBe(VideoQuality.FullHD);
        hit.OwnedQuality.ShouldBe(VideoQuality.HD);
        var seasonHit = await dbContext.PlexSeasonComparisons.SingleAsync(CancellationToken);
        seasonHit.RemotePlexMediaId.ShouldBe(remoteSeason.Id);
        seasonHit.OwnedPlexMediaId.ShouldBe(ownedSeason.Id);
        var episodeHit = await dbContext.PlexEpisodeComparisons.SingleAsync(CancellationToken);
        episodeHit.RemotePlexMediaId.ShouldBe(remoteEpisode.Id);
        episodeHit.OwnedPlexMediaId.ShouldBe(ownedEpisode.Id);
    }

    [Test]
    public async Task ShouldCreateSeasonAndEpisodeRows_WhenShowSeasonAndEpisodeNumbersMatch()
    {
        // Arrange
        await SetupDatabase(
            42,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var remoteShow = (await GetLibraryTvShowsAsync(remoteLibrary.Id))[0];
        var ownedShow = (await GetLibraryTvShowsAsync(ownedLibrary.Id))[0];
        await UpdateTvShowMatchFieldsAsync(
            remoteShow.Id,
            "numbered show",
            2011,
            500,
            VideoQuality.UHD_4K,
            tmdbGuid: 2222
        );
        await UpdateTvShowMatchFieldsAsync(ownedShow.Id, "numbered show", 2011, 500, VideoQuality.HD, tmdbGuid: 2222);

        var remoteSeasons = await GetLibrarySeasonsAsync(remoteLibrary.Id);
        var ownedSeasons = await GetLibrarySeasonsAsync(ownedLibrary.Id);
        var remoteEpisodes = await GetLibraryEpisodesAsync(remoteLibrary.Id);
        var ownedEpisodes = await GetLibraryEpisodesAsync(ownedLibrary.Id);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var showHit = await dbContext.PlexTvShowComparisons.SingleAsync(CancellationToken);
        showHit.RemotePlexMediaId.ShouldBe(remoteShow.Id);
        showHit.OwnedPlexMediaId.ShouldBe(ownedShow.Id);
        showHit.HitState.ShouldBe(PlexMediaComparisonHitState.HigherQuality);
        var seasonHits = await dbContext.PlexSeasonComparisons
            .OrderBy(x => x.RemotePlexMediaId)
            .ToListAsync(CancellationToken);
        seasonHits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId)).ShouldBe(
            remoteSeasons.Select(x => (x.Id, ownedSeasons.Single(y => y.SeasonNumber == x.SeasonNumber).Id))
        );
        seasonHits.ShouldAllBe(x => x.MatchType == PlexMediaComparisonMatchType.ParentAndChildNumbers);
        var episodeHits = await dbContext.PlexEpisodeComparisons
            .OrderBy(x => x.RemotePlexMediaId)
            .ToListAsync(CancellationToken);
        episodeHits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId)).ShouldBe(
            remoteEpisodes.Select(x => (
                x.Id,
                ownedEpisodes.Single(y =>
                    y.EpisodeNumber == x.EpisodeNumber
                    && y.TvShowSeasonId == ownedSeasons.Single(s =>
                        s.SeasonNumber == remoteSeasons.Single(r => r.Id == x.TvShowSeasonId).SeasonNumber
                    ).Id
                ).Id
            ))
        );
        episodeHits.ShouldAllBe(x => x.MatchType == PlexMediaComparisonMatchType.ParentAndChildNumbers);
    }

    [Test]
    public async Task ShouldCreateOneHitRowForTheFirstOwnedTvShow_WhenMultipleCandidatesExist()
    {
        // Arrange
        await SetupDatabase(
            87,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var remoteShows = await GetLibraryTvShowsAsync(remoteLibrary.Id);
        var remoteShow = remoteShows[0];
        var ownedShows = await GetLibraryTvShowsAsync(ownedLibrary.Id);
        await UpdateTvShowMatchFieldsAsync(
            remoteShows[1].Id,
            "remote no match",
            1984,
            99,
            VideoQuality.FullHD,
            tmdbGuid: 9999
        );
        await UpdateTvShowMatchFieldsAsync(remoteShow.Id, "same title duration", 2026, 120, VideoQuality.UHD_4K);
        await UpdateTvShowMatchFieldsAsync(ownedShows[0].Id, "same title duration", 2026, 120, VideoQuality.HD);
        await UpdateTvShowMatchFieldsAsync(ownedShows[1].Id, "same title duration", 2026, 120, VideoQuality.FullHD);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var hits = await IDbContext
            .PlexTvShowComparisons.Where(x =>
                x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id
            )
            .OrderBy(x => x.OwnedPlexMediaId)
            .ToListAsync(CancellationToken);
        hits.Count.ShouldBe(1);
        hits[0].OwnedPlexMediaId.ShouldBe(ownedShows[0].Id);
        hits.ShouldAllBe(x => x.RemotePlexMediaId == remoteShow.Id);
        hits.ShouldAllBe(x => x.MatchType == PlexMediaComparisonMatchType.NormalizedTitleYearAndDuration);
        hits.ShouldAllBe(x => x.HitState == PlexMediaComparisonHitState.HigherQuality);
    }

    [Test]
    public async Task ShouldNotCreateSeasonOrEpisodeRows_WhenShowMatchesButSeasonNumbersDoNot()
    {
        // Arrange
        await SetupDatabase(
            43,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var remoteShow = (await GetLibraryTvShowsAsync(remoteLibrary.Id))[0];
        var ownedShow = (await GetLibraryTvShowsAsync(ownedLibrary.Id))[0];
        var ownedSeason = (await GetLibrarySeasonsAsync(ownedLibrary.Id))[0];
        await UpdateTvShowMatchFieldsAsync(
            remoteShow.Id,
            "season mismatch",
            2012,
            500,
            VideoQuality.FullHD,
            tmdbGuid: 3333
        );
        await UpdateTvShowMatchFieldsAsync(
            ownedShow.Id,
            "season mismatch",
            2012,
            500,
            VideoQuality.FullHD,
            tmdbGuid: 3333
        );
        await UpdateSeasonNumberAsync(ownedSeason.Id, 99);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var showHit = await dbContext.PlexTvShowComparisons.SingleAsync(CancellationToken);
        showHit.RemotePlexMediaId.ShouldBe(remoteShow.Id);
        showHit.OwnedPlexMediaId.ShouldBe(ownedShow.Id);
        showHit.MatchType.ShouldBe(PlexMediaComparisonMatchType.TmdbGuid);
        showHit.HitState.ShouldBe(PlexMediaComparisonHitState.Matched);
        (await dbContext.PlexSeasonComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexEpisodeComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldDeleteOldTvHits_WhenRerunFindsNoShowMatches()
    {
        // Arrange
        await SetupDatabase(
            44,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var remoteShow = (await GetLibraryTvShowsAsync(remoteLibrary.Id))[0];
        var ownedShow = (await GetLibraryTvShowsAsync(ownedLibrary.Id))[0];
        var remoteSeason = (await GetLibrarySeasonsAsync(remoteLibrary.Id))[0];
        var ownedSeason = (await GetLibrarySeasonsAsync(ownedLibrary.Id))[0];
        var remoteEpisode = (await GetLibraryEpisodesAsync(remoteLibrary.Id))[0];
        var ownedEpisode = (await GetLibraryEpisodesAsync(ownedLibrary.Id))[0];
        dbContext.PlexTvShowComparisons.Add(
            CreateTvShowComparison(remoteLibrary.Id, ownedLibrary.Id, remoteShow.Id, ownedShow.Id)
        );
        dbContext.PlexSeasonComparisons.Add(
            CreateSeasonComparison(remoteLibrary.Id, ownedLibrary.Id, remoteSeason.Id, ownedSeason.Id)
        );
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(remoteLibrary.Id, ownedLibrary.Id, remoteEpisode.Id, ownedEpisode.Id)
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await UpdateTvShowMatchFieldsAsync(remoteShow.Id, "remote only", 2001, 120, VideoQuality.FullHD);
        await UpdateTvShowMatchFieldsAsync(ownedShow.Id, "owned only", 2002, 121, VideoQuality.FullHD);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var showHitCount = await IDbContext.PlexTvShowComparisons.CountAsync(
            x => x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id,
            CancellationToken
        );
        var seasonHitCount = await IDbContext.PlexSeasonComparisons.CountAsync(
            x => x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id,
            CancellationToken
        );
        var episodeHitCount = await IDbContext.PlexEpisodeComparisons.CountAsync(
            x => x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id,
            CancellationToken
        );
        showHitCount.ShouldBe(0);
        seasonHitCount.ShouldBe(0);
        episodeHitCount.ShouldBe(0);
        var scope = await IDbContext.PlexComparisonScopes.SingleAsync(
            x =>
                x.RemotePlexLibraryId == remoteLibrary.Id
                && x.OwnedPlexLibraryId == ownedLibrary.Id
                && x.MediaType == PlexMediaType.TvShow,
            CancellationToken
        );
        scope.CompletedAt.ShouldBeGreaterThan(default(DateTime));
    }

    [Test]
    public async Task ShouldAdvanceExistingComparisonScope_WhenComparisonCompletes()
    {
        // Arrange
        await SetupDatabase(
            34,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        var previousCompletedAt = new DateTime(2020, 7, 20, 22, 19, 16, DateTimeKind.Utc);
        var previousScope = new PlexComparisonState
        {
            RemotePlexLibraryId = remoteLibrary.Id,
            OwnedPlexLibraryId = ownedLibrary.Id,
            MediaType = PlexMediaType.TvShow,
            CompletedAt = previousCompletedAt,
        };
        dbContext.PlexComparisonScopes.Add(previousScope);
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var scope = await dbContext.PlexComparisonScopes.AsNoTracking().SingleAsync(CancellationToken);
        scope.Id.ShouldBe(previousScope.Id);
        scope.RemotePlexLibraryId.ShouldBe(remoteLibrary.Id);
        scope.OwnedPlexLibraryId.ShouldBe(ownedLibrary.Id);
        scope.MediaType.ShouldBe(PlexMediaType.TvShow);
        scope.CompletedAt.ShouldBeGreaterThan(previousCompletedAt);
    }

    [Test]
    public async Task ShouldPersistComparisonRowsAcrossBatchBoundary()
    {
        // Arrange
        await SetupDatabase(
            62701,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 101;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);

        var remoteShows = await GetLibraryTvShowsAsync(remoteLibrary.Id);
        var firstRemoteShow = remoteShows[0];
        var remoteShow = remoteShows[^1];
        var ownedShow = (await GetLibraryTvShowsAsync(ownedLibrary.Id))[^1];
        await UpdateTvShowMatchFieldsAsync(
            firstRemoteShow.Id,
            "batch boundary show",
            2026,
            120,
            VideoQuality.UHD_4K,
            tmdbGuid: 62701
        );
        await UpdateTvShowMatchFieldsAsync(
            remoteShow.Id,
            "batch boundary show",
            2026,
            120,
            VideoQuality.UHD_4K,
            tmdbGuid: 62701
        );
        await UpdateTvShowMatchFieldsAsync(
            ownedShow.Id,
            "batch boundary show",
            2026,
            120,
            VideoQuality.HD,
            tmdbGuid: 62701
        );

        var remoteSeasons = await GetLibrarySeasonsAsync(remoteLibrary.Id);
        var firstRemoteSeason = remoteSeasons.Single(x => x.TvShowId == firstRemoteShow.Id);
        var remoteSeason = remoteSeasons.Single(x => x.TvShowId == remoteShow.Id);
        var ownedSeason = (await GetLibrarySeasonsAsync(ownedLibrary.Id)).Single(x => x.TvShowId == ownedShow.Id);
        var remoteEpisodes = await GetLibraryEpisodesAsync(remoteLibrary.Id);
        var firstRemoteEpisode = remoteEpisodes.Single(x => x.TvShowId == firstRemoteShow.Id);
        var remoteEpisode = remoteEpisodes.Single(x => x.TvShowId == remoteShow.Id);
        var ownedEpisode = (await GetLibraryEpisodesAsync(ownedLibrary.Id)).Single(x => x.TvShowId == ownedShow.Id);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        remoteShows.Count.ShouldBe(101);
        remoteShows[99].Id.ShouldBeLessThan(remoteShow.Id);
        var showHits = await dbContext
            .PlexTvShowComparisons.Where(x =>
                x.RemotePlexLibraryId == remoteLibrary.Id
                && x.OwnedPlexLibraryId == ownedLibrary.Id
                && (x.RemotePlexMediaId == firstRemoteShow.Id || x.RemotePlexMediaId == remoteShow.Id)
                && x.OwnedPlexMediaId == ownedShow.Id
            )
            .ToListAsync(CancellationToken);
        showHits.Count.ShouldBe(2);
        showHits.ShouldAllBe(x =>
            x.OwnedPlexMediaId == ownedShow.Id
            && x.MatchType == PlexMediaComparisonMatchType.TmdbGuid
            && x.HitState == PlexMediaComparisonHitState.HigherQuality
            && x.RemoteQuality == VideoQuality.UHD_4K
            && x.OwnedQuality == VideoQuality.HD
        );
        showHits
            .Select(x => x.RemotePlexMediaId)
            .ToHashSet()
            .SetEquals([firstRemoteShow.Id, remoteShow.Id])
            .ShouldBeTrue();

        var seasonHits = await dbContext
            .PlexSeasonComparisons.Where(x =>
                x.RemotePlexLibraryId == remoteLibrary.Id
                && x.OwnedPlexLibraryId == ownedLibrary.Id
                && (x.RemotePlexMediaId == firstRemoteSeason.Id || x.RemotePlexMediaId == remoteSeason.Id)
                && x.OwnedPlexMediaId == ownedSeason.Id
            )
            .ToListAsync(CancellationToken);
        seasonHits.Count.ShouldBe(2);
        seasonHits.ShouldAllBe(x =>
            x.OwnedPlexMediaId == ownedSeason.Id
            && x.MatchType == PlexMediaComparisonMatchType.ParentAndChildNumbers
            && x.HitState == PlexMediaComparisonHitState.Matched
        );
        seasonHits
            .Select(x => x.RemotePlexMediaId)
            .ToHashSet()
            .SetEquals([firstRemoteSeason.Id, remoteSeason.Id])
            .ShouldBeTrue();

        var episodeHits = await dbContext
            .PlexEpisodeComparisons.Where(x =>
                x.RemotePlexLibraryId == remoteLibrary.Id
                && x.OwnedPlexLibraryId == ownedLibrary.Id
                && (x.RemotePlexMediaId == firstRemoteEpisode.Id || x.RemotePlexMediaId == remoteEpisode.Id)
                && x.OwnedPlexMediaId == ownedEpisode.Id
            )
            .ToListAsync(CancellationToken);
        episodeHits.Count.ShouldBe(2);
        episodeHits.ShouldAllBe(x =>
            x.OwnedPlexMediaId == ownedEpisode.Id
            && x.MatchType == PlexMediaComparisonMatchType.ParentAndChildNumbers
            && x.HitState == PlexMediaComparisonHitState.Matched
        );
        episodeHits
            .Select(x => x.RemotePlexMediaId)
            .ToHashSet()
            .SetEquals([firstRemoteEpisode.Id, remoteEpisode.Id])
            .ShouldBeTrue();

        var scope = await dbContext.PlexComparisonScopes.SingleAsync(
            x =>
                x.RemotePlexLibraryId == remoteLibrary.Id
                && x.OwnedPlexLibraryId == ownedLibrary.Id
                && x.MediaType == PlexMediaType.TvShow,
            CancellationToken
        );
        scope.CompletedAt.ShouldBeGreaterThan(default);
    }

    [Test]
    [Arguments(0, 120)]
    [Arguments(120, 0)]
    [Arguments(0, 0)]
    [Arguments(120, 121)]
    public async Task ShouldUseTitleYearFallback_WhenNoPositiveExactDurationMatches(
        int remoteDuration, int ownedDuration
    )
    {
        // Arrange
        await SetupDatabase(
            62702,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        var remoteShows = await GetLibraryTvShowsAsync(remoteLibrary.Id);
        var ownedShows = await GetLibraryTvShowsAsync(ownedLibrary.Id);
        await UpdateTvShowMatchFieldsAsync(
            remoteShows[0].Id, "duration fallback", 2026, remoteDuration, VideoQuality.FullHD
        );
        await UpdateTvShowMatchFieldsAsync(
            remoteShows[1].Id, "unmatched remote", 1984, 90, VideoQuality.FullHD
        );
        await UpdateTvShowMatchFieldsAsync(
            ownedShows[0].Id, "DURATION FALLBACK", 2026, ownedDuration, VideoQuality.HD
        );
        await UpdateTvShowMatchFieldsAsync(
            ownedShows[1].Id, "duration fallback", 2026, ownedDuration, VideoQuality.UHD_4K
        );

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var hit = await dbContext.PlexTvShowComparisons.SingleAsync(CancellationToken);
        hit.RemotePlexMediaId.ShouldBe(remoteShows[0].Id);
        hit.OwnedPlexMediaId.ShouldBe(ownedShows[0].Id);
        hit.MatchType.ShouldBe(PlexMediaComparisonMatchType.NormalizedTitleAndYear);
        hit.HitState.ShouldBe(PlexMediaComparisonHitState.HigherQuality);
        hit.RemoteQuality.ShouldBe(VideoQuality.FullHD);
        hit.OwnedQuality.ShouldBe(VideoQuality.HD);
    }

    [Test]
    public async Task ShouldNotMatchCrossedTitlesAndYears_WhenGuidsAreMissingOrEmpty()
    {
        // Arrange
        await SetupDatabase(
            62703,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        var remoteShows = await GetLibraryTvShowsAsync(remoteLibrary.Id);
        var ownedShows = await GetLibraryTvShowsAsync(ownedLibrary.Id);
        await UpdateTvShowMatchFieldsAsync(remoteShows[0].Id, "alpha", 2000, 120, VideoQuality.FullHD);
        await UpdateTvShowMatchFieldsAsync(
            remoteShows[1].Id, "beta", 2001, 120, VideoQuality.FullHD, imdbGuid: string.Empty
        );
        await UpdateTvShowMatchFieldsAsync(
            ownedShows[0].Id, "alpha", 2001, 120, VideoQuality.HD, imdbGuid: string.Empty
        );
        await UpdateTvShowMatchFieldsAsync(ownedShows[1].Id, "beta", 2000, 120, VideoQuality.HD);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        (await dbContext.PlexTvShowComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexSeasonComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexEpisodeComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        var scope = await dbContext.PlexComparisonScopes.SingleAsync(CancellationToken);
        scope.RemotePlexLibraryId.ShouldBe(remoteLibrary.Id);
        scope.OwnedPlexLibraryId.ShouldBe(ownedLibrary.Id);
        scope.MediaType.ShouldBe(PlexMediaType.TvShow);
        scope.CompletedAt.ShouldBeGreaterThan(default);
    }

    [Test]
    [Arguments(VideoQuality.FullHD, VideoQuality.HD, PlexMediaComparisonHitState.HigherQuality)]
    [Arguments(VideoQuality.FullHD, VideoQuality.FullHD, PlexMediaComparisonHitState.Matched)]
    [Arguments(VideoQuality.HD, VideoQuality.UHD_4K, PlexMediaComparisonHitState.Matched)]
    public async Task ShouldStoreShowQualitySnapshotsAndUpgradeState(
        VideoQuality remoteQuality, VideoQuality ownedQuality, PlexMediaComparisonHitState expectedState
    )
    {
        // Arrange
        await SetupDatabase(
            62704,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        var remoteShow = (await GetLibraryTvShowsAsync(remoteLibrary.Id)).Single();
        var ownedShow = (await GetLibraryTvShowsAsync(ownedLibrary.Id)).Single();
        await UpdateTvShowMatchFieldsAsync(remoteShow.Id, "remote", 2000, 120, remoteQuality, tmdbGuid: 62704);
        await UpdateTvShowMatchFieldsAsync(ownedShow.Id, "owned", 2001, 90, ownedQuality, tmdbGuid: 62704);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var showHit = await dbContext.PlexTvShowComparisons.SingleAsync(CancellationToken);
        showHit.RemotePlexMediaId.ShouldBe(remoteShow.Id);
        showHit.OwnedPlexMediaId.ShouldBe(ownedShow.Id);
        showHit.MatchType.ShouldBe(PlexMediaComparisonMatchType.TmdbGuid);
        showHit.HitState.ShouldBe(expectedState);
        showHit.RemoteQuality.ShouldBe(remoteQuality);
        showHit.OwnedQuality.ShouldBe(ownedQuality);
    }

    [Test]
    public async Task ShouldNotUseChildrenOfUnselectedDuplicate_WhenSelectedShowHasNoMatchingSeason()
    {
        // Arrange
        await SetupDatabase(
            62705,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        var remoteShows = await GetLibraryTvShowsAsync(remoteLibrary.Id);
        var ownedShows = await GetLibraryTvShowsAsync(ownedLibrary.Id);
        await UpdateTvShowMatchFieldsAsync(remoteShows[0].Id, "remote", 2000, 120, VideoQuality.FullHD, tmdbGuid: 62705);
        await UpdateTvShowMatchFieldsAsync(remoteShows[1].Id, "unmatched", 1999, 90, VideoQuality.FullHD);
        await UpdateTvShowMatchFieldsAsync(ownedShows[0].Id, "first", 2001, 90, VideoQuality.FullHD, tmdbGuid: 62705);
        await UpdateTvShowMatchFieldsAsync(ownedShows[1].Id, "second", 2002, 90, VideoQuality.FullHD, tmdbGuid: 62705);
        var remoteSeason = (await GetLibrarySeasonsAsync(remoteLibrary.Id))
            .Single(x => x.TvShowId == remoteShows[0].Id);
        var ownedSeasons = await GetLibrarySeasonsAsync(ownedLibrary.Id);
        await UpdateSeasonNumberAsync(ownedSeasons.Single(x => x.TvShowId == ownedShows[0].Id).Id, 99);
        await UpdateSeasonNumberAsync(
            ownedSeasons.Single(x => x.TvShowId == ownedShows[1].Id).Id, remoteSeason.SeasonNumber
        );

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var showHit = await dbContext.PlexTvShowComparisons.SingleAsync(CancellationToken);
        showHit.RemotePlexMediaId.ShouldBe(remoteShows[0].Id);
        showHit.OwnedPlexMediaId.ShouldBe(ownedShows[0].Id);
        showHit.MatchType.ShouldBe(PlexMediaComparisonMatchType.TmdbGuid);
        (await dbContext.PlexSeasonComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexEpisodeComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldContinueAfterUnmatchedBatch_WhenLaterShowMatches()
    {
        // Arrange
        await SetupDatabase(
            62706,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 101;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await dbContext.PlexTvShows.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.Guid_TMDB, (int?)null)
                .SetProperty(y => y.Guid_IMDB, (string?)null)
                .SetProperty(y => y.Guid_TVDB, (int?)null)
                .SetProperty(y => y.SearchTitle, y => y.PlexLibraryId == remoteLibrary.Id ? "remote only" : "owned only"),
            CancellationToken
        );
        var remoteShow = (await GetLibraryTvShowsAsync(remoteLibrary.Id)).Last();
        var ownedShow = (await GetLibraryTvShowsAsync(ownedLibrary.Id)).First();
        await UpdateTvShowMatchFieldsAsync(remoteShow.Id, "last remote", 2026, 120, VideoQuality.FullHD, tmdbGuid: 62706);
        await UpdateTvShowMatchFieldsAsync(ownedShow.Id, "first owned", 2000, 90, VideoQuality.HD, tmdbGuid: 62706);
        var remoteSeason = (await GetLibrarySeasonsAsync(remoteLibrary.Id)).Single(x => x.TvShowId == remoteShow.Id);
        var ownedSeason = (await GetLibrarySeasonsAsync(ownedLibrary.Id)).Single(x => x.TvShowId == ownedShow.Id);
        var remoteEpisode = (await GetLibraryEpisodesAsync(remoteLibrary.Id))
            .Single(x => x.TvShowSeasonId == remoteSeason.Id);
        var ownedEpisode = (await GetLibraryEpisodesAsync(ownedLibrary.Id))
            .Single(x => x.TvShowSeasonId == ownedSeason.Id);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var showHit = await dbContext.PlexTvShowComparisons.SingleAsync(CancellationToken);
        showHit.RemotePlexMediaId.ShouldBe(remoteShow.Id);
        showHit.OwnedPlexMediaId.ShouldBe(ownedShow.Id);
        showHit.MatchType.ShouldBe(PlexMediaComparisonMatchType.TmdbGuid);
        var seasonHit = await dbContext.PlexSeasonComparisons.SingleAsync(CancellationToken);
        seasonHit.RemotePlexMediaId.ShouldBe(remoteSeason.Id);
        seasonHit.OwnedPlexMediaId.ShouldBe(ownedSeason.Id);
        var episodeHit = await dbContext.PlexEpisodeComparisons.SingleAsync(CancellationToken);
        episodeHit.RemotePlexMediaId.ShouldBe(remoteEpisode.Id);
        episodeHit.OwnedPlexMediaId.ShouldBe(ownedEpisode.Id);
    }

    [Test]
    [Arguments(101)]
    [Arguments(201)]
    public async Task ShouldPersistExactlyOneHitPerRemoteAtEveryLevel_WhenLaterBatchRetries(int showCount)
    {
        // Arrange
        await SetupDatabase(
            62707,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = showCount;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await dbContext.PlexTvShows.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.Guid_TMDB, 62707)
                .SetProperty(y => y.Quality, y => y.PlexLibraryId == remoteLibrary.Id ? VideoQuality.FullHD : VideoQuality.HD),
            CancellationToken
        );
        var remoteShows = await GetLibraryTvShowsAsync(remoteLibrary.Id);
        var ownedShow = (await GetLibraryTvShowsAsync(ownedLibrary.Id)).First();
        var remoteSeasons = await GetLibrarySeasonsAsync(remoteLibrary.Id);
        var ownedSeason = (await GetLibrarySeasonsAsync(ownedLibrary.Id)).Single(x => x.TvShowId == ownedShow.Id);
        var remoteEpisodes = await GetLibraryEpisodesAsync(remoteLibrary.Id);
        var ownedEpisode = (await GetLibraryEpisodesAsync(ownedLibrary.Id))
            .Single(x => x.TvShowSeasonId == ownedSeason.Id);
        var persistenceContext = (DbContext)dbContext;
        var batchWrites = 0;
        var injectedFailure = false;
        persistenceContext.SavingChanges += (_, _) =>
        {
            if (persistenceContext.ChangeTracker.Entries<PlexTvShowComparison>().All(x => x.State != EntityState.Added))
                return;
            if (++batchWrites == 2)
            {
                injectedFailure = true;
                throw new Microsoft.Data.Sqlite.SqliteException("Injected later-batch write contention", 5);
            }
        };
        SetupDependencies(builder => builder.RegisterInstance(dbContext).As<IReaparrDbContext>().ExternallyOwned());

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        injectedFailure.ShouldBeTrue();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var showHits = await dbContext.PlexTvShowComparisons.OrderBy(x => x.RemotePlexMediaId).ToListAsync(CancellationToken);
        showHits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId))
            .ShouldBe(remoteShows.Select(x => (x.Id, ownedShow.Id)));
        showHits.ShouldAllBe(x =>
            x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id
            && x.MatchType == PlexMediaComparisonMatchType.TmdbGuid
            && x.HitState == PlexMediaComparisonHitState.HigherQuality
            && x.RemoteQuality == VideoQuality.FullHD && x.OwnedQuality == VideoQuality.HD
        );
        var seasonHits = await dbContext.PlexSeasonComparisons.OrderBy(x => x.RemotePlexMediaId).ToListAsync(CancellationToken);
        seasonHits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId))
            .ShouldBe(remoteSeasons.Select(x => (x.Id, ownedSeason.Id)));
        seasonHits.ShouldAllBe(x =>
            x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id
            && x.MatchType == PlexMediaComparisonMatchType.ParentAndChildNumbers
        );
        var episodeHits = await dbContext.PlexEpisodeComparisons.OrderBy(x => x.RemotePlexMediaId).ToListAsync(CancellationToken);
        episodeHits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId))
            .ShouldBe(remoteEpisodes.Select(x => (x.Id, ownedEpisode.Id)));
        episodeHits.ShouldAllBe(x =>
            x.RemotePlexLibraryId == remoteLibrary.Id && x.OwnedPlexLibraryId == ownedLibrary.Id
            && x.MatchType == PlexMediaComparisonMatchType.ParentAndChildNumbers
        );
        var scope = await dbContext.PlexComparisonScopes.SingleAsync(CancellationToken);
        scope.RemotePlexLibraryId.ShouldBe(remoteLibrary.Id);
        scope.OwnedPlexLibraryId.ShouldBe(ownedLibrary.Id);
        scope.MediaType.ShouldBe(PlexMediaType.TvShow);
        showHits.ShouldAllBe(x => x.ComparedAt == scope.CompletedAt);
        seasonHits.ShouldAllBe(x => x.ComparedAt == scope.CompletedAt);
        episodeHits.ShouldAllBe(x => x.ComparedAt == scope.CompletedAt);
    }

    [Test]
    public async Task ShouldPreservePreviousRowsAndScope_WhenPersistenceIsCancelled()
    {
        // Arrange
        await SetupDatabase(
            62708,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        var remoteShow = (await GetLibraryTvShowsAsync(remoteLibrary.Id)).Single();
        var ownedShow = (await GetLibraryTvShowsAsync(ownedLibrary.Id)).Single();
        var remoteSeason = (await GetLibrarySeasonsAsync(remoteLibrary.Id)).Single();
        var ownedSeason = (await GetLibrarySeasonsAsync(ownedLibrary.Id)).Single();
        var remoteEpisode = (await GetLibraryEpisodesAsync(remoteLibrary.Id)).Single();
        var ownedEpisode = (await GetLibraryEpisodesAsync(ownedLibrary.Id)).Single();
        await UpdateTvShowMatchFieldsAsync(remoteShow.Id, "remote", 2026, 120, VideoQuality.UHD_4K, tmdbGuid: 62708);
        await UpdateTvShowMatchFieldsAsync(ownedShow.Id, "owned", 2000, 90, VideoQuality.HD, tmdbGuid: 62708);
        var previousShowHit = CreateTvShowComparison(remoteLibrary.Id, ownedLibrary.Id, remoteShow.Id, ownedShow.Id);
        var previousSeasonHit = CreateSeasonComparison(remoteLibrary.Id, ownedLibrary.Id, remoteSeason.Id, ownedSeason.Id);
        var previousEpisodeHit = CreateEpisodeComparison(remoteLibrary.Id, ownedLibrary.Id, remoteEpisode.Id, ownedEpisode.Id);
        var previousCompletedAt = new DateTime(2020, 7, 20, 22, 19, 16, DateTimeKind.Utc);
        var previousScope = new PlexComparisonState
        {
            RemotePlexLibraryId = remoteLibrary.Id,
            OwnedPlexLibraryId = ownedLibrary.Id,
            MediaType = PlexMediaType.TvShow,
            CompletedAt = previousCompletedAt,
        };
        dbContext.PlexTvShowComparisons.Add(previousShowHit);
        dbContext.PlexSeasonComparisons.Add(previousSeasonHit);
        dbContext.PlexEpisodeComparisons.Add(previousEpisodeHit);
        dbContext.PlexComparisonScopes.Add(previousScope);
        await dbContext.SaveChangesAsync(CancellationToken);
        var persistenceCancellation = new CancellationToken(canceled: true);
        var cancelledWrite = false;
        ((DbContext)dbContext).SavingChanges += (_, _) =>
        {
            cancelledWrite = true;
            persistenceCancellation.ThrowIfCancellationRequested();
        };
        SetupDependencies(builder => builder.RegisterInstance(dbContext).As<IReaparrDbContext>().ExternallyOwned());

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        cancelledWrite.ShouldBeTrue();
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeTrue();
        var showHit = await dbContext.PlexTvShowComparisons.AsNoTracking().SingleAsync(CancellationToken);
        showHit.Id.ShouldBe(previousShowHit.Id);
        showHit.RemotePlexMediaId.ShouldBe(remoteShow.Id);
        showHit.OwnedPlexMediaId.ShouldBe(ownedShow.Id);
        showHit.HitState.ShouldBe(PlexMediaComparisonHitState.Matched);
        showHit.ComparedAt.ShouldBe(previousShowHit.ComparedAt);
        var seasonHit = await dbContext.PlexSeasonComparisons.AsNoTracking().SingleAsync(CancellationToken);
        seasonHit.Id.ShouldBe(previousSeasonHit.Id);
        seasonHit.RemotePlexMediaId.ShouldBe(remoteSeason.Id);
        seasonHit.OwnedPlexMediaId.ShouldBe(ownedSeason.Id);
        seasonHit.ComparedAt.ShouldBe(previousSeasonHit.ComparedAt);
        var episodeHit = await dbContext.PlexEpisodeComparisons.AsNoTracking().SingleAsync(CancellationToken);
        episodeHit.Id.ShouldBe(previousEpisodeHit.Id);
        episodeHit.RemotePlexMediaId.ShouldBe(remoteEpisode.Id);
        episodeHit.OwnedPlexMediaId.ShouldBe(ownedEpisode.Id);
        episodeHit.ComparedAt.ShouldBe(previousEpisodeHit.ComparedAt);
        var scope = await dbContext.PlexComparisonScopes.AsNoTracking().SingleAsync(CancellationToken);
        scope.Id.ShouldBe(previousScope.Id);
        scope.CompletedAt.ShouldBe(previousCompletedAt);
    }

    [Test]
    public async Task ShouldNotMatchEpisodeNumbersAcrossDifferentSeasons()
    {
        // Arrange
        await SetupDatabase(
            62709,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        var remoteShow = (await GetLibraryTvShowsAsync(remoteLibrary.Id)).Single();
        var ownedShow = (await GetLibraryTvShowsAsync(ownedLibrary.Id)).Single();
        await UpdateTvShowMatchFieldsAsync(remoteShow.Id, "remote", 2026, 120, VideoQuality.FullHD, tmdbGuid: 62709);
        await UpdateTvShowMatchFieldsAsync(ownedShow.Id, "owned", 2000, 90, VideoQuality.HD, tmdbGuid: 62709);
        var remoteSeasons = await GetLibrarySeasonsAsync(remoteLibrary.Id);
        var ownedSeasons = await GetLibrarySeasonsAsync(ownedLibrary.Id);
        for (var i = 0; i < 2; i++)
        {
            await UpdateSeasonNumberAsync(remoteSeasons[i].Id, i + 1);
            await UpdateSeasonNumberAsync(ownedSeasons[i].Id, i + 1);
            var remoteSeasonId = remoteSeasons[i].Id;
            var ownedSeasonId = ownedSeasons[i].Id;
            var remoteEpisodeNumber = i + 10;
            var ownedEpisodeNumber = 11 - i;
            await dbContext.PlexTvShowEpisodes
                .Where(x => x.TvShowSeasonId == remoteSeasonId)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.EpisodeNumber, remoteEpisodeNumber), CancellationToken);
            await dbContext.PlexTvShowEpisodes
                .Where(x => x.TvShowSeasonId == ownedSeasonId)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.EpisodeNumber, ownedEpisodeNumber), CancellationToken);
        }

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var showHit = await dbContext.PlexTvShowComparisons.SingleAsync(CancellationToken);
        showHit.RemotePlexMediaId.ShouldBe(remoteShow.Id);
        showHit.OwnedPlexMediaId.ShouldBe(ownedShow.Id);
        var seasonHits = await dbContext.PlexSeasonComparisons.OrderBy(x => x.RemotePlexMediaId).ToListAsync(CancellationToken);
        seasonHits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId)).ShouldBe(
            [(remoteSeasons[0].Id, ownedSeasons[0].Id), (remoteSeasons[1].Id, ownedSeasons[1].Id)]
        );
        (await dbContext.PlexEpisodeComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldPreserveOtherLibraryPairRowsAndScope_WhenRerunRemovesCurrentHits()
    {
        // Arrange
        await SetupDatabase(
            62710,
            config =>
            {
                config.PlexServerCount = 3;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        var otherRemoteLibrary = libraries[2];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetOwnedOverrideAsync(otherRemoteLibrary.PlexServerId, false);
        var remoteShow = (await GetLibraryTvShowsAsync(remoteLibrary.Id)).Single();
        var ownedShow = (await GetLibraryTvShowsAsync(ownedLibrary.Id)).Single();
        var otherRemoteShow = (await GetLibraryTvShowsAsync(otherRemoteLibrary.Id)).Single();
        var remoteSeason = (await GetLibrarySeasonsAsync(remoteLibrary.Id)).Single();
        var ownedSeason = (await GetLibrarySeasonsAsync(ownedLibrary.Id)).Single();
        var otherRemoteSeason = (await GetLibrarySeasonsAsync(otherRemoteLibrary.Id)).Single();
        var remoteEpisode = (await GetLibraryEpisodesAsync(remoteLibrary.Id)).Single();
        var ownedEpisode = (await GetLibraryEpisodesAsync(ownedLibrary.Id)).Single();
        var otherRemoteEpisode = (await GetLibraryEpisodesAsync(otherRemoteLibrary.Id)).Single();
        await UpdateTvShowMatchFieldsAsync(remoteShow.Id, "no longer matching", 2026, 120, VideoQuality.FullHD);
        await UpdateTvShowMatchFieldsAsync(ownedShow.Id, "owned only", 2000, 90, VideoQuality.HD);
        var otherShowHit = CreateTvShowComparison(
            otherRemoteLibrary.Id, ownedLibrary.Id, otherRemoteShow.Id, ownedShow.Id
        );
        var otherSeasonHit = CreateSeasonComparison(
            otherRemoteLibrary.Id, ownedLibrary.Id, otherRemoteSeason.Id, ownedSeason.Id
        );
        var otherEpisodeHit = CreateEpisodeComparison(
            otherRemoteLibrary.Id, ownedLibrary.Id, otherRemoteEpisode.Id, ownedEpisode.Id
        );
        dbContext.PlexTvShowComparisons.AddRange(
            CreateTvShowComparison(remoteLibrary.Id, ownedLibrary.Id, remoteShow.Id, ownedShow.Id), otherShowHit
        );
        dbContext.PlexSeasonComparisons.AddRange(
            CreateSeasonComparison(remoteLibrary.Id, ownedLibrary.Id, remoteSeason.Id, ownedSeason.Id), otherSeasonHit
        );
        dbContext.PlexEpisodeComparisons.AddRange(
            CreateEpisodeComparison(remoteLibrary.Id, ownedLibrary.Id, remoteEpisode.Id, ownedEpisode.Id), otherEpisodeHit
        );
        var previousCompletedAt = new DateTime(2020, 7, 20, 22, 19, 16, DateTimeKind.Utc);
        var currentScope = new PlexComparisonState
        {
            RemotePlexLibraryId = remoteLibrary.Id,
            OwnedPlexLibraryId = ownedLibrary.Id,
            MediaType = PlexMediaType.TvShow,
            CompletedAt = previousCompletedAt,
        };
        var otherScope = new PlexComparisonState
        {
            RemotePlexLibraryId = otherRemoteLibrary.Id,
            OwnedPlexLibraryId = ownedLibrary.Id,
            MediaType = PlexMediaType.TvShow,
            CompletedAt = previousCompletedAt,
        };
        dbContext.PlexComparisonScopes.AddRange(currentScope, otherScope);
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(
            new CompareTvShowPlexLibraryCommand(ownedLibrary.Id, remoteLibrary.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        var showHit = await dbContext.PlexTvShowComparisons.AsNoTracking().SingleAsync(CancellationToken);
        showHit.Id.ShouldBe(otherShowHit.Id);
        showHit.RemotePlexLibraryId.ShouldBe(otherRemoteLibrary.Id);
        showHit.RemotePlexMediaId.ShouldBe(otherRemoteShow.Id);
        showHit.OwnedPlexMediaId.ShouldBe(ownedShow.Id);
        showHit.ComparedAt.ShouldBe(otherShowHit.ComparedAt);
        var seasonHit = await dbContext.PlexSeasonComparisons.AsNoTracking().SingleAsync(CancellationToken);
        seasonHit.Id.ShouldBe(otherSeasonHit.Id);
        seasonHit.RemotePlexLibraryId.ShouldBe(otherRemoteLibrary.Id);
        seasonHit.RemotePlexMediaId.ShouldBe(otherRemoteSeason.Id);
        seasonHit.OwnedPlexMediaId.ShouldBe(ownedSeason.Id);
        seasonHit.ComparedAt.ShouldBe(otherSeasonHit.ComparedAt);
        var episodeHit = await dbContext.PlexEpisodeComparisons.AsNoTracking().SingleAsync(CancellationToken);
        episodeHit.Id.ShouldBe(otherEpisodeHit.Id);
        episodeHit.RemotePlexLibraryId.ShouldBe(otherRemoteLibrary.Id);
        episodeHit.RemotePlexMediaId.ShouldBe(otherRemoteEpisode.Id);
        episodeHit.OwnedPlexMediaId.ShouldBe(ownedEpisode.Id);
        episodeHit.ComparedAt.ShouldBe(otherEpisodeHit.ComparedAt);
        var scopes = await dbContext.PlexComparisonScopes.AsNoTracking().OrderBy(x => x.RemotePlexLibraryId).ToListAsync(CancellationToken);
        scopes.Select(x => x.Id).ShouldBe([currentScope.Id, otherScope.Id]);
        scopes[0].CompletedAt.ShouldBeGreaterThan(previousCompletedAt);
        scopes[1].CompletedAt.ShouldBe(previousCompletedAt);
    }

    private async Task<List<PlexTvShow>> GetLibraryTvShowsAsync(int plexLibraryId) =>
        await IDbContext
            .PlexTvShows.Where(x => x.PlexLibraryId == plexLibraryId)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

    private async Task<List<PlexTvShowSeason>> GetLibrarySeasonsAsync(int plexLibraryId) =>
        await IDbContext
            .PlexTvShowSeason.Where(x => x.PlexLibraryId == plexLibraryId)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

    private async Task UpdateTvShowMatchFieldsAsync(
        int plexTvShowId,
        string searchTitle,
        int year,
        int duration,
        VideoQuality quality,
        int? tmdbGuid = null,
        string? imdbGuid = null,
        int? tvdbGuid = null
    )
    {
        await IDbContext
            .PlexTvShows.Where(x => x.Id == plexTvShowId)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.SearchTitle, searchTitle)
                        .SetProperty(y => y.Year, year)
                        .SetProperty(y => y.Duration, duration)
                        .SetProperty(y => y.Quality, quality)
                        .SetProperty(y => y.Guid_TMDB, tmdbGuid)
                        .SetProperty(y => y.Guid_IMDB, imdbGuid)
                        .SetProperty(y => y.Guid_TVDB, tvdbGuid),
                CancellationToken
            );
    }

    private async Task UpdateSeasonNumberAsync(int plexTvShowSeasonId, int seasonNumber)
    {
        await IDbContext
            .PlexTvShowSeason.Where(x => x.Id == plexTvShowSeasonId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SeasonNumber, seasonNumber), CancellationToken);
    }
}
