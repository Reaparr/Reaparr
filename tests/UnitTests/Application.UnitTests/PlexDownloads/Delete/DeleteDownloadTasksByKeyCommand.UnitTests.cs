namespace Reaparr.Application.UnitTests;

public class DeleteDownloadTasksByKeyCommandUnitTests : BaseUnitTest<DeleteDownloadTasksByKeyCommandHandler>
{
    [Test]
    public async Task ShouldDeleteMovieTask_WhenMovieKeyIsGiven()
    {
        // Arrange
        await SetupDatabase(86001, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var movieKey = await dbContext.DownloadTaskMovie.ProjectToKey().FirstAsync(CancellationToken);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(k => k.Id == movieKey.Id),
                    DownloadStatus.Deleted,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new DeleteDownloadTasksByKeyCommand([movieKey]), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await dbContext.DownloadTaskMovie.Where(x => x.Id == movieKey.Id).CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskMovieFile.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldDeleteMovieFileTask_WhenMovieFileKeyIsGiven()
    {
        // Arrange
        await SetupDatabase(86002, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var movieFileKey = await dbContext.DownloadTaskMovieFile.ProjectToKey().FirstAsync(CancellationToken);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DownloadStatus.Deleted,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));

        // Act
        var result = await Sut.ExecuteAsync(new DeleteDownloadTasksByKeyCommand([movieFileKey]), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (
            await dbContext.DownloadTaskMovieFile.Where(x => x.Id == movieFileKey.Id).CountAsync(CancellationToken)
        ).ShouldBe(0);
    }

    [Test]
    public async Task ShouldRemoveOrphanedParents_WhenLastEpisodeFileKeyIsGiven()
    {
        // Arrange
        await SetupDatabase(86003, config =>
        {
            config.PlexServerCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var episodeFileKey = await dbContext
            .DownloadTaskTvShowEpisodeFile.ProjectToKey()
            .SingleAsync(CancellationToken);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DownloadStatus.Deleted,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(4));

        // Act
        var result = await Sut.ExecuteAsync(new DeleteDownloadTasksByKeyCommand([episodeFileKey]), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await dbContext.DownloadTaskTvShowEpisodeFile.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskTvShowEpisode.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskTvShowSeason.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskTvShow.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldNotRemoveRemainingEpisodes_WhenOnlyOneOfTwoIsDeleted()
    {
        // Arrange — one season with two episodes; deleting one file should not orphan the season.
        await SetupDatabase(86004, config =>
        {
            config.PlexServerCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 2;
        });
        var dbContext = IDbContext;
        var episodeFileKeys = await dbContext
            .DownloadTaskTvShowEpisodeFile.ProjectToKey()
            .ToListAsync(CancellationToken);
        episodeFileKeys.Count.ShouldBe(2);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DownloadStatus.Deleted,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));

        // Act — delete only the first episode file
        var result = await Sut.ExecuteAsync(
            new DeleteDownloadTasksByKeyCommand([episodeFileKeys[0]]),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await dbContext.DownloadTaskTvShowEpisodeFile.CountAsync(CancellationToken)).ShouldBe(1);

        // Season and TvShow parent must still exist because one episode remains.
        (await dbContext.DownloadTaskTvShowSeason.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskTvShow.CountAsync(CancellationToken)).ShouldBe(1);
    }

    [Test]
    public async Task ShouldDeleteMultipleKeysAtOnce()
    {
        // Arrange
        await SetupDatabase(86005, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 3;
            config.MovieDownloadTasksCount = 3;
        });
        var dbContext = IDbContext;
        var allMovieKeys = await dbContext.DownloadTaskMovie.ProjectToKey().ToListAsync(CancellationToken);
        allMovieKeys.Count.ShouldBe(3);

        var toDeleteKeys = allMovieKeys.Take(2).ToList();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DownloadStatus.Deleted,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));

        // Act
        var result = await Sut.ExecuteAsync(new DeleteDownloadTasksByKeyCommand(toDeleteKeys), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await dbContext.DownloadTaskMovie.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskMovieFile.CountAsync(CancellationToken)).ShouldBe(1);
    }

    [Test]
    public async Task ShouldSucceed_WhenNoneOfTheKeysExistInDatabase()
    {
        // Arrange — keys that do not exist; no error, no rows deleted.
        await SetupDatabase(86006, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var nonExistentKey = new DownloadTaskKey
        {
            Id = Guid.NewGuid(),
            Type = DownloadTaskType.Movie,
            PlexServerId = 1,
            PlexLibraryId = 1,
        };

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(k => k.Id == nonExistentKey.Id),
                    DownloadStatus.Deleted,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new DeleteDownloadTasksByKeyCommand([nonExistentKey]), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await dbContext.DownloadTaskMovie.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskMovieFile.CountAsync(CancellationToken)).ShouldBe(1);
    }
}
