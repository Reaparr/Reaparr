using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;

namespace Reaparr.Data.UnitTests;

public class GetMovieMediaByTypeCommandQualityFiltersUnitTests : BaseCommandUnitTest<GetMovieMediaByTypeCommand>
{
    [Test]
    public async Task ShouldSelectOnlyMatchingVideoQuality_WhenFilteringTheRootQualityCollection()
    {
        await SetupDatabase(
            70351,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );
        var dbContext = IDbContext;
        var roots = await dbContext
            .PlexMovies.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexLibraryId })
            .ToListAsync(CancellationToken);
        roots.Count.ShouldBe(2);
        var selectedId = roots[0].Id;
        var libraryId = roots[0].PlexLibraryId;
        await dbContext.PlexMovieData.ExecuteUpdateAsync(
            update =>
                update.SetProperty(
                    x => x.VideoQuality,
                    x => x.PlexMovieId == selectedId ? VideoQuality.FullHD : VideoQuality.HD
                ),
            CancellationToken
        );
        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = libraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 1,
                    PageSize = 10,
                    Filter = "MediaDataList:any:VideoQuality:eq:FullHD",
                },
            }
        );
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ApplyComparisonStateCommand>(state =>
                        state.MediaType == PlexMediaType.Movie
                        && state.PlexLibraryId == libraryId
                        && state.Items.Count == 1
                        && state.Items[0].Id == selectedId
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(1);
        result.Value.Items.Select(x => x.Id).ShouldBe([selectedId]);
        result.Value.Qualities.ShouldBe([7]);
        Mock.Mock<ICommandExecutor>().Verify();
    }
}

public class GetTvShowMediaByTypeCommandQualityFiltersUnitTests : BaseCommandUnitTest<GetTvShowMediaByTypeCommand>
{
    [Test]
    public async Task ShouldSelectOnlyMatchingVideoQuality_WhenFilteringTheRootQualityCollection()
    {
        await SetupDatabase(
            70351,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 2;
            }
        );
        var dbContext = IDbContext;
        var roots = await dbContext
            .PlexTvShows.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexLibraryId })
            .ToListAsync(CancellationToken);
        roots.Count.ShouldBe(2);
        var selectedId = roots[0].Id;
        var libraryId = roots[0].PlexLibraryId;
        await dbContext.PlexTvShowMediaQualities.ExecuteDeleteAsync(CancellationToken);
        dbContext.PlexTvShowMediaQualities.AddRange(
            new PlexTvShowMediaQuality
            {
                PlexTvShowId = selectedId,
                PlexLibraryId = libraryId,
                VideoQuality = VideoQuality.FullHD,
            },
            new PlexTvShowMediaQuality
            {
                PlexTvShowId = roots[1].Id,
                PlexLibraryId = libraryId,
                VideoQuality = VideoQuality.HD,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var command = new GetTvShowMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.TvShow,
                PlexLibraryId = libraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 1,
                    PageSize = 10,
                    Filter = "Qualities:any:VideoQuality:eq:FullHD",
                },
            }
        );
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ApplyComparisonStateCommand>(state =>
                        state.MediaType == PlexMediaType.TvShow
                        && state.PlexLibraryId == libraryId
                        && state.Items.Count == 1
                        && state.Items[0].Id == selectedId
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(1);
        result.Value.Items.Select(x => x.Id).ShouldBe([selectedId]);
        result.Value.Qualities.ShouldBe([7]);
        Mock.Mock<ICommandExecutor>().Verify();
    }
}

public class GetOtherVideoMediaByTypeCommandQualityFiltersUnitTests
    : BaseCommandUnitTest<GetOtherVideoMediaByTypeCommand>
{
    [Test]
    public async Task ShouldSelectOnlyMatchingVideoQuality_WhenFilteringTheRootQualityCollection()
    {
        await SetupDatabase(
            70351,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoCount = 2;
            }
        );
        var dbContext = IDbContext;
        var roots = await dbContext
            .PlexOtherVideos.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexLibraryId })
            .ToListAsync(CancellationToken);
        roots.Count.ShouldBe(2);
        var selectedId = roots[0].Id;
        var libraryId = roots[0].PlexLibraryId;
        await dbContext.PlexOtherVideoData.ExecuteUpdateAsync(
            update =>
                update.SetProperty(
                    x => x.VideoQuality,
                    x => x.PlexOtherVideoId == selectedId ? VideoQuality.FullHD : VideoQuality.HD
                ),
            CancellationToken
        );
        var command = new GetOtherVideoMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.OtherVideos,
                PlexLibraryId = libraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 1,
                    PageSize = 10,
                    Filter = "MediaDataList:any:VideoQuality:eq:FullHD",
                },
            }
        );

        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(1);
        result.Value.Items.Select(x => x.Id).ShouldBe([selectedId]);
        result.Value.Qualities.ShouldBe([7]);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
