namespace Reaparr.Application.UnitTests;

public class GenerateDownloadTaskPhotoAlbumsCommandUnitTests : BaseCommandUnitTest<GenerateDownloadTaskPhotoAlbumsCommand>
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldCreateOrReuseAlbumAndExpandImages_WhenAlbumIsSelected(bool reuse)
    {
        // Arrange
        await SetupDatabase(62401, config =>
        {
            config.PlexServerCount = 1; config.PlexPhotoLibraryCount = 1; config.PhotoAlbumCount = 1; config.PhotoCount = 2;
        });
        var dbContext = IDbContext;
        var album = await dbContext.PlexPhotoAlbums.Include(x => x.Photos).SingleAsync(CancellationToken);
        var expectedIds = album.Photos.Select(x => x.Id).Order().ToArray();
        Guid? existingId = null;
        if (reuse)
        {
            var existing = album.MapToDownloadTask(null);
            dbContext.DownloadTaskPhotoAlbums.Add(existing);
            await dbContext.SaveChangesAsync(CancellationToken);
            existingId = existing.Id;
        }
        (await dbContext.DownloadTaskPhotoAlbums.CountAsync(CancellationToken)).ShouldBe(reuse ? 1 : 0);
        var request = new CreateDownloadTasksRequest([new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoAlbum, PlexServerId = album.PlexServerId, PlexLibraryId = album.PlexLibraryId,
            MediaIds = [album.Id], Qualities = [], KeepCompletedInDownloadFolder = true,
        }]);
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(
            It.Is<GenerateDownloadTaskPhotoImagesCommand>(c =>
                c.Request.DownloadMedias.Count == 2 &&
                c.Request.DownloadMedias.SelectMany(m => m.MediaIds).Order().SequenceEqual(expectedIds) &&
                c.Request.DownloadMedias.All(m => m.Type == PlexMediaType.PhotoImage && m.PlexServerId == album.PlexServerId && m.PlexLibraryId == album.PlexLibraryId && m.KeepCompletedInDownloadFolder && m.Qualities.Count == 0)),
            CancellationToken)).ReturnsAsync(Result.Ok(new DownloadTaskCreationReport { PhotoImages = 2 })).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskPhotoAlbumsCommand(request));

        // Assert
        result.IsSuccess.ShouldBeTrue(); result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { PhotoAlbums = reuse ? 0 : 1, PhotoImages = 2 });
        var persisted = await dbContext.DownloadTaskPhotoAlbums.SingleAsync(CancellationToken);
        if (existingId.HasValue)
            persisted.Id.ShouldBe(existingId.Value);
        persisted.PlexApiRatingKey.ShouldBe(album.PlexApiRatingKey);
        persisted.PlexLibraryId.ShouldBe(album.PlexLibraryId);
        persisted.PlexServerId.ShouldBe(album.PlexServerId);
        persisted.Title.ShouldBe(album.Title);
        persisted.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        (await dbContext.DownloadTaskPhotoImages.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImageFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldPreserveInheritedChoiceUnlessImageExplicitlyOverrides_WhenExpandingAlbum(bool explicitChoice)
    {
        // Arrange
        await SetupDatabase(62402, config =>
        {
            config.PlexServerCount = 1; config.PlexPhotoLibraryCount = 1; config.PhotoAlbumCount = 1; config.PhotoCount = 2;
        });
        var dbContext = IDbContext;
        var album = await dbContext.PlexPhotoAlbums.Include(x => x.Photos).ThenInclude(x => x.MediaDataList).SingleAsync(CancellationToken);
        var images = album.Photos.OrderBy(x => x.Id).ToList();
        var target = images[0];
        var other = images[1];
        var inherited = new PlexMediaQualityDTO { MediaId = target.Id, DataId = 111, MediaDataType = PlexMediaType.PhotoImage, Quality = VideoQuality.Unknown };
        var direct = inherited with { DataId = 222 };
        var expectedDataId = explicitChoice ? direct.DataId : inherited.DataId;
        var albumSelection = new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoAlbum, PlexServerId = album.PlexServerId, PlexLibraryId = album.PlexLibraryId,
            MediaIds = [album.Id], Qualities = [inherited], KeepCompletedInDownloadFolder = false,
        };
        var imageSelection = albumSelection with
        {
            Type = PlexMediaType.PhotoImage, MediaIds = [target.Id], Qualities = explicitChoice ? [direct] : [], KeepCompletedInDownloadFolder = true,
        };
        var folder = (await dbContext.GetDestinationFolder(album.PlexLibraryId))!;
        var request = new CreateDownloadTasksRequest([albumSelection, imageSelection], destinationFolderPathId: folder.Id, customDestinationFolderPath: "/custom");
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(
            It.Is<GenerateDownloadTaskPhotoImagesCommand>(c =>
                c.Request.DestinationFolderPathId == folder.Id && c.Request.CustomDestinationFolderPath == "/custom" && c.Request.Integration == null &&
                c.Request.DownloadMedias.Count == 2 && c.Request.DownloadMedias.All(m => m.PlexServerId == album.PlexServerId && m.PlexLibraryId == album.PlexLibraryId && m.Type == PlexMediaType.PhotoImage) &&
                c.Request.DownloadMedias.Any(m => m.MediaIds.Count == 1 && m.MediaIds[0] == target.Id && m.KeepCompletedInDownloadFolder && m.Qualities.Count == 1 && m.Qualities[0].MediaId == target.Id && m.Qualities[0].DataId == expectedDataId) &&
                c.Request.DownloadMedias.Any(m => m.MediaIds.Count == 1 && m.MediaIds[0] == other.Id && !m.KeepCompletedInDownloadFolder && m.Qualities.Count == 0)),
            CancellationToken)).ReturnsAsync(Result.Ok(new DownloadTaskCreationReport { PhotoImages = 2 })).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskPhotoAlbumsCommand(request));

        // Assert
        result.IsSuccess.ShouldBeTrue(); result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { PhotoAlbums = 1, PhotoImages = 2 });
        (await dbContext.DownloadTaskPhotoAlbums.Select(x => x.PlexApiRatingKey).ToListAsync(CancellationToken)).ShouldBe([album.PlexApiRatingKey]);
        albumSelection.Qualities.ShouldBe([inherited]);
        imageSelection.Qualities.ShouldBe(explicitChoice ? [direct] : []);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    [Arguments("server")]
    [Arguments("library")]
    [Arguments("album")]
    [Arguments("integration")]
    public async Task ShouldRejectWithoutPersistenceOrDelegation_WhenAlbumOwnershipIsInvalid(string invalid)
    {
        // Arrange
        await SetupDatabase(62403, config =>
        {
            config.PlexServerCount = 1; config.PlexPhotoLibraryCount = 1; config.PhotoAlbumCount = 1; config.PhotoCount = 1;
        });
        var dbContext = IDbContext;
        var album = await dbContext.PlexPhotoAlbums.SingleAsync(CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoAlbum, PlexServerId = invalid == "server" ? int.MaxValue : album.PlexServerId,
            PlexLibraryId = invalid == "library" ? int.MaxValue : album.PlexLibraryId,
            MediaIds = [invalid == "album" ? int.MaxValue : album.Id], Qualities = [],
        };
        var request = new CreateDownloadTasksRequest([selection], integration: invalid == "integration" ? new IntegrationIdentity(IntegrationType.Sonarr, Guid.NewGuid()) : null);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskPhotoAlbumsCommand(request));

        // Assert
        result.IsFailed.ShouldBeTrue(); result.Errors.Count.ShouldBe(1);
        (await dbContext.DownloadTaskPhotoAlbums.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImages.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<GenerateDownloadTaskPhotoImagesCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }
    [Test]
    public async Task ShouldPropagateChildFailure_WhenImageGenerationFails()
    {
        // Arrange
        await SetupDatabase(62404, config =>
        {
            config.PlexServerCount = 1; config.PlexPhotoLibraryCount = 1; config.PhotoAlbumCount = 1; config.PhotoCount = 1;
        });
        var dbContext = IDbContext;
        var album = await dbContext.PlexPhotoAlbums.Include(x => x.Photos).SingleAsync(CancellationToken);
        var imageId = album.Photos.Single().Id;
        var request = new CreateDownloadTasksRequest([new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoAlbum, PlexServerId = album.PlexServerId, PlexLibraryId = album.PlexLibraryId,
            MediaIds = [album.Id], Qualities = [],
        }]);
        var error = new Error("Image persistence failed");
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(
            It.Is<GenerateDownloadTaskPhotoImagesCommand>(c => c.Request.DownloadMedias.Count == 1 &&
                c.Request.DownloadMedias[0].PlexLibraryId == album.PlexLibraryId &&
                c.Request.DownloadMedias[0].PlexServerId == album.PlexServerId &&
                c.Request.DownloadMedias[0].MediaIds.SequenceEqual(new[] { imageId })),
            CancellationToken)).ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error)).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskPhotoAlbumsCommand(request));

        // Assert
        result.IsFailed.ShouldBeTrue(); result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ShouldBeSameAs(error);
        (await dbContext.DownloadTaskPhotoAlbums.Select(x => x.PlexApiRatingKey).ToListAsync(CancellationToken)).ShouldBe([album.PlexApiRatingKey]);
        (await dbContext.DownloadTaskPhotoImages.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldRejectInvalidRequestWithoutDelegation_WhenRequestIsNullOrEmpty(bool nullRequest)
    {
        // Arrange
        await SetupDatabase(62405);
        var dbContext = IDbContext;
        var request = nullRequest ? null! : new CreateDownloadTasksRequest([]);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskPhotoAlbumsCommand(request));

        // Assert
        result.IsFailed.ShouldBeTrue(); result.Errors.Count.ShouldBe(1);
        (await dbContext.DownloadTaskPhotoAlbums.CountAsync(CancellationToken)).ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<GenerateDownloadTaskPhotoImagesCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
