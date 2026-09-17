using Reaparr.PublicAPI.Contracts;
using Reaparr.Application.Contracts;
using Reaparr.Settings.Contracts;

namespace Reaparr.PublicAPI.UnitTests;

public class SearchGenericCommandUnitTests : BaseCommandUnitTest<SearchGenericCommand>
{
    [Test]
    public async Task ShouldReturnSingleGloballyOrderedPageAndExactCombinedTotal_WhenBothMediaTypesMatch()
    {
        // Arrange
        await SetupDatabase(
            47301,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 3;
                config.IncludeMultiPartMovies = false;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 3;
            }
        );

        var dbContext = IDbContext;
        var movieRows = await dbContext.PlexMovieData.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var episodeRows = await dbContext.PlexTvShowEpisodeData.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var baseline = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < movieRows.Count; i++)
        {
            await dbContext
                .PlexMovieData.Where(x => x.Id == movieRows[i].Id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.GeneratedFilename, $"movie-{i}.mkv"),
                    CancellationToken
                );
            await dbContext
                .PlexMovies.Where(x => x.Id == movieRows[i].PlexMovieId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.AddedAt, baseline.AddMinutes(i)),
                    CancellationToken
                );
        }

        for (var i = 0; i < episodeRows.Count; i++)
        {
            await dbContext
                .PlexTvShowEpisodeData.Where(x => x.Id == episodeRows[i].Id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.GeneratedFilename, $"episode-{i}.mkv"),
                    CancellationToken
                );
            await dbContext
                .PlexTvShowEpisodes.Where(x => x.Id == episodeRows[i].PlexTvShowEpisodeId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.AddedAt, baseline.AddMinutes(10 + i)),
                    CancellationToken
                );
        }

        var command = CreateCommand(limit: 3, offset: 1);

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Channel.Response.Total.ShouldBe(6);
        result.Value.Channel.Response.Offset.ShouldBe(1);
        result.Value.Channel.Items.Select(x => x.Title).ShouldBe(["episode-1.mkv", "episode-0.mkv", "movie-2.mkv"]);
    }

    [Test]
    public async Task ShouldExcludeEpisodeBranch_WhenMovieCategoryIsRequested()
    {
        // Arrange
        await SetupDatabase(
            47302,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.IncludeMultiPartMovies = false;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var expectedMovieCount = await IDbContext.PlexMovieData.CountAsync(CancellationToken);
        expectedMovieCount.ShouldBeGreaterThan(0);
        var categories = new[] { (int)TorznabCategoryId.Movies };
        var command = CreateCommand(categories: categories);

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Channel.Response.Total.ShouldBe(expectedMovieCount);
        result.Value.Channel.Items.Count.ShouldBe(expectedMovieCount);
        result.Value.Channel.Items.ShouldAllBe(x =>
            x.Attributes.Any(attribute => attribute.Name == "type" && attribute.Value == "movie")
        );
    }

    [Test]
    public async Task ShouldExcludeMediaFromRevokedLibraryAccess_WhenGenericSearchIncludesBothMediaTypes()
    {
        // Arrange
        await SetupDatabase(
            47303,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.IncludeMultiPartMovies = false;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var revokedLibraryId = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .Select(x => x.Id)
            .SingleAsync(CancellationToken);
        await dbContext
            .PlexAccountLibraries.Where(x => x.PlexLibraryId == revokedLibraryId)
            .ExecuteDeleteAsync(CancellationToken);

        var expectedEpisodeCount = await dbContext.PlexTvShowEpisodeData.CountAsync(CancellationToken);
        expectedEpisodeCount.ShouldBeGreaterThan(0);
        var command = CreateCommand();

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Channel.Response.Total.ShouldBe(expectedEpisodeCount);
        result.Value.Channel.Items.Count.ShouldBe(expectedEpisodeCount);
        result.Value.Channel.Items.ShouldAllBe(x =>
            x.Attributes.Any(attribute => attribute.Name == "type" && attribute.Value == "series")
        );
    }

    [Test]
    public async Task ShouldReturnNoItemsWithExactTotal_WhenLimitIsZeroOrOffsetIsOutOfRange()
    {
        // Arrange
        await SetupDatabase(
            47304,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.IncludeMultiPartMovies = false;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );

        var dbContext = IDbContext;
        var expectedTotal =
            await dbContext.PlexMovieData.CountAsync(CancellationToken)
            + await dbContext.PlexTvShowEpisodeData.CountAsync(CancellationToken);
        var zeroLimitCommand = CreateCommand(limit: 0, offset: 0);
        var outOfRangeCommand = CreateCommand(limit: 1, offset: expectedTotal + 1);

        // Act
        var zeroLimitResult = await ExecuteCommandAsync(zeroLimitCommand);
        var outOfRangeResult = await ExecuteCommandAsync(outOfRangeCommand);

        // Assert
        zeroLimitResult.IsSuccess.ShouldBeTrue();
        zeroLimitResult.Errors.Count.ShouldBe(0);
        zeroLimitResult.Value.Channel.Response.Total.ShouldBe(expectedTotal);
        zeroLimitResult.Value.Channel.Items.ShouldBeEmpty();

        outOfRangeResult.IsSuccess.ShouldBeTrue();
        outOfRangeResult.Errors.Count.ShouldBe(0);
        outOfRangeResult.Value.Channel.Response.Total.ShouldBe(expectedTotal);
        outOfRangeResult.Value.Channel.Response.Offset.ShouldBe(expectedTotal + 1);
        outOfRangeResult.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnExactCrossMediaMatches_WhenTitlePrefixIsProvided()
    {
        // Arrange
        await SetupDatabase(
            47305,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.IncludeMultiPartMovies = false;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var dbContext = IDbContext;
        var movies = await dbContext.PlexMovies.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var tvShows = await dbContext.PlexTvShows.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await dbContext
            .PlexMovies.Where(x => x.Id == movies[0].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(movie => movie.SearchTitle, "matching movie"), CancellationToken);
        await dbContext
            .PlexMovies.Where(x => x.Id == movies[1].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(movie => movie.SearchTitle, "different movie"), CancellationToken);
        await dbContext
            .PlexTvShows.Where(x => x.Id == tvShows[0].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(show => show.SearchTitle, "matching show"), CancellationToken);
        await dbContext
            .PlexTvShows.Where(x => x.Id == tvShows[1].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(show => show.SearchTitle, "different show"), CancellationToken);
        await dbContext
            .PlexMovieData.Where(x => x.PlexMovieId == movies[0].Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(data => data.GeneratedFilename, "matching-movie.mkv"),
                CancellationToken
            );
        var matchingEpisodeId = await dbContext
            .PlexTvShowEpisodes.Where(x => x.TvShowId == tvShows[0].Id)
            .Select(x => x.Id)
            .SingleAsync(CancellationToken);
        await dbContext
            .PlexTvShowEpisodeData.Where(x => x.PlexTvShowEpisodeId == matchingEpisodeId)
            .ExecuteUpdateAsync(
                x => x.SetProperty(data => data.GeneratedFilename, "matching-episode.mkv"),
                CancellationToken
            );
        var command = CreateCommand(query: "MATCHING");

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Channel.Response.Total.ShouldBe(2);
        result
            .Value.Channel.Items.Select(x => x.Title)
            .Order()
            .ShouldBe(["matching-episode.mkv", "matching-movie.mkv"]);
    }

    [Test]
    public async Task ShouldReturnExactTvShowEpisodes_WhenTvdbSelectorIsProvided()
    {
        // Arrange
        await SetupDatabase(
            47306,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
            }
        );
        var dbContext = IDbContext;
        var target = await dbContext.PlexTvShows.OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var expectedTitles = await dbContext
            .PlexTvShowEpisodeData.Where(x => x.PlexTvShowEpisode!.TvShowId == target.Id)
            .OrderBy(x => x.Id)
            .Select(x => x.GetFileName)
            .ToListAsync(CancellationToken);
        expectedTitles.ShouldNotBeEmpty();
        var command = CreateCommand(tvdbId: target.Guid_TVDB!.Value, includeMovies: false);

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Channel.Response.Total.ShouldBe(expectedTitles.Count);
        result.Value.Channel.Items.Select(x => x.Title).Order().ShouldBe(expectedTitles.Order());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReturnExactMovie_WhenTmdbAndImdbSelectorsAgree(bool includeImdbPrefix)
    {
        // Arrange
        await SetupDatabase(
            includeImdbPrefix ? 47307 : 47308,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.IncludeMultiPartMovies = false;
            }
        );
        var dbContext = IDbContext;
        var target = await dbContext
            .PlexMovies.OrderBy(x => x.Id)
            .FirstAsync(x => x.Guid_IMDB != null && x.Guid_TMDB != null, CancellationToken);
        var expectedTitles = await dbContext
            .PlexMovieData.Where(x => x.PlexMovieId == target.Id)
            .OrderBy(x => x.Id)
            .Select(x => x.GetFileName)
            .ToListAsync(CancellationToken);
        var imdbId = includeImdbPrefix ? target.Guid_IMDB! : target.Guid_IMDB!.Replace("tt", string.Empty);
        var command = CreateCommand(tmdbId: target.Guid_TMDB!.Value, imdbId: imdbId, includeEpisodes: false);

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Channel.Response.Total.ShouldBe(expectedTitles.Count);
        result.Value.Channel.Items.Select(x => x.Title).ShouldBe(expectedTitles);
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenGenericExternalIdsConflict()
    {
        // Arrange
        await SetupDatabase(
            47309,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.IncludeMultiPartMovies = false;
            }
        );
        var movies = await IDbContext
            .PlexMovies.Where(x => x.Guid_IMDB != null && x.Guid_TMDB != null)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        movies.Count.ShouldBe(2);
        var command = CreateCommand(
            tmdbId: movies[0].Guid_TMDB!.Value,
            imdbId: movies[1].Guid_IMDB!,
            includeEpisodes: false
        );

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Channel.Response.Total.ShouldBe(0);
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenGenericImdbSelectorIsMalformed()
    {
        // Arrange
        var command = CreateCommand(imdbId: "imdb://tt123");

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Channel.Response.Total.ShouldBe(0);
        result.Value.Channel.Items.ShouldBeEmpty();
    }

    [Test]
    [Arguments(-1, 0, 3)]
    [Arguments(101, 0, 2)]
    [Arguments(1, -1, 3)]
    [Arguments(100, 9_901, 2)]
    public async Task ShouldFailValidation_WhenPaginationIsInvalid(int limit, int offset, int expectedErrorCount)
    {
        // Arrange
        var command = CreateCommand(limit: limit, offset: offset);

        // Act
        var result = await ExecuteCommandAsync(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(expectedErrorCount);
    }

    [Test]
    [Arguments(0, 0)]
    [Arguments(100, 9_900)]
    public void ShouldPassValidation_WhenPaginationIsWithinBoundaries(int limit, int offset)
    {
        // Arrange
        var validator = new SearchGenericCommandValidator();
        var command = CreateCommand(limit: limit, offset: offset);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    private async Task<Result<TorznabMediaSearchResponseDTO>> ExecuteCommandAsync(SearchGenericCommand command)
    {
        var networkSettings = Mock.Mock<INetworkSettings>();
        networkSettings.SetupGet(x => x.Url).Returns("http://localhost").Verifiable(Times.AtMostOnce());

        var result = await TestHandlerExecuteAsync<TorznabMediaSearchResponseDTO>(command);

        networkSettings.Verify();
        return result;
    }

    private static SearchGenericCommand CreateCommand(
        int limit = 100,
        int offset = 0,
        int[]? categories = null,
        string query = "",
        int tvdbId = 0,
        int tmdbId = 0,
        string imdbId = "",
        bool includeMovies = true,
        bool includeEpisodes = true
    )
    {
        var type = (includeMovies, includeEpisodes) switch
        {
            (true, true) => TorznabQueryType.Search,
            (true, false) => TorznabQueryType.Movie,
            (false, true) => TorznabQueryType.TvSearch,
            _ => TorznabQueryType.Unknown,
        };
        var requestCategories = categories ?? [];

        return new SearchGenericCommand(
            new TorznabRequest
            {
                Type = type,
                Query = query,
                Season = 0,
                Episode = 0,
                TvdbId = tvdbId,
                ImdbId = imdbId,
                TmdbId = tmdbId,
                ApiKey = "",
                Limit = limit,
                Offset = offset,
                Categories = requestCategories,
                Attributes = [],
                Integration = new IntegrationIdentity(IntegrationType.Sonarr, Guid.Empty),
            }
        );
    }
}
