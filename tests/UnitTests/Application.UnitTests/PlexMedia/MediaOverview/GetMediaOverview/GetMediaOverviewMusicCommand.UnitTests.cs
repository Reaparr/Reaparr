using FlexQuery.NET.Models;

namespace Reaparr.Application.UnitTests;

public class GetMediaOverviewMusicCommandUnitTests : BaseCommandUnitTest<GetMediaOverviewMusicCommand>
{
    [Test]
    [Arguments("sortIndex:asc")]
    [Arguments("sortIndex:desc")]
    public async Task ShouldReturnFilteredArtistPageInSnapshotOrder_WithFullNavigationAndHierarchy(string sort)
    {
        // Arrange
        await SetupDatabase(84201, config =>
        {
            config.PlexMusicLibraryCount = 2;
            config.MusicArtistCount = 4;
            config.MusicAlbumCount = 2;
            config.MusicTrackCount = 3;
        });
        var dbContext = IDbContext;
        var libraryIds = await dbContext.PlexLibraries.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        libraryIds.Count.ShouldBe(2);
        var artists = await dbContext.PlexArtists.Include(x => x.Albums).ThenInclude(x => x.Tracks)
            .OrderBy(x => x.PlexLibraryId).ThenBy(x => x.Id).ToListAsync(CancellationToken);
        var target = artists.Where(x => x.PlexLibraryId == libraryIds[0]).ToList();
        target.Count.ShouldBe(4);
        artists.Count(x => x.PlexLibraryId == libraryIds[1]).ShouldBe(4);
        target[2].Albums.Count.ShouldBe(2);
        target[2].Albums.Select(x => x.Tracks.Count).ShouldBe([3, 3]);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, true), CancellationToken);
        foreach (var artist in artists)
        {
            var targetIndex = target.FindIndex(x => x.Id == artist.Id);
            var rank = targetIndex switch { 0 => 2, 1 => 0, 2 => 1, _ => 3 };
            var title = targetIndex switch { 0 => "alpha", 1 => "bravo", 2 => "charlie", _ => "control" };
            await dbContext.PlexArtists.Where(x => x.Id == artist.Id).ExecuteUpdateAsync(x => x
                .SetProperty(y => y.SearchTitle, title)
                .SetProperty(y => y.Year, targetIndex == 3 ? 1999 : 2000)
                .SetProperty(y => y.MediaSize, targetIndex >= 0 ? (targetIndex + 1) * 100L : 9000L)
                .SetProperty(y => y.HasThumb, true), CancellationToken);
            dbContext.MediaOverviewMusicArtistSnapshots.Add(new MediaOverviewMusicArtistSnapshot
            {
                PlexArtistId = artist.Id, PlexLibraryId = artist.PlexLibraryId,
                TitleRank = rank, YearRank = rank, AddedAtRank = rank,
                UpdatedAtRank = rank, DurationRank = rank, MediaSizeRank = rank,
            });
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.Music, PlexLibraryId = libraryIds[0],
            FilterOfflineMedia = false, FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters { Page = 2, PageSize = 1, Sort = sort, Filter = "Year:eq:2000" },
        };

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewMusicCommand(filter));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.QueryHash.ShouldBe(filter.QueryHash);
        (result.Value.Page, result.Value.PageSize, result.Value.TotalCount, result.Value.MediaCount).ShouldBe((2, 1, 3, 3));
        (result.Value.MediaSize, result.Value.TotalMediaSize).ShouldBe((600L, 600L));
        result.Value.Items.Select(x => x.Id).ShouldBe([target[2].Id]);
        var item = result.Value.Items.Single();
        (item.Type, item.SortIndex, item.ChildCount, item.GrandChildCount).ShouldBe((PlexMediaType.Music, 2, 2, 6));
        (item.PlexLibraryId, item.PlexServerId, item.PlexApiRatingKey, item.PlexApiMetaDataKey, item.HasThumb)
            .ShouldBe((libraryIds[0], target[2].PlexServerId, target[2].PlexApiRatingKey, target[2].PlexApiMetaDataKey, true));
        (item.SearchTitle, item.MediaSize).ShouldBe(("charlie", 300L));
        item.Qualities.ShouldBeEmpty();
        result.Value.Qualities.ShouldBeEmpty();
        result.Value.NavigationIndexes.Select(x => (x.Label, x.Index)).ShouldBe(sort.EndsWith("asc")
            ? [("B", 0), ("C", 1), ("A", 2)] : [("A", 0), ("C", 1), ("B", 2)]);
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<GetMediaByTypeCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
