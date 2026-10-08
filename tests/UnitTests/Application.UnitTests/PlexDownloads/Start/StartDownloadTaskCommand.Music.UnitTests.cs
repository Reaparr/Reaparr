namespace Reaparr.Application.UnitTests;

public class StartDownloadTaskCommandMusicUnitTests : BaseCommandUnitTest<StartDownloadTaskCommand>
{
    [Test]
    [Arguments(DownloadTaskType.MusicArtist, false)]
    [Arguments(DownloadTaskType.MusicAlbum, false)]
    [Arguments(DownloadTaskType.MusicTrack, false)]
    [Arguments(DownloadTaskType.MusicTrackData, false)]
    [Arguments(DownloadTaskType.MusicArtist, true)]
    [Arguments(DownloadTaskType.MusicAlbum, true)]
    [Arguments(DownloadTaskType.MusicTrack, true)]
    [Arguments(DownloadTaskType.MusicTrackData, true)]
    public async Task ShouldResumeOnlySelectedMusicOrRestoreRejectedStart_WhenStarting(
        DownloadTaskType selection,
        bool failStart
    )
    {
        // Arrange
        await SetupDatabase(
            88200,
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
        var target = await dbContext.DownloadTaskMusicTrackFiles.SingleAsync(CancellationToken);
        DownloadTaskBase node = selection switch
        {
            DownloadTaskType.MusicArtist => target.Parent!.Parent!.Parent!,
            DownloadTaskType.MusicAlbum => target.Parent!.Parent!,
            DownloadTaskType.MusicTrack => target.Parent!,
            _ => target,
        };
        await dbContext.SetDownloadStatus(target.ToKey(), DownloadStatus.AutoPaused);
        (await dbContext.GetDownloadableChildTaskKeys(node.ToKey(), CancellationToken)).ShouldBe([target.ToKey()]);
        var error = new Error("rejected start");
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(target.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(target.ToKey(), CancellationToken))
            .ReturnsAsync(failStart ? Result.Fail(error) : Result.Ok())
            .Verifiable(Times.Once());
        if (!failStart)
        {
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.GetCurrentlyDownloadingKeysByServer(target.PlexServerId))
                .ReturnsAsync([target.ToKey()])
                .Verifiable(Times.Once());
            Mock.Mock<IEventPublisher>()
                .Setup(x =>
                    x.PublishAsync(
                        It.Is<CheckDownloadQueueEvent>(e => e.PlexServerIds.SequenceEqual(new[] { target.PlexServerId })),
                        CancellationToken
                    )
                )
                .Returns(Task.CompletedTask)
                .Verifiable(Times.Once());
        }

        // Act
        var result = await TestHandlerExecuteAsync(new StartDownloadTaskCommand(node.Id));

        // Assert
        result.IsSuccess.ShouldBe(!failStart);
        result.Errors.Count.ShouldBe(failStart ? 1 : 0);
        if (failStart)
            result.Errors.Single().ShouldBeSameAs(error);
        (await dbContext.GetDownloadTaskFileAsync(target.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(
            failStart ? DownloadStatus.AutoPaused : DownloadStatus.Queued
        );
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<PauseDownloadTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        if (failStart)
            Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
    }

    [Test]
    [Arguments(DownloadTaskType.MusicArtist, false)]
    [Arguments(DownloadTaskType.MusicAlbum, false)]
    [Arguments(DownloadTaskType.MusicTrack, false)]
    [Arguments(DownloadTaskType.MusicArtist, true)]
    [Arguments(DownloadTaskType.MusicAlbum, true)]
    [Arguments(DownloadTaskType.MusicTrack, true)]
    public async Task ShouldResumeAllSelectedMusicByPhaseAndRestoreRejectedMove_WhenContinuing(
        DownloadTaskType selection,
        bool failMove
    )
    {
        // Arrange
        await SetupDatabase(
            88330,
            c =>
            {
                c.PlexMusicLibraryCount = 1;
                c.MusicArtistDownloadTasksCount = 4;
                c.MusicAlbumDownloadTasksCount = 1;
                c.MusicTrackDownloadTasksCount = 1;
                c.MusicTrackFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var files = (await dbContext.DownloadTaskMusicTrackFiles.OrderBy(x => x.PlexApiRatingKey).ToListAsync(CancellationToken))
            .Cast<DownloadTaskFileBase>()
            .ToList();
        var download = files[0];
        for (var index = 1; index < 3; index++)
        {
            await dbContext
                .DownloadTaskMusicTrackFiles.Where(x => x.Id == files[index].Id)
                .ExecuteUpdateAsync(
                    p => p.SetProperty(x => x.ParentId, ((DownloadTaskMusicTrackFile)download).ParentId),
                    CancellationToken
                );
        }
        DownloadTaskBase node = selection switch
        {
            DownloadTaskType.MusicArtist => ((DownloadTaskMusicTrackFile)download).Parent!.Parent!.Parent!,
            DownloadTaskType.MusicAlbum => ((DownloadTaskMusicTrackFile)download).Parent!.Parent!,
            _ => ((DownloadTaskMusicTrackFile)download).Parent!,
        };
        DownloadStatus[] statuses =
        [
            DownloadStatus.Paused,
            DownloadStatus.AutoMovePaused,
            DownloadStatus.MovePaused,
            DownloadStatus.Paused,
        ];
        for (var index = 0; index < files.Count; index++)
            await dbContext.SetDownloadStatus(files[index].ToKey(), statuses[index]);
        var selected = await dbContext.GetDownloadableChildTasks(node.ToKey(), CancellationToken);
        selected.Select(x => x.Id).Order().ShouldBe(files.Take(3).Select(x => x.Id).Order());
        var moves = selected.Where(x => x.DownloadTaskPhase == DownloadTaskPhase.FileTransfer).ToList();
        moves.Count.ShouldBe(2);
        var error = new Error("move rejected");
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(download.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(download.ToKey(), CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(download.PlexServerId))
            .ReturnsAsync([download.ToKey()])
            .Verifiable(Times.Once());
        foreach (var move in moves)
        {
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.IsDownloadFileMoving(move.ToKey(), CancellationToken))
                .ReturnsAsync(false)
                .Verifiable(Times.Once());
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.StartMoveDownloadFileJob(move.ToKey(), CancellationToken))
                .ReturnsAsync(failMove && move.Id == moves.Last().Id ? Result.Fail(error) : Result.Ok())
                .Verifiable(Times.Once());
        }
        if (!failMove)
            Mock.Mock<IEventPublisher>()
                .Setup(x =>
                    x.PublishAsync(
                        It.Is<CheckDownloadQueueEvent>(e =>
                            e.PlexServerIds.SequenceEqual(new[] { download.PlexServerId })
                        ),
                        CancellationToken
                    )
                )
                .Returns(Task.CompletedTask)
                .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new StartDownloadTaskCommand(node.Id));

        // Assert
        result.IsSuccess.ShouldBe(!failMove);
        result.Errors.Count.ShouldBe(failMove ? 1 : 0);
        if (failMove)
            result.Errors.Single().ShouldBeSameAs(error);
        (await dbContext.GetDownloadTaskFileAsync(download.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(
            DownloadStatus.Queued
        );
        foreach (var move in moves)
            (await dbContext.GetDownloadTaskFileAsync(move.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(
                failMove && move.Id == moves.Last().Id ? move.DownloadStatus : DownloadStatus.DownloadFinished
            );
        (await dbContext.GetDownloadTaskFileAsync(files[3].ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(
            DownloadStatus.Paused
        );
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x =>
                    x.StartDownloadTaskJob(
                        It.Is<DownloadTaskKey>(k => k.Id != download.Id),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(x => x.StartMoveDownloadFileJob(files[3].ToKey(), It.IsAny<CancellationToken>()), Times.Never());
        if (failMove)
            Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
    }

    [Test]
    public async Task ShouldRestorePausedMusicAndPropagateCancellation_WhenStartIsCancelled()
    {
        // Arrange
        await SetupDatabase(
            88340,
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
        await dbContext.SetDownloadStatus(file.ToKey(), DownloadStatus.AutoPaused);
        (await dbContext.GetDownloadTaskFileAsync(file.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(
            DownloadStatus.AutoPaused
        );
        var cancelled = Result.Try((Action)(() => throw new OperationCanceledException(CancellationToken)));
        cancelled.IsCancelled.ShouldBeTrue();
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(file.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(file.ToKey(), CancellationToken))
            .ReturnsAsync(cancelled)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new StartDownloadTaskCommand(file.Id));

        // Assert
        result.ShouldBeSameAs(cancelled);
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        (await dbContext.GetDownloadTaskFileAsync(file.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(
            DownloadStatus.AutoPaused
        );
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()), Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<PauseDownloadTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
    }
}
