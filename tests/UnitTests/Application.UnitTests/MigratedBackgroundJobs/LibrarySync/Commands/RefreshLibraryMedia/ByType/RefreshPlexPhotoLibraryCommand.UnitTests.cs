namespace Reaparr.Application.UnitTests;

public class RefreshPlexPhotoLibraryCommandUnitTests : BaseCommandUnitTest<RefreshPlexPhotoLibraryCommand>
{
    [Test]
    public async Task ShouldRejectOrphansBeforeChangingAlbumsOrSyncing_WhenPhotoParentIsMissing()
    {
        // Arrange
        var seed = new Seed(625631);
        var library = FakeData.GetPlexLibrary(seed, PlexMediaType.PhotoAlbum).Generate();
        library.Id = 17;
        library.PhotoAlbums.Clear();
        library.Photos.Clear();
        var album = FakeData.GetPlexPhotoAlbums(seed, x =>
        {
            x.PhotoCount = 0;
            x.PhotoClipCount = 0;
        }).Generate();
        album.PlexApiRatingKey = 100;
        var photo = FakeData.GetPlexPhotos(seed).Generate();
        photo.ParentKey = 999;
        photo.PlexPhotoAlbum = album;
        album.Photos.Add(photo);
        library.PhotoAlbums.Add(album);
        library.Photos.Add(photo);
        var albumState = (album.ChildCount, album.Duration, album.MediaSize, album.Quality);
        var response = new InsertMediaMetaDataCommandResponse(library) { PhotoClipCount = 5 };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(
                It.Is<GetLibraryMediaFromPlexApiCommand>(c =>
                    c.PlexLibrary == library && c.MediaType == PlexMediaType.Photos),
                CancellationToken))
            .ReturnsAsync(Result.Ok(new LibraryMetadata(library) { PhotoClipCount = 1 }))
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.UpdateErrorAsync(
                library.Id, It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1), CancellationToken))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexPhotoLibraryCommand(response));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        library.PhotoAlbums.ShouldBe([album]);
        library.Photos.ShouldBe([photo]);
        album.Photos.ShouldBe([photo]);
        photo.PlexPhotoAlbum.ShouldBeSameAs(album);
        (album.ChildCount, album.Duration, album.MediaSize, album.Quality).ShouldBe(albumState);
        response.PhotoClipCount.ShouldBe(5);
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(It.IsAny<SyncPlexPhotosCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }

    [Test]
    public async Task ShouldRejectLeafPhotosLibraryWithoutDispatch_WhenRefreshingPhotoLibrary()
    {
        // Arrange
        var library = FakeData.GetPlexLibrary(new Seed(625622), PlexMediaType.Photos).Generate();
        library.Id = 17;
        var response = new InsertMediaMetaDataCommandResponse(library);

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexPhotoLibraryCommand(response));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(It.IsAny<GetLibraryMediaFromPlexApiCommand>(), It.IsAny<CancellationToken>()),
            Times.Never());
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(It.IsAny<SyncPlexPhotosCommand>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Test]
    public async Task ShouldPublishStructuredErrorAndSkipOptimization_WhenSyncFails()
    {
        // Arrange
        await SetupDatabase(
            625623,
            x =>
            {
                x.PlexServerCount = 1;
                x.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        await dbContext.PlexLibraries.ExecuteUpdateAsync(
            p => p.SetProperty(x => x.Type, PlexMediaType.PhotoAlbum),
            CancellationToken
        );
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var response = new InsertMediaMetaDataCommandResponse(library);
        var failure = Result.Fail("catalog retrieval incomplete");

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetLibraryMediaFromPlexApiCommand>(c =>
                        c.PlexLibrary == library && c.MediaType == PlexMediaType.Photos
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok(new LibraryMetadata(library)))
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateErrorAsync(library.Id, It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1), CancellationToken)
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<SyncPlexPhotosCommand>(c => c.LibraryMetadata == response && !c.ForceMediaRefresh),
                    CancellationToken
                )
            )
            .ReturnsAsync(failure)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexPhotoLibraryCommand(response));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors.ShouldBe(failure.Errors);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ScheduleOptimizeDatabaseJobCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateItemAsync(It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldSkipReconciliationAndPreserveCancellation_WhenPhotoRetrievalFailsWithEmptyRoots(
        bool cancelled
    )
    {
        // Arrange
        await SetupDatabase(
            625624,
            x =>
            {
                x.PlexServerCount = 1;
                x.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        await dbContext.PlexLibraries.ExecuteUpdateAsync(
            p => p.SetProperty(x => x.Type, PlexMediaType.PhotoAlbum),
            CancellationToken
        );
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        library.PhotoAlbums.ShouldBeEmpty();
        var response = new InsertMediaMetaDataCommandResponse(library);
        var failure = cancelled
            ? ResultExtensions.TaskIsCancelled("descendants")
            : Result.Fail("incomplete descendants");

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetLibraryMediaFromPlexApiCommand>(c =>
                        c.PlexLibrary == library && c.MediaType == PlexMediaType.Photos
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(failure)
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateErrorAsync(library.Id, It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1), CancellationToken)
            )
            .Returns(Task.CompletedTask)
            .Verifiable(cancelled ? Times.Never() : Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexPhotoLibraryCommand(response));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBe(cancelled);
        result.Errors.Count.ShouldBe(1);
        result.Errors.ShouldBe(failure.Errors);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<SyncPlexPhotosCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ScheduleOptimizeDatabaseJobCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateItemAsync(It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        if (cancelled)
            Mock.Mock<ILibrarySyncProgressStore>()
                .Verify(
                    x => x.UpdateErrorAsync(It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()),
                    Times.Never()
                );
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldBuildAlbumsWithoutDroppingTypedPhotos_WhenRefreshingPhotosAndClips()
    {
        // Arrange
        await SetupDatabase(
            625626,
            x =>
            {
                x.PlexServerCount = 1;
                x.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        await dbContext.PlexLibraries.ExecuteUpdateAsync(
            p => p.SetProperty(x => x.Type, PlexMediaType.PhotoAlbum),
            CancellationToken
        );
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var seed = new Seed(625626);
        var album = FakeData.GetPlexPhotoAlbums(seed, x =>
        {
            x.PhotoCount = 0;
            x.PhotoClipCount = 0;
        }).Generate();
        library.PhotoAlbums.Add(album);
        var photos = FakeData.GetPlexPhotos(seed).Generate(2);
        foreach (var photo in photos)
        {
            photo.PlexPhotoAlbum = null!;
            photo.ParentKey = album.PlexApiRatingKey;
            library.Photos.Add(photo);
        }
        var response = new InsertMediaMetaDataCommandResponse(library);
        InsertMediaMetaDataCommandResponse? captured = null;

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetLibraryMediaFromPlexApiCommand>(c =>
                        c.PlexLibrary == library && c.MediaType == PlexMediaType.Photos
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok(new LibraryMetadata(library) { PhotoClipCount = 1 }))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<SyncPlexPhotosCommand>(c => c.LibraryMetadata == response && !c.ForceMediaRefresh),
                    CancellationToken
                )
            )
            .Callback<ICommand<Result<CrudPhotosReport>>, CancellationToken>(
                (command, _) => captured = ((SyncPlexPhotosCommand)command).LibraryMetadata
            )
            .ReturnsAsync(Result.Ok(new CrudPhotosReport()))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<ScheduleOptimizeDatabaseJobCommand>(c => c.ChangedItemCount == 0), CancellationToken)
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateItemAsync(
                    library.Id,
                    It.Is<LibraryProgressItem>(p =>
                        p.MediaType == PlexMediaType.PhotoAlbum
                        && p.Received == 1
                        && p.Total == 1
                        && p.TimeRemaining == TimeSpan.Zero
                    ),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateItemAsync(
                    library.Id,
                    It.Is<LibraryProgressItem>(p =>
                        p.MediaType == PlexMediaType.Photos
                        && p.Received == 2
                        && p.Total == 2
                        && p.TimeRemaining == TimeSpan.Zero
                    ),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexPhotoLibraryCommand(response));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (result.Value.Id, result.Value.Type).ShouldBe((library.Id, PlexMediaType.PhotoAlbum));
        captured.ShouldBeSameAs(response);
        captured.ShouldNotBeNull();
        captured.PhotoClipCount.ShouldBe(1);
        captured.PlexLibrary.Photos.ShouldBe(photos);
        album.Photos.ShouldBe(photos);
        photos.ShouldAllBe(x => x.PlexPhotoAlbum == album);
        album.MediaSize.ShouldBe(photos.Sum(x => x.MediaSize));
        album.ChildCount.ShouldBe(photos.Count);
        album.Duration.ShouldBe(photos.Sum(x => x.Duration));
        album.Quality.ShouldBe(photos.Max(x => x.Quality));
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateErrorAsync(It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }
}
