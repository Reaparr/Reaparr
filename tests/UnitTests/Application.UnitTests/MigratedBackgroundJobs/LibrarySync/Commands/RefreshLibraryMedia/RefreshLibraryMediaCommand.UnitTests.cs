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
        result.Has404NotFoundError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenSyncLibraryMediaFails()
    {
        // Arrange
        await SetupDatabase(1338, config => config.PlexMovieLibraryCount = 1);
        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        Mock.SetupCommand(It.IsAny<GetLibraryMediaFromPlexApiCommand>)
            .ReturnsAsync(Result.Fail<LibraryMetadata>("Sync failed"));

        var command = new RefreshLibraryMediaCommand(plexLibrary.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message.Contains("Sync failed"));
    }

    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.TvShow)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    public async Task ShouldReturnOkResult_WhenLibraryIsSynced(PlexMediaType libraryType)
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
        await IDbContext
            .PlexLibraries.Where(x => x.Id == updatedLibrary.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Outdated, true), CancellationToken);

        Mock.SetupCommand(It.IsAny<GetLibraryMediaFromPlexApiCommand>)
            .ReturnsAsync(
                (ICommand<Result<LibraryMetadata>> _, CancellationToken _) =>
                    Result.Ok(
                        new LibraryMetadata(updatedLibrary)
                        {
                            Countries = [],
                            Genres = [],
                            Actors = [],
                        }
                    )
            );
        Mock.SetupCommand(It.IsAny<InsertMediaMetaDataCommand>)
            .ReturnsAsync(Result.Ok(new InsertMediaMetaDataCommandResponse(updatedLibrary)));
        Mock.SetupCommand(It.IsAny<SyncPlexLibraryMediaMetaDataCommand>).ReturnsAsync(Result.Ok());

        switch (libraryType)
        {
            case PlexMediaType.Movie:
                Mock.SetupCommand(It.IsAny<RefreshPlexMovieLibraryCommand>).ReturnsAsync(Result.Ok(updatedLibrary));
                break;

            case PlexMediaType.TvShow:
                Mock.SetupCommand(It.IsAny<RefreshPlexTvShowLibraryCommand>).ReturnsAsync(Result.Ok(updatedLibrary));
                break;

            case PlexMediaType.PhotoAlbum:
                Mock.SetupCommand(() => It.Is<RefreshPlexPhotoLibraryCommand>(c =>
                        c.LibraryMetadata.PlexLibrary == updatedLibrary && !c.ForceMediaRefresh))
                    .ReturnsAsync(Result.Ok(updatedLibrary))
                    .Verifiable(Times.Once());
                break;
        }

        // Act
        var command = new RefreshLibraryMediaCommand(updatedLibrary.Id);
        var result = await TestHandlerExecuteAsync<PlexLibrary>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Type.ShouldBe(libraryType);
        if (libraryType == PlexMediaType.PhotoAlbum)
            Mock.Mock<ICommandExecutor>().Verify(
                x => x.Send(
                    It.Is<RefreshPlexPhotoLibraryCommand>(c => c.LibraryMetadata.PlexLibrary == updatedLibrary),
                    It.IsAny<CancellationToken>()),
                Times.Once());

        var dbLibrary = await IDbContext
            .PlexLibraries.Where(x => x.Id == updatedLibrary.Id)
            .FirstOrDefaultAsync(CancellationToken);
        dbLibrary.ShouldNotBeNull();
        dbLibrary.SyncedAt.ShouldNotBeNull();
        dbLibrary.SyncedAt.Value.ShouldBeGreaterThan(updatedLibrary.UpdatedAt!.Value);
        dbLibrary.Outdated.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenSyncLibraryMediaMetadataFails()
    {
        // Arrange
        await SetupDatabase(
            1440,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 3;
            }
        );
        var plexLibrary = await IDbContext.PlexLibraries.FirstOrDefaultAsync(CancellationToken);
        plexLibrary.ShouldNotBeNull();

        Mock.SetupCommand(It.IsAny<GetLibraryMediaFromPlexApiCommand>)
            .ReturnsAsync(Result.Ok(new LibraryMetadata(plexLibrary)));

        Mock.SetupCommand(It.IsAny<InsertMediaMetaDataCommand>)
            .ReturnsAsync(Result.Ok(new InsertMediaMetaDataCommandResponse(plexLibrary)));
        Mock.SetupCommand(It.IsAny<SyncPlexLibraryMediaMetaDataCommand>)
            .ReturnsAsync(Result.Fail("Metadata sync failed"));

        // Act
        var command = new RefreshLibraryMediaCommand(plexLibrary.Id);
        var result = await TestHandlerExecuteAsync<PlexLibrary>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message.Contains("Metadata sync failed"));
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenMovieLibraryCommandFails()
    {
        // Arrange
        var seed = await SetupDatabase(
            1441,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 3;
            }
        );
        var updatedLibrary = await GetUpdatedLibrary(seed, PlexMediaType.Movie);

        Mock.SetupCommand(It.IsAny<InsertMediaMetaDataCommand>)
            .ReturnsAsync(Result.Ok(new InsertMediaMetaDataCommandResponse(updatedLibrary)));
        Mock.SetupCommand(It.IsAny<GetLibraryMediaFromPlexApiCommand>)
            .ReturnsAsync(Result.Ok(new LibraryMetadata(updatedLibrary)));

        Mock.SetupCommand(It.IsAny<SyncPlexLibraryMediaMetaDataCommand>).ReturnsAsync(Result.Ok());

        Mock.SetupCommand(It.IsAny<RefreshPlexMovieLibraryCommand>)
            .ReturnsAsync(Result.Fail<PlexLibrary>("Movie command failed"));

        var command = new RefreshLibraryMediaCommand(updatedLibrary.Id);

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message.Contains("Movie command failed"));
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
