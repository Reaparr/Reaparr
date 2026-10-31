namespace Reaparr.Application.UnitTests;

public class StartDownloadTaskCommandPhotoUnitTests : BaseCommandUnitTest<StartDownloadTaskCommand>
{
    [Test]
    public async Task ShouldQueueOnlySelectedPhotoSubtreeAndRestoreEveryQueuedDescendant_WhenStartingRootFails()
    {
        // Arrange
        await SetupDatabase(88020, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var root = FakeData.GetDownloadTaskPhotoFolder(new Seed(88020)).Generate();
        var nested = FakeData
            .GetDownloadTaskPhotoFolder(new Seed(88021))
            .RuleFor(x => x.Files, _ => FakeData.GetDownloadTaskPhotoImageFile(new Seed(88022)).Generate(1))
            .Generate();
        nested.ParentId = root.Id;
        nested.Parent = root;
        nested.Title = "Trips";
        nested.RelativePath = "Trips";
        nested.FullTitle = $"{root.FullTitle}/Trips";
        root.Children.Add(nested);
        new List<DownloadTaskPhotoFolder> { root }.SetRelationshipIds(library.PlexServerId, library.Id);
        root.DownloadStatus = DownloadStatus.Paused;
        nested.DownloadStatus = DownloadStatus.Paused;
        foreach (var file in root.Files.Concat(nested.Files))
        {
            file.DownloadStatus = DownloadStatus.Paused;
            file.DataReceived = 128;
            file.DirectoryMeta.PhotoFolder = file.ParentId == nested.Id ? "Trips" : string.Empty;
        }
        dbContext.DownloadTaskPhotoFolders.Add(root);
        await dbContext.SaveChangesAsync(CancellationToken);

        var childKeys = await dbContext.GetDownloadableChildTaskKeys(root.ToKey(), CancellationToken);
        childKeys.Select(x => x.Type).ShouldAllBe(x => x == DownloadTaskType.PhotoImage);
        childKeys.Count.ShouldBe(2);
        var expectedFiles = await dbContext
            .DownloadTaskPhotoImageFiles.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.DownloadStatus,
                x.DataReceived,
            })
            .ToListAsync(CancellationToken);
        expectedFiles.ShouldAllBe(x => x.DownloadStatus == DownloadStatus.Paused && x.DataReceived == 128);
        var selected = childKeys[0];
        var schedulerError = new Error("Download scheduler rejected the selected photo file");
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(selected, CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(selected, CancellationToken))
            .ReturnsAsync(Result.Fail(schedulerError))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new StartDownloadTaskCommand(root.Id));

        dbContext.ClearChangeTracker();
        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        var filesAfter = await dbContext
            .DownloadTaskPhotoImageFiles.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.DownloadStatus,
                x.DataReceived,
            })
            .ToListAsync(CancellationToken);
        filesAfter.ShouldBe(expectedFiles);
        filesAfter.ShouldAllBe(x => x.DownloadStatus == DownloadStatus.Paused && x.DataReceived == 128);
        (
            await dbContext.DownloadTaskPhotoFolders.SingleAsync(x => x.Id == root.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.Paused);
        (
            await dbContext.DownloadTaskPhotoFolders.SingleAsync(x => x.Id == nested.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.Paused);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x =>
                    x.StartDownloadTaskJob(
                        It.Is<DownloadTaskKey>(key => key != selected),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
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
    public async Task ShouldStartOnlyNextPhotoMoveAndPreserveStatuses_WhenStartingPhotos(
        bool selectFolder,
        bool includeDownloads,
        bool failMove
    )
    {
        // Arrange
        await SetupDatabase(88030, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var root = FakeData.GetDownloadTaskPhotoFolder(new Seed(88030)).Generate();
        var nested = FakeData
            .GetDownloadTaskPhotoFolder(new Seed(88031))
            .RuleFor(x => x.Files, _ => FakeData.GetDownloadTaskPhotoImageFile(new Seed(88032)).Generate(6))
            .Generate();
        nested.ParentId = root.Id;
        nested.Parent = root;
        nested.Title = "Trips";
        nested.RelativePath = "Trips";
        nested.FullTitle = $"{root.FullTitle}/Trips";
        root.Children.Add(nested);
        new List<DownloadTaskPhotoFolder> { root }.SetRelationshipIds(library.PlexServerId, library.Id);
        root.DownloadStatus = DownloadStatus.Queued;
        nested.DownloadStatus = DownloadStatus.Queued;
        root.Files.Single().DownloadStatus = DownloadStatus.Queued;
        var allFiles = nested.Files.ToArray();
        for (var index = 0; index < allFiles.Length; index++)
            allFiles[index].FullTitle = $"{nested.FullTitle}/{index:D2}";
        var directRootFile = root.Files.Single();
        var orderedFiles = nested.Files.OrderBy(x => x.FullTitle).ToArray();
        DownloadStatus[] statuses =
        [
            DownloadStatus.MovePaused,
            DownloadStatus.AutoMovePaused,
            includeDownloads ? DownloadStatus.Paused : DownloadStatus.MovePaused,
            includeDownloads ? DownloadStatus.AutoPaused : DownloadStatus.AutoMovePaused,
            DownloadStatus.DownloadFinished,
            DownloadStatus.Completed,
        ];
        for (var index = 0; index < orderedFiles.Length; index++)
        {
            orderedFiles[index].DownloadStatus = statuses[index];
            orderedFiles[index].DataReceived = 512;
            orderedFiles[index].DirectoryMeta.PhotoFolder = "Trips";
        }
        dbContext.DownloadTaskPhotoFolders.Add(root);
        await dbContext.SaveChangesAsync(CancellationToken);
        var target = orderedFiles[0];
        var moveError = new Error("Move scheduler rejected the selected photo file");
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(target.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StartMoveDownloadFileJob(target.ToKey(), CancellationToken))
            .ReturnsAsync(failMove ? Result.Fail(moveError) : Result.Ok())
            .Verifiable(Times.Once());
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
        var result = await TestHandlerExecuteAsync(new StartDownloadTaskCommand(selectFolder ? nested.Id : target.Id));

        dbContext.ClearChangeTracker();
        // Assert
        result.IsSuccess.ShouldBe(!failMove);
        result.IsFailed.ShouldBe(failMove);
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(failMove ? 1 : 0);
        if (failMove)
            result.Errors.Single().ShouldBeSameAs(moveError);
        var after = await dbContext
            .DownloadTaskPhotoImageFiles.Where(x => x.ParentId == nested.Id)
            .OrderBy(x => x.FullTitle)
            .Select(x => new
            {
                x.Id,
                x.DownloadStatus,
                x.DataReceived,
            })
            .ToListAsync(CancellationToken);
        after.Select(x => x.Id).ShouldBe(orderedFiles.Select(x => x.Id));
        after.Select(x => x.DownloadStatus).ShouldBe(statuses);
        after.ShouldAllBe(x => x.DataReceived == 512);
        (
            await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(x => x.Id == directRootFile.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.Queued);
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()), Times.Never());
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
                failMove ? Times.Never() : Times.Once()
            );
    }

    [Test]
    public async Task ShouldQueueNestedPhotoFileWithoutTouchingDirectRootFile_WhenStartingFile()
    {
        // Arrange
        await SetupDatabase(88021, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var root = FakeData.GetDownloadTaskPhotoFolder(new Seed(88023)).Generate();
        var nested = FakeData.GetDownloadTaskPhotoFolder(new Seed(88024)).Generate();
        nested.ParentId = root.Id;
        nested.Parent = root;
        nested.Title = "Trips";
        nested.RelativePath = "Trips";
        nested.FullTitle = $"{root.FullTitle}/Trips";
        root.Children.Add(nested);
        new List<DownloadTaskPhotoFolder> { root }.SetRelationshipIds(library.PlexServerId, library.Id);
        var directFile = root.Files.Single();
        var nestedFile = nested.Files.Single();
        directFile.DownloadStatus = DownloadStatus.Queued;
        nestedFile.DownloadStatus = DownloadStatus.Paused;
        nestedFile.DataReceived = 64;
        root.DownloadStatus = DownloadStatus.Queued;
        nested.DownloadStatus = DownloadStatus.Paused;
        nestedFile.DirectoryMeta.PhotoFolder = "Trips";
        dbContext.DownloadTaskPhotoFolders.Add(root);
        await dbContext.SaveChangesAsync(CancellationToken);

        var fileKey = nestedFile.ToKey();
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(fileKey, CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(fileKey, CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(library.PlexServerId))
            .ReturnsAsync([fileKey])
            .Verifiable(Times.Once());
        Mock.Mock<IEventPublisher>()
            .Setup(x =>
                x.PublishAsync(
                    It.Is<CheckDownloadQueueEvent>(e => e.PlexServerIds.SequenceEqual(new[] { library.PlexServerId })),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new StartDownloadTaskCommand(nestedFile.Id));

        dbContext.ClearChangeTracker();
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var persistedNestedFile = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(
            x => x.Id == nestedFile.Id,
            CancellationToken
        );
        persistedNestedFile.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        persistedNestedFile.DataReceived.ShouldBe(64);
        (
            await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(x => x.Id == directFile.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.Queued);
        (
            await dbContext.DownloadTaskPhotoFolders.SingleAsync(x => x.Id == nested.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.Queued);
        (
            await dbContext.DownloadTaskPhotoFolders.SingleAsync(x => x.Id == root.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.Queued);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x =>
                    x.StartDownloadTaskJob(
                        It.Is<DownloadTaskKey>(key => key.Id == directFile.Id),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldPropagateCancellationAndRestorePhotoStatuses_WhenStartingIsCancelled()
    {
        // Arrange
        await SetupDatabase(88022, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var folder = FakeData
            .GetDownloadTaskPhotoFolder(new Seed(88025))
            .RuleFor(x => x.Files, _ => FakeData.GetDownloadTaskPhotoImageFile(new Seed(88026)).Generate(2))
            .Generate();
        new List<DownloadTaskPhotoFolder> { folder }.SetRelationshipIds(library.PlexServerId, library.Id);
        folder.DownloadStatus = DownloadStatus.Paused;
        foreach (var file in folder.Files)
        {
            file.DownloadStatus = DownloadStatus.Paused;
            file.DataReceived = 256;
        }
        dbContext.DownloadTaskPhotoFolders.Add(folder);
        await dbContext.SaveChangesAsync(CancellationToken);
        var selected = (await dbContext.GetDownloadableChildTaskKeys(folder.ToKey(), CancellationToken)).First();
        var cancelled = Result.Try((Action)(() => throw new OperationCanceledException(CancellationToken)));
        cancelled.IsCancelled.ShouldBeTrue();
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(selected, CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(selected, CancellationToken))
            .ReturnsAsync(cancelled)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new StartDownloadTaskCommand(folder.Id));

        dbContext.ClearChangeTracker();
        // Assert
        result.ShouldBeSameAs(cancelled);
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        (
            await dbContext
                .DownloadTaskPhotoImageFiles.OrderBy(x => x.Id)
                .Select(x => x.DownloadStatus)
                .ToListAsync(CancellationToken)
        ).ShouldBe([DownloadStatus.Paused, DownloadStatus.Paused]);
        (await dbContext.DownloadTaskPhotoFolders.SingleAsync(CancellationToken)).DownloadStatus.ShouldBe(
            DownloadStatus.Paused
        );
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldRejectStartWithoutCollaborators_WhenPhotoServerIsPausedByUser()
    {
        // Arrange
        await SetupDatabase(88023, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var folder = await dbContext.DownloadTaskPhotoFolders.FirstOrDefaultAsync(CancellationToken);
        if (folder is null)
        {
            var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
            folder = FakeData.GetDownloadTaskPhotoFolder(new Seed(88027)).Generate();
            new List<DownloadTaskPhotoFolder> { folder }.SetRelationshipIds(library.PlexServerId, library.Id);
            dbContext.DownloadTaskPhotoFolders.Add(folder);
            await dbContext.SaveChangesAsync(CancellationToken);
        }
        await dbContext
            .PlexServers.Where(x => x.Id == folder.PlexServerId)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDownloadsPausedByUser, true), CancellationToken);
        var before = await dbContext
            .DownloadTaskPhotoImageFiles.Select(x => new { x.Id, x.DownloadStatus })
            .ToListAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new StartDownloadTaskCommand(folder.Id));

        dbContext.ClearChangeTracker();
        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        (
            await dbContext
                .DownloadTaskPhotoImageFiles.Select(x => new { x.Id, x.DownloadStatus })
                .ToListAsync(CancellationToken)
        ).ShouldBe(before);
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
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldFailWithoutScheduling_WhenAllPhotoFilesAreCompleted()
    {
        // Arrange
        await SetupDatabase(88024, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var folder = FakeData.GetDownloadTaskPhotoFolder(new Seed(88028)).Generate();
        new List<DownloadTaskPhotoFolder> { folder }.SetRelationshipIds(library.PlexServerId, library.Id);
        folder.DownloadStatus = DownloadStatus.Completed;
        folder.Files.Single().DownloadStatus = DownloadStatus.Completed;
        folder.Files.Single().DataReceived = folder.Files.Single().DataTotal;
        dbContext.DownloadTaskPhotoFolders.Add(folder);
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new StartDownloadTaskCommand(folder.Id));

        dbContext.ClearChangeTracker();
        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        (await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken)).DownloadStatus.ShouldBe(
            DownloadStatus.Completed
        );
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldFailWithoutScheduling_WhenPhotoTaskDoesNotExist()
    {
        // Arrange
        await SetupDatabase(88025, c => c.PlexPhotoLibraryCount = 1);

        // Act
        var result = await TestHandlerExecuteAsync(new StartDownloadTaskCommand(Guid.NewGuid()));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }
}
