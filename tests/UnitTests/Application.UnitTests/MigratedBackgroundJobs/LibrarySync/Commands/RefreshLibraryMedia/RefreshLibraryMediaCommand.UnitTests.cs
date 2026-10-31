namespace Reaparr.Application.UnitTests;

public class RefreshLibraryMediaCommandUnitTests : BaseCommandUnitTest<RefreshLibraryMediaCommand>
{
    private async Task<PlexLibrary> GetUpdatedLibrary(Seed seed, PlexMediaType type)
    {
        var plexLibrary = await IDbContext
            .PlexLibraries.Where(x => x.Type == type)
            .FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        var fakeLibrary = FakeData.GetPlexLibrary(seed, type).Generate();
        var newLibrary = new PlexLibrary
        {
            Id = plexLibrary.Id,
            Key = plexLibrary.Key,
            Type = type,
            Title = fakeLibrary.Title,
            PlexServerId = plexLibrary.PlexServerId,
            PlexServer = plexLibrary.PlexServer,
            Uuid = fakeLibrary.Uuid,
            Language = fakeLibrary.Language,
            CreatedAt = fakeLibrary.CreatedAt,
            UpdatedAt = fakeLibrary.UpdatedAt,
            ScannedAt = fakeLibrary.ScannedAt,
            ContentChangedAt = fakeLibrary.ContentChangedAt,
            DefaultDestination = fakeLibrary.DefaultDestination,
            DefaultDestinationId = fakeLibrary.DefaultDestinationId,
        };

        newLibrary.Movies.AddRange(fakeLibrary.Movies);
        newLibrary.TvShows.AddRange(fakeLibrary.TvShows);
        newLibrary.PlexAccountLibraries.AddRange(fakeLibrary.PlexAccountLibraries);

        return newLibrary;
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenPlexLibraryNotFound()
    {
        // Arrange
        await SetupDatabase(
            1337,
            config =>
            {
                config.PlexMovieLibraryCount = 0;
            }
        );

        // Act
        var command = new RefreshLibraryMediaCommand(PlexLibraryId: 1);
        var result = await TestHandlerExecuteAsync<PlexLibrary>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Has404NotFoundError().ShouldBeTrue();
    }

    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    public async Task ShouldReturnFailedResult_WhenSyncLibraryMediaFails(PlexMediaType libraryType)
    {
        // Arrange
        await SetupDatabase(
            1338,
            config =>
            {
                config.PlexMovieLibraryCount = libraryType == PlexMediaType.Movie ? 1 : 0;
                config.PlexPhotoLibraryCount = libraryType == PlexMediaType.PhotoAlbum ? 1 : 0;
            }
        );
        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();
        var before = (plexLibrary.SyncedAt, plexLibrary.SyncedContentChangedAt, plexLibrary.Outdated);

        Mock.SetupCommand(It.IsAny<GetLibraryMediaFromPlexApiCommand>)
            .ReturnsAsync(Result.Fail<LibraryMetadata>("Sync failed"));

        var command = new RefreshLibraryMediaCommand(plexLibrary.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors.ShouldContain(x => x.Message.Contains("Sync failed"));
        var after = await IDbContext.PlexLibraries.SingleAsync(x => x.Id == plexLibrary.Id);
        (after.SyncedAt, after.SyncedContentChangedAt, after.Outdated).ShouldBe(before);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<InsertMediaMetaDataCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<RefreshPlexPhotoLibraryCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    [Arguments(PlexMediaType.Movie, false)]
    [Arguments(PlexMediaType.Movie, true)]
    [Arguments(PlexMediaType.TvShow, false)]
    [Arguments(PlexMediaType.TvShow, true)]
    [Arguments(PlexMediaType.PhotoAlbum, false)]
    [Arguments(PlexMediaType.PhotoAlbum, true)]
    public async Task ShouldReturnOkResult_WhenLibraryIsSynced(PlexMediaType libraryType, bool forceMediaRefresh)
    {
        // Arrange
        var seed = await SetupDatabase(
            1339,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = libraryType == PlexMediaType.Movie ? 1 : 0;
                config.PlexTvShowLibraryCount = libraryType == PlexMediaType.TvShow ? 1 : 0;
                config.PlexPhotoLibraryCount = libraryType == PlexMediaType.PhotoAlbum ? 1 : 0;
            }
        );

        var updatedLibrary = await GetUpdatedLibrary(seed, libraryType);
        var previousContentVersion = await IDbContext
            .PlexLibraries.Where(x => x.Id == updatedLibrary.Id)
            .Select(x => x.ContentChangedAt)
            .SingleAsync();
        updatedLibrary.ContentChangedAt = previousContentVersion + 1;
        await IDbContext
            .PlexLibraries.Where(x => x.Id == updatedLibrary.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Outdated, true), CancellationToken);
        var beforeSyncState = await IDbContext
            .PlexLibraries.Where(x => x.Id == updatedLibrary.Id)
            .Select(x => new
            {
                x.SyncedAt,
                x.SyncedContentChangedAt,
                x.Outdated,
            })
            .SingleAsync();

        Mock.SetupCommand(() => It.Is<GetLibraryMediaFromPlexApiCommand>(c => c.PlexLibrary.Id == updatedLibrary.Id))
            .ReturnsAsync(
                (ICommand<Result<LibraryMetadata>> _, CancellationToken _) =>
                    Result.Ok(
                        new LibraryMetadata(updatedLibrary)
                        {
                            Countries = [],
                            GenresVideo = [],
                            GenresAudio = [],
                            Actors = [],
                        }
                    )
            )
            .Verifiable(Times.Once());
        Mock.SetupCommand(() => It.Is<InsertMediaMetaDataCommand>(c => c.LibraryMetadata.Library == updatedLibrary))
            .ReturnsAsync(Result.Ok(new InsertMediaMetaDataCommandResponse(updatedLibrary)))
            .Verifiable(Times.Once());
        Mock.SetupCommand(() =>
                It.Is<SyncPlexLibraryMediaMetaDataCommand>(c => c.LibraryMetadata.PlexLibrary == updatedLibrary)
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        switch (libraryType)
        {
            case PlexMediaType.Movie:
                Mock.SetupCommand(() =>
                        It.Is<RefreshPlexMovieLibraryCommand>(c =>
                            c.LibraryMetadata.PlexLibrary == updatedLibrary && c.ForceMediaRefresh == forceMediaRefresh
                        )
                    )
                    .ReturnsAsync(Result.Ok(updatedLibrary))
                    .Verifiable(Times.Once());
                break;

            case PlexMediaType.TvShow:
                Mock.SetupCommand(() =>
                        It.Is<RefreshPlexTvShowLibraryCommand>(c =>
                            c.LibraryMetadata.PlexLibrary == updatedLibrary && c.ForceMediaRefresh == forceMediaRefresh
                        )
                    )
                    .ReturnsAsync(Result.Ok(updatedLibrary))
                    .Verifiable(Times.Once());
                break;

            case PlexMediaType.PhotoAlbum:
                Mock.SetupCommand(() =>
                        It.Is<RefreshPlexPhotoLibraryCommand>(c =>
                            c.LibraryMetadata.PlexLibrary == updatedLibrary && c.ForceMediaRefresh == forceMediaRefresh
                        )
                    )
                    .ReturnsAsync(Result.Ok(updatedLibrary))
                    .Verifiable(Times.Once());
                break;
        }

        // Act
        var command = new RefreshLibraryMediaCommand(updatedLibrary.Id, forceMediaRefresh);
        var result = await TestHandlerExecuteAsync<PlexLibrary>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Type.ShouldBe(libraryType);
        Mock.Mock<ICommandExecutor>().Verify();

        var dbLibrary = await IDbContext
            .PlexLibraries.Where(x => x.Id == updatedLibrary.Id)
            .FirstOrDefaultAsync(CancellationToken);
        dbLibrary.ShouldNotBeNull();
        if (libraryType == PlexMediaType.PhotoAlbum)
        {
            // Photo completion is committed by SyncPlexPhotosCommand; this mocked typed command cannot perform that write.
            (dbLibrary.SyncedAt, dbLibrary.SyncedContentChangedAt, dbLibrary.Outdated).ShouldBe(
                (beforeSyncState.SyncedAt, beforeSyncState.SyncedContentChangedAt, beforeSyncState.Outdated)
            );
            result.Value.SyncedContentChangedAt.ShouldBe(beforeSyncState.SyncedContentChangedAt);
        }
        else
        {
            dbLibrary.SyncedAt.ShouldNotBeNull();
            dbLibrary.SyncedAt.Value.ShouldBeGreaterThan(updatedLibrary.UpdatedAt!.Value);
            dbLibrary.Outdated.ShouldBeFalse();
            dbLibrary.SyncedContentChangedAt.ShouldBe(updatedLibrary.ContentChangedAt);
            result.Value.SyncedContentChangedAt.ShouldBe(updatedLibrary.ContentChangedAt);
        }
    }

    [Test]
    public async Task ShouldReturnPersistedPhotoCompletionState_WhenTrackedLibraryIsStale()
    {
        // Arrange
        var seed = await SetupDatabase(
            1340,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
            }
        );
        var updatedLibrary = await GetUpdatedLibrary(seed, PlexMediaType.PhotoAlbum);
        var trackedContext = IDbContext;
        var staleLibrary = await trackedContext.PlexLibraries.GetAsync(updatedLibrary.Id, CancellationToken.None);
        staleLibrary.ShouldNotBeNull();

        var persistedSyncedAt = new DateTime(2026, 10, 20, 13, 0, 0, DateTimeKind.Utc);
        var persistedSyncedContentChangedAt = 901_235L;
        await IDbContext
            .PlexLibraries.Where(x => x.Id == updatedLibrary.Id)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(x => x.Title, "Completed Photos")
                        .SetProperty(x => x.ContentChangedAt, persistedSyncedContentChangedAt)
                        .SetProperty(x => x.PhotoAlbumCount, 1)
                        .SetProperty(x => x.PhotoImageCount, 1)
                        .SetProperty(x => x.PhotoClipCount, 0)
                        .SetProperty(x => x.MediaSize, 20L)
                        .SetProperty(x => x.SyncedAt, (DateTime?)persistedSyncedAt)
                        .SetProperty(x => x.SyncedContentChangedAt, (long?)persistedSyncedContentChangedAt)
                        .SetProperty(x => x.Outdated, false),
                CancellationToken
            );

        SetupDependencies(builder =>
            builder.RegisterInstance(trackedContext).As<IReaparrDbContext>().ExternallyOwned()
        );

        Mock.SetupCommand(() => It.Is<GetLibraryMediaFromPlexApiCommand>(c => c.PlexLibrary.Id == updatedLibrary.Id))
            .ReturnsAsync(Result.Ok(new LibraryMetadata(updatedLibrary)))
            .Verifiable(Times.Once());
        Mock.SetupCommand(() => It.Is<InsertMediaMetaDataCommand>(c => c.LibraryMetadata.Library == updatedLibrary))
            .ReturnsAsync(Result.Ok(new InsertMediaMetaDataCommandResponse(updatedLibrary)))
            .Verifiable(Times.Once());
        Mock.SetupCommand(() =>
                It.Is<SyncPlexLibraryMediaMetaDataCommand>(c => c.LibraryMetadata.PlexLibrary == updatedLibrary)
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.SetupCommand(() =>
                It.Is<RefreshPlexPhotoLibraryCommand>(c => c.LibraryMetadata.PlexLibrary == updatedLibrary)
            )
            .ReturnsAsync(Result.Ok(updatedLibrary))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshLibraryMediaCommand(updatedLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Errors.Count.ShouldBe(0);
        var persisted = await IDbContext
            .PlexLibraries.Where(x => x.Id == updatedLibrary.Id)
            .Select(x => new
            {
                x.Title,
                x.ContentChangedAt,
                x.PhotoAlbumCount,
                x.PhotoImageCount,
                x.PhotoClipCount,
                x.MediaSize,
                x.SyncedAt,
                x.SyncedContentChangedAt,
                x.Outdated,
            })
            .SingleAsync(CancellationToken);
        new
        {
            result.Value.Title,
            result.Value.ContentChangedAt,
            result.Value.PhotoAlbumCount,
            result.Value.PhotoImageCount,
            result.Value.PhotoClipCount,
            result.Value.MediaSize,
            result.Value.SyncedAt,
            result.Value.SyncedContentChangedAt,
            result.Value.Outdated,
        }.ShouldBe(persisted);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    [Arguments(PlexMediaType.Movie, false)]
    [Arguments(PlexMediaType.Movie, true)]
    [Arguments(PlexMediaType.PhotoAlbum, false)]
    [Arguments(PlexMediaType.PhotoAlbum, true)]
    public async Task ShouldNotAdvanceSync_WhenLibraryMetadataPersistenceFails(
        PlexMediaType libraryType,
        bool insertionFails
    )
    {
        // Arrange
        await SetupDatabase(
            1440,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = libraryType == PlexMediaType.Movie ? 1 : 0;
                config.PlexPhotoLibraryCount = libraryType == PlexMediaType.PhotoAlbum ? 1 : 0;
            }
        );
        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();
        var before = (plexLibrary.SyncedAt, plexLibrary.SyncedContentChangedAt, plexLibrary.Outdated);

        Mock.SetupCommand(() => It.Is<GetLibraryMediaFromPlexApiCommand>(c => c.PlexLibrary.Id == plexLibrary.Id))
            .ReturnsAsync(Result.Ok(new LibraryMetadata(plexLibrary)))
            .Verifiable(Times.Once());

        Mock.SetupCommand(() => It.Is<InsertMediaMetaDataCommand>(c => c.LibraryMetadata.Library.Id == plexLibrary.Id))
            .ReturnsAsync(
                insertionFails
                    ? Result.Fail<InsertMediaMetaDataCommandResponse>("Metadata insert failed")
                    : Result.Ok(new InsertMediaMetaDataCommandResponse(plexLibrary))
            )
            .Verifiable(Times.Once());
        Mock.SetupCommand(() =>
                It.Is<SyncPlexLibraryMediaMetaDataCommand>(c => c.LibraryMetadata.PlexLibraryId == plexLibrary.Id)
            )
            .ReturnsAsync(Result.Fail("Metadata sync failed"))
            .Verifiable(insertionFails ? Times.Never() : Times.Once());

        // Act
        var command = new RefreshLibraryMediaCommand(plexLibrary.Id);
        var result = await TestHandlerExecuteAsync<PlexLibrary>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().Message.ShouldBe(insertionFails ? "Metadata insert failed" : "Metadata sync failed");
        var after = await IDbContext.PlexLibraries.SingleAsync(x => x.Id == plexLibrary.Id);
        (after.SyncedAt, after.SyncedContentChangedAt, after.Outdated).ShouldBe(before);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<RefreshPlexPhotoLibraryCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    public async Task ShouldNotAdvanceSync_WhenTypedLibraryRefreshFails(PlexMediaType libraryType)
    {
        // Arrange
        var seed = await SetupDatabase(
            1441,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = libraryType == PlexMediaType.Movie ? 1 : 0;
                config.PlexPhotoLibraryCount = libraryType == PlexMediaType.PhotoAlbum ? 1 : 0;
            }
        );
        var updatedLibrary = await GetUpdatedLibrary(seed, libraryType);
        var before = await IDbContext
            .PlexLibraries.Where(x => x.Id == updatedLibrary.Id)
            .Select(x => new
            {
                x.SyncedAt,
                x.SyncedContentChangedAt,
                x.Outdated,
            })
            .SingleAsync();

        Mock.SetupCommand(It.IsAny<InsertMediaMetaDataCommand>)
            .ReturnsAsync(Result.Ok(new InsertMediaMetaDataCommandResponse(updatedLibrary)));
        Mock.SetupCommand(It.IsAny<GetLibraryMediaFromPlexApiCommand>)
            .ReturnsAsync(Result.Ok(new LibraryMetadata(updatedLibrary)));

        Mock.SetupCommand(It.IsAny<SyncPlexLibraryMediaMetaDataCommand>).ReturnsAsync(Result.Ok());

        if (libraryType == PlexMediaType.PhotoAlbum)
            Mock.SetupCommand(() =>
                    It.Is<RefreshPlexPhotoLibraryCommand>(c => c.LibraryMetadata.PlexLibrary == updatedLibrary)
                )
                .ReturnsAsync(Result.Fail<PlexLibrary>("Typed refresh failed"))
                .Verifiable(Times.Once());
        else
            Mock.SetupCommand(() =>
                    It.Is<RefreshPlexMovieLibraryCommand>(c => c.LibraryMetadata.PlexLibrary == updatedLibrary)
                )
                .ReturnsAsync(Result.Fail<PlexLibrary>("Typed refresh failed"))
                .Verifiable(Times.Once());

        var command = new RefreshLibraryMediaCommand(updatedLibrary.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().Message.ShouldBe("Typed refresh failed");
        var after = await IDbContext
            .PlexLibraries.Where(x => x.Id == updatedLibrary.Id)
            .Select(x => new
            {
                x.SyncedAt,
                x.SyncedContentChangedAt,
                x.Outdated,
            })
            .SingleAsync();
        after.ShouldBe(before);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenTvShowLibraryCommandFails()
    {
        // Arrange
        var seed = await SetupDatabase(
            1442,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 3;
            }
        );
        var updatedLibrary = await GetUpdatedLibrary(seed, PlexMediaType.TvShow);

        Mock.SetupCommand(It.IsAny<GetLibraryMediaFromPlexApiCommand>)
            .ReturnsAsync(Result.Ok(new LibraryMetadata(updatedLibrary)));

        Mock.SetupCommand(It.IsAny<InsertMediaMetaDataCommand>)
            .ReturnsAsync(Result.Ok(new InsertMediaMetaDataCommandResponse(updatedLibrary)));
        Mock.SetupCommand(It.IsAny<SyncPlexLibraryMediaMetaDataCommand>).ReturnsAsync(Result.Ok());

        Mock.SetupCommand(It.IsAny<RefreshPlexTvShowLibraryCommand>)
            .ReturnsAsync(Result.Fail<PlexLibrary>("TV Show command failed"));

        var command = new RefreshLibraryMediaCommand(updatedLibrary.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message.Contains("TV Show command failed"));
    }
}
