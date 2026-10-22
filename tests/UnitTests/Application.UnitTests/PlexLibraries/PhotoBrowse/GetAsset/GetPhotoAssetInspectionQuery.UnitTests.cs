namespace Reaparr.Application.UnitTests;

public class GetPhotoAssetInspectionQueryUnitTests : BaseCommandUnitTest<GetPhotoAssetInspectionQuery>
{
    [Test]
    public async Task ShouldReturnMetadataAndMembershipsWithoutSensitiveSourceFields()
    {
        // Arrange
        await SetupDatabase(71006, config =>
        {
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 2;
            config.PhotoCount = 2;
            config.PhotoClipCount = 1;
        });
        var dbContext = IDbContext;
        var asset = await dbContext.PlexPhotoMediaData
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        await dbContext.PlexPhotoMediaData
            .Where(x => x.Id == asset.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.PlexPhotoFolderId, (int?)null), CancellationToken);
        asset.PlexPhotoFolderId = null;
        var expectedAlbumCount = await dbContext.PlexPhotoAlbumMemberships
            .Where(x => x.PlexPhotoMediaDataId == asset.Id)
            .Select(x => x.PlexPhotoAlbumId)
            .Distinct()
            .CountAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync<PhotoAssetInspectionDTO>(
            new GetPhotoAssetInspectionQuery(asset.PlexLibraryId, asset.Id, 1, 100)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Id.ShouldBe(asset.Id);
        result.Value.PlexServerId.ShouldBe(asset.PlexServerId);
        result.Value.PlexLibraryId.ShouldBe(asset.PlexLibraryId);
        result.Value.PlexApiRatingKey.ShouldBe(asset.PlexApiRatingKey);
        result.Value.PlexApiMetaDataKey.ShouldBe(asset.PlexApiRatingKey);
        result.Value.OriginalFilename.ShouldBe(asset.OriginalFilename);
        result.Value.Size.ShouldBe(asset.Size);
        result.Value.Albums.TotalCount.ShouldBe(expectedAlbumCount);
        result.Value.Breadcrumbs.ShouldBeEmpty();
        result.Value.RelativeFolderPath.ShouldBeNull();
        result.Value.SourceRootTitle.ShouldBeNull();
        var serialized = System.Text.Json.JsonSerializer.Serialize(result.Value);
        asset.SourcePath.ShouldNotBeNull();
        serialized.ShouldNotContain(asset.SourcePath);
        serialized.ShouldNotContain(asset.Key);
    }
    [Test]
    public async Task ShouldReturnDeepAncestryClipMetadataAndBoundMembershipPages()
    {
        // Arrange
        var fixture = await ArrangeDeepInspectionFixtureAsync();

        // Act
        var firstPage = await TestHandlerExecuteAsync<PhotoAssetInspectionDTO>(
            new GetPhotoAssetInspectionQuery(fixture.LibraryId, fixture.Asset.Id, 1, 1)
        );
        var secondPage = await TestHandlerExecuteAsync<PhotoAssetInspectionDTO>(
            new GetPhotoAssetInspectionQuery(fixture.LibraryId, fixture.Asset.Id, 2, 1)
        );

        // Assert
        firstPage.IsSuccess.ShouldBeTrue();
        firstPage.Errors.Count.ShouldBe(0);
        secondPage.IsSuccess.ShouldBeTrue();
        secondPage.Errors.Count.ShouldBe(0);
        firstPage.Value.AssetKind.ShouldBe(PhotoAssetKind.VideoClip);
        firstPage.Value.VideoCodec.ShouldNotBeNullOrWhiteSpace();
        firstPage.Value.AudioCodec.ShouldNotBeNullOrWhiteSpace();
        firstPage.Value.Breadcrumbs.Select(x => x.Id).ShouldBe(
            [fixture.Root.Id, .. fixture.Chain.Select(x => x.Id)]
        );
        firstPage.Value.Albums.TotalCount.ShouldBe(2);
        secondPage.Value.Albums.TotalCount.ShouldBe(2);
        firstPage.Value.Albums.Items.Count.ShouldBe(1);
        secondPage.Value.Albums.Items.Count.ShouldBe(1);
        firstPage.Value.Albums.Items[0].Id.ShouldNotBe(secondPage.Value.Albums.Items[0].Id);
    }

