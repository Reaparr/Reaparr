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
