namespace Reaparr.Application.UnitTests;

public class StopDownloadTaskCommandTvShowUnitTests : BaseUnitTest<StopDownloadTaskCommandHandler>
{
    [Test]
    public async Task ShouldOnlyStopActiveTvShowChildren_WhenStoppingTvShow()
    {
        // Arrange

        await SetupDatabase(
            81582,
            config =>
            {
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 2;
                config.TvShowEpisodeDownloadTasksCount = 2;
            }
        );
        var testDownloadTask = await IDbContext.DownloadTaskTvShow.FirstAsync(CancellationToken);
        var downloadableTasks = await IDbContext.GetDownloadableChildTaskKeys(
            testDownloadTask.ToKey(),
            CancellationToken
        );

        downloadableTasks.Count.ShouldBeGreaterThan(1);

        var dbContext = IDbContext;
        var episodeFileTasks = await dbContext.DownloadTaskTvShowEpisodeFile.ToListAsync(CancellationToken);

        SetupFileSystem(fs =>
        {
            foreach (var fileTask in episodeFileTasks)
                fs.AddFile(fileTask.DownloadFilePath, new MockFileData([]));
        });

        var activelyDownloadingTask = downloadableTasks.First();

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x =>
                x.IsDownloading(
                    It.Is<DownloadTaskKey>(key => key.Id == activelyDownloadingTask.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x =>
                x.IsDownloading(
                    It.Is<DownloadTaskKey>(key => key.Id != activelyDownloadingTask.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new StopDownloadTaskCommand(testDownloadTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Exactly(downloadableTasks.Count)
            );
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(x => x.StopDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()), Times.Once);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Exactly(downloadableTasks.Count)
            );
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StopMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );

        // File deletion is delegated to DeleteDownloadTaskFilesCommand for the one actively downloading child.
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()), Times.Once);

        var downloadTasks = await IDbContext.GetDownloadableChildTasks(testDownloadTask.ToKey(), CancellationToken);

        var stoppedTaskIds = downloadTasks
            .Where(x => x.DownloadStatus == DownloadStatus.Stopped)
            .Select(x => x.Id)
            .ToList();
        stoppedTaskIds.Count.ShouldBe(1);
        stoppedTaskIds.ShouldContain(activelyDownloadingTask.Id);

        downloadTasks
            .Where(x => x.Id != activelyDownloadingTask.Id)
            .All(x => x.DownloadStatus == DownloadStatus.Queued)
            .ShouldBeTrue();
    }

    [Test]
    public async Task ShouldOnlyStopActiveSeasonChildren_WhenStoppingSeason()
    {
        // Arrange
        await SetupDatabase(
            52814,
            config =>
            {
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 2;
            }
        );

        var seasonTask = await IDbContext.DownloadTaskTvShowSeason.FirstAsync(CancellationToken);
        var seasonChildTasks = await IDbContext.GetDownloadableChildTaskKeys(seasonTask.ToKey(), CancellationToken);

        seasonChildTasks.Count.ShouldBe(2);

        var dbContext = IDbContext;
        var episodeFileTasks = await dbContext.DownloadTaskTvShowEpisodeFile.ToListAsync(CancellationToken);

        SetupFileSystem(fs =>
        {
            foreach (var fileTask in episodeFileTasks)
                fs.AddFile(fileTask.DownloadFilePath, new MockFileData([]));
        });

        Mock.Mock<IDownloadTaskScheduler>()
            .SetupSequence(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new StopDownloadTaskCommand(seasonTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(x => x.StopDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()), Times.Once);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2)
            );
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StopMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );

        // File deletion is delegated to DeleteDownloadTaskFilesCommand for the one actively downloading child only.
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()), Times.Once);

        var downloadTasks = await IDbContext.GetDownloadableChildTasks(seasonTask.ToKey(), CancellationToken);
        downloadTasks.First(x => x.Id == seasonChildTasks.First().Id).DownloadStatus.ShouldBe(DownloadStatus.Stopped);
        downloadTasks.First(x => x.Id == seasonChildTasks.Last().Id).DownloadStatus.ShouldBe(DownloadStatus.Queued);
    }
    [Test]
    public async Task ShouldStopBothDownloadingAndMovingChildren_WhenTvShowHasOneChildDownloadingAndOneMoving()
    {
        // Arrange

        await SetupDatabase(
            72956,
            config =>
            {
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 2;
            }
        );

        var tvShow = await IDbContext.DownloadTaskTvShow.FirstAsync(CancellationToken);
        var childKeys = await IDbContext.GetDownloadableChildTaskKeys(tvShow.ToKey(), CancellationToken);
        childKeys.Count.ShouldBe(2);

        var episodeFileTasks = await IDbContext.DownloadTaskTvShowEpisodeFile.ToListAsync(CancellationToken);

        SetupFileSystem(fs =>
        {
            foreach (var t in episodeFileTasks)
                fs.AddFile(t.DownloadFilePath, new MockFileData([]));
        });

        // First child is downloading; second child is moving
        Mock.Mock<IDownloadTaskScheduler>()
            .SetupSequence(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .SetupSequence(x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StopMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));

        // Both children (downloading + moving) are in the FileTransfer phase so file deletion is dispatched for each.
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(2));

        // Act
        var result = await Sut.ExecuteAsync(new StopDownloadTaskCommand(tvShow.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(x => x.StopDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()), Times.Once);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StopMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Exactly(2)
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2)
            );
        var downloadTasksAfter = await IDbContext.GetDownloadableChildTasks(tvShow.ToKey(), CancellationToken);
        downloadTasksAfter.Count(x => x.DownloadStatus == DownloadStatus.Stopped).ShouldBe(2);
    }
}