    private async Task<DeepInspectionFixture> ArrangeDeepInspectionFixtureAsync()
    {
        await SetupDatabase(71014, config =>
        {
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 3;
            config.PhotoCount = 1;
            config.PhotoClipCount = 1;
        });
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var root = await dbContext.PlexPhotoFolders.SingleAsync(
            x => x.PlexLibraryId == library.Id && x.ParentId == null,
            CancellationToken
        );
        var chain = new List<PlexPhotoFolder>();
        var parentId = root.Id;
        for (var depth = 1; depth <= 3; depth++)
        {
            var folder = new PlexPhotoFolder
            {
                PlexServerId = root.PlexServerId,
                PlexLibraryId = root.PlexLibraryId,
                ParentId = parentId,
                RelativePath = $"inspection-level-{depth}",
                Title = $"Inspection level {depth}",
                RootIdentityKey = null,
                SourceRootPath = null,
                PlexApiLocationId = null,
                IsActive = null,
            };
            dbContext.PlexPhotoFolders.Add(folder);
            await dbContext.SaveChangesAsync(CancellationToken);
            chain.Add(folder);
            parentId = folder.Id;
        }
        var asset = await dbContext.PlexPhotoMediaData
            .Where(x => x.PlexLibraryId == library.Id && x.AssetKind == PhotoAssetKind.VideoClip)
            .FirstAsync(CancellationToken);
        await dbContext.PlexPhotoMediaData
            .Where(x => x.Id == asset.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.PlexPhotoFolderId, chain[^1].Id), CancellationToken);
        var albumIds = await dbContext.PlexPhotoAlbums
            .Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Take(2)
            .ToListAsync(CancellationToken);
        if (!await dbContext.PlexPhotoAlbumMemberships.AnyAsync(
                x => x.PlexPhotoAlbumId == albumIds[1] && x.PlexPhotoMediaDataId == asset.Id,
                CancellationToken
            ))
        {
            dbContext.PlexPhotoAlbumMemberships.Add(
                new PlexPhotoAlbumMembership
                {
                    PlexPhotoAlbumId = albumIds[1],
                    PlexPhotoMediaDataId = asset.Id,
                }
            );
            await dbContext.SaveChangesAsync(CancellationToken);
        }

        return new DeepInspectionFixture(library.Id, asset, root, chain);
    }

    private sealed record DeepInspectionFixture(
        int LibraryId,
        PlexPhotoMediaData Asset,
        PlexPhotoFolder Root,
        IReadOnlyList<PlexPhotoFolder> Chain
    );

    [Test]
    public async Task ShouldPreserveLocationMetadata_WhenSourceRootIsInactive()
    {
        // Arrange
        await SetupDatabase(71016, config =>
        {
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 1;
        });
        var dbContext = IDbContext;
        var asset = await dbContext.PlexPhotoMediaData.FirstAsync(CancellationToken);
        var inactiveRoot = new PlexPhotoFolder
        {
            PlexServerId = asset.PlexServerId,
            PlexLibraryId = asset.PlexLibraryId,
            RelativePath = string.Empty,
            Title = "Inspection inactive",
            RootIdentityKey = "path:inspection-inactive",
            SourceRootPath = "/inspection-inactive",
            PlexApiLocationId = 9301,
            IsActive = false,
        };
        dbContext.PlexPhotoFolders.Add(inactiveRoot);
        await dbContext.SaveChangesAsync(CancellationToken);
        var child = new PlexPhotoFolder
        {
            PlexServerId = asset.PlexServerId,
            PlexLibraryId = asset.PlexLibraryId,
            ParentId = inactiveRoot.Id,
            RelativePath = "inspection-inactive/child",
            Title = "Inspection inactive child",
        };
        dbContext.PlexPhotoFolders.Add(child);
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexPhotoMediaData
            .Where(x => x.Id == asset.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.PlexPhotoFolderId, child.Id), CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync<PhotoAssetInspectionDTO>(
            new GetPhotoAssetInspectionQuery(asset.PlexLibraryId, asset.Id)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.SourceRootTitle.ShouldBe(inactiveRoot.Title);
        result.Value.Breadcrumbs.Select(x => x.Id).ShouldBe([inactiveRoot.Id, child.Id]);
    }
}
