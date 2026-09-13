using FlexQuery.NET.Models;

namespace Reaparr.Application.UnitTests;

public class GetTvShowMediaOverviewCommandUnitTests : BaseCommandUnitTest<GetMediaOverviewTvShowCommand>
{
    [Test]
    public async Task ShouldReturnBoundedOrderedTvShowPage_WithNestedQualitiesAndMetadata()
    {
        // Arrange
        await SetupDatabase(
            84101,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 3;
            }
        );
        var dbContext = IDbContext;
        var tvShows = await dbContext
            .PlexTvShows.Include(x => x.Qualities)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var libraryId = tvShows[0].PlexLibraryId;
        await dbContext.MediaOverviewTvShowSnapshots.AddRangeAsync(
            tvShows.Select((show, index) => CreateSnapshot(show.Id, tvShows.Count - index - 1, libraryId)),
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var filter = CreateFilter(libraryId, page: 2, pageSize: 1, sort: "year:desc");

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewTvShowCommand(filter));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.QueryHash.ShouldBe(filter.QueryHash);
        result.Value.Page.ShouldBe(2);
        result.Value.PageSize.ShouldBe(1);
        result.Value.TotalCount.ShouldBe(3);
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items[0].Id.ShouldBe(tvShows[1].Id);
        result.Value.Items[0].Type.ShouldBe(PlexMediaType.TvShow);
        result.Value.Items[0].SortIndex.ShouldBe(2);
        result.Value.Items[0].GrandChildCount.ShouldBe(tvShows[1].GrandChildCount);
        result
            .Value.Items[0]
            .Qualities.Select(x => x.Quality)
            .ShouldBe(tvShows[1].Qualities.OrderBy(x => x.Quality).Select(x => x.Quality));
        result.Value.Qualities.ShouldBe(
            result.Value.Items[0].Qualities.Select(x => x.Quality.ToId()).Distinct().OrderBy(x => x)
        );
    }

    [Test]
    public async Task ShouldFallbackOnce_WhenSnapshotIsMissing()
    {
        // Arrange
        await SetupDatabase(
            84102,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
            }
        );
        var libraryId = await IDbContext.PlexTvShows.Select(x => x.PlexLibraryId).SingleAsync(CancellationToken);
        var filter = CreateFilter(libraryId);
        var expected = new PagedMediaQueryResult
        {
            QueryHash = filter.QueryHash,
            Page = 1,
            PageSize = 25,
            TotalCount = 1,
        };
        Mock.SetupCommand<Result<PagedMediaQueryResult>>(command =>
                command is GetMediaByTypeCommand && ((GetMediaByTypeCommand)command).Filter == filter
            )
            .ReturnsAsync(Result.Ok(expected))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewTvShowCommand(filter));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBeSameAs(expected);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldFallbackOnce_WhenComparisonFilterRequested()
    {
        // Arrange
        var filter = CreateFilter(0) with
        {
            ComparisonState = PlexMediaComparisonState.Missing,
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
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewTvShowCommand(filter));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(expected);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IReaparrDbContextFactory>().Verify(x => x.CreateAsync(), Times.Never());
    }

    [Test]
    public async Task ShouldRejectMovieFilter_WithoutDatabaseAccess()
    {
        // Arrange
        var command = new GetMediaOverviewTvShowCommand(CreateFilter(0) with { MediaType = PlexMediaType.Movie });

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(0);
        Mock.Mock<IReaparrDbContextFactory>().Verify(x => x.CreateAsync(), Times.Never());
    }

    private static MediaQueryFilter CreateFilter(int libraryId, int page = 1, int pageSize = 25, string? sort = null) =>
        new()
        {
            MediaType = PlexMediaType.TvShow,
            PlexLibraryId = libraryId,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters
            {
                Page = page,
                PageSize = pageSize,
                Sort = sort,
            },
        };

    private static MediaOverviewTvShowSnapshot CreateSnapshot(int tvShowId, int rank, int libraryId) =>
        new()
        {
            PlexTvShowId = tvShowId,
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
