namespace Reaparr.Application.UnitTests;

public class RestartDownloadTaskCommandOtherVideoUnitTests : BaseCommandUnitTest<RestartDownloadTaskCommand>
{
    [Test]
    [Arguments(DownloadTaskType.OtherVideo, false)]
    [Arguments(DownloadTaskType.OtherVideoData, false)]
    [Arguments(DownloadTaskType.OtherVideo, true)]
    [Arguments(DownloadTaskType.OtherVideoData, true)]
    public async Task ShouldRefreshSelectedOriginalOtherVideoOrPreserveMissingSourceSettings_WhenRestarting(
        DownloadTaskType selection,
        bool sourceMissing
    )
    {
        // Arrange
        await SetupDatabase(
            88290,
            c =>
            {
                c.PlexOtherVideoLibraryCount = 1;
                c.OtherVideoDownloadTasksCount = 2;
                c.OtherVideoFileDownloadTasksCount = 1;
                c.OtherVideoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var otherVideoFiles = await dbContext.DownloadTaskOtherVideoFiles
            .Include(x => x.Parent)
            .OrderBy(x => x.PlexApiRatingKey)
            .ToArrayAsync(CancellationToken);
        DownloadTaskFileBase target = otherVideoFiles[0];
        DownloadTaskFileBase retained = otherVideoFiles[1];
        BasePlexMediaData source = await dbContext.PlexOtherVideoData
            .Include(x => x.PlexOtherVideo)
            .SingleAsync(CancellationToken);
        DownloadTaskBase node = selection == DownloadTaskType.OtherVideo ? ((DownloadTaskOtherVideoFile)target).Parent! : target;
        dbContext
            .Entry(target)
            .CurrentValues.SetValues(
                new
                {
                    source.PlexApiRatingKey,
                    source.PlexApiMediaId,
                    source.PlexApiPartId,
                    FileName = "old.mp4",
                    DataReceived = 128L,
                    FileDataTransferred = 64L,
                    CurrentFileTransferBytesOffset = 32L,
                    Percentage = 50m,
                    HashId = "retained-hash",
                    DownloadClientType = PlexDownloadClientType.Dash,
                }
            );
        target.DirectoryMeta.KeepCompletedInDownloadFolder = true;
        dbContext.Entry(target).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var before = (await dbContext.GetDownloadTaskFileAsync(target.ToKey(), CancellationToken))!;
        before.DataReceived.ShouldBe(128);
        before.FileDataTransferred.ShouldBe(64);
        var retainedBefore = (await dbContext.GetDownloadTaskFileAsync(retained.ToKey(), CancellationToken))!;
        var parentKeys = new[] { ((DownloadTaskOtherVideoFile)target).Parent!.ToKey() };
        if (sourceMissing)
            await dbContext.PlexOtherVideoData.Where(x => x.Id == source.Id).ExecuteDeleteAsync(CancellationToken);
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
        Mock.Mock<IEventPublisher>()
            .Setup(x =>
                x.PublishAsync(
                    It.Is<CheckDownloadQueueEvent>(e => e.PlexServerIds.SequenceEqual(new[] { target.PlexServerId })),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new RestartDownloadTaskCommand(node.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = (await dbContext.GetDownloadTaskFileAsync(target.ToKey(), CancellationToken))!;
        after.DownloadStatus.ShouldBe(sourceMissing ? DownloadStatus.SourceUnavailable : DownloadStatus.Queued);
        after.FileName.ShouldBe(sourceMissing ? before.FileName : source.OriginalFilename.GetFileName());
        after.FileLocationUrl.ShouldBe(sourceMissing ? before.FileLocationUrl : source.Key);
        after.DataTotal.ShouldBe(sourceMissing ? before.DataTotal : source.Size);
        after.DataReceived.ShouldBe(sourceMissing ? 128 : 0);
        after.FileDataTransferred.ShouldBe(sourceMissing ? 64 : 0);
        after.CurrentFileTransferBytesOffset.ShouldBe(sourceMissing ? 32 : 0);
        after.Percentage.ShouldBe(sourceMissing ? 50 : 0);
        after.HashId.ShouldBe("retained-hash");
        after.DownloadClientType.ShouldBe(PlexDownloadClientType.Dash);
        after.DirectoryMeta.DownloadRootPath.ShouldBe(before.DirectoryMeta.DownloadRootPath);
        after.DirectoryMeta.DestinationRootPath.ShouldBe(before.DirectoryMeta.DestinationRootPath);
        after.DirectoryMeta.KeepCompletedInDownloadFolder.ShouldBeTrue();
        after.PlexApiMediaId.ShouldBe(source.PlexApiMediaId);
        after.PlexApiPartId.ShouldBe(source.PlexApiPartId);
        after.ToParentKey().ShouldBe(before.ToParentKey());
        if (!sourceMissing)
            after.DirectoryMeta.OtherVideoFolder.ShouldBe(
                ((PlexOtherVideoMediaData)source).PlexOtherVideo!.Title.SanitizeFolderName()
            );
        foreach (var parentKey in parentKeys)
            (await dbContext.GetDownloadTaskAsync(parentKey, CancellationToken))!.DownloadStatus.ShouldBe(
                after.DownloadStatus
            );
        var control = (await dbContext.GetDownloadTaskFileAsync(retained.ToKey(), CancellationToken))!;
        control.FileName.ShouldBe(retainedBefore.FileName);
        control.DataReceived.ShouldBe(retainedBefore.DataReceived);
        control.DownloadStatus.ShouldBe(retainedBefore.DownloadStatus);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StopDownloadTaskCommand>(c => c.DownloadTaskGuid != target.Id),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldPreserveOtherVideoProgressAndPropagateCancellation_WhenRestartStopIsCancelled()
    {
        // Arrange
        await SetupDatabase(
            88350,
            c =>
            {
                c.PlexOtherVideoLibraryCount = 1;
                c.OtherVideoDownloadTasksCount = 1;
                c.OtherVideoFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var file = await dbContext.DownloadTaskOtherVideoFiles.SingleAsync(CancellationToken);
        file.DataReceived = 128;
        dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
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
        var after = (await dbContext.GetDownloadTaskFileAsync(file.ToKey(), CancellationToken))!;
        after.DownloadStatus.ShouldBe(DownloadStatus.Restarting);
        after.DataReceived.ShouldBe(128);
        after.FileName.ShouldBe(file.FileName);
        after.FileLocationUrl.ShouldBe(file.FileLocationUrl);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
    }
}
