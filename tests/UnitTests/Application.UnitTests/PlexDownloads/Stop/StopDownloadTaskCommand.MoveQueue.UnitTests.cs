using Microsoft.Extensions.Hosting;

namespace Reaparr.Application.UnitTests;

public class StopDownloadTaskCommandMoveQueueUnitTests : BaseCommandUnitTest<StopDownloadTaskCommand>
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    public async Task ShouldContinueWaitingMoveOnlyAfterCurrentMoveIsStopped_UnlessStopFailsOrHostStops(
        bool stopFails,
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
            33002,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieDownloadTasksCount = 2;
            }
        );
        var dbContext = IDbContext;
        var files = await dbContext
            .DownloadTaskMovieFile.AsTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        files.Count.ShouldBe(2);
        var target = files[0];
        var waiting = files[1];
        target.DirectoryMeta.DownloadRootPath = Mock
            .Container.Resolve<IPathProvider>()
            .DefaultDownloadsDestinationFolder;
        target.DownloadStatus = DownloadStatus.Moving;
        target.FileDataTransferred = 2;
        target.CurrentFileTransferBytesOffset = 2;
        waiting.DownloadStatus = DownloadStatus.DownloadFinished;
        await dbContext.SaveChangesAsync(CancellationToken);
        var targetKey = target.ToKey();
        var waitingKey = waiting.ToKey();
        var waitingBefore = (
            waiting.Id,
            waiting.DownloadStatus,
            waiting.CurrentFileTransferBytesOffset,
            waiting.FileDataTransferred
        );
        byte[] bytes = [11, 22, 33, 44];
        SetupFileSystem(fs => fs.AddFile(target.DownloadFilePath, new MockFileData(bytes)));
        using var stopping = new CancellationTokenSource();
        if (shuttingDown)
            stopping.Cancel();
        Mock.Mock<IHostApplicationLifetime>().SetupGet(x => x.ApplicationStopping).Returns(stopping.Token);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(targetKey, CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(targetKey, CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StopMoveDownloadFileJob(targetKey, CancellationToken))
            .ReturnsAsync(stopFails ? Result.Fail("Cannot stop move") : Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StartMoveDownloadFileJob(waitingKey, CancellationToken.None))
            .ReturnsAsync(Result.Ok())
            .Verifiable(!stopFails && !shuttingDown ? Times.Once() : Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync(new StopDownloadTaskCommand(target.Id, DeleteFiles: false));

        // Assert
        result.IsFailed.ShouldBe(stopFails);
        result.Errors.Count.ShouldBe(stopFails ? 1 : 0);
        var persisted = (await dbContext.GetDownloadTaskFileAsync(targetKey, CancellationToken))!;
        (
            persisted.Id,
            persisted.DownloadStatus,
            persisted.CurrentFileTransferBytesOffset,
            persisted.FileDataTransferred
        ).ShouldBe(
            (
                target.Id,
                stopFails ? DownloadStatus.Moving : DownloadStatus.Stopped,
                stopFails ? 2L : 0L,
                stopFails ? 2L : 0L
            )
        );
        var retained = (await dbContext.GetDownloadTaskFileAsync(waitingKey, CancellationToken))!;
        (
            retained.Id,
            retained.DownloadStatus,
            retained.CurrentFileTransferBytesOffset,
            retained.FileDataTransferred
        ).ShouldBe(waitingBefore);
        Mock.Container.Resolve<IFileSystem>().File.ReadAllBytes(target.DownloadFilePath).ShouldBe(bytes);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
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
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }
}
