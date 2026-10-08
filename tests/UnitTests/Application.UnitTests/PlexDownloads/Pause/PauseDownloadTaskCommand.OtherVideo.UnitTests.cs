namespace Reaparr.Application.UnitTests;

public class PauseDownloadTaskCommandOtherVideoUnitTests : BaseCommandUnitTest<PauseDownloadTaskCommand>
{
    [Test]
    [Arguments(DownloadTaskType.OtherVideo, false, DownloadStatus.Downloading)]
    [Arguments(DownloadTaskType.OtherVideoData, false, DownloadStatus.Downloading)]
    [Arguments(DownloadTaskType.OtherVideo, true, DownloadStatus.Moving)]
    [Arguments(DownloadTaskType.OtherVideoData, true, DownloadStatus.DownloadFinished)]
    [Arguments(DownloadTaskType.OtherVideoData, true, DownloadStatus.Completed)]
    public async Task ShouldPauseSelectedOtherVideoByPhase_WhenPausing(
        DownloadTaskType selection,
        bool autoPause,
        DownloadStatus before
    )
    {
        // Arrange
        await SetupDatabase(
            88211,
            c =>
            {
                c.PlexOtherVideoLibraryCount = 1;
                c.OtherVideoDownloadTasksCount = 1;
                c.OtherVideoFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var target = await dbContext.DownloadTaskOtherVideoFiles.SingleAsync(CancellationToken);
        target.DataReceived = 128;
        target.FileDataTransferred = 64;
        dbContext.Entry(target).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.SetDownloadStatus(target.ToKey(), before);

        var nodeKey = selection == DownloadTaskType.OtherVideo ? target.Parent!.ToKey() : target.ToKey();
        (await dbContext.GetDownloadableChildTaskKeys(nodeKey, CancellationToken)).ShouldBe([target.ToKey()]);
        var expected = before switch
        {
            DownloadStatus.Completed => DownloadStatus.Completed,
            DownloadStatus.Moving or DownloadStatus.DownloadFinished => autoPause
                ? DownloadStatus.AutoMovePaused
                : DownloadStatus.MovePaused,
            _ => autoPause ? DownloadStatus.AutoPaused : DownloadStatus.Paused,
        };
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        if (before == DownloadStatus.Downloading)
        {
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.IsDownloading(target.ToKey(), CancellationToken))
                .ReturnsAsync(true)
                .Verifiable(Times.Once());
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.StopDownloadTaskJob(target.ToKey(), CancellationToken))
                .ReturnsAsync(Result.Ok())
                .Verifiable(Times.Once());
        }
        if (before is DownloadStatus.Moving or DownloadStatus.DownloadFinished)
        {
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.IsDownloadFileMoving(target.ToKey(), CancellationToken))
                .ReturnsAsync(true)
                .Verifiable(Times.Once());
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.StopMoveDownloadFileJob(target.ToKey(), CancellationToken))
                .ReturnsAsync(Result.Ok())
                .Verifiable(Times.Once());
        }

        // Act
        var result = await TestHandlerExecuteAsync(new PauseDownloadTaskCommand(nodeKey.Id, autoPause));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = (await dbContext.GetDownloadTaskFileAsync(target.ToKey(), CancellationToken))!;
        after.DownloadStatus.ShouldBe(expected);
        after.DataReceived.ShouldBe(before == DownloadStatus.Moving ? 0 : 128);
        after.FileDataTransferred.ShouldBe(before == DownloadStatus.Moving ? 0 : 64);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldPropagateRejectedMoveStop_WhenPausingOtherVideo()
    {
        // Arrange
        await SetupDatabase(
            88321,
            c =>
            {
                c.PlexOtherVideoLibraryCount = 1;
                c.OtherVideoDownloadTasksCount = 1;
                c.OtherVideoFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var file = await dbContext.DownloadTaskOtherVideoFiles.SingleAsync(CancellationToken);
        file.DownloadStatus = DownloadStatus.Moving;
        file.DataReceived = 128;
        file.FileDataTransferred = 64;
        dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var stopResult = Result.Fail(new Error("move stop rejected"));

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(file.ToKey(), CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StopMoveDownloadFileJob(file.ToKey(), CancellationToken))
            .ReturnsAsync(stopResult)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new PauseDownloadTaskCommand(file.Id, AutoPause: true));

        // Assert
        result.ShouldBeSameAs(stopResult);
        result.IsFailed.ShouldBeTrue();
        var after = (await dbContext.GetDownloadTaskFileAsync(file.ToKey(), CancellationToken))!;
        after.DownloadStatus.ShouldBe(DownloadStatus.Moving);
        after.DataReceived.ShouldBe(128);
        after.FileDataTransferred.ShouldBe(64);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(x => x.OnStatusChangedAsync(It.IsAny<DownloadTaskKey>(), It.IsAny<DownloadStatus>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
