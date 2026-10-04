using FlexQuery.NET.Models;

namespace Reaparr.Application.UnitTests;

public class GetMediaOverviewPhotoCommandUnitTests : BaseCommandUnitTest<GetMediaOverviewPhotoCommand>
{
    [Test]
    [Arguments("sortIndex:asc")]
    [Arguments("sortIndex:desc")]
    public async Task ShouldReturnFilteredPhotoAlbumInItems_WithSnapshotNavigationAndPhotoCounts(string sort)
    {
        // Arrange
        await SetupDatabase(84202, config =>
        {
            config.PlexPhotoLibraryCount = 2;
            config.PhotoAlbumCount = 4;
            config.PhotoCount = 2;
            config.PhotoClipCount = 1;
        });
        var dbContext = IDbContext;
        var libraryIds = await dbContext.PlexLibraries.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        libraryIds.Count.ShouldBe(2);
        (await dbContext.PlexLibraries.OrderBy(x => x.Id).Select(x => x.Type).ToListAsync(CancellationToken))
            .ShouldBe([PlexMediaType.PhotoAlbum, PlexMediaType.PhotoAlbum]);
        var albums = await dbContext.PlexPhotoAlbums.Include(x => x.Photos)
            .OrderBy(x => x.PlexLibraryId).ThenBy(x => x.Id).ToListAsync(CancellationToken);
        var target = albums.Where(x => x.PlexLibraryId == libraryIds[0]).ToList();
        target.Count.ShouldBe(4);
        albums.Count(x => x.PlexLibraryId == libraryIds[1]).ShouldBe(4);
        target[2].Photos.Count.ShouldBe(3);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, true), CancellationToken);
        foreach (var album in albums)
        {
            var targetIndex = target.FindIndex(x => x.Id == album.Id);
            var rank = targetIndex switch { 0 => 2, 1 => 0, 2 => 1, _ => 3 };
            var title = targetIndex switch { 0 => "alpha", 1 => "bravo", 2 => "charlie", _ => "control" };
            await dbContext.PlexPhotoAlbums.Where(x => x.Id == album.Id).ExecuteUpdateAsync(x => x
                .SetProperty(y => y.SearchTitle, title)
                .SetProperty(y => y.Year, targetIndex == 3 ? 1999 : 2000)
                .SetProperty(y => y.MediaSize, targetIndex >= 0 ? (targetIndex + 1) * 100L : 9000L)
                .SetProperty(y => y.HasThumb, true), CancellationToken);
            dbContext.MediaOverviewPhotoAlbumSnapshots.Add(new MediaOverviewPhotoAlbumSnapshot
            {
                PlexPhotoAlbumId = album.Id, PlexLibraryId = album.PlexLibraryId,
                TitleRank = rank, YearRank = rank, AddedAtRank = rank,
                UpdatedAtRank = rank, DurationRank = rank, MediaSizeRank = rank,
            });
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.PhotoAlbum, PlexLibraryId = libraryIds[0],
            FilterOfflineMedia = false, FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters { Page = 2, PageSize = 1, Sort = sort, Filter = "Year:eq:2000" },
        };

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(new GetMediaOverviewPhotoCommand(filter));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.QueryHash.ShouldBe(filter.QueryHash);
        (result.Value.Page, result.Value.PageSize, result.Value.TotalCount, result.Value.MediaCount).ShouldBe((2, 1, 3, 3));
        (result.Value.MediaSize, result.Value.TotalMediaSize).ShouldBe((600L, 600L));
        result.Value.Items.Select(x => x.Id).ShouldBe([target[2].Id]);
        var item = result.Value.Items.Single();
        (item.Type, item.SortIndex, item.ChildCount, item.GrandChildCount).ShouldBe((PlexMediaType.PhotoAlbum, 2, 3, 0));
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
