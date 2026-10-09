namespace Reaparr.Application.UnitTests;

public class StopDownloadTaskCommandOtherVideoUnitTests : BaseCommandUnitTest<StopDownloadTaskCommand>
{
    [Test]
    [Arguments(DownloadTaskType.OtherVideo, false)]
    [Arguments(DownloadTaskType.OtherVideoData, false)]
    [Arguments(DownloadTaskType.OtherVideoData, true)]
    public async Task ShouldStopOnlyActiveSelectedOtherVideoOrPreserveStateOnFailure_WhenStopping(
        DownloadTaskType selection,
        bool failStop
    )
    {
        // Arrange
        await SetupDatabase(
            88220,
            c =>
            {
                c.PlexOtherVideoLibraryCount = 1;
                c.OtherVideoDownloadTasksCount = 2;
                c.OtherVideoFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var otherVideoFiles = await dbContext.DownloadTaskOtherVideoFiles
            .Include(x => x.Parent)
            .OrderBy(x => x.PlexApiRatingKey)
            .ToArrayAsync(CancellationToken);
        DownloadTaskFileBase target = otherVideoFiles[0];
        DownloadTaskFileBase control = otherVideoFiles[1];
        DownloadTaskBase node = selection == DownloadTaskType.OtherVideo
            ? ((DownloadTaskOtherVideoFile)target).Parent!
            : target;
        target.DataReceived = 128;
        dbContext.Entry(target).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.SetDownloadStatus(target.ToKey(), DownloadStatus.Downloading);
        var selected = await dbContext.GetDownloadableChildTaskKeys(node.ToKey(), CancellationToken);
        selected.Select(x => x.Id).Order().ShouldBe(new[] { target.Id }.Order());
        var error = new Error("stop rejected");
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        foreach (var key in selected)
        {
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.IsDownloading(key, CancellationToken))
                .ReturnsAsync(key.Id == target.Id)
                .Verifiable(Times.Once());
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.IsDownloadFileMoving(key, CancellationToken))
                .ReturnsAsync(false)
                .Verifiable(Times.Once());
        }
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(target.ToKey(), CancellationToken))
            .ReturnsAsync(failStop ? Result.Fail(error) : Result.Ok())
            .Verifiable(Times.Once());
        if (!failStop)
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
        var result = await TestHandlerExecuteAsync(new StopDownloadTaskCommand(node.Id));

        // Assert
        result.IsSuccess.ShouldBe(!failStop);
        result.Errors.Count.ShouldBe(failStop ? 1 : 0);
        if (failStop)
            result.Errors.Single().ShouldBeSameAs(error);
        var after = (await dbContext.GetDownloadTaskFileAsync(target.ToKey(), CancellationToken))!;
        after.DownloadStatus.ShouldBe(failStop ? DownloadStatus.Downloading : DownloadStatus.Stopped);
        after.DataReceived.ShouldBe(failStop ? 128 : 0);
        (await dbContext.GetDownloadTaskFileAsync(control.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(
            DownloadStatus.Queued
        );
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(x => x.StopDownloadTaskJob(control.ToKey(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTaskFilesCommand>(c => c.Keys.Any(k => k.Id == control.Id)),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }
    [Test]
    public async Task ShouldStopAllOtherVideoChildren_WhenStoppingOtherVideo()
    {
        // Arrange
        await SetupDatabase(
            88221,
            c =>
            {
                c.PlexOtherVideoLibraryCount = 1;
                c.OtherVideoDownloadTasksCount = 1;
                c.OtherVideoFileDownloadTasksCount = 2;
            }
        );
        var dbContext = IDbContext;
        var otherVideo = await dbContext.DownloadTaskOtherVideos.FirstAsync(CancellationToken);
        var files = await dbContext.DownloadTaskOtherVideoFiles.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        files.Count.ShouldBe(2);
        await dbContext.SetDownloadStatus(files[0].ToKey(), DownloadStatus.Downloading);
        await dbContext.SetDownloadStatus(files[1].ToKey(), DownloadStatus.Queued);
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(files[0].ToKey(), CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(files[1].ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(files[0].ToKey(), CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        foreach (var file in files)
        {
            var key = file.ToKey();
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.IsDownloadFileMoving(key, CancellationToken))
                .ReturnsAsync(false)
                .Verifiable(Times.Once());
            Mock.Mock<ICommandExecutor>()
                .Setup(x =>
                    x.Send(
                        It.Is<DeleteDownloadTaskFilesCommand>(c => c.Keys.SequenceEqual(new[] { key })),
                        CancellationToken
                    )
                )
                .ReturnsAsync(Result.Ok())
                .Verifiable(Times.Once());
        }

        // Act
        var result = await TestHandlerExecuteAsync(new StopDownloadTaskCommand(otherVideo.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = await dbContext.DownloadTaskOtherVideoFiles.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        after.Select(x => new { x.Id, x.ParentId, x.DownloadStatus }).ShouldBe(
            files.Select(x => new { x.Id, x.ParentId, DownloadStatus = DownloadStatus.Stopped })
        );
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        (await dbContext.DownloadTaskOtherVideos.SingleAsync(CancellationToken)).DownloadStatus.ShouldBe(
            DownloadStatus.Stopped
        );
    }
}
