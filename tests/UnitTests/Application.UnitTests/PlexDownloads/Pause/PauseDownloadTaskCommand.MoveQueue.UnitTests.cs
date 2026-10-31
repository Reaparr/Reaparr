using Microsoft.Extensions.Hosting;

namespace Reaparr.Application.UnitTests;

public class PauseDownloadTaskCommandMoveQueueUnitTests : BaseCommandUnitTest<PauseDownloadTaskCommand>
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    public async Task ShouldContinueWaitingMoveOnlyAfterSelectedChildrenArePaused_UnlessAutoPauseOrShutdown(
        bool autoPause,
        bool shuttingDown
    )
    {
        // Arrange
        SetupDependencies(builder =>
        {
            builder.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>().SingleInstance();
            builder.RegisterType<MoveDownloadFileJobQueue>().As<IMoveDownloadFileQueue>();
        });
        await SetupDatabase(
            33001,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieDownloadTasksCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 2;
            }
        );
        var dbContext = IDbContext;
        var root = await dbContext.DownloadTaskTvShow.SingleAsync(CancellationToken);
        var selected = await dbContext
            .DownloadTaskTvShowEpisodeFile.AsTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        selected.Count.ShouldBe(2);
        var paths = Mock.Container.Resolve<IPathProvider>();
        foreach (var file in selected)
        {
            file.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
            file.DownloadStatus = file.Id == selected[0].Id ? DownloadStatus.Moving : DownloadStatus.DownloadFinished;
            file.FileDataTransferred = 2;
            file.CurrentFileTransferBytesOffset = 2;
        }
        var waiting = await dbContext.DownloadTaskMovieFile.AsTracking().SingleAsync(CancellationToken);
        waiting.DownloadStatus = DownloadStatus.DownloadFinished;
        await dbContext.SaveChangesAsync(CancellationToken);
        var before = selected
            .Select(x => new
            {
                x.Id,
                x.DownloadFilePath,
                x.CurrentFileTransferBytesOffset,
                x.FileDataTransferred,
            })
            .ToList();
        byte[] bytes = [11, 22, 33, 44];
        SetupFileSystem(fs =>
        {
            foreach (var file in selected)
                fs.AddFile(file.DownloadFilePath, new MockFileData(bytes));
        });
        using var stopping = new CancellationTokenSource();
        if (shuttingDown)
            stopping.Cancel();
        var activeKey = selected[0].ToKey();
        var waitingKey = waiting.ToKey();
        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStopping).Returns(stopping.Token);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x =>
                x.IsDownloadFileMoving(
                    It.Is<DownloadTaskKey>(key => selected.Select(file => file.Id).Contains(key.Id)),
                    CancellationToken
                )
            )
            .ReturnsAsync((DownloadTaskKey key, CancellationToken _) => key == activeKey)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StopMoveDownloadFileJob(activeKey, CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StartMoveDownloadFileJob(waitingKey, CancellationToken.None))
            .ReturnsAsync(Result.Ok())
            .Verifiable(!autoPause && !shuttingDown ? Times.Once() : Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync(new PauseDownloadTaskCommand(root.Id, autoPause));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = await dbContext.DownloadTaskTvShowEpisodeFile.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        after
            .Select(x => (x.Id, x.DownloadStatus, x.CurrentFileTransferBytesOffset, x.FileDataTransferred))
            .ShouldBe(
                before.Select(x =>
                    (
                        x.Id,
                        autoPause ? DownloadStatus.AutoMovePaused : DownloadStatus.MovePaused,
                        x.CurrentFileTransferBytesOffset,
                        x.FileDataTransferred
                    )
                )
            );
        var fs = Mock.Container.Resolve<IFileSystem>();
        foreach (var file in before)
            fs.File.ReadAllBytes(file.DownloadFilePath).ShouldBe(bytes);
        (await dbContext.GetDownloadStatusAsync(waitingKey)).ShouldBe(DownloadStatus.DownloadFinished);
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x =>
                    x.StartMoveDownloadFileJob(
                        It.Is<DownloadTaskKey>(key => key != waitingKey),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x =>
                    x.StopDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()),
                Times.Never()
            );
    }
}
