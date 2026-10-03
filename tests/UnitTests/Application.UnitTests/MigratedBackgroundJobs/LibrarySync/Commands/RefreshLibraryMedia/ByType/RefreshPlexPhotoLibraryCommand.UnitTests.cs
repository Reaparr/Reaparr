namespace Reaparr.Application.UnitTests;

public class RefreshPlexPhotoLibraryCommandUnitTests : BaseCommandUnitTest<RefreshPlexPhotoLibraryCommand>
{
    [Test]
    public async Task ShouldPublishStructuredErrorAndSkipOptimization_WhenSyncFails()
    {
        // Arrange
        await SetupDatabase(625623, x => { x.PlexServerCount = 1; x.PlexMovieLibraryCount = 1; });
        var dbContext = IDbContext;
        await dbContext.PlexLibraries.ExecuteUpdateAsync(p => p.SetProperty(x => x.Type, PlexMediaType.Photos), CancellationToken);
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var response = new InsertMediaMetaDataCommandResponse(library);
        var failure = Result.Fail("catalog retrieval incomplete");

        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<GetAllMediaByTypeFromPlexApiCommand>(c =>
                c.PlexLibrary == library && c.MediaType == PlexMediaType.Photos), CancellationToken))
            .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO>())).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>().Setup(x => x.UpdateErrorAsync(library.Id,
                It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1), CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<SyncPlexPhotosCommand>(c =>
                c.LibraryMetadata == response && !c.ForceMediaRefresh), CancellationToken))
            .ReturnsAsync(failure).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexPhotoLibraryCommand(response));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors.ShouldBe(failure.Errors);
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<ScheduleOptimizeDatabaseJobCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ILibrarySyncProgressStore>().Verify(x => x.UpdateItemAsync(
            It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldSkipReconciliationAndPreserveCancellation_WhenPhotoRetrievalFailsWithEmptyRoots(bool cancelled)
    {
        // Arrange
        await SetupDatabase(625624, x => { x.PlexServerCount = 1; x.PlexMovieLibraryCount = 1; });
        var dbContext = IDbContext;
        await dbContext.PlexLibraries.ExecuteUpdateAsync(p => p.SetProperty(x => x.Type, PlexMediaType.Photos), CancellationToken);
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        library.PhotoAlbums.ShouldBeEmpty();
        var response = new InsertMediaMetaDataCommandResponse(library);
        var failure = cancelled ? ResultExtensions.TaskIsCancelled("descendants") : Result.Fail("incomplete descendants");

        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<GetAllMediaByTypeFromPlexApiCommand>(c =>
                c.PlexLibrary == library && c.MediaType == PlexMediaType.Photos), CancellationToken))
            .ReturnsAsync(failure).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>().Setup(x => x.UpdateErrorAsync(library.Id,
                It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1), CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(cancelled ? Times.Never() : Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexPhotoLibraryCommand(response));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBe(cancelled);
        result.Errors.Count.ShouldBe(1);
        result.Errors.ShouldBe(failure.Errors);
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<SyncPlexPhotosCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<ScheduleOptimizeDatabaseJobCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ILibrarySyncProgressStore>().Verify(x => x.UpdateItemAsync(
            It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>()), Times.Never());
        if (cancelled)
            Mock.Mock<ILibrarySyncProgressStore>().Verify(x => x.UpdateErrorAsync(
                It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldMapOriginalsParentsAndClipCount_WhenRefreshingPhotosAndClips()
    {
        // Arrange
        await SetupDatabase(625626, x => { x.PlexServerCount = 1; x.PlexMovieLibraryCount = 1; });
        var dbContext = IDbContext;
        await dbContext.PlexLibraries.ExecuteUpdateAsync(p => p.SetProperty(x => x.Type, PlexMediaType.Photos), CancellationToken);
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var template = FakeData.GetLibraryMediaItemDTO(new Seed(625626)).Generate();
        var album = (template with { RatingKey = 101, Type = PlexMediaType.PhotoAlbum }).ToPlexPhotoAlbum(library);
        library.PhotoAlbums.Add(album);
        var media = template.Media.Single();
        var part = media.Parts.First() with
        {
            Id = 1001, Duration = 17000, Size = 4200, File = "/source/item.bin",
            Key = "/library/parts/1001/file.bin",
        };
        var photo = template with
        {
            RatingKey = 103, Type = PlexMediaType.Photos, ParentRatingKey = "101", Duration = 17,
            Media = [media with { Id = 901, Duration = 17000, Parts = [part] }],
        };
        var clip = photo with { RatingKey = 104, Type = PlexMediaType.OtherVideos, SortTitle = "Clip 2" };
        var response = new InsertMediaMetaDataCommandResponse(library);
        InsertMediaMetaDataCommandResponse? captured = null;

        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<GetAllMediaByTypeFromPlexApiCommand>(c =>
                c.PlexLibrary == library && c.MediaType == PlexMediaType.Photos), CancellationToken))
            .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO> { clip, photo })).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<SyncPlexPhotosCommand>(c =>
                c.LibraryMetadata == response && !c.ForceMediaRefresh), CancellationToken))
            .Callback<ICommand<Result<CrudPhotosReport>>, CancellationToken>((command, _) => captured = ((SyncPlexPhotosCommand)command).LibraryMetadata)
            .ReturnsAsync(Result.Ok(new CrudPhotosReport())).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<ScheduleOptimizeDatabaseJobCommand>(c =>
                c.ChangedItemCount == 0), CancellationToken))
            .ReturnsAsync(Result.Ok()).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>().Setup(x => x.UpdateItemAsync(library.Id,
                It.Is<LibraryProgressItem>(p => p.MediaType == PlexMediaType.PhotoAlbum && p.Received == 1 && p.Total == 1 && p.TimeRemaining == TimeSpan.Zero), CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>().Setup(x => x.UpdateItemAsync(library.Id,
                It.Is<LibraryProgressItem>(p => p.MediaType == PlexMediaType.Photos && p.Received == 2 && p.Total == 2 && p.TimeRemaining == TimeSpan.Zero), CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexPhotoLibraryCommand(response));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (result.Value.Id, result.Value.Type).ShouldBe((library.Id, PlexMediaType.Photos));
        captured.ShouldBeSameAs(response);
        captured.ShouldNotBeNull();
        captured.PhotoClipCount.ShouldBe(1);
        captured.PlexLibrary.Photos.Select(x => x.PlexApiRatingKey).ShouldBe([104, 103]);
        captured.PlexLibrary.Photos.ShouldAllBe(x => x.PlexPhotoAlbum == album);
        album.Photos.Select(x => x.PlexApiRatingKey).ShouldBe([104, 103]);
        var capturedPhoto = captured.PlexLibrary.Photos.Single(x => x.PlexApiRatingKey == 103);
        (capturedPhoto.Duration, capturedPhoto.MediaSize).ShouldBe((17, 4200L));
        capturedPhoto.MediaDataList.Select(x => (x.PlexApiMediaId, x.PlexApiPartId, x.Duration, x.Size, x.OriginalFilename))
            .ShouldBe([(901, 1001, 17000, 4200L, "item.bin")]);
        capturedPhoto.MediaDataList.Single().PlexPhoto.ShouldBeSameAs(capturedPhoto);
        Mock.Mock<ILibrarySyncProgressStore>().Verify(x => x.UpdateErrorAsync(
            It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }

    [Test]
    public async Task ShouldRejectPhotoBeforeSync_WhenAlbumWasNotRetrieved()
    {
        // Arrange
        await SetupDatabase(625627, x => { x.PlexServerCount = 1; x.PlexMovieLibraryCount = 1; });
        var dbContext = IDbContext;
        await dbContext.PlexLibraries.ExecuteUpdateAsync(p => p.SetProperty(x => x.Type, PlexMediaType.Photos), CancellationToken);
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var template = FakeData.GetLibraryMediaItemDTO(new Seed(625627)).Generate();
        library.PhotoAlbums.Add((template with { RatingKey = 100, Type = PlexMediaType.PhotoAlbum }).ToPlexPhotoAlbum(library));
        var orphan = template with { RatingKey = 103, ParentRatingKey = "999", Type = PlexMediaType.Photos };
        var response = new InsertMediaMetaDataCommandResponse(library);

        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<GetAllMediaByTypeFromPlexApiCommand>(c =>
                c.PlexLibrary == library && c.MediaType == PlexMediaType.Photos), CancellationToken))
            .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO> { orphan })).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>().Setup(x => x.UpdateErrorAsync(library.Id,
                It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1), CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexPhotoLibraryCommand(response));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<SyncPlexPhotosCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<ScheduleOptimizeDatabaseJobCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ILibrarySyncProgressStore>().Verify(x => x.UpdateItemAsync(
            It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }
}
