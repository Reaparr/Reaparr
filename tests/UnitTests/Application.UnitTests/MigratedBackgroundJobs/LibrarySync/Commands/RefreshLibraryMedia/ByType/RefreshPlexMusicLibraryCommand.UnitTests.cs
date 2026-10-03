namespace Reaparr.Application.UnitTests;

public class RefreshPlexMusicLibraryCommandUnitTests : BaseCommandUnitTest<RefreshPlexMusicLibraryCommand>
{
    [Test]
    public async Task ShouldPublishStructuredErrorAndSkipOptimization_WhenSyncFails()
    {
        // Arrange
        await SetupDatabase(625623, x => { x.PlexServerCount = 1; x.PlexMovieLibraryCount = 1; });
        var dbContext = IDbContext;
        await dbContext.PlexLibraries.ExecuteUpdateAsync(p => p.SetProperty(x => x.Type, PlexMediaType.Music), CancellationToken);
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var response = new InsertMediaMetaDataCommandResponse(library);
        var failure = Result.Fail("catalog retrieval incomplete");

        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<GetAllMediaByTypeFromPlexApiCommand>(c =>
                c.PlexLibrary == library && c.MediaType == PlexMediaType.Album), CancellationToken))
            .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO>())).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<GetAllMediaByTypeFromPlexApiCommand>(c =>
                c.PlexLibrary == library && c.MediaType == PlexMediaType.Song), CancellationToken))
            .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO>())).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>().Setup(x => x.UpdateErrorAsync(library.Id,
                It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1), CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<SyncPlexMusicCommand>(c =>
                c.LibraryMetadata == response && !c.ForceMediaRefresh), CancellationToken))
            .ReturnsAsync(failure).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexMusicLibraryCommand(response));

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
    [Arguments(PlexMediaType.Album, false)]
    [Arguments(PlexMediaType.Song, false)]
    [Arguments(PlexMediaType.Album, true)]
    [Arguments(PlexMediaType.Song, true)]
    public async Task ShouldSkipReconciliationAndPreserveCancellation_WhenDescendantRetrievalFailsWithEmptyRoots(
        PlexMediaType mediaType, bool cancelled)
    {
        // Arrange
        await SetupDatabase(625624, x => { x.PlexServerCount = 1; x.PlexMovieLibraryCount = 1; });
        var dbContext = IDbContext;
        await dbContext.PlexLibraries.ExecuteUpdateAsync(p => p.SetProperty(x => x.Type, PlexMediaType.Music), CancellationToken);
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        library.Artists.ShouldBeEmpty();
        var response = new InsertMediaMetaDataCommandResponse(library);
        var failure = cancelled ? ResultExtensions.TaskIsCancelled("descendants") : Result.Fail("incomplete descendants");

        if (mediaType == PlexMediaType.Song)
        {
            Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<GetAllMediaByTypeFromPlexApiCommand>(c =>
                    c.PlexLibrary == library && c.MediaType == PlexMediaType.Album), CancellationToken))
                .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO>())).Verifiable(Times.Once());
        }
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<GetAllMediaByTypeFromPlexApiCommand>(c =>
                c.PlexLibrary == library && c.MediaType == mediaType), CancellationToken))
            .ReturnsAsync(failure).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>().Setup(x => x.UpdateErrorAsync(library.Id,
                It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1), CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(cancelled ? Times.Never() : Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexMusicLibraryCommand(response));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBe(cancelled);
        result.Errors.Count.ShouldBe(1);
        result.Errors.ShouldBe(failure.Errors);
        if (mediaType == PlexMediaType.Album)
            Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.Is<GetAllMediaByTypeFromPlexApiCommand>(c =>
                c.MediaType == PlexMediaType.Song), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<SyncPlexMusicCommand>(), It.IsAny<CancellationToken>()), Times.Never());
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
    public async Task ShouldMapNaturalOrderHierarchyAndOriginals_WhenRefreshingMusic()
    {
        // Arrange
        await SetupDatabase(625625, x => { x.PlexServerCount = 1; x.PlexMovieLibraryCount = 1; });
        var dbContext = IDbContext;
        await dbContext.PlexLibraries.ExecuteUpdateAsync(p => p.SetProperty(x => x.Type, PlexMediaType.Music), CancellationToken);
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var template = FakeData.GetLibraryMediaItemDTO(new Seed(625625)).Generate();
        var artist = (template with { RatingKey = 100, Type = PlexMediaType.Artist }).ToPlexMusicArtist(library);
        library.Artists.Add(artist);
        var album10 = template with { RatingKey = 101, Type = PlexMediaType.Album, ParentRatingKey = "100", SortTitle = "Album 10" };
        var album2 = album10 with { RatingKey = 102, SortTitle = "Album 2" };
        var media = template.Media.Single();
        var part = media.Parts.First() with
        {
            Id = 1001, Duration = 17000, Size = 4200, File = "/source/item.bin",
            Key = "/library/parts/1001/file.bin",
        };
        var track = template with
        {
            RatingKey = 103, Type = PlexMediaType.Song, ParentRatingKey = "102", Duration = 17,
            Media = [media with { Id = 901, Duration = 17000, Parts = [part] }],
        };
        var response = new InsertMediaMetaDataCommandResponse(library);
        InsertMediaMetaDataCommandResponse? captured = null;

        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<GetAllMediaByTypeFromPlexApiCommand>(c =>
                c.PlexLibrary == library && c.MediaType == PlexMediaType.Album), CancellationToken))
            .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO> { album10, album2 })).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<GetAllMediaByTypeFromPlexApiCommand>(c =>
                c.PlexLibrary == library && c.MediaType == PlexMediaType.Song), CancellationToken))
            .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO> { track })).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<SyncPlexMusicCommand>(c =>
                c.LibraryMetadata == response && !c.ForceMediaRefresh), CancellationToken))
            .Callback<ICommand<Result<CrudMusicReport>>, CancellationToken>((command, _) => captured = ((SyncPlexMusicCommand)command).LibraryMetadata)
            .ReturnsAsync(Result.Ok(new CrudMusicReport())).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<ScheduleOptimizeDatabaseJobCommand>(c =>
                c.ChangedItemCount == 0), CancellationToken))
            .ReturnsAsync(Result.Ok()).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>().Setup(x => x.UpdateItemAsync(library.Id,
                It.Is<LibraryProgressItem>(p => p.MediaType == PlexMediaType.Artist && p.Received == 1 && p.Total == 1 && p.TimeRemaining == TimeSpan.Zero), CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>().Setup(x => x.UpdateItemAsync(library.Id,
                It.Is<LibraryProgressItem>(p => p.MediaType == PlexMediaType.Album && p.Received == 2 && p.Total == 2 && p.TimeRemaining == TimeSpan.Zero), CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>().Setup(x => x.UpdateItemAsync(library.Id,
                It.Is<LibraryProgressItem>(p => p.MediaType == PlexMediaType.Song && p.Received == 1 && p.Total == 1 && p.TimeRemaining == TimeSpan.Zero), CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexMusicLibraryCommand(response));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (result.Value.Id, result.Value.Type).ShouldBe((library.Id, PlexMediaType.Music));
        captured.ShouldBeSameAs(response);
        captured.ShouldNotBeNull();
        var capturedArtist = captured.PlexLibrary.Artists.Single();
        capturedArtist.ShouldBeSameAs(artist);
        capturedArtist.Albums.Select(x => (x.PlexApiRatingKey, x.SortIndex)).ShouldBe([(102, 1), (101, 2)]);
        var capturedTrack = capturedArtist.Albums.First().Tracks.Single();
        captured.PlexLibrary.Tracks.Select(x => x.PlexApiRatingKey).ShouldBe([103]);
        capturedTrack.PlexAlbum.ShouldBeSameAs(capturedArtist.Albums.First());
        (capturedTrack.Duration, capturedTrack.MediaSize).ShouldBe((17, 4200L));
        capturedTrack.MediaDataList.Select(x => (x.PlexApiMediaId, x.PlexApiPartId, x.Duration, x.Size, x.OriginalFilename))
            .ShouldBe([(901, 1001, 17000, 4200L, "item.bin")]);
        capturedTrack.MediaDataList.Single().PlexTrack.ShouldBeSameAs(capturedTrack);
        Mock.Mock<ILibrarySyncProgressStore>().Verify(x => x.UpdateErrorAsync(
            It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }

    [Test]
    [Arguments(PlexMediaType.Album)]
    [Arguments(PlexMediaType.Song)]
    public async Task ShouldRejectDescendantsBeforeSync_WhenParentWasNotRetrieved(PlexMediaType mediaType)
    {
        // Arrange
        await SetupDatabase(625627, x => { x.PlexServerCount = 1; x.PlexMovieLibraryCount = 1; });
        var dbContext = IDbContext;
        await dbContext.PlexLibraries.ExecuteUpdateAsync(p => p.SetProperty(x => x.Type, PlexMediaType.Music), CancellationToken);
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var template = FakeData.GetLibraryMediaItemDTO(new Seed(625627)).Generate();
        library.Artists.Add((template with { RatingKey = 100, Type = PlexMediaType.Artist }).ToPlexMusicArtist(library));
        var orphan = template with { RatingKey = 103, ParentRatingKey = "999", Type = mediaType };
        var album = template with { RatingKey = 101, ParentRatingKey = "100", Type = PlexMediaType.Album };
        var response = new InsertMediaMetaDataCommandResponse(library);

        if (mediaType == PlexMediaType.Song)
        {
            Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<GetAllMediaByTypeFromPlexApiCommand>(c =>
                    c.PlexLibrary == library && c.MediaType == PlexMediaType.Album), CancellationToken))
                .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO> { album })).Verifiable(Times.Once());
        }
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<GetAllMediaByTypeFromPlexApiCommand>(c =>
                c.PlexLibrary == library && c.MediaType == mediaType), CancellationToken))
            .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO> { orphan })).Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>().Setup(x => x.UpdateErrorAsync(library.Id,
                It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1), CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexMusicLibraryCommand(response));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<SyncPlexMusicCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<ScheduleOptimizeDatabaseJobCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ILibrarySyncProgressStore>().Verify(x => x.UpdateItemAsync(
            It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>()), Times.Never());
        if (mediaType == PlexMediaType.Album)
            Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.Is<GetAllMediaByTypeFromPlexApiCommand>(c =>
                c.MediaType == PlexMediaType.Song), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }
}
