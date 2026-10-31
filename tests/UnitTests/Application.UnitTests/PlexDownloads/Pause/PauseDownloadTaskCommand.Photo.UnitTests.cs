namespace Reaparr.Application.UnitTests;

public class PauseDownloadTaskCommandPhotoUnitTests : BaseCommandUnitTest<PauseDownloadTaskCommand>
{
    [Test]
    [Arguments(DownloadStatus.Downloading, DownloadStatus.Paused, DownloadTaskType.PhotoImage)]
    [Arguments(DownloadStatus.DownloadFinished, DownloadStatus.MovePaused, DownloadTaskType.PhotoImage)]
    [Arguments(DownloadStatus.Moving, DownloadStatus.MovePaused, DownloadTaskType.PhotoImage)]
    [Arguments(DownloadStatus.MoveFinished, DownloadStatus.MoveFinished, DownloadTaskType.PhotoImage)]
    [Arguments(DownloadStatus.Completed, DownloadStatus.Completed, DownloadTaskType.PhotoImage)]
    [Arguments(DownloadStatus.Downloading, DownloadStatus.Paused, DownloadTaskType.PhotoFolder)]
    public async Task ShouldPauseSelectedPhotoFilesAndPreserveFinishedMoves_WhenPausing(
        DownloadStatus before,
        DownloadStatus expected,
        DownloadTaskType selection
    )
    {
        // Arrange
        await SetupDatabase(88090, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var root = FakeData.GetDownloadTaskPhotoFolder(new Seed(88090)).Generate();
        var nested = FakeData
            .GetDownloadTaskPhotoFolder(new Seed(88091))
            .RuleFor(x => x.Files, _ => FakeData.GetDownloadTaskPhotoImageFile(new Seed(88092)).Generate(2))
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
        target.DownloadStatus = before;
        target.DataReceived = 512;
        target.FileDataTransferred = 256;
        target.CurrentFileTransferBytesOffset = 256;
        sibling.DownloadStatus = DownloadStatus.Queued;
        sibling.DataReceived = 64;
        root.DownloadStatus = DownloadStatus.Queued;
        nested.DownloadStatus = DownloadStatus.Queued;
        foreach (var file in nested.Files)
            file.DirectoryMeta.PhotoFolder = "Trips";
        dbContext.DownloadTaskPhotoFolders.Add(root);
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
                .ReturnsAsync(Result.Ok())
                .Verifiable(Times.Once());
        }
        if (before is DownloadStatus.Moving or DownloadStatus.DownloadFinished)
        {
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.IsDownloadFileMoving(target.ToKey(), CancellationToken))
                .ReturnsAsync(before == DownloadStatus.Moving)
                .Verifiable(Times.Once());
            if (before == DownloadStatus.Moving)
                Mock.Mock<IMoveDownloadFileScheduler>()
                    .Setup(x => x.StopMoveDownloadFileJob(target.ToKey(), CancellationToken))
                    .ReturnsAsync(Result.Ok())
                    .Verifiable(Times.Once());
        }
        if (selection == DownloadTaskType.PhotoFolder)
        {
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.IsDownloading(sibling.ToKey(), CancellationToken))
                .ReturnsAsync(false)
                .Verifiable(Times.Once());
        }

        var selectedId = selection == DownloadTaskType.PhotoFolder ? nested.Id : target.Id;

        // Act
        var result = await TestHandlerExecuteAsync(new PauseDownloadTaskCommand(selectedId));

        dbContext.ClearChangeTracker();
        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var afterTarget = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(
            x => x.Id == target.Id,
            CancellationToken
        );
        afterTarget.DownloadStatus.ShouldBe(expected);
        afterTarget.DataReceived.ShouldBe(512);
        afterTarget.FileDataTransferred.ShouldBe(256);
        afterTarget.CurrentFileTransferBytesOffset.ShouldBe(256);
        var afterSibling = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(
            x => x.Id == sibling.Id,
            CancellationToken
        );
        afterSibling.DownloadStatus.ShouldBe(
            selection == DownloadTaskType.PhotoFolder ? DownloadStatus.Paused : DownloadStatus.Queued
        );
        afterSibling.DataReceived.ShouldBe(64);
        (
            await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(x => x.ParentId == root.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.Queued);
        var afterNested = await dbContext.DownloadTaskPhotoFolders.SingleAsync(
            x => x.Id == nested.Id,
            CancellationToken
        );
        afterNested.DownloadStatus.ShouldBe(
            before is DownloadStatus.MoveFinished or DownloadStatus.Completed ? DownloadStatus.Queued
            : selection == DownloadTaskType.PhotoFolder ? DownloadStatus.Paused
            : expected
        );
        (
            await dbContext.DownloadTaskPhotoFolders.SingleAsync(x => x.Id == root.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(
            afterNested.DownloadStatus is DownloadStatus.Paused or DownloadStatus.MovePaused
                ? afterNested.DownloadStatus
                : DownloadStatus.Queued
        );
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StopMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                before == DownloadStatus.Moving ? Times.Once() : Times.Never()
            );
    }

    [Test]
    public async Task ShouldPauseDirectPhotoFileInRootFolder()
    {
        // Arrange
        await SetupDatabase(88101, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum);
        var folder = FakeData.GetDownloadTaskPhotoFolder(new Seed(88101)).Generate();
        new List<DownloadTaskPhotoFolder> { folder }.SetRelationshipIds(library.PlexServerId, library.Id);
        folder.DownloadStatus = DownloadStatus.Downloading;
        var file = folder.Files.Single();
        file.DownloadStatus = DownloadStatus.Downloading;
        file.DataReceived = 700;
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
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new PauseDownloadTaskCommand(file.Id));

        // Assert
        dbContext.ClearChangeTracker();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var persisted = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        persisted.ParentId.ShouldBe(folder.Id);
        persisted.DownloadStatus.ShouldBe(DownloadStatus.Paused);
        persisted.DataReceived.ShouldBe(700);
        (await dbContext.DownloadTaskPhotoFolders.SingleAsync(CancellationToken)).DownloadStatus.ShouldBe(
            DownloadStatus.Paused
        );
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StopMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldSetAutoPausedForEveryNestedPhotoFile_WhenAutoPauseIsRequested()
    {
        // Arrange
        await SetupDatabase(88104, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var root = FakeData.GetDownloadTaskPhotoFolder(new Seed(88104)).Generate();
        var nested = FakeData
            .GetDownloadTaskPhotoFolder(new Seed(88105))
            .RuleFor(x => x.Files, _ => FakeData.GetDownloadTaskPhotoImageFile(new Seed(88106)).Generate(2))
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
        foreach (var file in nested.Files)
            file.DownloadStatus = DownloadStatus.Downloading;
        dbContext.DownloadTaskPhotoFolders.Add(root);
        await dbContext.SaveChangesAsync(CancellationToken);
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        foreach (var file in nested.Files)
        {
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.IsDownloading(file.ToKey(), CancellationToken))
                .ReturnsAsync(true)
                .Verifiable(Times.Once());
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.StopDownloadTaskJob(file.ToKey(), CancellationToken))
                .ReturnsAsync(Result.Ok())
                .Verifiable(Times.Once());
        }
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(nested.Files.Count));

        // Act
        var result = await TestHandlerExecuteAsync(new PauseDownloadTaskCommand(nested.Id, AutoPause: true));

        // Assert
        dbContext.ClearChangeTracker();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (
            await dbContext
                .DownloadTaskPhotoImageFiles.Where(x => x.ParentId == nested.Id)
                .OrderBy(x => x.Id)
                .Select(x => x.DownloadStatus)
                .ToListAsync(CancellationToken)
        ).ShouldBe([DownloadStatus.AutoPaused, DownloadStatus.AutoPaused]);
        (
            await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(x => x.ParentId == root.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.Queued);
        (
            await dbContext.DownloadTaskPhotoFolders.SingleAsync(x => x.Id == nested.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.AutoPaused);
        (
            await dbContext.DownloadTaskPhotoFolders.SingleAsync(x => x.Id == root.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.AutoPaused);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldReturnStopFailureWithoutResettingPhotoProgress_WhenDownloadStopFails()
    {
        // Arrange
        await SetupDatabase(88107, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var folder = FakeData.GetDownloadTaskPhotoFolder(new Seed(88107)).Generate();
        new List<DownloadTaskPhotoFolder> { folder }.SetRelationshipIds(library.PlexServerId, library.Id);
        var file = folder.Files.Single();
        file.DownloadStatus = DownloadStatus.Downloading;
        file.DataReceived = 321;
        dbContext.DownloadTaskPhotoFolders.Add(folder);
        await dbContext.SaveChangesAsync(CancellationToken);
        var stopError = Result.Fail("photo stop failed");
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(file.ToKey(), CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(file.ToKey(), CancellationToken))
            .ReturnsAsync(stopError)
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
        var result = await TestHandlerExecuteAsync(new PauseDownloadTaskCommand(file.Id));

        // Assert
        dbContext.ClearChangeTracker();
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        var after = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        after.DownloadStatus.ShouldBe(DownloadStatus.Downloading);
        after.DataReceived.ShouldBe(321);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StopMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldPropagateCancellationWithoutChangingPhotoFile_WhenDownloadStopIsCancelled()
    {
        // Arrange
        await SetupDatabase(88108, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var folder = FakeData.GetDownloadTaskPhotoFolder(new Seed(88108)).Generate();
        new List<DownloadTaskPhotoFolder> { folder }.SetRelationshipIds(library.PlexServerId, library.Id);
        var file = folder.Files.Single();
        file.DownloadStatus = DownloadStatus.Downloading;
        file.DataReceived = 654;
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
        var result = await TestHandlerExecuteAsync(new PauseDownloadTaskCommand(file.Id));

        // Assert
        dbContext.ClearChangeTracker();
        result.ShouldBeSameAs(cancelled);
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        var after = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        after.DownloadStatus.ShouldBe(DownloadStatus.Downloading);
        after.DataReceived.ShouldBe(654);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
    }
}
