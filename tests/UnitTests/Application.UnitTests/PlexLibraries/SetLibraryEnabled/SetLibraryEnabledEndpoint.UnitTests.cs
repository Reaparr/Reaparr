namespace Reaparr.Application.UnitTests;

public class SetLibraryEnabledEndpointUnitTests
    : BaseEndpointUnitTest<SetLibraryEnabledEndpoint, SetLibraryEnabledRequest, ResultDTO<PlexLibraryDTO>>
{
    [Test]
    public async Task ShouldDisablePhotoLibraryByClearingCatalogAndDescendantsWhileRetainingRoots()
    {
        // Arrange
        await SetupDatabase(73301, x => x.PlexServerCount = 1);
        var dbContext = IDbContext;
        var serverId = await dbContext.PlexServers.Select(x => x.Id).SingleAsync(CancellationToken);
        var target = FakeData.GetPlexLibrary(new Seed(73301), PlexMediaType.PhotoAlbum).Generate();
        target.PlexServerId = serverId;
        var control = FakeData.GetPlexLibrary(new Seed(73302), PlexMediaType.PhotoAlbum).Generate();
        control.PlexServerId = serverId;
        dbContext.PlexLibraries.AddRange(target, control);
        await dbContext.SaveChangesAsync(CancellationToken);

        var targetRoot = CreateRoot(target, serverId, "/fixture/target", true);
        var controlRoot = CreateRoot(control, serverId, "/fixture/control", true);
        dbContext.PlexPhotoFolders.AddRange(targetRoot, controlRoot);
        await dbContext.SaveChangesAsync(CancellationToken);
        var targetChild = CreateChild(target, serverId, targetRoot, "Trips", "Trips");
        dbContext.PlexPhotoFolders.Add(targetChild);
        await dbContext.SaveChangesAsync(CancellationToken);
        var targetAlbum = CreateAlbum(target, serverId, 801);
        var controlAlbum = CreateAlbum(control, serverId, 802);
        var targetFile = CreateFile(target, serverId, 901, targetChild.Id);
        var controlFile = CreateFile(control, serverId, 902, controlRoot.Id);
        dbContext.PlexPhotoAlbums.AddRange(targetAlbum, controlAlbum);
        dbContext.PlexPhotoMediaData.AddRange(targetFile, controlFile);
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.PlexPhotoAlbumMemberships.Add(new PlexPhotoAlbumMembership
        {
            PlexPhotoAlbumId = targetAlbum.Id,
            PlexPhotoMediaDataId = targetFile.Id,
        });
        await dbContext.SaveChangesAsync(CancellationToken);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<CancelLibrarySyncJobCommand>(c => c.PlexLibraryId == target.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand(PlexMediaType.PhotoAlbum)).ReturnsAsync(Result.Ok()).Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.Is<List<RefreshDataType>>(types =>
                types.SequenceEqual(new[] { RefreshDataType.PlexLibrary, RefreshDataType.PlexLibrarySyncStatus })
            )))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetLibraryEnabledRequest { PlexLibraryId = target.Id, IsEnabled = false }
        );

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        var response = endpointResult.Response.ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        response.Value.ShouldNotBeNull().IsEnabled.ShouldBeFalse();
        (await dbContext.PlexPhotoAlbums.Select(x => new { x.PlexLibraryId, x.PlexApiRatingKey }).ToListAsync(CancellationToken))
            .ShouldBe([new { PlexLibraryId = control.Id, PlexApiRatingKey = 802 }]);
        (await dbContext.PlexPhotoMediaData.Select(x => new { x.PlexLibraryId, x.PlexApiRatingKey }).ToListAsync(CancellationToken))
            .ShouldBe([new { PlexLibraryId = control.Id, PlexApiRatingKey = 902 }]);
        (await dbContext.PlexPhotoFolders.Where(x => x.PlexLibraryId == target.Id).Select(x => new { x.Id, x.ParentId, x.IsActive }).ToListAsync(CancellationToken))
            .ShouldBe([new { Id = targetRoot.Id, ParentId = (int?)null, IsActive = (bool?)false }]);
        (await dbContext.PlexPhotoFolders.Where(x => x.PlexLibraryId == control.Id).Select(x => new { x.Id, x.ParentId, x.IsActive }).ToListAsync(CancellationToken))
            .ShouldBe([new { Id = controlRoot.Id, ParentId = (int?)null, IsActive = (bool?)true }]);
        (await dbContext.PlexPhotoAlbumMemberships.CountAsync(CancellationToken)).ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldEnableDisabledPhotoLibraryAndQueueFreshSync()
    {
        // Arrange
        await SetupDatabase(73302, x => x.PlexServerCount = 1);
        var dbContext = IDbContext;
        var serverId = await dbContext.PlexServers.Select(x => x.Id).SingleAsync(CancellationToken);
        var library = FakeData.GetPlexLibrary(new Seed(73303), PlexMediaType.PhotoAlbum).Generate();
        library.PlexServerId = serverId;
        library.IsEnabled = false;
        dbContext.PlexLibraries.Add(library);
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<QueueLibrarySyncJobCommand>(c =>
                c.PlexLibraryIds.SequenceEqual(new[] { library.Id }) && !c.ForceLibrarySync && !c.ForceMediaRefresh
            ), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand(PlexMediaType.PhotoAlbum)).ReturnsAsync(Result.Ok()).Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.Is<List<RefreshDataType>>(types =>
                types.SequenceEqual(new[] { RefreshDataType.PlexLibrary, RefreshDataType.PlexLibrarySyncStatus })
            )))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new SetLibraryEnabledRequest { PlexLibraryId = library.Id, IsEnabled = true }
        );

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        var response = endpointResult.Response.ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        response.Value.ShouldNotBeNull().IsEnabled.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    private static PlexPhotoFolder CreateRoot(PlexLibrary library, int serverId, string path, bool active) =>
        new()
        {
            Id = 0,
            PlexServerId = serverId,
            PlexLibraryId = library.Id,
            ParentId = null,
            RelativePath = string.Empty,
            Title = path[(path.LastIndexOf('/') + 1)..],
            RootIdentityKey = $"path:posix:{path}",
            SourceRootPath = path,
            PlexApiLocationId = null,
            IsActive = active,
        };

    private static PlexPhotoFolder CreateChild(PlexLibrary library, int serverId, PlexPhotoFolder root, string path, string title) =>
        new()
        {
            Id = 0,
            PlexServerId = serverId,
            PlexLibraryId = library.Id,
            ParentId = root.Id,
            RelativePath = path,
            Title = title,
            RootIdentityKey = null,
            SourceRootPath = null,
            PlexApiLocationId = null,
            IsActive = null,
        };

    private static PlexPhotoAlbum CreateAlbum(PlexLibrary library, int serverId, int ratingKey) =>
        new()
        {
            Id = 0,
            PlexServerId = serverId,
            PlexLibraryId = library.Id,
            PlexApiRatingKey = ratingKey,
            Title = $"Album {ratingKey}",
            SearchTitle = $"album {ratingKey}",
            Summary = null,
            HasThumb = false,
            AddedAt = DateTime.UtcNow,
            UpdatedAt = null,
        };

    private static PlexPhotoMediaData CreateFile(PlexLibrary library, int serverId, int ratingKey, int folderId) =>
        new()
        {
            Id = 0,
            PlexServerId = serverId,
            PlexLibraryId = library.Id,
            PlexApiRatingKey = ratingKey,
            PlexApiMediaId = ratingKey + 100,
            PlexApiPartId = ratingKey + 200,
            AssetKind = PhotoAssetKind.Image,
            Title = $"file-{ratingKey}.jpg",
            SearchTitle = $"file-{ratingKey}.jpg",
            Summary = null,
            HasThumb = false,
            AddedAt = DateTime.UtcNow,
            UpdatedAt = null,
            OriginallyAvailableAt = null,
            SourcePath = null,
            OriginalFilename = $"file-{ratingKey}.jpg",
            Key = $"/library/parts/{ratingKey}/file.jpg",
            Size = 10,
            Container = "jpg",
            Width = 100,
            Height = 100,
            Duration = null,
            VideoCodec = null,
            AudioCodec = null,
            PlexPhotoFolderId = folderId,
        };
}
