using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;

namespace Reaparr.Data.UnitTests;

public class GetTvShowMediaByTypeCommandUnitTests : BaseCommandUnitTest<GetTvShowMediaByTypeCommand>
{
    public GetTvShowMediaByTypeCommandUnitTests()
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ICommand<Result>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
    }

    [Test]
    public async Task ShouldReturnOnlyTvShowsFromSpecificLibrary_WhenPlexLibraryIdIsSet()
    {
        // Arrange
        await SetupDatabase(
            70002,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexTvShowLibraryCount = 2;
                cfg.TvShowCount = 6;
            }
        );

        var dbContext = IDbContext;
        var targetLibraryId = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.TvShow)
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);

        var command = new GetTvShowMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.TvShow,
                PlexLibraryId = targetLibraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        var projectedLibraryIds = new List<int?>();
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<Result>, CancellationToken>(
                (x, _) => projectedLibraryIds.Add(((ApplyComparisonStateCommand)x).PlexLibraryId)
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId == targetLibraryId);
        projectedLibraryIds.ShouldBe([targetLibraryId]);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldProjectTvShowsInOneCommand_WhenPlexLibraryIdIsZero()
    {
        // Arrange
        await SetupDatabase(
            70037,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexTvShowLibraryCount = 3;
                cfg.TvShowCount = 2;
            }
        );

        var command = new GetTvShowMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.TvShow,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        var projectedLibraryIds = new List<int?>();
        var projectedItemLibraryIds = new List<int>();
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<Result>, CancellationToken>(
                (x, _) =>
                {
                    var applyCommand = (ApplyComparisonStateCommand)x;
                    projectedLibraryIds.Add(applyCommand.PlexLibraryId);
                    projectedItemLibraryIds.AddRange(applyCommand.Items.Select(item => item.PlexLibraryId));
                    applyCommand.Items.ShouldAllBe(item => item.PlexLibraryId > 0);
                }
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Items.Select(x => x.PlexLibraryId).Distinct().Count().ShouldBe(3);
        projectedLibraryIds.ShouldBe([null]);
        projectedItemLibraryIds.Distinct().Count().ShouldBe(3);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldAssignSortIndexesSequentiallyForTvShows_WhenResultsReturned()
    {
        // Arrange
        await SetupDatabase(
            70013,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexTvShowLibraryCount = 1;
                cfg.TvShowCount = 6;
            }
        );

        var command = new GetTvShowMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.TvShow,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(6);
        result.Value.MediaCount.ShouldBe(6);
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.Select(x => x.SortIndex).ShouldBe(Enumerable.Range(1, result.Value.Items.Count));
    }

    [Test]
    public async Task ShouldReturnOnlyTvShows_WhenTvShowTypeRequestedAndBothMediaTypesExist()
    {
        // Arrange
        await SetupDatabase(
            70018,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.PlexTvShowLibraryCount = 1;
                cfg.MovieCount = 5;
                cfg.TvShowCount = 5;
            }
        );

        var command = new GetTvShowMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.TvShow,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.ShouldAllBe(x => x.Type == PlexMediaType.TvShow);
    }

    [Test]
    public async Task ShouldSortTvShowsByVideoQualityAscending_WhenVideoQualitySortFieldIsUsed()
    {
        // Arrange
        await SetupDatabase(
            70032,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexTvShowLibraryCount = 1;
                cfg.TvShowCount = 4;
            }
        );

        var dbContext = IDbContext;
        var tvShowIds = await dbContext.PlexTvShows.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);

        await SetTvShowQualitySortTestDataAsync(dbContext, tvShowIds);

        var command = new GetTvShowMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.TvShow,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = 10,
                    Sort = "videoQuality:asc,Year:desc",
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Items.Select(x => x.Title).ShouldBe(["SD Show (2021)", "SD Show (2000)", "HD Show", "4K Show"]);
        result.Value.NavigationIndexes.Select(x => new { x.Label, x.Index }).ShouldBe([
            new { Label = "480", Index = 0 },
            new { Label = "1080", Index = 2 },
            new { Label = "2160", Index = 3 },
        ]);
    }

    [Test]
    public async Task ShouldSortTvShowsByVideoQualityDescending_WhenVideoQualitySortFieldIsUsed()
    {
        // Arrange
        await SetupDatabase(
            70033,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexTvShowLibraryCount = 1;
                cfg.TvShowCount = 4;
            }
        );

        var dbContext = IDbContext;
        var tvShowIds = await dbContext.PlexTvShows.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);

        await SetTvShowQualitySortTestDataAsync(dbContext, tvShowIds);

        var command = new GetTvShowMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.TvShow,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = 10,
                    Sort = "videoQuality:desc,Year:asc",
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Items.Select(x => x.Title).ShouldBe(["4K Show", "HD Show", "SD Show (2000)", "SD Show (2021)"]);
        result.Value.NavigationIndexes.Select(x => new { x.Label, x.Index }).ShouldBe([
            new { Label = "2160", Index = 0 },
            new { Label = "1080", Index = 1 },
            new { Label = "480", Index = 2 },
        ]);
    }

    [Test]
    [Arguments(null, null)]
    [Arguments("Title:asc", PlexMediaComparisonState.Owned)]
    [Arguments("comparisonState:desc,Title:asc", null)]
    public async Task ShouldProjectTvShowQualitiesInVideoQualityOrderAcrossQueryBranches(
        string? sort,
        PlexMediaComparisonState? comparisonState
    )
    {
        // Arrange
        await SetupDatabase(73105, config =>
        {
            config.PlexServerCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.TvShowCount = 1;
        });
        var dbContext = IDbContext;
        var tvShow = await dbContext.PlexTvShows.SingleAsync(CancellationToken);
        await dbContext.PlexTvShowMediaQualities.ExecuteDeleteAsync(CancellationToken);
        dbContext.PlexTvShowMediaQualities.AddRange(
            new PlexTvShowMediaQuality { PlexLibraryId = tvShow.PlexLibraryId, PlexTvShowId = tvShow.Id, VideoQuality = VideoQuality.UHD_4K },
            new PlexTvShowMediaQuality { PlexLibraryId = tvShow.PlexLibraryId, PlexTvShowId = tvShow.Id, VideoQuality = VideoQuality.SD }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var persisted = await dbContext.PlexTvShowMediaQualities.OrderBy(x => x.Id)
            .Select(x => x.VideoQuality).ToListAsync(CancellationToken);
        persisted.ShouldBe([VideoQuality.UHD_4K, VideoQuality.SD]);
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.TvShow,
            PlexLibraryId = tvShow.PlexLibraryId,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            ComparisonState = comparisonState,
            Parameters = new FlexQueryParameters { Page = 1, PageSize = 2, Sort = sort },
        };
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<ApplyComparisonStateCommand>(c =>
                c.MediaType == PlexMediaType.TvShow && c.PlexLibraryId == tvShow.PlexLibraryId
                && c.Items.Count == 1 && c.Items[0].Id == tvShow.Id), CancellationToken))
            .Callback<ICommand<Result>, CancellationToken>((command, _) =>
                ((ApplyComparisonStateCommand)command).Items[0].SetComparisonState(PlexMediaComparisonState.Owned))
            .ReturnsAsync(Result.Ok()).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetTvShowMediaByTypeCommand(filter));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(1);
        var item = result.Value.Items.Single();
        (item.Id, item.Type).ShouldBe((tvShow.Id, PlexMediaType.TvShow));
        item.Qualities.Select(x => (x.VideoQuality, x.DataId, x.MediaId, x.MediaDataType)).ShouldBe([
            (VideoQuality.SD, -1, -1, PlexMediaType.TvShow),
            (VideoQuality.UHD_4K, -1, -1, PlexMediaType.TvShow),
        ]);
        result.Value.Qualities.ShouldBe([VideoQuality.SD.ToId(), VideoQuality.UHD_4K.ToId()]);
        Mock.Mock<ICommandExecutor>().Verify();
    }


    private async Task SetTvShowQualitySortTestDataAsync(IReaparrDbContext dbContext, IReadOnlyList<int> tvShowIds)
    {
        await dbContext
            .PlexTvShows.Where(x => x.Id == tvShowIds[0])
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.Title, "SD Show (2000)")
                        .SetProperty(y => y.Year, 2000)
                        .SetProperty(y => y.VideoQuality, VideoQuality.SD),
                CancellationToken
            );

        await dbContext
            .PlexTvShows.Where(x => x.Id == tvShowIds[1])
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.Title, "4K Show")
                        .SetProperty(y => y.Year, 2020)
                        .SetProperty(y => y.VideoQuality, VideoQuality.UHD_4K),
                CancellationToken
            );

        await dbContext
            .PlexTvShows.Where(x => x.Id == tvShowIds[2])
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.Title, "HD Show")
                        .SetProperty(y => y.Year, 2010)
                        .SetProperty(y => y.VideoQuality, VideoQuality.FullHD),
                CancellationToken
            );

        await dbContext
            .PlexTvShows.Where(x => x.Id == tvShowIds[3])
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.Title, "SD Show (2021)")
                        .SetProperty(y => y.Year, 2021)
                        .SetProperty(y => y.VideoQuality, VideoQuality.SD),
                CancellationToken
            );
    }
}
