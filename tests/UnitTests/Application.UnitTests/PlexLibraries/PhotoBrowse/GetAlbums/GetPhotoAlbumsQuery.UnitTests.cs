namespace Reaparr.Application.UnitTests;

public class GetPhotoAlbumsQueryUnitTests : BaseCommandUnitTest<GetPhotoAlbumsQueryCommand>
{
    [Test]
    public async Task ShouldReturnScopedAlbumsWithUniqueTotalsAndNaturalTieOrdering()
    {
        // Arrange
        await SetupDatabase(
            71001,
            config =>
            {
                config.PlexPhotoLibraryCount = 2;
                config.PhotoAlbumCount = 3;
                config.PhotoCount = 3;
                config.PhotoClipCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var albums = await dbContext
            .PlexPhotoAlbums.Where(x => x.PlexLibraryId == libraries[0].Id)
            .OrderBy(x => x.Title)
            .ThenBy(x => x.Id)
            .ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        albums.Count.ShouldBe(3);
        await dbContext
            .PlexPhotoAlbums.Where(x => x.PlexLibraryId == libraries[0].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Title, "Album"), CancellationToken);
        albums = await dbContext
            .PlexPhotoAlbums.Where(x => x.PlexLibraryId == libraries[0].Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync<PhotoAlbumPageDTO>(
            new GetPhotoAlbumsQueryCommand(libraries[0].Id, 1, 2, SortType.Title)
        );
        var descending = await TestHandlerExecuteAsync<PhotoAlbumPageDTO>(
            new GetPhotoAlbumsQueryCommand(libraries[0].Id, 1, 2, SortType.Title, true)
        );
        var invalidSort = await TestHandlerExecuteAsync<PhotoAlbumPageDTO>(
            new GetPhotoAlbumsQueryCommand(libraries[0].Id, 1, 2, (SortType)999)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Page.ShouldBe(1);
        result.Value.PageSize.ShouldBe(2);
        result.Value.TotalCount.ShouldBe(albums.Count);
        result.Value.Items.Select(x => x.Id).ShouldBe(albums.Take(2).Select(x => x.Id));
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId == libraries[0].Id);
        descending.IsSuccess.ShouldBeTrue();
        descending.Errors.Count.ShouldBe(0);
        descending.Value.Items.Select(x => x.Id).ShouldBe(albums.OrderByDescending(x => x.Id).Take(2).Select(x => x.Id));
        invalidSort.IsFailed.ShouldBeTrue();
        invalidSort.Errors.Count.ShouldBe(2);
        invalidSort.Has400BadRequestError().ShouldBeTrue();
        result.Value.TotalAssetBytes.ShouldBe(
            await dbContext
                .PlexPhotoMediaData.Where(x => x.PlexLibraryId == libraries[0].Id)
                .Where(x => x.AlbumMemberships.Any(m => m.PlexPhotoAlbum!.PlexLibraryId == libraries[0].Id))
                .SumAsync(x => x.Size, CancellationToken)
        );
    }

    [Test]
    [Arguments(SortType.Title, false, new[] { 1, 3, 2, 0 })]
    [Arguments(SortType.Title, true, new[] { 0, 2, 3, 1 })]
    [Arguments(SortType.AddedAt, false, new[] { 1, 0, 3, 2 })]
    [Arguments(SortType.AddedAt, true, new[] { 2, 3, 0, 1 })]
    [Arguments(SortType.UpdatedAt, false, new[] { 0, 2, 1, 3 })]
    [Arguments(SortType.UpdatedAt, true, new[] { 3, 1, 2, 0 })]
    [Arguments(SortType.Duration, false, new[] { 0, 3, 1, 2 })]
    [Arguments(SortType.Duration, true, new[] { 2, 1, 3, 0 })]
    [Arguments(SortType.MediaSize, false, new[] { 2, 0, 3, 1 })]
    [Arguments(SortType.MediaSize, true, new[] { 1, 3, 0, 2 })]
    public async Task ShouldApplyAggregateAndDateSortFieldsBeforePaging(
        SortType sort,
        bool descending,
        int[] expectedOrder
    )
    {
        // Arrange
        var (libraryId, albums) = await ArrangeSortableAlbumsAsync();

        // Act
        var firstPage = await TestHandlerExecuteAsync<PhotoAlbumPageDTO>(
            new GetPhotoAlbumsQueryCommand(libraryId, 1, 2, sort, descending)
        );
        var secondPage = await TestHandlerExecuteAsync<PhotoAlbumPageDTO>(
            new GetPhotoAlbumsQueryCommand(libraryId, 2, 2, sort, descending)
        );

        // Assert
        firstPage.IsSuccess.ShouldBeTrue();
        firstPage.Errors.Count.ShouldBe(0);
        secondPage.IsSuccess.ShouldBeTrue();
        secondPage.Errors.Count.ShouldBe(0);
        firstPage.Value.TotalCount.ShouldBe(4);
        secondPage.Value.TotalCount.ShouldBe(4);
        firstPage.Value.Items.Select(x => x.Id).ShouldBe(expectedOrder.Take(2).Select(index => albums[index].Id));
        secondPage.Value.Items.Select(x => x.Id).ShouldBe(expectedOrder.Skip(2).Select(index => albums[index].Id));
    }

    private async Task<(int LibraryId, List<PlexPhotoAlbum> Albums)> ArrangeSortableAlbumsAsync()
    {
        await SetupDatabase(71013, config =>
        {
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 4;
            config.PhotoCount = 1;
        });
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum);
        var albums = await dbContext.PlexPhotoAlbums.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var assets = await dbContext.PlexPhotoAlbumMemberships
            .Where(x => x.PlexPhotoAlbum!.PlexLibraryId == library.Id)
            .OrderBy(x => x.PlexPhotoAlbumId).Select(x => x.PlexPhotoMediaData!).ToListAsync(CancellationToken);
        albums.Count.ShouldBe(4);
        assets.Count.ShouldBe(4);
        var date = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var values = new (int? Duration, long Size, DateTime AddedAt, DateTime? UpdatedAt)[]
        {
            (null, 200, date.AddDays(1), null),
            (10, 400, date, date.AddDays(1)),
            (10, 100, date.AddDays(3), date),
            (null, 300, date.AddDays(2), date.AddDays(2)),
        };
        var titles = new[] { "Zulu", "Alpha", "Charlie", "Bravo" };
        for (var index = 0; index < albums.Count; index++)
        {
            var value = values[index];
            await dbContext.PlexPhotoAlbums.Where(x => x.Id == albums[index].Id).ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.Title, titles[index])
                    .SetProperty(x => x.AddedAt, value.AddedAt).SetProperty(x => x.UpdatedAt, value.UpdatedAt),
                CancellationToken
            );
            await dbContext.PlexPhotoMediaData.Where(x => x.Id == assets[index].Id).ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.Duration, value.Duration).SetProperty(x => x.Size, value.Size),
                CancellationToken
            );
        }
        return (library.Id, albums);
    }

    [Test]
    public async Task ShouldKeepOfflineLinkedLibrariesAndExcludeLibrariesAfterAccountLinkRemoval()
    {
        // Arrange
        await SetupDatabase(
            71008,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexPhotoLibraryCount = 2;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var allAlbums = await dbContext.PlexPhotoAlbums.CountAsync(CancellationToken);
        var serverId = libraries[0].PlexServerId;
        await dbContext
            .PlexServerStatuses.Where(x => x.PlexServerId == serverId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsSuccessful, false), CancellationToken);

        // Act: offline status is not an eligibility filter.
        var offlineResult = await TestHandlerExecuteAsync<PhotoAlbumPageDTO>(new GetPhotoAlbumsQueryCommand(null));

        // Assert
        offlineResult.IsSuccess.ShouldBeTrue();
        offlineResult.Value.TotalCount.ShouldBe(allAlbums);

        // Act: removing the library link removes only that library from aggregate visibility.
        await dbContext
            .PlexAccountLibraries.Where(x => x.PlexLibraryId == libraries[1].Id)
            .ExecuteDeleteAsync(CancellationToken);
        var linkedResult = await TestHandlerExecuteAsync<PhotoAlbumPageDTO>(new GetPhotoAlbumsQueryCommand(null));
        var rejectedResult = await TestHandlerExecuteAsync<PhotoAlbumPageDTO>(
            new GetPhotoAlbumsQueryCommand(libraries[1].Id)
        );

        // Assert
        linkedResult.IsSuccess.ShouldBeTrue();
        linkedResult.Value.Items.ShouldAllBe(x => x.PlexLibraryId == libraries[0].Id);
        linkedResult.Value.TotalCount.ShouldBe(
            await dbContext.PlexPhotoAlbums.CountAsync(x => x.PlexLibraryId == libraries[0].Id, CancellationToken)
        );
        rejectedResult.Has404NotFoundError().ShouldBeTrue();
    }
}
