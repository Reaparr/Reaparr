namespace Reaparr.Application.UnitTests;

public class RestartDownloadTaskCommandPhotoUnitTests : BaseCommandUnitTest<RestartDownloadTaskCommand>
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task ShouldRefreshSelectedPhotoFilesOrMarkUnavailable_WhilePreservingCapturedPaths(
        bool sourceMissing,
        bool selectFile
    )
    {
        // Arrange
        await SetupDatabase(88070, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var root = FakeData.GetDownloadTaskPhotoFolder(new Seed(88070)).Generate();
        var nested = FakeData
            .GetDownloadTaskPhotoFolder(new Seed(88071))
            .RuleFor(x => x.Files, _ => FakeData.GetDownloadTaskPhotoImageFile(new Seed(88072)).Generate(2))
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
        root.DownloadStatus = DownloadStatus.Completed;
        nested.DownloadStatus = DownloadStatus.Completed;
        root.Files.Single().DownloadStatus = DownloadStatus.Completed;
        target.DownloadStatus = DownloadStatus.Completed;
        sibling.DownloadStatus = DownloadStatus.Completed;
        target.DataReceived = target.DataTotal;
        target.FileDataTransferred = target.DataTotal;
        target.CurrentFileTransferBytesOffset = target.DataTotal;
        target.Percentage = 100;
        target.DownloadSpeed = 777;
        target.FileTransferSpeed = 888;
        target.TimeRemaining = 12;
        target.DirectoryMeta.DownloadRootPath = "/captured/downloads";
        target.DirectoryMeta.DestinationRootPath = "/captured/photos";
        target.DirectoryMeta.PhotoFolder = "Trips";
        target.DirectoryMeta.KeepCompletedInDownloadFolder = true;
        target.DirectDownloadSnapshot = new DirectDownloadSnapshot
        {
            SaveProgress = 100,
            Status = 2,
            Urls = ["https://plex.example/target"],
            TotalFileSize = target.DataTotal,
            FileName = target.FileName,
            DownloadingFileExtension = ".reaptemp",
            IsSupportDownloadInRange = true,
            Chunks = [],
        };
        sibling.DirectoryMeta.DownloadRootPath = "/captured/downloads";
        sibling.DirectoryMeta.DestinationRootPath = "/captured/photos";
        sibling.DirectoryMeta.PhotoFolder = "Trips";
        dbContext.DownloadTaskPhotoFolders.Add(root);
        await dbContext.SaveChangesAsync(CancellationToken);

        var targetSource = FakeData
            .GetPlexPhotoMediaData(new Seed(88073))
            .RuleFor(x => x.PlexServerId, _ => library.PlexServerId)
            .RuleFor(x => x.PlexLibraryId, _ => library.Id)
            .RuleFor(x => x.PlexApiRatingKey, _ => target.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => target.PlexApiMediaId)
            .RuleFor(x => x.PlexApiPartId, _ => target.PlexApiPartId)
            .RuleFor(x => x.Title, _ => "Refreshed target")
            .RuleFor(x => x.OriginalFilename, _ => "/catalog/target-new.jpg")
            .RuleFor(x => x.Key, _ => "/library/parts/target-new/file.jpg")
            .RuleFor(x => x.Size, _ => 9_876L)
            .Generate();
        var siblingSource = FakeData
            .GetPlexPhotoMediaData(new Seed(88074))
            .RuleFor(x => x.PlexServerId, _ => library.PlexServerId)
            .RuleFor(x => x.PlexLibraryId, _ => library.Id)
            .RuleFor(x => x.PlexApiRatingKey, _ => sibling.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => sibling.PlexApiMediaId)
            .RuleFor(x => x.PlexApiPartId, _ => sibling.PlexApiPartId)
            .RuleFor(x => x.Title, _ => "Refreshed sibling")
            .RuleFor(x => x.OriginalFilename, _ => "/catalog/sibling-new.jpg")
            .RuleFor(x => x.Key, _ => "/library/parts/sibling-new/file.jpg")
            .RuleFor(x => x.Size, _ => 8_765L)
            .Generate();
        dbContext.PlexPhotoMediaData.AddRange(targetSource, siblingSource);
        await dbContext.SaveChangesAsync(CancellationToken);
        var targetBefore = new
        {
            target.ParentId,
            target.FileName,
            target.Title,
            target.FullTitle,
            target.FileLocationUrl,
            target.DataTotal,
            target.DataReceived,
            target.FileDataTransferred,
            target.CurrentFileTransferBytesOffset,
            target.Percentage,
            target.DownloadSpeed,
            target.FileTransferSpeed,
            target.TimeRemaining,
            target.DirectoryMeta.DownloadRootPath,
            target.DirectoryMeta.DestinationRootPath,
            target.DirectoryMeta.PhotoFolder,
            target.DirectoryMeta.KeepCompletedInDownloadFolder,
        };
        var siblingBefore = new
        {
            sibling.FileName,
            sibling.DataReceived,
            sibling.DownloadStatus,
            sibling.DirectoryMeta.DownloadRootPath,
            sibling.DirectoryMeta.DestinationRootPath,
            sibling.DirectoryMeta.PhotoFolder,
        };
        if (sourceMissing)
            await dbContext
                .PlexPhotoMediaData.Where(x => x.Id == targetSource.Id)
                .ExecuteDeleteAsync(CancellationToken);
        dbContext.ClearChangeTracker();
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<StopDownloadTaskCommand>(c => c.DownloadTaskGuid == target.Id && c.DeleteFiles),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        if (!selectFile)
        {
            Mock.Mock<ICommandExecutor>()
                .Setup(x =>
                    x.Send(
                        It.Is<StopDownloadTaskCommand>(c => c.DownloadTaskGuid == sibling.Id && c.DeleteFiles),
                        CancellationToken
                    )
                )
                .ReturnsAsync(Result.Ok())
                .Verifiable(Times.Once());
        }
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
        var result = await TestHandlerExecuteAsync(new RestartDownloadTaskCommand(selectFile ? target.Id : nested.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(x => x.Id == target.Id, CancellationToken);
        after.ParentId.ShouldBe(targetBefore.ParentId);
        after.PlexApiRatingKey.ShouldBe(target.PlexApiRatingKey);
        after.PlexApiMediaId.ShouldBe(target.PlexApiMediaId);
        after.PlexApiPartId.ShouldBe(target.PlexApiPartId);
        after.DownloadStatus.ShouldBe(sourceMissing ? DownloadStatus.SourceUnavailable : DownloadStatus.Queued);
        after.FileName.ShouldBe(sourceMissing ? targetBefore.FileName : targetSource.OriginalFilename.GetFileName());
        after.Title.ShouldBe(sourceMissing ? targetBefore.Title : targetSource.OriginalFilename.GetFileName());
        after.FullTitle.ShouldBe(
            sourceMissing
                ? targetBefore.FullTitle
                : $"{targetSource.Title}/{targetSource.OriginalFilename.GetFileName()}"
        );
        after.FileLocationUrl.ShouldBe(sourceMissing ? targetBefore.FileLocationUrl : targetSource.Key);
        after.DataTotal.ShouldBe(sourceMissing ? targetBefore.DataTotal : targetSource.Size);
        after.DataReceived.ShouldBe(sourceMissing ? targetBefore.DataReceived : 0);
        after.FileDataTransferred.ShouldBe(sourceMissing ? targetBefore.FileDataTransferred : 0);
        after.CurrentFileTransferBytesOffset.ShouldBe(sourceMissing ? targetBefore.CurrentFileTransferBytesOffset : 0);
        after.Percentage.ShouldBe(sourceMissing ? targetBefore.Percentage : 0);
        after.DownloadSpeed.ShouldBe(sourceMissing ? targetBefore.DownloadSpeed : 0);
        after.FileTransferSpeed.ShouldBe(sourceMissing ? targetBefore.FileTransferSpeed : 0);
        after.TimeRemaining.ShouldBe(sourceMissing ? targetBefore.TimeRemaining : 0);
        after.DownloadClientType.ShouldBe(PlexDownloadClientType.Direct);
        if (sourceMissing)
        {
            after.DirectDownloadSnapshot.ShouldNotBeNull();
            after.DirectDownloadSnapshot!.SaveProgress.ShouldBe(100);
            after.DirectDownloadSnapshot.Status.ShouldBe(2);
            after.DirectDownloadSnapshot.Urls.ShouldBe(["https://plex.example/target"]);
            after.DirectDownloadSnapshot.TotalFileSize.ShouldBe(targetBefore.DataTotal);
        }
        else
        {
            after.DirectDownloadSnapshot.ShouldBeNull();
        }
        after.DirectoryMeta.DownloadRootPath.ShouldBe(targetBefore.DownloadRootPath);
        after.DirectoryMeta.DestinationRootPath.ShouldBe(targetBefore.DestinationRootPath);
        after.DirectoryMeta.KeepCompletedInDownloadFolder.ShouldBe(targetBefore.KeepCompletedInDownloadFolder);
        after.DirectoryMeta.PhotoFolder.ShouldBe(targetBefore.PhotoFolder);

        var siblingAfter = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(
            x => x.Id == sibling.Id,
            CancellationToken
        );
        if (selectFile)
        {
            siblingAfter.FileName.ShouldBe(siblingBefore.FileName);
            siblingAfter.DataReceived.ShouldBe(siblingBefore.DataReceived);
            siblingAfter.DownloadStatus.ShouldBe(siblingBefore.DownloadStatus);
            siblingAfter.DirectoryMeta.DownloadRootPath.ShouldBe(siblingBefore.DownloadRootPath);
            siblingAfter.DirectoryMeta.DestinationRootPath.ShouldBe(siblingBefore.DestinationRootPath);
            siblingAfter.DirectoryMeta.PhotoFolder.ShouldBe(siblingBefore.PhotoFolder);
        }
        else
        {
            siblingAfter.DownloadStatus.ShouldBe(DownloadStatus.Queued);
            siblingAfter.FileName.ShouldBe(siblingSource.OriginalFilename.GetFileName());
            siblingAfter.FileLocationUrl.ShouldBe(siblingSource.Key);
            siblingAfter.DataTotal.ShouldBe(siblingSource.Size);
            siblingAfter.DataReceived.ShouldBe(0);
        }
        (
            await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(x => x.ParentId == root.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.Completed);
        (
            await dbContext.DownloadTaskPhotoFolders.SingleAsync(x => x.Id == nested.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(after.DownloadStatus);
        (
            await dbContext.DownloadTaskPhotoFolders.SingleAsync(x => x.Id == root.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(after.DownloadStatus);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StopDownloadTaskCommand>(c => c.DownloadTaskGuid != target.Id),
                        It.IsAny<CancellationToken>()
                    ),
                selectFile ? Times.Never() : Times.Once()
            );
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldPropagateCancellationAndPreserveCapturedPhotoPath_WhenRestartStopIsCancelled()
    {
        // Arrange
        await SetupDatabase(88103, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var folder = FakeData.GetDownloadTaskPhotoFolder(new Seed(88103)).Generate();
        new List<DownloadTaskPhotoFolder> { folder }.SetRelationshipIds(library.PlexServerId, library.Id);
        var file = folder.Files.Single();
        folder.DownloadStatus = DownloadStatus.Completed;
        file.DownloadStatus = DownloadStatus.Completed;
        file.DataReceived = 128;
        file.FileDataTransferred = 64;
        file.CurrentFileTransferBytesOffset = 32;
        file.DirectoryMeta.DownloadRootPath = "/captured/downloads";
        file.DirectoryMeta.DestinationRootPath = "/captured/photos";
        file.DirectoryMeta.PhotoFolder = "Trips";
        dbContext.DownloadTaskPhotoFolders.Add(folder);
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.ClearChangeTracker();
        var cancelled = Result.Try((Action)(() => throw new OperationCanceledException(CancellationToken)));
        cancelled.IsCancelled.ShouldBeTrue();
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<StopDownloadTaskCommand>(c => c.DownloadTaskGuid == file.Id && c.DeleteFiles),
                    CancellationToken
                )
            )
            .ReturnsAsync(cancelled)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new RestartDownloadTaskCommand(file.Id));

        // Assert
        result.ShouldBeSameAs(cancelled);
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        var after = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        after.DownloadStatus.ShouldBe(DownloadStatus.Restarting);
        after.DataReceived.ShouldBe(128);
        after.FileDataTransferred.ShouldBe(64);
        after.CurrentFileTransferBytesOffset.ShouldBe(32);
        after.DirectoryMeta.DownloadRootPath.ShouldBe("/captured/downloads");
        after.DirectoryMeta.DestinationRootPath.ShouldBe("/captured/photos");
        after.DirectoryMeta.PhotoFolder.ShouldBe("Trips");
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldReturnStopFailureWithoutRefreshingPhotoSource_WhenRestartStopFails()
    {
        // Arrange
        await SetupDatabase(88104, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var folder = FakeData.GetDownloadTaskPhotoFolder(new Seed(88105)).Generate();
        new List<DownloadTaskPhotoFolder> { folder }.SetRelationshipIds(library.PlexServerId, library.Id);
        var file = folder.Files.Single();
        folder.DownloadStatus = DownloadStatus.Completed;
        file.DownloadStatus = DownloadStatus.Completed;
        file.DataReceived = 256;
        dbContext.DownloadTaskPhotoFolders.Add(folder);
        await dbContext.SaveChangesAsync(CancellationToken);
        var source = FakeData
            .GetPlexPhotoMediaData(new Seed(88106))
            .RuleFor(x => x.PlexServerId, _ => library.PlexServerId)
            .RuleFor(x => x.PlexLibraryId, _ => library.Id)
            .RuleFor(x => x.PlexApiRatingKey, _ => file.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => file.PlexApiMediaId)
            .RuleFor(x => x.PlexApiPartId, _ => file.PlexApiPartId)
            .RuleFor(x => x.OriginalFilename, _ => "/catalog/new.jpg")
            .Generate();
        dbContext.PlexPhotoMediaData.Add(source);
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.ClearChangeTracker();
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<StopDownloadTaskCommand>(c => c.DownloadTaskGuid == file.Id && c.DeleteFiles),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Fail("stop failed"))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new RestartDownloadTaskCommand(file.Id));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        var after = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        after.DownloadStatus.ShouldBe(DownloadStatus.Restarting);
        after.DataReceived.ShouldBe(256);
        after.FileName.ShouldNotBe("new.jpg");
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldFailWithoutCollaborators_WhenPhotoRestartTaskDoesNotExist()
    {
        // Arrange
        await SetupDatabase(88107, c => c.PlexPhotoLibraryCount = 1);

        // Act
        var result = await TestHandlerExecuteAsync(new RestartDownloadTaskCommand(Guid.NewGuid()));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }
}
