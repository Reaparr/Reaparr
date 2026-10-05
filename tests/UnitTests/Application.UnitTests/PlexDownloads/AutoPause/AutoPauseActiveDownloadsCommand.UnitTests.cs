namespace Reaparr.Application.UnitTests;

public class AutoPauseActiveDownloadsCommandUnitTests : BaseUnitTest<AutoPauseActiveDownloadsCommandHandler>
{
    [Test]
    public async Task ShouldPauseAllUniqueActiveDownloadsAcrossServers_WithAutoPauseFlag()
    {
        // Arrange
        await SetupDatabase(
            68101,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.MovieDownloadTasksCount = 2;
            }
        );

        var serverIds = await IDbContext.PlexServers.AsNoTracking().Select(x => x.Id).ToListAsync(CancellationToken);
        serverIds.Count.ShouldBe(2);

        var fileTasks = await IDbContext.DownloadTaskMovieFile.AsNoTracking().ToListAsync(CancellationToken);
        var keyA = fileTasks[0].ToKey();
        var keyB = fileTasks[1].ToKey();

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(serverIds[0]))
            .ReturnsAsync([keyA, keyA])
            .Verifiable(Times.Exactly(2));

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(serverIds[1]))
            .ReturnsAsync([keyB])
            .Verifiable(Times.Exactly(2));

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.GetCurrentlyMovingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([])
            .Verifiable(Times.Exactly(4));

        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(
            It.Is<PauseDownloadTaskCommand>(c => c.DownloadTaskGuid == keyA.Id && c.AutoPause), CancellationToken))
            .ReturnsAsync(Result.Ok()).Verifiable(Times.Exactly(2));
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(
            It.Is<PauseDownloadTaskCommand>(c => c.DownloadTaskGuid == keyB.Id && c.AutoPause), CancellationToken))
            .ReturnsAsync(Result.Ok()).Verifiable(Times.Exactly(2));

        // Act
        var result = await Sut.ExecuteAsync(new AutoPauseActiveDownloadsCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();

        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();

        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<PauseDownloadTaskCommand>(c => c.DownloadTaskGuid == keyA.Id && c.AutoPause),
                        CancellationToken
                    ),
                Times.Exactly(2)
            );

        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<PauseDownloadTaskCommand>(c => c.DownloadTaskGuid == keyB.Id && c.AutoPause),
                        CancellationToken
                    ),
                Times.Exactly(2)
            );
    }

    [Test]
    public async Task ShouldPauseActiveMoveKeys_WithAutoPauseFlag()
    {
        // Arrange
        await SetupDatabase(
            68103,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var serverId = await IDbContext.PlexServers.AsNoTracking().Select(x => x.Id).FirstAsync(CancellationToken);
        var moveKey = (await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken)).ToKey();

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(serverId))
            .ReturnsAsync([])
            .Verifiable(Times.Exactly(2));

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.GetCurrentlyMovingKeysByServer(serverId))
            .ReturnsAsync([moveKey])
            .Verifiable(Times.Exactly(2));

        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(
            It.Is<PauseDownloadTaskCommand>(c => c.DownloadTaskGuid == moveKey.Id && c.AutoPause), CancellationToken))
            .ReturnsAsync(Result.Ok()).Verifiable(Times.Exactly(2));

        // Act
        var result = await Sut.ExecuteAsync(new AutoPauseActiveDownloadsCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<PauseDownloadTaskCommand>(c => c.DownloadTaskGuid == moveKey.Id && c.AutoPause),
                        CancellationToken
                    ),
                Times.Exactly(2)
            );
    }

    [Test]
    public async Task ShouldRunSecondSnapshotPass_WhenLateArrivalAppears()
    {
        // Arrange
        await SetupDatabase(
            68104,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.MovieDownloadTasksCount = 2;
            }
        );

        var serverId = await IDbContext.PlexServers.AsNoTracking().Select(x => x.Id).FirstAsync(CancellationToken);
        var fileTasks = await IDbContext.DownloadTaskMovieFile.AsNoTracking().ToListAsync(CancellationToken);
        var firstPassKey = fileTasks[0].ToKey();
        var secondPassKey = fileTasks[1].ToKey();

        Mock.Mock<IDownloadTaskScheduler>()
            .SetupSequence(x => x.GetCurrentlyDownloadingKeysByServer(serverId))
            .ReturnsAsync([firstPassKey])
            .ReturnsAsync([secondPassKey]);

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.GetCurrentlyMovingKeysByServer(serverId))
            .ReturnsAsync([])
            .Verifiable(Times.Exactly(2));

        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(
            It.Is<PauseDownloadTaskCommand>(c => c.DownloadTaskGuid == firstPassKey.Id && c.AutoPause), CancellationToken))
            .ReturnsAsync(Result.Ok()).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(
            It.Is<PauseDownloadTaskCommand>(c => c.DownloadTaskGuid == secondPassKey.Id && c.AutoPause), CancellationToken))
            .ReturnsAsync(Result.Ok()).Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new AutoPauseActiveDownloadsCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadTaskScheduler>().Verify(x => x.GetCurrentlyDownloadingKeysByServer(serverId), Times.Exactly(2));
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<PauseDownloadTaskCommand>(c => c.DownloadTaskGuid == firstPassKey.Id && c.AutoPause),
                        CancellationToken
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<PauseDownloadTaskCommand>(c => c.DownloadTaskGuid == secondPassKey.Id && c.AutoPause),
                        CancellationToken
                    ),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenPauseCommandFails()
    {
        // Arrange
        await SetupDatabase(
            68102,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var serverId = await IDbContext.PlexServers.AsNoTracking().Select(x => x.Id).FirstAsync(CancellationToken);
        var fileTask = await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(serverId))
            .ReturnsAsync([fileTask.ToKey()])
            .Verifiable(Times.Exactly(2));

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.GetCurrentlyMovingKeysByServer(serverId))
            .ReturnsAsync([])
            .Verifiable(Times.Exactly(2));

        var error = new Error("pause failed");
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(
            It.Is<PauseDownloadTaskCommand>(c => c.DownloadTaskGuid == fileTask.Id && c.AutoPause), CancellationToken))
            .ReturnsAsync(Result.Fail(error)).Verifiable(Times.Exactly(2));

        // Act
        var result = await Sut.ExecuteAsync(new AutoPauseActiveDownloadsCommand(), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        result.Errors.ShouldAllBe(x => ReferenceEquals(x, error));
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
    }

    [Test]
    public async Task ShouldPauseEachUniqueMusicAndOtherDownloadOrMoveOnBothPasses_WhenAutoPausing()
    {
        // Arrange
        await SetupDatabase(88280, c => { c.PlexMusicLibraryCount = 1; c.PlexOtherVideoLibraryCount = 1; });
        var dbContext = IDbContext;
        var music = await FakeData.AddMusicTask(dbContext, 1);
        var video = await FakeData.AddOtherVideoTask(dbContext, 1);
        var keys = new[] { music.ToKey(), video.ToKey() };
        var serverId = music.PlexServerId;
        (await dbContext.GetDownloadTaskKeysAsync(keys.Select(x => x.Id).ToList(), CancellationToken))
            .OrderBy(x => x.Id).ShouldBe(keys.OrderBy(x => x.Id));
        Mock.Mock<IDownloadTaskScheduler>().Setup(x => x.GetCurrentlyDownloadingKeysByServer(serverId))
            .ReturnsAsync([music.ToKey(), music.ToKey()]).Verifiable(Times.Exactly(2));
        Mock.Mock<IMoveDownloadFileScheduler>().Setup(x => x.GetCurrentlyMovingKeysByServer(serverId))
            .ReturnsAsync([music.ToKey(), video.ToKey()]).Verifiable(Times.Exactly(2));
        foreach (var key in keys)
            Mock.Mock<ICommandExecutor>().Setup(x => x.Send(
                It.Is<PauseDownloadTaskCommand>(c => c.DownloadTaskGuid == key.Id && c.AutoPause), CancellationToken))
                .ReturnsAsync(Result.Ok()).Verifiable(Times.Exactly(2));

        // Act
        var result = await Sut.ExecuteAsync(new AutoPauseActiveDownloadsCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (await dbContext.GetDownloadTaskFileAsync(music.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        (await dbContext.GetDownloadTaskFileAsync(video.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(
            It.Is<PauseDownloadTaskCommand>(c => !keys.Any(k => k.Id == c.DownloadTaskGuid) || !c.AutoPause),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldPropagateCancellationWithoutTakingAnotherSnapshot_WhenAutoPauseIsCancelled()
    {
        // Arrange
        await SetupDatabase(88310, c => c.PlexMusicLibraryCount = 1);
        var dbContext = IDbContext;
        var file = await FakeData.AddMusicTask(dbContext, 1);
        var cancelled = Result.Try((Action)(() => throw new OperationCanceledException(CancellationToken)));
        cancelled.IsCancelled.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>().Setup(x => x.GetCurrentlyDownloadingKeysByServer(file.PlexServerId))
            .ReturnsAsync([file.ToKey()]).Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>().Setup(x => x.GetCurrentlyMovingKeysByServer(file.PlexServerId))
            .ReturnsAsync([]).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(
            It.Is<PauseDownloadTaskCommand>(c => c.DownloadTaskGuid == file.Id && c.AutoPause), CancellationToken))
            .ReturnsAsync(cancelled).Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new AutoPauseActiveDownloadsCommand(), CancellationToken);

        // Assert
        result.ShouldBeSameAs(cancelled);
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        (await dbContext.GetDownloadTaskFileAsync(file.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
    }
}
