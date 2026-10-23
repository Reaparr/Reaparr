using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;

namespace Reaparr.Data.UnitTests;

public class GetMediaByTypeCommandHandlerQualityFiltersUnitTests : BaseCommandUnitTest<GetMediaByTypeCommand>
{
    public GetMediaByTypeCommandHandlerQualityFiltersUnitTests()
    {
        SetupMediaByTypeCommandDispatch();
    }

    [Test]
    [Arguments(PlexMediaType.Movie, "MediaDataList")]
    [Arguments(PlexMediaType.TvShow, "Qualities")]
    [Arguments(PlexMediaType.OtherVideos, "MediaDataList")]
    public async Task ShouldSelectOnlyMatchingVideoQuality_WhenFilteringTheRootQualityCollection(
        PlexMediaType mediaType,
        string collection
    )
    {
        // Arrange
        await SetupDatabase(70351, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = mediaType == PlexMediaType.Movie ? 1 : 0;
            config.PlexTvShowLibraryCount = mediaType == PlexMediaType.TvShow ? 1 : 0;
            config.PlexOtherVideoLibraryCount = mediaType == PlexMediaType.OtherVideos ? 1 : 0;
            config.MovieCount = 2;
            config.TvShowCount = 2;
            config.OtherVideoCount = 2;
        });
        var dbContext = IDbContext;
        var roots = mediaType switch
        {
            PlexMediaType.Movie => await dbContext
                .PlexMovies.OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.PlexLibraryId })
                .ToListAsync(CancellationToken),
            PlexMediaType.TvShow => await dbContext
                .PlexTvShows.OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.PlexLibraryId })
                .ToListAsync(CancellationToken),
            _ => await dbContext
                .PlexOtherVideos.OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.PlexLibraryId })
                .ToListAsync(CancellationToken),
        };
        roots.Count.ShouldBe(2);
        var selectedId = roots[0].Id;
        var libraryId = roots[0].PlexLibraryId;
        if (mediaType == PlexMediaType.TvShow)
        {
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
        }
        else if (mediaType == PlexMediaType.Movie)
        {
            await dbContext.PlexMovieData.ExecuteUpdateAsync(
                update => update.SetProperty(
                    x => x.VideoQuality,
                    x => x.PlexMovieId == selectedId ? VideoQuality.FullHD : VideoQuality.HD
                ),
                CancellationToken
            );
        }
        else
        {
            await dbContext.PlexOtherVideoData.ExecuteUpdateAsync(
                update => update.SetProperty(
                    x => x.VideoQuality,
                    x => x.PlexOtherVideoId == selectedId ? VideoQuality.FullHD : VideoQuality.HD
                ),
                CancellationToken
            );
        }
        var command = new GetMediaByTypeCommand
        {
            Filter = new MediaQueryFilter
            {
                MediaType = mediaType,
                PlexLibraryId = libraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 1,
                    PageSize = 10,
                    Filter = $"{collection}:any:VideoQuality:eq:FullHD",
                },
            },
        };
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ApplyComparisonStateCommand>(state =>
                        state.MediaType == mediaType
                        && state.PlexLibraryId == libraryId
                        && state.Items.Count == 1
                        && state.Items[0].Id == selectedId
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(mediaType.SupportsComparison() ? Times.Once() : Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(1);
        result.Value.Items.Select(x => x.Id).ShouldBe([selectedId]);
        result.Value.Qualities.ShouldBe([7]);
        Mock.Mock<ICommandExecutor>().Verify();
        if (!mediaType.SupportsComparison())
            Mock.Mock<ICommandExecutor>().Verify(
                x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }
}
