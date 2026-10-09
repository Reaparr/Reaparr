namespace Reaparr.Application.UnitTests;

public class PauseDownloadTaskCommandMusicUnitTests : BaseCommandUnitTest<PauseDownloadTaskCommand>
{
    [Test]
    [Arguments(DownloadTaskType.MusicArtist, false, DownloadStatus.Downloading)]
    [Arguments(DownloadTaskType.MusicAlbum, false, DownloadStatus.Downloading)]
    [Arguments(DownloadTaskType.MusicTrack, false, DownloadStatus.Downloading)]
    [Arguments(DownloadTaskType.MusicTrackData, false, DownloadStatus.Downloading)]
    [Arguments(DownloadTaskType.MusicArtist, true, DownloadStatus.Moving)]
    [Arguments(DownloadTaskType.MusicAlbum, true, DownloadStatus.Moving)]
    [Arguments(DownloadTaskType.MusicTrack, true, DownloadStatus.Moving)]
    [Arguments(DownloadTaskType.MusicTrackData, true, DownloadStatus.DownloadFinished)]
    [Arguments(DownloadTaskType.MusicTrackData, true, DownloadStatus.Completed)]
    public async Task ShouldPauseSelectedMusicByPhase_WhenPausing(
        DownloadTaskType selection,
        bool autoPause,
        DownloadStatus before
    )
    {
        // Arrange
        await SetupDatabase(
            88210,
            c =>
            {
                c.PlexMusicLibraryCount = 1;
                c.MusicArtistDownloadTasksCount = 1;
                c.MusicAlbumDownloadTasksCount = 1;
                c.MusicTrackDownloadTasksCount = 1;
                c.MusicTrackFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var target = await dbContext.DownloadTaskMusicTrackFiles
            .Include(x => x.Parent)
                .ThenInclude(x => x!.Parent)
                    .ThenInclude(x => x!.Parent)
            .SingleAsync(CancellationToken);
        target.DataReceived = 128;
        target.FileDataTransferred = 64;
        dbContext.Entry(target).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.SetDownloadStatus(target.ToKey(), before);

        var nodeKey = selection switch
        {
            DownloadTaskType.MusicArtist => target.Parent!.Parent!.Parent!.ToKey(),
            DownloadTaskType.MusicAlbum => target.Parent!.Parent!.ToKey(),
            DownloadTaskType.MusicTrack => target.Parent!.ToKey(),
            _ => target.ToKey(),
        };
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
    public async Task ShouldPropagateRejectedMoveStop_WhenPausingMusic()
    {
        // Arrange
        await SetupDatabase(
            88320,
            c =>
            {
                c.PlexMusicLibraryCount = 1;
                c.MusicArtistDownloadTasksCount = 1;
                c.MusicAlbumDownloadTasksCount = 1;
                c.MusicTrackDownloadTasksCount = 1;
                c.MusicTrackFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var file = await dbContext.DownloadTaskMusicTrackFiles.SingleAsync(CancellationToken);
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
