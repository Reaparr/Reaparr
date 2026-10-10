namespace Reaparr.Application.UnitTests;

public class PauseDownloadTaskCommandPhotoUnitTests : BaseCommandUnitTest<PauseDownloadTaskCommand>
{
    [Test]
    [Arguments(DownloadStatus.Downloading, DownloadStatus.Paused, DownloadTaskType.PhotoImage)]
    [Arguments(DownloadStatus.DownloadFinished, DownloadStatus.MovePaused, DownloadTaskType.PhotoImage)]
    [Arguments(DownloadStatus.Moving, DownloadStatus.MovePaused, DownloadTaskType.PhotoImage)]
    [Arguments(DownloadStatus.MoveFinished, DownloadStatus.MoveFinished, DownloadTaskType.PhotoImage)]
    [Arguments(DownloadStatus.Completed, DownloadStatus.Completed, DownloadTaskType.PhotoImage)]
    [Arguments(DownloadStatus.Downloading, DownloadStatus.Paused, DownloadTaskType.PhotoData)]
    [Arguments(DownloadStatus.Downloading, DownloadStatus.Paused, DownloadTaskType.PhotoAlbum)]
    public async Task ShouldPauseSelectedPhotosAndPreserveFinishedMoves_WhenPausing(
        DownloadStatus before,
        DownloadStatus expected,
        DownloadTaskType selection
    )
    {
        // Arrange
        await SetupDatabase(88090, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var album = FakeData
            .GetDownloadTaskPhotoAlbum(new Seed(88090))
            .RuleFor(x => x.Children, _ => FakeData.GetDownloadTaskPhotoImage(new Seed(88091)).Generate(2))
            .Generate();
        var files = album.Children.SelectMany(x => x.Children).ToList();
        foreach (
            var node in new DownloadTaskBase[] { album }
                .Concat(album.Children)
                .Concat(files)
        )
        {
            node.PlexServerId = library.PlexServerId;
            node.PlexLibraryId = library.Id;
        }
        var target = files[0];
        var sibling = files[1];
        target.DownloadStatus = before;
        album.Children.First().DownloadStatus = before;
        target.DataReceived = 512;
        dbContext.DownloadTaskPhotoAlbums.Add(album);
        await dbContext.SaveChangesAsync(CancellationToken);
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

            Mock.Mock<ICommandExecutor>()
                .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
                .ReturnsAsync(Result.Ok());
        }
        if (before is DownloadStatus.Moving or DownloadStatus.DownloadFinished)
        {
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.IsDownloadFileMoving(target.ToKey(), CancellationToken))
                .ReturnsAsync(before == DownloadStatus.Moving)
                .Verifiable(Times.Once());
        }
        if (before == DownloadStatus.Moving)
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.StopMoveDownloadFileJob(target.ToKey(), CancellationToken))
                .ReturnsAsync(Result.Ok())
                .Verifiable(Times.Once());
        if (selection == DownloadTaskType.PhotoAlbum)
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.IsDownloading(sibling.ToKey(), CancellationToken))
                .ReturnsAsync(false)
                .Verifiable(Times.Once());

        // Act
        var id = selection switch
        {
            DownloadTaskType.PhotoAlbum => album.Id,
            DownloadTaskType.PhotoImage => target.ParentId,
            _ => target.Id,
        };
        var result = await TestHandlerExecuteAsync(new PauseDownloadTaskCommand(id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(x => x.Id == target.Id, CancellationToken);
        after.DownloadStatus.ShouldBe(expected);
        after.DataReceived.ShouldBe(before == DownloadStatus.Moving ? 0 : 512);
        (
            await dbContext.DownloadTaskPhotoImages.SingleAsync(x => x.Id == target.ParentId, CancellationToken)
        ).DownloadStatus.ShouldBe(expected);
        (await dbContext.DownloadTaskPhotoAlbums.SingleAsync(CancellationToken)).DownloadStatus.ShouldBe(
            before is DownloadStatus.MoveFinished or DownloadStatus.Completed ? DownloadStatus.Queued : expected
        );
        (
            await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(x => x.Id == sibling.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(
            selection == DownloadTaskType.PhotoAlbum ? DownloadStatus.Paused : DownloadStatus.Queued
        );
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }


}
