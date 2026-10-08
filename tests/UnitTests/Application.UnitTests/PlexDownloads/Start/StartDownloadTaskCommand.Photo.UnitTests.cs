namespace Reaparr.Application.UnitTests;

public class StartDownloadTaskCommandPhotoUnitTests : BaseCommandUnitTest<StartDownloadTaskCommand>
{
    [Test]
    [Arguments(DownloadTaskType.PhotoImage, false)]
    [Arguments(DownloadTaskType.PhotoImage, true)]
    [Arguments(DownloadTaskType.PhotoAlbum, false)]
    [Arguments(DownloadTaskType.PhotoAlbum, true)]
    [Arguments(DownloadTaskType.PhotoData, false)]
    [Arguments(DownloadTaskType.PhotoData, true)]
    public async Task ShouldQueueOnlySelectedPausedPhotoPartsOrRestoreThem_WhenStartingPhotos(
        DownloadTaskType selection,
        bool failStart
    )
    {
        // Arrange
        await SetupDatabase(88020, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var partSeed = new Seed(88022);
        var album = FakeData
            .GetDownloadTaskPhotoAlbum(new Seed(88020))
            .RuleFor(
                x => x.Children,
                _ =>
                    FakeData
                        .GetDownloadTaskPhotoImage(new Seed(88021))
                        .RuleFor(x => x.Children, _ => FakeData.GetDownloadTaskPhotoImageFile(partSeed).Generate(3))
                        .Generate(2)
            )
            .Generate();
        var unselectedAlbum = FakeData.GetDownloadTaskPhotoAlbum(new Seed(88023)).Generate();
        var images = album.Children.Concat(unselectedAlbum.Children).ToList();
        var files = images.SelectMany(x => x.Children).ToList();
        foreach (
            var node in new DownloadTaskBase[] { album, unselectedAlbum }
                .Concat(images)
                .Concat(files)
        )
        {
            node.PlexServerId = library.PlexServerId;
            node.PlexLibraryId = library.Id;
            node.DownloadStatus = DownloadStatus.Paused;
        }
        dbContext.DownloadTaskPhotoAlbums.AddRange(album, unselectedAlbum);
        await dbContext.SaveChangesAsync(CancellationToken);
        var key = selection switch
        {
            DownloadTaskType.PhotoAlbum => album.ToKey(),
            DownloadTaskType.PhotoImage => album.Children.First().ToKey(),
            _ => album.Children.First().Children.First().ToKey(),
        };
        var selectedFiles = await dbContext.GetDownloadableChildTasks(key, CancellationToken);
        selectedFiles.Count.ShouldBe(
            selection == DownloadTaskType.PhotoAlbum ? 6
            : selection == DownloadTaskType.PhotoImage ? 3
            : 1
        );
        selectedFiles.Select(x => x.DownloadStatus).ShouldAllBe(x => x == DownloadStatus.Paused);
        var selected = selectedFiles.First().ToKey();
        var selectedIds = selectedFiles.Select(x => x.Id).ToHashSet();
        var before = await dbContext
            .DownloadTaskPhotoImageFiles.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.DownloadStatus,
            })
            .ToListAsync(CancellationToken);
        var schedulerError = new Error("Download scheduler rejected selected photo part");
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(selected, CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(selected, CancellationToken))
            .ReturnsAsync(failStart ? Result.Fail(schedulerError) : Result.Ok())
            .Verifiable(Times.Once());
        if (!failStart)
        {
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.GetCurrentlyDownloadingKeysByServer(library.PlexServerId))
                .ReturnsAsync([selected])
                .Verifiable(Times.Once());
            Mock.Mock<IEventPublisher>()
                .Setup(x =>
                    x.PublishAsync(
                        It.Is<CheckDownloadQueueEvent>(e =>
                            e.PlexServerIds.SequenceEqual(new[] { library.PlexServerId })
                        ),
                        CancellationToken
                    )
                )
                .Returns(Task.CompletedTask)
                .Verifiable(Times.Once());
        }

        // Act
        var result = await TestHandlerExecuteAsync(new StartDownloadTaskCommand(key.Id));

        // Assert
        result.IsSuccess.ShouldBe(!failStart);
        result.Errors.Count.ShouldBe(failStart ? 1 : 0);
        var expectedStatus = failStart ? DownloadStatus.Paused : DownloadStatus.Queued;
        var after = await dbContext
            .DownloadTaskPhotoImageFiles.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.DownloadStatus,
            })
            .ToListAsync(CancellationToken);
        after.ShouldBe(
            before.Select(x => new
            {
                x.Id,
                x.ParentId,
                DownloadStatus = selectedIds.Contains(x.Id) ? expectedStatus : x.DownloadStatus,
            })
        );
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.Is<DownloadTaskKey>(k => k != selected), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IEventPublisher>().Verify();
        if (failStart)
        {
            result.Errors.Single().ShouldBeSameAs(schedulerError);
            Mock.Mock<IDownloadTaskScheduler>()
                .Verify(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()), Times.Never());
            Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
        }
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, false, true)]
    [Arguments(false, true, false)]
    [Arguments(false, true, true)]
    [Arguments(true, false, false)]
    [Arguments(true, false, true)]
    [Arguments(true, true, false)]
    [Arguments(true, true, true)]
    public async Task ShouldResumeSelectedPhotoPartsByPhaseAndRestoreRejectedMove_WhenStartingPhotos(
        bool startAlbum,
        bool includeDownloads,
        bool failMove
    )
    {
        // Arrange
        await SetupDatabase(88030, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var partSeed = new Seed(88032);
        var album = FakeData
            .GetDownloadTaskPhotoAlbum(new Seed(88030))
            .RuleFor(
                x => x.Children,
                _ =>
                    FakeData
                        .GetDownloadTaskPhotoImage(new Seed(88031))
                        .RuleFor(x => x.Children, _ => FakeData.GetDownloadTaskPhotoImageFile(partSeed).Generate(6))
                        .Generate(2)
            )
            .Generate();
        var unselectedAlbum = FakeData.GetDownloadTaskPhotoAlbum(new Seed(88033)).Generate();
        var images = album.Children.Concat(unselectedAlbum.Children).ToList();
        foreach (
            var node in new DownloadTaskBase[] { album, unselectedAlbum }
                .Concat(images)
                .Concat(images.SelectMany(x => x.Children))
        )
        {
            node.PlexServerId = library.PlexServerId;
            node.PlexLibraryId = library.Id;
            node.DownloadStatus = DownloadStatus.Paused;
        }
        dbContext.DownloadTaskPhotoAlbums.AddRange(album, unselectedAlbum);
        await dbContext.SaveChangesAsync(CancellationToken);
        var key = startAlbum ? album.ToKey() : album.Children.First().ToKey();
        var selectedFiles = await dbContext.GetDownloadableChildTasks(key, CancellationToken);
        DownloadStatus[] statuses =
        [
            DownloadStatus.MovePaused,
            DownloadStatus.AutoMovePaused,
            includeDownloads ? DownloadStatus.Paused : DownloadStatus.MovePaused,
            includeDownloads ? DownloadStatus.AutoPaused : DownloadStatus.AutoMovePaused,
            DownloadStatus.DownloadFinished,
            DownloadStatus.Completed,
        ];
        for (var index = 0; index < selectedFiles.Count; index++)
            await dbContext.SetDownloadStatus(selectedFiles[index].ToKey(), statuses[index % statuses.Length]);

        selectedFiles = await dbContext.GetDownloadableChildTasks(key, CancellationToken);
        selectedFiles
            .Select(x => x.DownloadStatus)
            .ShouldBe(Enumerable.Range(0, selectedFiles.Count).Select(x => statuses[x % statuses.Length]));
        var before = await dbContext
            .DownloadTaskPhotoImageFiles.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.DownloadStatus,
            })
            .ToListAsync(CancellationToken);
        var downloadKeys = selectedFiles
            .Where(x => x.DownloadTaskPhase == DownloadTaskPhase.Downloading)
            .Select(x => x.ToKey())
            .ToList();
        var moveKeys = selectedFiles
            .Where(x => x.DownloadTaskPhase == DownloadTaskPhase.FileTransfer)
            .Select(x => x.ToKey())
            .ToList();
        var attemptedMoves = failMove ? moveKeys.Take(includeDownloads ? 2 : 1).ToList() : moveKeys;
        var successfulMoveIds = attemptedMoves
            .Where(x => !failMove || x != attemptedMoves.Last())
            .Select(x => x.Id)
            .ToHashSet();
        var queuedIds = downloadKeys.Select(x => x.Id).ToHashSet();
        var schedulerError = new Error("Move scheduler rejected selected photo part");
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        if (includeDownloads)
        {
            var selectedDownload = downloadKeys.First();
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.IsDownloading(selectedDownload, CancellationToken))
                .ReturnsAsync(false)
                .Verifiable(Times.Once());
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.StartDownloadTaskJob(selectedDownload, CancellationToken))
                .ReturnsAsync(Result.Ok())
                .Verifiable(Times.Once());
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.GetCurrentlyDownloadingKeysByServer(library.PlexServerId))
                .ReturnsAsync([selectedDownload])
                .Verifiable(Times.Once());
        }
        foreach (var moveKey in attemptedMoves)
        {
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.IsDownloadFileMoving(moveKey, CancellationToken))
                .ReturnsAsync(false)
                .Verifiable(Times.Once());
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.StartMoveDownloadFileJob(moveKey, CancellationToken))
                .ReturnsAsync(failMove && moveKey == attemptedMoves.Last() ? Result.Fail(schedulerError) : Result.Ok())
                .Verifiable(Times.Once());
        }
        if (!failMove)
        {
            Mock.Mock<IEventPublisher>()
                .Setup(x =>
                    x.PublishAsync(
                        It.Is<CheckDownloadQueueEvent>(e =>
                            e.PlexServerIds.SequenceEqual(new[] { library.PlexServerId })
                        ),
                        CancellationToken
                    )
                )
                .Returns(Task.CompletedTask)
                .Verifiable(Times.Once());
        }

        // Act
        var result = await TestHandlerExecuteAsync(new StartDownloadTaskCommand(key.Id));

        // Assert
        result.IsSuccess.ShouldBe(!failMove);
        result.Errors.Count.ShouldBe(failMove ? 1 : 0);
        if (failMove)
            result.Errors.Single().ShouldBeSameAs(schedulerError);
        var after = await dbContext
            .DownloadTaskPhotoImageFiles.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.DownloadStatus,
            })
            .ToListAsync(CancellationToken);
        after.ShouldBe(
            before.Select(x => new
            {
                x.Id,
                x.ParentId,
                DownloadStatus = queuedIds.Contains(x.Id) ? DownloadStatus.Queued
                : successfulMoveIds.Contains(x.Id) ? DownloadStatus.DownloadFinished
                : x.DownloadStatus,
            })
        );
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x =>
                    x.StartDownloadTaskJob(
                        It.Is<DownloadTaskKey>(k => !includeDownloads || k != downloadKeys[0]),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x =>
                    x.StartMoveDownloadFileJob(
                        It.Is<DownloadTaskKey>(k => !attemptedMoves.Contains(k)),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.Mock<IEventPublisher>().Verify();
        if (failMove)
            Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldLeaveAllPhotoPartsUnchanged_WhenServerIsPaused(bool startAlbum)
    {
        // Arrange
        await SetupDatabase(88040, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var album = FakeData
            .GetDownloadTaskPhotoAlbum(new Seed(88040))
            .RuleFor(
                x => x.Children,
                _ =>
                    FakeData
                        .GetDownloadTaskPhotoImage(new Seed(88041))
                        .RuleFor(
                            x => x.Children,
                            _ => FakeData.GetDownloadTaskPhotoImageFile(new Seed(88042)).Generate(2)
                        )
                        .Generate(1)
            )
            .Generate();
        var image = album.Children.Single();
        foreach (var node in new DownloadTaskBase[] { album, image }.Concat(image.Children))
        {
            node.PlexServerId = library.PlexServerId;
            node.PlexLibraryId = library.Id;
            node.DownloadStatus = DownloadStatus.Paused;
        }
        image.Children.Last().DownloadStatus = DownloadStatus.MovePaused;
        dbContext.DownloadTaskPhotoAlbums.Add(album);
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext
            .PlexServers.Where(x => x.Id == library.PlexServerId)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDownloadsPausedByUser, true), CancellationToken);
        var before = await dbContext
            .DownloadTaskPhotoImageFiles.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.DownloadStatus,
            })
            .ToListAsync(CancellationToken);
        before
            .Select(x => x.DownloadStatus)
            .OrderBy(x => x)
            .ShouldBe(new[] { DownloadStatus.Paused, DownloadStatus.MovePaused }.OrderBy(x => x));

        // Act
        var result = await TestHandlerExecuteAsync(new StartDownloadTaskCommand(startAlbum ? album.Id : image.Id));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        var after = await dbContext
            .DownloadTaskPhotoImageFiles.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.DownloadStatus,
            })
            .ToListAsync(CancellationToken);
        after.ShouldBe(before);
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
    }

}
