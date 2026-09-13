namespace Reaparr.Application.UnitTests;

public class RefreshPlexMovieLibraryCommandUnitTests : BaseUnitTest<RefreshPlexMovieLibraryCommandHandler>
{
    [Test]
    public async Task ShouldSuccessfullyRefreshLibraryAndKeepSyncedAtUnchanged_WhenMoviesExist()
    {
        // Arrange
        await SetupDatabase(
            8423,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.MovieCount = 3;
            }
        );
        var testLibrary = IDbContext.PlexLibraries.Include(x => x.Movies).First();
        var originalSyncedAt = testLibrary.SyncedAt;

        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateItemAsync(It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SyncPlexMoviesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new CrudMoviesReport()));

        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(
            new RefreshPlexMovieLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var updatedLibrary = await IDbContext.PlexLibraries.GetAsync(testLibrary.Id, CancellationToken);
        updatedLibrary.ShouldNotBeNull();
        updatedLibrary.SyncedAt.ShouldBe(originalSyncedAt);
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateItemAsync(It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce()
            );
    }

    [Test]
    public async Task ShouldSendProgressWithMovieItem_WhenMoviesExist()
    {
        // Arrange
        await SetupDatabase(
            8423,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.MovieCount = 3;
            }
        );
        var testLibrary = IDbContext.PlexLibraries.Include(x => x.Movies).First();
        var capturedItems = new List<LibraryProgressItem>();

        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateItemAsync(It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>())
            )
            .Callback<int, LibraryProgressItem, CancellationToken>((_, item, _) => capturedItems.Add(item))
            .Returns(Task.CompletedTask);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SyncPlexMoviesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new CrudMoviesReport()));

        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        await Sut.ExecuteAsync(
            new RefreshPlexMovieLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        capturedItems.ShouldNotBeEmpty();
        capturedItems.ShouldContain(i => i.MediaType == PlexMediaType.Movie);
    }

    [Test]
    public async Task ShouldStillUpdateLibraryAndLogWarning_WhenNoMoviesExist()
    {
        // Arrange
        await SetupDatabase(
            8424,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.MovieCount = 0;
            }
        );
        var testLibrary = IDbContext.PlexLibraries.First();
        var originalSyncedAt = testLibrary.SyncedAt;

        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateItemAsync(It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SyncPlexMoviesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new CrudMoviesReport()));
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(
            new RefreshPlexMovieLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var updatedLibrary = await IDbContext.PlexLibraries.GetAsync(testLibrary.Id, CancellationToken);
        updatedLibrary.ShouldNotBeNull();
        updatedLibrary.SyncedAt.ShouldBe(originalSyncedAt);

        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<SyncPlexMoviesCommand>(), It.IsAny<CancellationToken>()), Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateItemAsync(It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>()),
                Times.Exactly(1)
            );
    }

    [Test]
    public async Task ShouldUpdateLibraryAndLogError_WhenMoviesExistWithZeroMediaSize()
    {
        // Arrange
        await SetupDatabase(
            8425,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.MovieCount = 3;
            }
        );
        var testLibrary = IDbContext.PlexLibraries.Include(x => x.Movies).First();
        var originalSyncedAt = testLibrary.SyncedAt;
        foreach (var movie in testLibrary.Movies)
        {
            movie.MediaSize = 0;
        }

        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateItemAsync(It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SyncPlexMoviesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new CrudMoviesReport()));
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(
            new RefreshPlexMovieLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var updatedLibrary = await IDbContext.PlexLibraries.GetAsync(testLibrary.Id, CancellationToken);
        updatedLibrary.ShouldNotBeNull();
        updatedLibrary.SyncedAt.ShouldBe(originalSyncedAt);

        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateItemAsync(It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce()
            );
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenSyncPlexMoviesCommandFails()
    {
        // Arrange
        await SetupDatabase(
            8426,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.MovieCount = 5;
            }
        );
        var testLibrary = IDbContext.PlexLibraries.Include(x => x.Movies).First();
        LibraryProgressItem? capturedItem = null;

        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateItemAsync(It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>())
            )
            .Callback<int, LibraryProgressItem, CancellationToken>((_, item, _) => capturedItem = item)
            .Returns(Task.CompletedTask);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<SyncPlexMoviesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Failed to sync movies"));

        // Act
        var result = await Sut.ExecuteAsync(
            new RefreshPlexMovieLibraryCommand(new InsertMediaMetaDataCommandResponse(testLibrary)),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();

        // On failure a progress update is sent with Received=0 for the movie item
        capturedItem.ShouldNotBeNull();
        capturedItem.MediaType.ShouldBe(PlexMediaType.Movie);
        capturedItem.Received.ShouldBe(0);

        Mock.Mock<ILibrarySyncProgressStore>()
            .Verify(
                x => x.UpdateItemAsync(It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }
}
