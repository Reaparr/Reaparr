namespace Reaparr.Application.UnitTests;

public class RefreshPlexMusicLibraryCommandUnitTests : BaseCommandUnitTest<RefreshPlexMusicLibraryCommand>
{
    [Test]
    [Arguments(PlexMediaType.MusicAlbum)]
    [Arguments(PlexMediaType.MusicTrack)]
    public async Task ShouldRejectOrphansBeforeChangingHierarchyOrSyncing_WhenDescendantParentIsMissing(
        PlexMediaType mediaType
    )
    {
        // Arrange
        var seed = new Seed(625630);
        var library = FakeData.GetPlexLibrary(seed, PlexMediaType.MusicArtist).Generate();
        library.Id = 17;
        library.Music.Clear();
        library.Albums.Clear();
        library.Tracks.Clear();
        var artist = FakeData.GetPlexMusicArtists(seed, x => x.MusicAlbumCount = 0).Generate();
        artist.PlexApiRatingKey = 100;
        var album = FakeData.GetPlexMusicAlbums(seed, x => x.MusicTrackCount = 0).Generate();
        album.PlexApiRatingKey = 101;
        album.ParentKey = mediaType == PlexMediaType.MusicAlbum ? 999 : artist.PlexApiRatingKey;
        album.PlexArtist = artist;
        var track = FakeData.GetPlexMusicTracks(seed).Generate();
        track.ParentKey = mediaType == PlexMediaType.MusicTrack ? 999 : album.PlexApiRatingKey;
        track.PlexAlbum = album;
        artist.Albums.Add(album);
        album.Tracks.Add(track);
        library.Music.Add(artist);
        library.Albums.Add(album);
        library.Tracks.Add(track);
        var artistState = (artist.ChildCount, artist.Duration, artist.MediaSize);
        var albumState = (album.ChildCount, album.TrackCount, album.Duration, album.MediaSize);
        var response = new InsertMediaMetaDataCommandResponse(library);

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetLibraryMediaFromPlexApiCommand>(c =>
                        c.PlexLibrary == library && c.MediaType == PlexMediaType.MusicAlbum
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok(new LibraryMetadata(library)))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetLibraryMediaFromPlexApiCommand>(c =>
                        c.PlexLibrary == library && c.MediaType == PlexMediaType.MusicTrack
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

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexMusicLibraryCommand(response));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        library.Music.ShouldBe([artist]);
        library.Albums.ShouldBe([album]);
        library.Tracks.ShouldBe([track]);
        artist.Albums.ShouldBe([album]);
        album.Tracks.ShouldBe([track]);
        album.PlexArtist.ShouldBeSameAs(artist);
        track.PlexAlbum.ShouldBeSameAs(album);
        (artist.ChildCount, artist.Duration, artist.MediaSize).ShouldBe(artistState);
        (album.ChildCount, album.TrackCount, album.Duration, album.MediaSize).ShouldBe(albumState);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<SyncPlexMusicCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
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
            p => p.SetProperty(x => x.Type, PlexMediaType.MusicArtist),
            CancellationToken
        );
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var response = new InsertMediaMetaDataCommandResponse(library);
        var failure = Result.Fail("catalog retrieval incomplete");

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetLibraryMediaFromPlexApiCommand>(c =>
                        c.PlexLibrary == library && c.MediaType == PlexMediaType.MusicAlbum
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok(new LibraryMetadata(library)))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetLibraryMediaFromPlexApiCommand>(c =>
                        c.PlexLibrary == library && c.MediaType == PlexMediaType.MusicTrack
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
                    It.Is<SyncPlexMusicCommand>(c => c.LibraryMetadata == response && !c.ForceMediaRefresh),
                    CancellationToken
                )
            )
            .ReturnsAsync(failure)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexMusicLibraryCommand(response));

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
    [Arguments(PlexMediaType.MusicAlbum, false)]
    [Arguments(PlexMediaType.MusicTrack, false)]
    [Arguments(PlexMediaType.MusicAlbum, true)]
    [Arguments(PlexMediaType.MusicTrack, true)]
    public async Task ShouldSkipReconciliationAndPreserveCancellation_WhenDescendantRetrievalFailsWithEmptyRoots(
        PlexMediaType mediaType,
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
            p => p.SetProperty(x => x.Type, PlexMediaType.MusicArtist),
            CancellationToken
        );
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        library.Music.ShouldBeEmpty();
        var response = new InsertMediaMetaDataCommandResponse(library);
        var failure = cancelled
            ? ResultExtensions.TaskIsCancelled("descendants")
            : Result.Fail("incomplete descendants");

        if (mediaType == PlexMediaType.MusicTrack)
        {
            Mock.Mock<ICommandExecutor>()
                .Setup(x =>
                    x.Send(
                        It.Is<GetLibraryMediaFromPlexApiCommand>(c =>
                            c.PlexLibrary == library && c.MediaType == PlexMediaType.MusicAlbum
                        ),
                        CancellationToken
                    )
                )
                .ReturnsAsync(Result.Ok(new LibraryMetadata(library)))
                .Verifiable(Times.Once());
        }
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetLibraryMediaFromPlexApiCommand>(c => c.PlexLibrary == library && c.MediaType == mediaType),
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
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexMusicLibraryCommand(response));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBe(cancelled);
        result.Errors.Count.ShouldBe(1);
        result.Errors.ShouldBe(failure.Errors);
        if (mediaType == PlexMediaType.MusicAlbum)
            Mock.Mock<ICommandExecutor>()
                .Verify(
                    x =>
                        x.Send(
                            It.Is<GetLibraryMediaFromPlexApiCommand>(c => c.MediaType == PlexMediaType.MusicTrack),
                            It.IsAny<CancellationToken>()
                        ),
                    Times.Never()
                );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<SyncPlexMusicCommand>(), It.IsAny<CancellationToken>()), Times.Never());
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
    public async Task ShouldBuildHierarchyWithoutDroppingTypedDescendants_WhenRefreshingMusic()
    {
        // Arrange
        await SetupDatabase(
            625625,
            x =>
            {
                x.PlexServerCount = 1;
                x.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        await dbContext.PlexLibraries.ExecuteUpdateAsync(
            p => p.SetProperty(x => x.Type, PlexMediaType.MusicArtist),
            CancellationToken
        );
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var seed = new Seed(625625);
        var artist = FakeData.GetPlexMusicArtists(seed, x => x.MusicAlbumCount = 0).Generate();
        library.Music.Add(artist);
        var albums = FakeData.GetPlexMusicAlbums(seed, x => x.MusicTrackCount = 0).Generate(2);
        foreach (var album in albums)
        {
            album.PlexArtist = null!;
            album.ParentKey = artist.PlexApiRatingKey;
            library.Albums.Add(album);
        }
        var track = FakeData.GetPlexMusicTracks(seed).Generate();
        track.PlexAlbum = null!;
        track.ParentKey = albums[0].PlexApiRatingKey;
        library.Tracks.Add(track);
        var response = new InsertMediaMetaDataCommandResponse(library);
        InsertMediaMetaDataCommandResponse? captured = null;

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetLibraryMediaFromPlexApiCommand>(c =>
                        c.PlexLibrary == library && c.MediaType == PlexMediaType.MusicAlbum
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok(new LibraryMetadata(library)))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetLibraryMediaFromPlexApiCommand>(c =>
                        c.PlexLibrary == library && c.MediaType == PlexMediaType.MusicTrack
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok(new LibraryMetadata(library)))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<SyncPlexMusicCommand>(c => c.LibraryMetadata == response && !c.ForceMediaRefresh),
                    CancellationToken
                )
            )
            .Callback<ICommand<Result<CrudMusicReport>>, CancellationToken>(
                (command, _) => captured = ((SyncPlexMusicCommand)command).LibraryMetadata
            )
            .ReturnsAsync(Result.Ok(new CrudMusicReport()))
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
                        p.MediaType == PlexMediaType.MusicArtist
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
                        p.MediaType == PlexMediaType.MusicAlbum
                        && p.Received == 2
                        && p.Total == 2
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
                        p.MediaType == PlexMediaType.MusicTrack
                        && p.Received == 1
                        && p.Total == 1
                        && p.TimeRemaining == TimeSpan.Zero
                    ),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexMusicLibraryCommand(response));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (result.Value.Id, result.Value.Type).ShouldBe((library.Id, PlexMediaType.MusicArtist));
        captured.ShouldBeSameAs(response);
        captured.ShouldNotBeNull();
        var capturedArtist = captured.PlexLibrary.Music.Single();
        capturedArtist.ShouldBeSameAs(artist);
        capturedArtist.Albums.ShouldBe(albums);
        captured.PlexLibrary.Tracks.ShouldBe([track]);
        albums[0].Tracks.ShouldBe([track]);
        albums.ShouldAllBe(x => x.PlexArtist == artist);
        track.PlexAlbum.ShouldBeSameAs(albums[0]);
        albums[1].Tracks.ShouldBeEmpty();
        (albums[0].TrackCount, albums[0].Duration, albums[0].MediaSize).ShouldBe((1, track.Duration, track.MediaSize));
        (artist.ChildCount, artist.Duration, artist.MediaSize).ShouldBe((2, track.Duration, track.MediaSize));
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateErrorAsync(It.IsAny<int>(), It.IsAny<Result>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }
}
