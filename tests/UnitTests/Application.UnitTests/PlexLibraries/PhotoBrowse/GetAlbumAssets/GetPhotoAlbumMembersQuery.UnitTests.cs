namespace Reaparr.Application.UnitTests;

public class GetPhotoAlbumMembersQueryUnitTests : BaseCommandUnitTest<GetPhotoAlbumMembersQuery>
{
    [Test]
    public async Task ShouldReturnOnlyDistinctMembersForTheRequestedAlbumAndLibrary()
    {
        // Arrange
        await SetupDatabase(
            71005,
            config =>
            {
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 2;
                config.PhotoCount = 3;
                config.PhotoClipCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var album = await dbContext
            .PlexPhotoAlbums.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        var expectedIds = await dbContext
            .PlexPhotoAlbumMemberships.Where(x => x.PlexPhotoAlbumId == album.Id)
            .Select(x => x.PlexPhotoMediaDataId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(CancellationToken);
        expectedIds.Count.ShouldBeGreaterThan(0);
        await dbContext.PlexPhotoMediaData
            .Where(x => expectedIds.Contains(x.Id))
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Title, "Album member"), CancellationToken);
        var inactiveAssetId = expectedIds[0];
        var inactiveRoot = new PlexPhotoFolder
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            RelativePath = string.Empty,
            Title = "Album inactive",
            RootIdentityKey = "path:album-inactive",
            SourceRootPath = "/album-inactive",
            PlexApiLocationId = 9201,
            IsActive = false,
        };
        dbContext.PlexPhotoFolders.Add(inactiveRoot);
        await dbContext.SaveChangesAsync(CancellationToken);
        var inactiveChild = new PlexPhotoFolder
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            ParentId = inactiveRoot.Id,
            RelativePath = "album-inactive/child",
            Title = "Album inactive child",
        };
        dbContext.PlexPhotoFolders.Add(inactiveChild);
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext
            .PlexPhotoMediaData.Where(x => x.Id == inactiveAssetId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.PlexPhotoFolderId, inactiveChild.Id), CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAlbumMembersQuery(library.Id, album.Id, 1, 100, SortType.Title)
        );
        var descending = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAlbumMembersQuery(library.Id, album.Id, 1, 100, SortType.Title, true)
        );
        var invalidSort = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAlbumMembersQuery(library.Id, album.Id, 1, 100, (SortType)999)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        descending.IsSuccess.ShouldBeTrue();
        descending.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(expectedIds.Count);
        result.Value.Items.Select(x => x.Id).Distinct().Count().ShouldBe(result.Value.Items.Count);
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId == library.Id);
        result
            .Value.Items.Select(x => x.Id)
            .ShouldBe(
                result
                    .Value.Items.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.Id)
                    .Select(x => x.Id)
            );
        descending.Value.Items.Select(x => x.Id).ShouldBe(expectedIds.OrderByDescending(x => x));
        result.Value.Items.Select(x => x.Id).ShouldAllBe(id => expectedIds.Contains(id));
        result.Value.Items.Single(x => x.Id == inactiveAssetId).SourceRootTitle.ShouldBe(inactiveRoot.Title);
        invalidSort.IsFailed.ShouldBeTrue();
        invalidSort.Errors.Count.ShouldBe(2);
        invalidSort.Has400BadRequestError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldPageOverlappingMembershipsAndRejectForeignAlbumScope()
    {
        // Arrange
        await SetupDatabase(
            71013,
            config =>
            {
                config.PlexPhotoLibraryCount = 2;
                config.PhotoAlbumCount = 2;
                config.PhotoCount = 2;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var album = await dbContext
            .PlexPhotoAlbums.Where(x => x.PlexLibraryId == libraries[0].Id)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        var otherAlbumId = await dbContext
            .PlexPhotoAlbums.Where(x => x.PlexLibraryId == libraries[0].Id && x.Id != album.Id)
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);
        var overlapAssetId = await dbContext
            .PlexPhotoAlbumMemberships.Where(x => x.PlexPhotoAlbumId == otherAlbumId)
            .Select(x => x.PlexPhotoMediaDataId)
            .FirstAsync(CancellationToken);
        dbContext.PlexPhotoAlbumMemberships.Add(
            new PlexPhotoAlbumMembership { PlexPhotoAlbumId = album.Id, PlexPhotoMediaDataId = overlapAssetId }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext
            .PlexPhotoMediaData.Where(x => x.Id == overlapAssetId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.PlexPhotoFolderId, (int?)null), CancellationToken);
        var expectedIds = await dbContext
            .PlexPhotoAlbumMemberships.Where(x => x.PlexPhotoAlbumId == album.Id)
            .Select(x => x.PlexPhotoMediaDataId)
            .Distinct()
            .ToListAsync(CancellationToken);

        // Act
        var firstPage = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAlbumMembersQuery(libraries[0].Id, album.Id, 1, 1)
        );
        var secondPage = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAlbumMembersQuery(libraries[0].Id, album.Id, 2, 1)
        );
        var thirdPage = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAlbumMembersQuery(libraries[0].Id, album.Id, 3, 1)
        );
        var foreignAlbum = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAlbumMembersQuery(libraries[1].Id, album.Id)
        );

        // Assert
        firstPage.IsSuccess.ShouldBeTrue();
        secondPage.IsSuccess.ShouldBeTrue();
        thirdPage.IsSuccess.ShouldBeTrue();
        firstPage.Value.TotalCount.ShouldBe(expectedIds.Count);
        secondPage.Value.TotalCount.ShouldBe(expectedIds.Count);
        thirdPage.Value.TotalCount.ShouldBe(expectedIds.Count);
        firstPage.Value.Items.Count.ShouldBe(1);
        secondPage.Value.Items.Count.ShouldBe(1);
        thirdPage.Value.Items.Count.ShouldBe(1);
        var pagedItems = firstPage.Value.Items.Concat(secondPage.Value.Items).Concat(thirdPage.Value.Items).ToList();
        pagedItems.Select(x => x.Id).Distinct().Count().ShouldBe(expectedIds.Count);
        pagedItems.ShouldAllBe(x => expectedIds.Contains(x.Id));
        pagedItems.First(x => x.Id == overlapAssetId).RelativeFolderPath.ShouldBeNull();
        pagedItems.First(x => x.Id == overlapAssetId).SourceRootTitle.ShouldBeNull();
        foreignAlbum.Has404NotFoundError().ShouldBeTrue();
    }
}
