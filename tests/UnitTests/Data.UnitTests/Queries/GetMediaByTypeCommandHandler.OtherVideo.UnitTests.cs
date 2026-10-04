using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;

namespace Reaparr.Data.UnitTests;

public class GetMediaByTypeCommandHandlerOtherVideoUnitTests : BaseCommandUnitTest<GetMediaByTypeCommand>
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReturnExactScopedOtherVideoPage_WhenFilteringAndSortingCanonicalMedia(bool sortByQuality)
    {
        // Arrange
        await SetupDatabase(
            73101,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexOtherVideoLibraryCount = 2;
                cfg.OtherVideoCount = 6;
            }
        );

        var dbContext = IDbContext;
        var libraryIds = await dbContext.PlexLibraries.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        libraryIds.Count.ShouldBe(2);
        var targetLibraryId = libraryIds[0];
        var controlLibraryId = libraryIds[1];
        var videos = await dbContext.PlexOtherVideos.AsTracking().ToListAsync(CancellationToken);
        var targetVideos = videos.Where(x => x.PlexLibraryId == targetLibraryId).OrderBy(x => x.Id).ToList();
        var controlVideos = videos.Where(x => x.PlexLibraryId == controlLibraryId).OrderBy(x => x.Id).ToList();
        targetVideos.Count.ShouldBe(6);
        controlVideos.Count.ShouldBe(6);
        int[] years = [1999, 2000, 2000, 2001, 2001, 2002];
        foreach (var libraryVideos in new[] { targetVideos, controlVideos })
        {
            for (var i = 0; i < libraryVideos.Count; i++)
            {
                var video = libraryVideos[i];
                video.Year = years[i];
                video.Duration = (i + 1) * 100;
                video.MediaSize = (i + 1) * 100L;
                video.Title = $"{video.PlexLibraryId}-root-{i}";
                video.HasThumb = i % 2 == 0;
                video.Quality = VideoQuality.FullHD;
            }
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.ClearChangeTracker();

        var expectedVideos = new[] { targetVideos[4], targetVideos[3] };
        var qualityRows = await dbContext.PlexOtherVideoData
            .Where(x => x.PlexLibraryId == targetLibraryId &&
                (x.PlexOtherVideoId == expectedVideos[0].Id || x.PlexOtherVideoId == expectedVideos[1].Id))
            .OrderBy(x => x.PlexOtherVideoId).ThenBy(x => x.Id)
            .Select(x => new { x.PlexOtherVideoId, x.Id, x.Quality, x.Type })
            .ToListAsync(CancellationToken);
        qualityRows.Count.ShouldBe(2);

        var command = new GetMediaByTypeCommand
        {
            Filter = new MediaQueryFilter
            {
                MediaType = PlexMediaType.OtherVideos,
                PlexLibraryId = targetLibraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 2,
                    PageSize = 2,
                    Filter = "Year:gte:2000",
                    Sort = sortByQuality ? "quality:desc,Year:asc,Duration:desc" : "Year:asc,Duration:desc",
                },
            },
        };

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var response = result.Value;
        response.QueryHash.ShouldBe(command.Filter.QueryHash);
        response.Page.ShouldBe(2);
        response.PageSize.ShouldBe(2);
        response.TotalCount.ShouldBe(5);
        response.MediaCount.ShouldBe(5);
        response.MediaSize.ShouldBe(2000);
        response.TotalMediaSize.ShouldBe(2000);
        response.MovieCount.ShouldBe(0);
        response.TvShowCount.ShouldBe(0);
        response.SeasonCount.ShouldBe(0);
        response.EpisodeCount.ShouldBe(0);
        response.TotalMovieCount.ShouldBe(0);
        response.TotalTvShowCount.ShouldBe(0);
        response.TotalSeasonCount.ShouldBe(0);
        response.TotalEpisodeCount.ShouldBe(0);
        response.Items.Select(x => x.Id).ShouldBe(expectedVideos.Select(x => x.Id));
        response.Items.Select(x => x.SortIndex).ShouldBe([3, 4]);
        response.Items.Select(x => new
        {
            x.Id, x.Title, x.SearchTitle, x.Year, x.Duration, x.MediaSize,
            x.PlexApiRatingKey, x.PlexApiMetaDataKey, x.AddedAt, x.UpdatedAt,
            x.PlexLibraryId, x.PlexServerId, x.Type, x.HasThumb,
        }).ShouldBe(expectedVideos.Select(x => new
        {
            x.Id, x.Title, x.SearchTitle, x.Year, x.Duration, x.MediaSize,
            x.PlexApiRatingKey, x.PlexApiMetaDataKey, x.AddedAt, x.UpdatedAt,
            x.PlexLibraryId, x.PlexServerId, Type = PlexMediaType.OtherVideos, x.HasThumb,
        }));
        response.Items.Select(x => x.ParentId).ShouldBe(new int?[] { null, null });
        response.Items.Select(x => x.ChildCount).ShouldBe([0, 0]);
        response.Items.Select(x => x.GrandChildCount).ShouldBe([0, 0]);
        response.Items.Select(x => x.ComparisonId).ShouldBe([0, 0]);
        if (sortByQuality)
        {
            response.NavigationIndexes.Select(x => new { x.Label, x.Index }).ShouldBe([
                new { Label = "1080", Index = 0 },
            ]);
        }
        else
        {
            response.NavigationIndexes.Select(x => new { x.Label, x.Index }).ShouldBe([
                new { Label = "2000", Index = 0 },
                new { Label = "2001", Index = 2 },
                new { Label = "2002", Index = 4 },
            ]);
        }
        response.Items.SelectMany(x => x.Qualities).OrderBy(x => x.MediaId).ThenBy(x => x.DataId)
            .Select(x => new { x.MediaId, x.DataId, x.Quality, x.MediaDataType })
            .ShouldBe(qualityRows.Select(x => new
            {
                MediaId = x.PlexOtherVideoId, DataId = x.Id, x.Quality, MediaDataType = x.Type,
            }));
        response.Qualities.ShouldBe(new[] { VideoQuality.FullHD.ToId() });
        response.Roles.ShouldBeEmpty();
        response.Countries.ShouldBeEmpty();
        response.Genres.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
