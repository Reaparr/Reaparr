namespace Reaparr.Application.UnitTests;

public class RecoverInterruptedDownloadsCommandTvShowUnitTests : BaseUnitTest<RecoverInterruptedDownloadsCommandHandler>
{
    [Test]
    public async Task ShouldRecoverInterruptedTvShowEpisodeFiles_WhenActiveStatusesExist()
    {
        // Arrange
        await SetupDatabase(
            68115,
            config =>
            {
                config.PlexServerCount = 1;
                config.TvShowCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 4;
            }
        );
        var files = await IDbContext
            .DownloadTaskTvShowEpisodeFile.OrderBy(x => x.Id).Take(4).ToListAsync(CancellationToken);
        var statuses = new[]
        {
            DownloadStatus.Downloading,
            DownloadStatus.Moving,
            DownloadStatus.MoveFinished,
            DownloadStatus.Paused,
        };
        for (var index = 0; index < statuses.Length; index++)
        {
            var fileId = files[index].Id;
            var status = statuses[index];
            await IDbContext.DownloadTaskTvShowEpisodeFile
                .Where(x => x.Id == fileId)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.DownloadStatus, status), CancellationToken);
        }

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x => x.OnStatusChangedAsync(It.IsAny<DownloadTaskKey>(), It.IsAny<DownloadStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new RecoverInterruptedDownloadsCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(x => x.OnStatusChangedAsync(files[0].ToKey(), DownloadStatus.AutoPaused, CancellationToken), Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(x => x.OnStatusChangedAsync(files[1].ToKey(), DownloadStatus.AutoMovePaused, CancellationToken), Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(x => x.OnStatusChangedAsync(files[2].ToKey(), DownloadStatus.Completed, CancellationToken), Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(x => x.OnStatusChangedAsync(It.IsAny<DownloadTaskKey>(), It.IsAny<DownloadStatus>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldNotDispatchStatusChanges_WhenTvShowEpisodeFilesAreAlreadyIdle()
    {
        // Arrange
        await SetupDatabase(
            68116,
            config =>
            {
                config.PlexServerCount = 1;
                config.TvShowCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 2;
            }
        );
        var files = await IDbContext.DownloadTaskTvShowEpisodeFile.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await IDbContext.DownloadTaskTvShowEpisodeFile
            .Where(x => x.Id == files[0].Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Paused), CancellationToken);
        await IDbContext.DownloadTaskTvShowEpisodeFile
            .Where(x => x.Id == files[1].Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.MovePaused), CancellationToken);

        // Act
        var result = await Sut.ExecuteAsync(new RecoverInterruptedDownloadsCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(x => x.OnStatusChangedAsync(It.IsAny<DownloadTaskKey>(), It.IsAny<DownloadStatus>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
