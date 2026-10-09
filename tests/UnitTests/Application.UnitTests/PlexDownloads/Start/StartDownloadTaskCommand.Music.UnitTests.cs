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
    public async Task ShouldQueueWaitingMusicOnlyForContainersAndRestoreRejectedStart_WhenStarting(
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
                c.MusicArtistDownloadTasksCount = 2;
                c.MusicAlbumDownloadTasksCount = 1;
                c.MusicTrackDownloadTasksCount = 1;
                c.MusicTrackFileDownloadTasksCount = 3;
            }
        );
        var dbContext = IDbContext;
        var files = await dbContext.DownloadTaskMusicTrackFiles
            .Include(x => x.Parent).ThenInclude(x => x!.Parent).ThenInclude(x => x!.Parent)
            .OrderBy(x => x.Id).ToArrayAsync(CancellationToken);
        foreach (var file in files)
            await dbContext.SetDownloadStatus(file.ToKey(), DownloadStatus.AutoPaused);
        var target = files[0];
        DownloadTaskBase node = selection switch
        {
            DownloadTaskType.MusicArtist => target.Parent!.Parent!.Parent!,
            DownloadTaskType.MusicAlbum => target.Parent!.Parent!,
            DownloadTaskType.MusicTrack => target.Parent!,
            _ => target,
        };
        var selectedFiles = await dbContext.GetDownloadableChildTasks(node.ToKey(), CancellationToken);
        selectedFiles.Count.ShouldBe(selection == DownloadTaskType.MusicTrackData ? 1 : 3);
        var selectedIds = selectedFiles.Select(x => x.Id).ToHashSet();
        target = files.Single(x => x.Id == selectedFiles.First().Id);
        var container = selection is DownloadTaskType.MusicArtist or DownloadTaskType.MusicAlbum;
        var error = new Error("rejected start");
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(target.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(target.ToKey(), CancellationToken))
            .Returns(async () =>
            {
                foreach (var file in files)
                    (await dbContext.GetDownloadTaskFileAsync(file.ToKey(), CancellationToken))!.DownloadStatus
                        .ShouldBe(file.Id == target.Id || container && selectedIds.Contains(file.Id)
                            ? DownloadStatus.Queued : DownloadStatus.AutoPaused);
                return failStart ? Result.Fail(error) : Result.Ok();
            })
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
        foreach (var file in files)
            (await dbContext.GetDownloadTaskFileAsync(file.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(
                !failStart && (file.Id == target.Id || container && selectedIds.Contains(file.Id))
                    ? DownloadStatus.Queued : DownloadStatus.AutoPaused
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
    public async Task ShouldResumeOnlyNextMusicMoveAndRestoreRejectedMove_WhenContinuing(
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
        var files = (await dbContext.DownloadTaskMusicTrackFiles
            .Include(x => x.Parent).ThenInclude(x => x!.Parent).ThenInclude(x => x!.Parent)
            .OrderBy(x => x.PlexApiRatingKey).ToListAsync(CancellationToken))
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
            DownloadStatus.MovePaused,
            DownloadStatus.AutoMovePaused,
            DownloadStatus.MovePaused,
            DownloadStatus.Paused,
        ];
        for (var index = 0; index < files.Count; index++)
            await dbContext.SetDownloadStatus(files[index].ToKey(), statuses[index]);
        var selected = await dbContext.GetDownloadableChildTasks(node.ToKey(), CancellationToken);
        selected.Select(x => x.Id).Order().ShouldBe(files.Take(3).Select(x => x.Id).Order());
        var moves = selected.ToList();
        moves.Count.ShouldBe(3);
        var nextMove = moves.First();
        var error = new Error("move rejected");
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        foreach (var move in moves.Take(1))
        {
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.IsDownloadFileMoving(move.ToKey(), CancellationToken))
                .ReturnsAsync(false)
                .Verifiable(Times.Once());
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.StartMoveDownloadFileJob(move.ToKey(), CancellationToken))
                .ReturnsAsync(failMove ? Result.Fail(error) : Result.Ok())
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
        foreach (var move in moves)
            (await dbContext.GetDownloadTaskFileAsync(move.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(
                !failMove && move.Id == nextMove.Id ? DownloadStatus.DownloadFinished : move.DownloadStatus
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
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(x => x.StartMoveDownloadFileJob(
                It.Is<DownloadTaskKey>(k => k.Id != nextMove.Id), It.IsAny<CancellationToken>()), Times.Never());
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
