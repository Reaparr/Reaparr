using FlexQuery.NET.Models;

namespace Reaparr.Application.UnitTests;

public class GetMovieMediaOverviewCommandUnitTests : BaseCommandUnitTest<GetMediaOverviewMovieCommand>
{
    [Test]
    public async Task ShouldReturnOrderedBoundedMoviePageFromSnapshotRows()
    {
        // Arrange
        await SetupDatabase(
            84001,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 3;
            }
        );
        var movieIds = await IDbContext.PlexMovies.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        var libraryId = await IDbContext
            .PlexMovies.Where(x => x.Id == movieIds[0])
            .Select(x => x.PlexLibraryId)
            .SingleAsync(CancellationToken);
        var setupContext = IDbContext;
        await setupContext.MediaOverviewMovieSnapshots.AddRangeAsync(
            movieIds.Select((id, index) => CreateMovieSnapshot(id, movieIds.Count - index - 1, libraryId)),
            CancellationToken
        );
        await setupContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(
            new GetMediaOverviewMovieCommand(
                new MediaQueryFilter
                {
                    MediaType = PlexMediaType.Movie,
                    PlexLibraryId = libraryId,
                    FilterOfflineMedia = false,
                    FilterOwnedMedia = false,
                    Parameters = new FlexQueryParameters
                    {
                        Page = 2,
                        PageSize = 1,
                        Sort = "year:desc",
                    },
                }
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(1);
        result.Value.TotalCount.ShouldBe(3);
        result.Value.Items[0].Id.ShouldBe(movieIds[1]);
        result.Value.Items[0].SortIndex.ShouldBe(2);
        result.Errors.Count.ShouldBe(0);
        result.Value.QueryHash.ShouldBe(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = libraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 2,
                    PageSize = 1,
                    Sort = "year:desc",
                },
            }.QueryHash
        );
        result.Value.Page.ShouldBe(2);
        result.Value.PageSize.ShouldBe(1);
        result.Value.Items[0].Type.ShouldBe(PlexMediaType.Movie);
        result.Value.Items[0].GrandChildCount.ShouldBe(0);
        result.Value.Items[0].Qualities.Select(x => x.Quality).ShouldBeInOrder();
    }

    [Test]
    public async Task ShouldBuildNavigationIndexesFromFullOrderedSnapshot_WhenFirstPageIsBounded()
    {
        // Arrange
        await SetupDatabase(
            84003,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 3;
            }
        );
        var dbContext = IDbContext;
        var movies = await dbContext.PlexMovies.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var libraryId = movies[0].PlexLibraryId;
        await dbContext
            .PlexMovies.Where(x => x.Id == movies[0].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SearchTitle, "alien"), CancellationToken);
        await dbContext
            .PlexMovies.Where(x => x.Id == movies[1].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SearchTitle, "blade runner"), CancellationToken);
        await dbContext
            .PlexMovies.Where(x => x.Id == movies[2].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SearchTitle, "arrival"), CancellationToken);
        await dbContext.MediaOverviewMovieSnapshots.AddRangeAsync(
            movies.Select((movie, index) => CreateMovieSnapshot(movie.Id, index, libraryId)),
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.Movie,
            PlexLibraryId = libraryId,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters { Page = 1, PageSize = 1, Sort = "sortIndex:asc" },
        };

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewMovieCommand(filter));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Items.Count.ShouldBe(1);
        result.Value.NavigationIndexes.Select(x => (x.Label, x.Index)).ShouldBe([("A", 0), ("B", 1)]);
    }

    [Test]
    public async Task ShouldReturnNavigationIndexes_WhenRequestingLaterPage()
    {
        // Arrange
        await SetupDatabase(
            84005,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 3;
            }
        );
        var dbContext = IDbContext;
        var movies = await dbContext.PlexMovies.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var libraryId = movies[0].PlexLibraryId;
        await dbContext.MediaOverviewMovieSnapshots.AddRangeAsync(
            movies.Select((movie, index) => CreateMovieSnapshot(movie.Id, index, libraryId)),
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.Movie,
            PlexLibraryId = libraryId,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters { Page = 2, PageSize = 1, Sort = "sortIndex:asc" },
        };

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewMovieCommand(filter));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Page.ShouldBe(2);
        result.Value.NavigationIndexes.ShouldNotBeEmpty();
        result.Value.NavigationIndexes[0].Index.ShouldBe(0);
    }

    [Test]
    [Arguments("sortIndex:asc")]
    [Arguments("sortIndex:desc")]
    [Arguments("year:asc")]
    [Arguments("year:desc")]
    [Arguments("addedAt:asc")]
    [Arguments("addedAt:desc")]
    [Arguments("updatedAt:asc")]
    [Arguments("updatedAt:desc")]
    [Arguments("duration:asc")]
    [Arguments("duration:desc")]
    [Arguments("mediaSize:asc")]
    [Arguments("mediaSize:desc")]
    [Arguments("quality:asc")]
    [Arguments("quality:desc")]
    public async Task ShouldReturnNavigationIndexes_ForEverySupportedSort(string sort)
    {
        // Arrange
        await SetupDatabase(
            84004,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 3;
            }
        );
        var dbContext = IDbContext;
        var movies = await dbContext.PlexMovies.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var libraryId = movies[0].PlexLibraryId;
        await dbContext.MediaOverviewMovieSnapshots.AddRangeAsync(
            movies.Select((movie, index) => CreateMovieSnapshot(movie.Id, index, libraryId)),
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.Movie,
            PlexLibraryId = libraryId,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters { Page = 1, PageSize = 1, Sort = sort },
        };

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewMovieCommand(filter));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.NavigationIndexes.ShouldNotBeEmpty();
        result.Value.NavigationIndexes[0].Index.ShouldBe(0);
        result.Value.NavigationIndexes.ShouldAllBe(x => x.Index >= 0 && x.Index < result.Value.TotalCount);
    }

    [Test]
    public async Task ShouldFilterSnapshotRowsBeforePaging()
    {
        await SetupDatabase(
            84002,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );
        var movies = await IDbContext.PlexMovies.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var libraryId = movies[0].PlexLibraryId;
        var setupContext = IDbContext;
        await setupContext.MediaOverviewMovieSnapshots.AddRangeAsync(
            movies.Select((movie, index) => CreateMovieSnapshot(movie.Id, index, libraryId)),
            CancellationToken
        );
        await setupContext.SaveChangesAsync(CancellationToken);
        var genre = new PlexGenre
        {
            Name = "Drama",
            Key = "drama",
            Type = PlexGenreType.Drama,
        };
        await setupContext.PlexGenres.AddAsync(genre, CancellationToken);
        await setupContext.SaveChangesAsync(CancellationToken);
        await setupContext.PlexMovieGenres.AddAsync(
            new PlexMovieGenres(genre.Id, libraryId, movies[0].Id),
            CancellationToken
        );
        await setupContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(
            new GetMediaOverviewMovieCommand(
                new MediaQueryFilter
                {
                    MediaType = PlexMediaType.Movie,
                    PlexLibraryId = libraryId,
                    FilterOfflineMedia = false,
                    FilterOwnedMedia = false,
                    Parameters = new FlexQueryParameters
                    {
                        Page = 1,
                        PageSize = 1,
                        Filter = $"Genres:any:Id:eq:{genre.Id}",
                    },
                }
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(1);
        result.Value.Items.Select(x => x.Id).ShouldBe([movies[0].Id]);
        result.Errors.Count.ShouldBe(0);
        result.Value.Page.ShouldBe(1);
        result.Value.PageSize.ShouldBe(1);
        result.Value.Items[0].Type.ShouldBe(PlexMediaType.Movie);
    }

    [Test]
    public async Task ShouldRejectPageSizeAboveHardMaximumBeforeDatabaseWork()
    {
        // Arrange
        var command = new GetMediaOverviewMovieCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters { Page = 1, PageSize = 101 },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        Mock.Mock<IReaparrDbContextFactory>().Verify(x => x.CreateAsync(), Times.Never());
    }

    [Test]
    public async Task ShouldFallbackOnce_WhenComparisonFilterRequested()
    {
        // Arrange
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.Movie,
            PlexLibraryId = 0,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            ComparisonState = PlexMediaComparisonState.Missing,
            Parameters = new FlexQueryParameters { Page = 1, PageSize = 25 },
        };
        var expected = new PagedMediaQueryResult
        {
            QueryHash = filter.QueryHash,
            Page = 1,
            PageSize = 25,
        };
        Mock.SetupCommand<Result<PagedMediaQueryResult>>(command =>
                command is GetMediaByTypeCommand && ((GetMediaByTypeCommand)command).Filter == filter
            )
            .ReturnsAsync(Result.Ok(expected))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewMovieCommand(filter));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBeSameAs(expected);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IReaparrDbContextFactory>().Verify(x => x.CreateAsync(), Times.Never());
    }

    [Test]
    public async Task ShouldRejectTvShowFilter_WithoutDatabaseAccess()
    {
        // Arrange
        var command = new GetMediaOverviewMovieCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.TvShow,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters { Page = 1, PageSize = 25 },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(0);
        Mock.Mock<IReaparrDbContextFactory>().Verify(x => x.CreateAsync(), Times.Never());
    }

    private static MediaOverviewMovieSnapshot CreateMovieSnapshot(int movieId, int rank, int libraryId) =>
        new()
        {
            PlexMovieId = movieId,
            PlexLibraryId = libraryId,
            TitleRank = rank,
            YearRank = rank,
            AddedAtRank = rank,
            UpdatedAtRank = rank,
            DurationRank = rank,
            MediaSizeRank = rank,
            QualityRank = rank,
        };
}
