namespace Reaparr.Application.UnitTests;

public class StopDownloadTaskCommandPhotoUnitTests : BaseCommandUnitTest<StopDownloadTaskCommand>
{
    [Test]
    public async Task ShouldStopOnlyActiveFilesInSelectedNestedPhotoFolder_WhenStopping()
    {
        // Arrange
        await SetupDatabase(88010, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var root = FakeData.GetDownloadTaskPhotoFolder(new Seed(88010)).Generate();
        var nested = FakeData
            .GetDownloadTaskPhotoFolder(new Seed(88011))
            .RuleFor(x => x.Files, _ => FakeData.GetDownloadTaskPhotoImageFile(new Seed(88012)).Generate(2))
            .Generate();
        nested.ParentId = root.Id;
        nested.Parent = root;
        nested.Title = "Trips";
        nested.RelativePath = "Trips";
        nested.FullTitle = $"{root.FullTitle}/Trips";
        root.Children.Add(nested);
        new List<DownloadTaskPhotoFolder> { root }.SetRelationshipIds(library.PlexServerId, library.Id);
        var target = nested.Files.First();
        var sibling = nested.Files.Last();
        target.DownloadStatus = DownloadStatus.Downloading;
        target.DataReceived = 128;
        sibling.DownloadStatus = DownloadStatus.Queued;
        sibling.DataReceived = 64;
        root.DownloadStatus = DownloadStatus.Queued;
        nested.DownloadStatus = DownloadStatus.Queued;
        foreach (var file in nested.Files)
            file.DirectoryMeta.PhotoFolder = "Trips";
        dbContext.DownloadTaskPhotoFolders.Add(root);
        await dbContext.SaveChangesAsync(CancellationToken);
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(target.ToKey(), CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(target.ToKey(), CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(sibling.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(target.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(sibling.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<DeleteDownloadTaskFilesCommand>(c => c.Keys.SequenceEqual(new[] { target.ToKey() })),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new StopDownloadTaskCommand(nested.Id));

        // Assert
        dbContext.ClearChangeTracker();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = await dbContext
            .DownloadTaskPhotoImageFiles.Where(x => x.ParentId == nested.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.DownloadStatus,
                x.DataReceived,
            })
            .ToListAsync(CancellationToken);
        after.ShouldBe(
            new[]
            {
                new
                {
                    target.Id,
                    target.ParentId,
                    DownloadStatus = DownloadStatus.Stopped,
                    DataReceived = 0L,
                },
                new
                {
                    sibling.Id,
                    sibling.ParentId,
                    DownloadStatus = DownloadStatus.Queued,
                    DataReceived = 64L,
                },
            }.OrderBy(x => x.Id)
        );
        (
            await dbContext.DownloadTaskPhotoFolders.SingleAsync(x => x.Id == nested.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.Stopped);
        (
            await dbContext.DownloadTaskPhotoFolders.SingleAsync(x => x.Id == root.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.Stopped);
        (
            await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(x => x.ParentId == root.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.Queued);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldStopSelectedPhotoFileAndPreserveItsQueuedSibling_WhenStoppingDirectFile()
    {
        // Arrange
        await SetupDatabase(88011, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var folder = FakeData
            .GetDownloadTaskPhotoFolder(new Seed(88013))
            .RuleFor(x => x.Files, _ => FakeData.GetDownloadTaskPhotoImageFile(new Seed(88014)).Generate(2))
            .Generate();
        new List<DownloadTaskPhotoFolder> { folder }.SetRelationshipIds(library.PlexServerId, library.Id);
        var target = folder.Files.First();
        var sibling = folder.Files.Last();
        target.DownloadStatus = DownloadStatus.Downloading;
        target.DataReceived = 256;
        sibling.DownloadStatus = DownloadStatus.Queued;
        sibling.DataReceived = 96;
        folder.DownloadStatus = DownloadStatus.Queued;
        dbContext.DownloadTaskPhotoFolders.Add(folder);
        await dbContext.SaveChangesAsync(CancellationToken);
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(target.ToKey(), CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(target.ToKey(), CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(target.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<DeleteDownloadTaskFilesCommand>(c => c.Keys.SequenceEqual(new[] { target.ToKey() })),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new StopDownloadTaskCommand(target.Id));

        // Assert
        dbContext.ClearChangeTracker();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var persistedTarget = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(
            x => x.Id == target.Id,
            CancellationToken
        );
        persistedTarget.ParentId.ShouldBe(folder.Id);
        persistedTarget.DownloadStatus.ShouldBe(DownloadStatus.Stopped);
        persistedTarget.DataReceived.ShouldBe(0);
        var persistedSibling = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(
            x => x.Id == sibling.Id,
            CancellationToken
        );
        persistedSibling.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        persistedSibling.DataReceived.ShouldBe(96);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldSkipEveryInactivePhotoChild_WhenStoppingPhotoFolder()
    {
        // Arrange
        await SetupDatabase(88012, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var folder = FakeData
            .GetDownloadTaskPhotoFolder(new Seed(88015))
            .RuleFor(x => x.Files, _ => FakeData.GetDownloadTaskPhotoImageFile(new Seed(88016)).Generate(2))
            .Generate();
        new List<DownloadTaskPhotoFolder> { folder }.SetRelationshipIds(library.PlexServerId, library.Id);
        folder.DownloadStatus = DownloadStatus.Queued;
        foreach (var file in folder.Files)
        {
            file.DownloadStatus = DownloadStatus.Queued;
            file.DataReceived = 77;
        }
        dbContext.DownloadTaskPhotoFolders.Add(folder);
        await dbContext.SaveChangesAsync(CancellationToken);
        foreach (var file in folder.Files)
        {
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.IsDownloading(file.ToKey(), CancellationToken))
                .ReturnsAsync(false)
                .Verifiable(Times.Once());
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.IsDownloadFileMoving(file.ToKey(), CancellationToken))
                .ReturnsAsync(false)
                .Verifiable(Times.Once());
        }
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync(new StopDownloadTaskCommand(folder.Id));

        // Assert
        dbContext.ClearChangeTracker();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (
            await dbContext
                .DownloadTaskPhotoImageFiles.OrderBy(x => x.Id)
                .Select(x => new { x.DownloadStatus, x.DataReceived })
                .ToListAsync(CancellationToken)
        ).ShouldBe(
            new[]
            {
                new { DownloadStatus = DownloadStatus.Queued, DataReceived = 77L },
                new { DownloadStatus = DownloadStatus.Queued, DataReceived = 77L },
            }
        );
        (await dbContext.DownloadTaskPhotoFolders.SingleAsync(CancellationToken)).DownloadStatus.ShouldBe(
            DownloadStatus.Queued
        );
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldReturnFailureAndPreservePhotoProgress_WhenDeleteFilesCommandFails()
    {
        // Arrange
        await SetupDatabase(88013, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var folder = FakeData.GetDownloadTaskPhotoFolder(new Seed(88017)).Generate();
        new List<DownloadTaskPhotoFolder> { folder }.SetRelationshipIds(library.PlexServerId, library.Id);
        var file = folder.Files.Single();
        file.DownloadStatus = DownloadStatus.Downloading;
        file.DataReceived = 333;
        dbContext.DownloadTaskPhotoFolders.Add(folder);
        await dbContext.SaveChangesAsync(CancellationToken);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(file.ToKey(), CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(file.ToKey(), CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(file.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<DeleteDownloadTaskFilesCommand>(c => c.Keys.SequenceEqual(new[] { file.ToKey() })),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Fail("delete files failed"))
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync(new StopDownloadTaskCommand(file.Id));

        // Assert
        dbContext.ClearChangeTracker();
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        var after = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        after.DownloadStatus.ShouldBe(DownloadStatus.Downloading);
        after.DataReceived.ShouldBe(333);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
    }

    [Test]
    public async Task ShouldPropagateCancellationAndPreservePhotoProgress_WhenStopJobIsCancelled()
    {
        // Arrange
        await SetupDatabase(88014, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var folder = FakeData.GetDownloadTaskPhotoFolder(new Seed(88018)).Generate();
        new List<DownloadTaskPhotoFolder> { folder }.SetRelationshipIds(library.PlexServerId, library.Id);
        var file = folder.Files.Single();
        file.DownloadStatus = DownloadStatus.Downloading;
        file.DataReceived = 444;
        dbContext.DownloadTaskPhotoFolders.Add(folder);
        await dbContext.SaveChangesAsync(CancellationToken);
        var cancelled = Result.Try((Action)(() => throw new OperationCanceledException(CancellationToken)));
        cancelled.IsCancelled.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(file.ToKey(), CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(file.ToKey(), CancellationToken))
            .ReturnsAsync(cancelled)
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(file.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync(new StopDownloadTaskCommand(file.Id));

        // Assert
        dbContext.ClearChangeTracker();
        result.ShouldBeSameAs(cancelled);
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        var after = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        after.DownloadStatus.ShouldBe(DownloadStatus.Downloading);
        after.DataReceived.ShouldBe(444);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldReturnMoveFailureBeforeDeletingPhotoFile_WhenMoveStopFails()
    {
        // Arrange
        await SetupDatabase(88015, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var folder = FakeData.GetDownloadTaskPhotoFolder(new Seed(88019)).Generate();
        new List<DownloadTaskPhotoFolder> { folder }.SetRelationshipIds(library.PlexServerId, library.Id);
        var file = folder.Files.Single();
        file.DownloadStatus = DownloadStatus.Moving;
        file.DataReceived = file.DataTotal;
        file.FileDataTransferred = 100;
        dbContext.DownloadTaskPhotoFolders.Add(folder);
        await dbContext.SaveChangesAsync(CancellationToken);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(file.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(file.ToKey(), CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StopMoveDownloadFileJob(file.ToKey(), CancellationToken))
            .ReturnsAsync(Result.Fail("move stop failed"))
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync(new StopDownloadTaskCommand(file.Id, DeleteFiles: false));

        // Assert
        dbContext.ClearChangeTracker();
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        var after = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        after.DownloadStatus.ShouldBe(DownloadStatus.Moving);
        after.DataReceived.ShouldBe(file.DataTotal);
        after.FileDataTransferred.ShouldBe(100);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldStopActivePhotoFileWithoutDeletingFiles_WhenDeleteFilesIsFalse()
    {
        // Arrange
        await SetupDatabase(88016, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var folder = FakeData.GetDownloadTaskPhotoFolder(new Seed(88020)).Generate();
        new List<DownloadTaskPhotoFolder> { folder }.SetRelationshipIds(library.PlexServerId, library.Id);
        var file = folder.Files.Single();
        file.DownloadStatus = DownloadStatus.Downloading;
        file.DataReceived = 555;
        dbContext.DownloadTaskPhotoFolders.Add(folder);
        await dbContext.SaveChangesAsync(CancellationToken);
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(file.ToKey(), CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(file.ToKey(), CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(file.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new StopDownloadTaskCommand(file.Id, DeleteFiles: false));

        // Assert
        dbContext.ClearChangeTracker();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        after.DownloadStatus.ShouldBe(DownloadStatus.Stopped);
        after.DataReceived.ShouldBe(0);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }
}
