namespace Reaparr.Application.UnitTests;

public class PauseDownloadTaskCommandTvShowUnitTests : BaseUnitTest<PauseDownloadTaskCommandHandler>
{
    [Test]
    public async Task ShouldCallStopDownloadJob_WhenTaskIsDownloadingAndAtLeastOneValidIdIsGiven()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(4));
        await SetupDatabase(
            19965,
            config =>
            {
                config.TvShowDownloadTasksCount = 2;
                config.TvShowSeasonDownloadTasksCount = 2;
                config.TvShowEpisodeDownloadTasksCount = 2;
            }
        );
        var tvShowDownloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            cancellationToken: CancellationToken
        );
        var testDownloadTask = tvShowDownloadTasks.First().ToKey();
        var downloadableTasks = await IDbContext.GetDownloadableChildTaskKeys(testDownloadTask, CancellationToken);

        downloadableTasks.Count.ShouldBeGreaterThan(0);
        var downloadingKey = downloadableTasks.First();

        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == downloadingKey.Id)
            .ExecuteUpdateAsync(
                p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x =>
                x.IsDownloading(
                    It.Is<DownloadTaskKey>(key => key.Id == downloadingKey.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x =>
                x.IsDownloading(
                    It.Is<DownloadTaskKey>(key => key.Id != downloadingKey.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk()
            .Verifiable(Times.Once);

        // Act
        var result = await Sut.ExecuteAsync(new PauseDownloadTaskCommand(testDownloadTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }
    [Test]
    public async Task ShouldPauseDownloadingAndMovingTasks_WhenMultipleChildrenAreActive()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        await SetupDatabase(
            45112,
            config =>
            {
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 3;
            }
        );

        var tvShowDownloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            cancellationToken: CancellationToken
        );
        var testDownloadTask = tvShowDownloadTasks.First().ToKey();
        var fileTasks = await IDbContext
            .DownloadTaskTvShowEpisodeFile.AsNoTracking()
            .Where(x => x.PlexServerId == testDownloadTask.PlexServerId)
            .OrderBy(x => x.FullTitle)
            .ToListAsync(CancellationToken);

        fileTasks.Count.ShouldBeGreaterThan(2);
        var downloadingKey = fileTasks[0].ToKey();
        var movingKey = fileTasks[1].ToKey();
        var inactiveKey = fileTasks[2].ToKey();

        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == downloadingKey.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.DownloadStatus, DownloadStatus.Downloading)
                        .SetProperty(x => x.DownloadSpeed, 1234),
                CancellationToken
            );
        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == movingKey.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.DownloadStatus, DownloadStatus.Moving)
                        .SetProperty(x => x.FileTransferSpeed, 4321),
                CancellationToken
            );

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(3));

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x =>
                x.IsDownloading(
                    It.Is<DownloadTaskKey>(key => key.Id == downloadingKey.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x =>
                x.IsDownloading(
                    It.Is<DownloadTaskKey>(key => key.Id != downloadingKey.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x =>
                x.IsDownloadFileMoving(
                    It.Is<DownloadTaskKey>(key => key.Id == movingKey.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x =>
                x.IsDownloadFileMoving(
                    It.Is<DownloadTaskKey>(key => key.Id != movingKey.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StopMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();

        // Act
        var result = await Sut.ExecuteAsync(new PauseDownloadTaskCommand(testDownloadTask.Id), CancellationToken);

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
                        It.Is<DownloadTaskKey>(k => k.Id == downloadingKey.Id),
                        DownloadStatus.Paused,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k.Id == inactiveKey.Id),
                        DownloadStatus.Paused,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(x => x.OnStatusChangedAsync(
                movingKey, DownloadStatus.MovePaused, CancellationToken), Times.Once());

        var downloadingTask = await IDbContext.GetDownloadTaskFileAsync(downloadingKey, CancellationToken);
        downloadingTask.ShouldNotBeNull();
        downloadingTask.DownloadStatus.ShouldBe(DownloadStatus.Downloading);
        downloadingTask.DownloadSpeed.ShouldBe(1234);

        var movingTask = await IDbContext.GetDownloadTaskFileAsync(movingKey, CancellationToken);
        movingTask.ShouldNotBeNull();
        movingTask.DownloadStatus.ShouldBe(DownloadStatus.MovePaused);
        movingTask.FileTransferSpeed.ShouldBe(0);

        var inactiveTask = await IDbContext.GetDownloadTaskFileAsync(inactiveKey, CancellationToken);
        inactiveTask.ShouldNotBeNull();
        inactiveTask.DownloadStatus.ShouldBe(DownloadStatus.Queued);
    }
}
