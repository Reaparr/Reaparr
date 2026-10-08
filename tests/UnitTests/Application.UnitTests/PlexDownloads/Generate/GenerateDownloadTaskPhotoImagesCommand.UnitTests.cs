namespace Reaparr.Application.UnitTests;

public class GenerateDownloadTaskPhotoImagesCommandUnitTests : BaseCommandUnitTest<GenerateDownloadTaskPhotoImagesCommand>
{
    [Test]
    public async Task ShouldCreateMissingAlbumAndQueuedHierarchy_WhenImageIsSelected()
    {
        // Arrange
        await SetupDatabase(62304, config =>
        {
            config.PlexServerCount = 1;
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 1;
        });
        var dbContext = IDbContext;
        var image = await dbContext.PlexPhotoImages.Include(x => x.PlexPhotoAlbum).Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var source = image.MediaDataList.Single();
        (await dbContext.DownloadTaskPhotoAlbums.CountAsync(CancellationToken)).ShouldBe(0);
        var folder = (await dbContext.GetDestinationFolder(image.PlexLibraryId))!;
        var request = new CreateDownloadTasksRequest([new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoImage, PlexServerId = image.PlexServerId, PlexLibraryId = image.PlexLibraryId,
            MediaIds = [image.Id], Qualities = [], KeepCompletedInDownloadFolder = true,
        }], destinationFolderPathId: folder.Id, customDestinationFolderPath: "/destination");

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskPhotoImagesCommand(request));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { PhotoImages = 1 });
        var album = await dbContext.DownloadTaskPhotoAlbums.IncludeAll().SingleAsync(CancellationToken);
        album.PlexApiRatingKey.ShouldBe(image.PlexPhotoAlbum!.PlexApiRatingKey);
        album.PlexLibraryId.ShouldBe(image.PlexLibraryId);
        album.PlexServerId.ShouldBe(image.PlexServerId);
        album.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        var child = album.Children.Single();
        child.ParentId.ShouldBe(album.Id);
        child.PlexApiRatingKey.ShouldBe(image.PlexApiRatingKey);
        child.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        var file = child.Children.Single();
        file.ParentId.ShouldBe(child.Id);
        file.PlexApiMediaId.ShouldBe(source.PlexApiMediaId);
        file.PlexApiPartId.ShouldBe(source.PlexApiPartId);
        file.DataTotal.ShouldBe(source.Size);
        file.FileName.ShouldBe(source.OriginalFilename.GetFileName());
        file.FileLocationUrl.ShouldBe(source.Key);
        file.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        file.DownloadClientType.ShouldBe(PlexDownloadClientType.Direct);
        file.DestinationFolderPathId.ShouldBe(folder.Id);
        file.DirectoryMeta.DestinationRootPath.ShouldBe("/destination");
        file.DirectoryMeta.KeepCompletedInDownloadFolder.ShouldBeTrue();
        file.DirectoryMeta.PhotoAlbumFolder.ShouldBe(image.PlexPhotoAlbum.Title.SanitizeFolderName());
        var logs = await dbContext.DownloadTaskPhotoImageFileLogs.ToListAsync(CancellationToken);
        logs.Select(x => (x.DownloadTaskFileId, x.DownloadTaskPhotoId, x.DownloadTaskPhotoAlbumId, x.Status, x.LogLevel, x.Message))
            .ShouldBe([(file.Id, child.Id, album.Id, DownloadStatus.Queued, NotificationLevel.Information, $"DownloadTask {file.FileName} was queued for downloading")]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldSelectAllOriginalParts_WhenQualityIsRequestedOrFallsBack(bool requested)
    {
        // Arrange
        await SetupDatabase(62312, config =>
        {
            config.PlexServerCount = 1; config.PlexPhotoLibraryCount = 1; config.PhotoAlbumCount = 1; config.PhotoCount = 1;
        });
        var dbContext = IDbContext;
        var image = await dbContext.PlexPhotoImages.Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var original = image.MediaDataList.Single();
        var parts = FakeData.GetPlexPhotoMediaData(new Seed(62312))
            .RuleFor(x => x.Id, _ => 0).RuleFor(x => x.PlexPhotoId, _ => image.Id)
            .RuleFor(x => x.PlexLibraryId, _ => image.PlexLibraryId).RuleFor(x => x.PlexServerId, _ => image.PlexServerId)
            .RuleFor(x => x.PlexApiRatingKey, _ => image.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => original.PlexApiMediaId + 1).RuleFor(x => x.Size, _ => 300).Generate(2);
        dbContext.PlexPhotoData.AddRange(parts);
        await dbContext.SaveChangesAsync(CancellationToken);
        var available = await dbContext.PlexPhotoData.Where(x => x.PlexPhotoId == image.Id).ToListAsync(CancellationToken);
        var expectedQuality = requested ? parts.Last() : available.PickMediaQuality()!;
        var expectedParts = available.Where(x => x.PlexApiMediaId == expectedQuality.PlexApiMediaId).ToList();
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoImage, PlexServerId = image.PlexServerId, PlexLibraryId = image.PlexLibraryId,
            MediaIds = [image.Id], Qualities = [new PlexMediaQualityDTO
            {
                MediaId = image.Id, DataId = requested ? expectedQuality.Id : int.MaxValue,
                MediaDataType = PlexMediaType.PhotoImage, Quality = VideoQuality.Unknown,
            }],
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskPhotoImagesCommand(new CreateDownloadTasksRequest([selection])));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { PhotoImages = 1 });
        var files = await dbContext.DownloadTaskPhotoImageFiles.ToListAsync(CancellationToken);
        files.Select(x => (x.PlexApiMediaId, x.PlexApiPartId, x.DataTotal)).OrderBy(x => x.PlexApiPartId)
            .ShouldBe(expectedParts.Select(x => (x.PlexApiMediaId, x.PlexApiPartId, DataTotal: x.Size)).OrderBy(x => x.PlexApiPartId));
        (await dbContext.DownloadTaskPhotoImageFileLogs.Select(x => x.DownloadTaskFileId).ToListAsync(CancellationToken)).Order()
            .ShouldBe(files.Select(x => x.Id).Order());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldSkipExistingSourcePart_WhenDestinationIsSameOrDifferent(bool differentDestination)
    {
        // Arrange
        await SetupDatabase(62315, config =>
        {
            config.PlexServerCount = 1; config.PlexPhotoLibraryCount = 1; config.PhotoAlbumCount = 1; config.PhotoCount = 1;
        });
        var dbContext = IDbContext;
        var image = await dbContext.PlexPhotoImages.Include(x => x.PlexPhotoAlbum).Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoImage, PlexServerId = image.PlexServerId, PlexLibraryId = image.PlexLibraryId, MediaIds = [image.Id], Qualities = [],
        };
        var request = new CreateDownloadTasksRequest([selection], customDestinationFolderPath: "/original");
        var album = image.PlexPhotoAlbum!.MapToDownloadTask(null);
        var child = image.MapToDownloadTask(album, null);
        var file = image.MediaDataList.Single().MapToDownloadTask(child, image, image.PlexPhotoAlbum!, request, (await dbContext.GetDownloadFolder(null)).DirectoryPath, false);
        child.Children.Add(file);
        album.Children.Add(child);
        dbContext.DownloadTaskPhotoAlbums.Add(album);
        await dbContext.SaveChangesAsync(CancellationToken);
        var albumId = album.Id;
        var childId = child.Id;
        var fileId = file.Id;
        (await dbContext.DownloadTaskPhotoImageFiles.Select(x => x.Id).SingleAsync(CancellationToken)).ShouldBe(fileId);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskPhotoImagesCommand(request with { CustomDestinationFolderPath = differentDestination ? "/new" : "/original" }));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport());
        (await dbContext.DownloadTaskPhotoAlbums.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe([albumId]);
        (await dbContext.DownloadTaskPhotoImages.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe([childId]);
        var retained = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        retained.Id.ShouldBe(fileId);
        retained.DirectoryMeta.DestinationRootPath.ShouldBe("/original");
        (await dbContext.DownloadTaskPhotoImageFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldReuseCompletedParentsAndQueueNewPart_WhenOriginalChanges()
    {
        // Arrange
        await SetupDatabase(62313, config =>
        {
            config.PlexServerCount = 1; config.PlexPhotoLibraryCount = 1; config.PhotoAlbumCount = 1; config.PhotoCount = 1;
        });
        var dbContext = IDbContext;
        var image = await dbContext.PlexPhotoImages.Include(x => x.PlexPhotoAlbum).Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var original = image.MediaDataList.Single();
        var alternate = FakeData.GetPlexPhotoMediaData(new Seed(62314))
            .RuleFor(x => x.Id, _ => 0).RuleFor(x => x.PlexPhotoId, _ => image.Id)
            .RuleFor(x => x.PlexLibraryId, _ => image.PlexLibraryId).RuleFor(x => x.PlexServerId, _ => image.PlexServerId)
            .RuleFor(x => x.PlexApiRatingKey, _ => image.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => original.PlexApiMediaId + 1).Generate();
        dbContext.PlexPhotoData.Add(alternate);
        var album = image.PlexPhotoAlbum!.MapToDownloadTask(null);
        var child = image.MapToDownloadTask(album, null);
        var request = new CreateDownloadTasksRequest([new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoImage, PlexServerId = image.PlexServerId, PlexLibraryId = image.PlexLibraryId, MediaIds = [image.Id], Qualities = [],
        }]);
        var oldFile = original.MapToDownloadTask(child, image, image.PlexPhotoAlbum!, request, (await dbContext.GetDownloadFolder(null)).DirectoryPath, false);
        oldFile.DownloadStatus = DownloadStatus.Completed;
        child.DownloadStatus = DownloadStatus.Completed;
        album.DownloadStatus = DownloadStatus.Completed;
        child.Children.Add(oldFile); album.Children.Add(child);
        dbContext.DownloadTaskPhotoAlbums.Add(album);
        await dbContext.SaveChangesAsync(CancellationToken);
        var selection = request.DownloadMedias.Single() with { Qualities = [new PlexMediaQualityDTO
        {
            MediaId = image.Id, DataId = alternate.Id, MediaDataType = PlexMediaType.PhotoImage, Quality = VideoQuality.Unknown,
        }] };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskPhotoImagesCommand(request with { DownloadMedias = [selection] }));

        // Assert
        result.IsSuccess.ShouldBeTrue(); result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { PhotoImages = 1 });
        var retained = await dbContext.DownloadTaskPhotoAlbums.IncludeAll().SingleAsync(CancellationToken);
        retained.Id.ShouldBe(album.Id); retained.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        var retainedChild = retained.Children.Single();
        retainedChild.Id.ShouldBe(child.Id); retainedChild.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        retainedChild.Children.Select(x => (x.PlexApiMediaId, x.PlexApiPartId, x.DownloadStatus)).OrderBy(x => x.PlexApiMediaId)
            .ShouldBe(new[] { (original.PlexApiMediaId, original.PlexApiPartId, DownloadStatus.Completed), (alternate.PlexApiMediaId, alternate.PlexApiPartId, DownloadStatus.Queued) }.OrderBy(x => x.PlexApiMediaId));
        var newFile = retainedChild.Children.Single(x => x.PlexApiMediaId == alternate.PlexApiMediaId);
        (await dbContext.DownloadTaskPhotoImageFileLogs.Select(x => x.DownloadTaskFileId).ToListAsync(CancellationToken)).ShouldBe([newFile.Id]);
    }

    [Test]
    [Arguments("server")]
    [Arguments("library")]
    [Arguments("image")]
    [Arguments("integration")]
    public async Task ShouldRejectUnownedSelectionWithoutWrites_WhenOwnershipIsInvalid(string invalid)
    {
        // Arrange
        await SetupDatabase(62311, config =>
        {
            config.PlexServerCount = 1; config.PlexPhotoLibraryCount = 1; config.PhotoAlbumCount = 1; config.PhotoCount = 1;
        });
        var dbContext = IDbContext;
        var image = await dbContext.PlexPhotoImages.SingleAsync(CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoImage, PlexServerId = invalid == "server" ? int.MaxValue : image.PlexServerId,
            PlexLibraryId = invalid == "library" ? int.MaxValue : image.PlexLibraryId,
            MediaIds = [invalid == "image" ? int.MaxValue : image.Id], Qualities = [],
        };
        var request = new CreateDownloadTasksRequest([selection], integration: invalid == "integration" ? new IntegrationIdentity(IntegrationType.Radarr, Guid.NewGuid()) : null);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskPhotoImagesCommand(request));

        // Assert
        result.IsFailed.ShouldBeTrue(); result.Errors.Count.ShouldBe(1);
        (await dbContext.DownloadTaskPhotoAlbums.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImages.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImageFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
    }
    [Test]
    public async Task ShouldReturnEmptyReportWithoutParentsOrLogs_WhenImageHasNoMediaData()
    {
        // Arrange
        await SetupDatabase(62410, config =>
        {
            config.PlexServerCount = 1; config.PlexPhotoLibraryCount = 1; config.PhotoAlbumCount = 1; config.PhotoCount = 1;
        });
        var dbContext = IDbContext;
        var image = await dbContext.PlexPhotoImages.SingleAsync(CancellationToken);
        await dbContext.PlexPhotoData.Where(x => x.PlexPhotoId == image.Id).ExecuteDeleteAsync(CancellationToken);
        (await dbContext.PlexPhotoData.CountAsync(CancellationToken)).ShouldBe(0);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoImage, PlexServerId = image.PlexServerId, PlexLibraryId = image.PlexLibraryId,
            MediaIds = [image.Id], Qualities = [],
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskPhotoImagesCommand(new CreateDownloadTasksRequest([selection])));

        // Assert
        result.IsSuccess.ShouldBeTrue(); result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport());
        (await dbContext.DownloadTaskPhotoAlbums.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImages.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImageFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldUseOnlyOriginalBasename_WhenPhotoSourceContainsPathComponents()
    {
        // Arrange
        await SetupDatabase(62309, config =>
        {
            config.PlexServerCount = 1; config.PlexPhotoLibraryCount = 1; config.PhotoAlbumCount = 1; config.PhotoCount = 1;
        });
        var dbContext = IDbContext;
        var image = await dbContext.PlexPhotoImages.SingleAsync(CancellationToken);
        await dbContext.PlexPhotoData.Where(x => x.PlexPhotoId == image.Id).ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.OriginalFilename, @"C:\camera\..\private\photo.jpg"), CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoImage, PlexServerId = image.PlexServerId, PlexLibraryId = image.PlexLibraryId,
            MediaIds = [image.Id], Qualities = [],
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskPhotoImagesCommand(new CreateDownloadTasksRequest([selection])));

        // Assert
        result.IsSuccess.ShouldBeTrue(); result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { PhotoImages = 1 });
        var file = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        file.PlexApiRatingKey.ShouldBe(image.PlexApiRatingKey);
        file.FileName.ShouldBe("photo.jpg");
        file.DownloadFilePath.RemoveReapTempSuffix().ShouldBe(Path.Combine(file.DownloadDirectory, "photo.jpg"));
        file.DownloadFilePath.ShouldNotContain("private");
        file.DownloadFilePath.ShouldNotContain("..");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldRejectInvalidRequestBeforePersistence_WhenRequestIsNullOrEmpty(bool nullRequest)
    {
        // Arrange
        await SetupDatabase(62411);
        var dbContext = IDbContext;
        var request = nullRequest ? null! : new CreateDownloadTasksRequest([]);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new GenerateDownloadTaskPhotoImagesCommand(request));

        // Assert
        result.IsFailed.ShouldBeTrue(); result.Errors.Count.ShouldBe(1);
        (await dbContext.DownloadTaskPhotoAlbums.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImages.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(0);
    }
}
