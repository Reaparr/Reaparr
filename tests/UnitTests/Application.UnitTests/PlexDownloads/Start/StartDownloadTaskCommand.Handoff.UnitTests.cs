namespace Reaparr.Application.UnitTests;

public class StartDownloadTaskCommandHandoffUnitTests : BaseCommandUnitTest<StartDownloadTaskCommand>
{
    [Test]
    [Arguments(false, false, false, DownloadStatus.Queued, DownloadStatus.Queued)]
    [Arguments(true, false, false, DownloadStatus.Queued, DownloadStatus.Queued)]
    [Arguments(true, true, false, DownloadStatus.Queued, DownloadStatus.Queued)]
    [Arguments(false, false, false, DownloadStatus.Downloading, DownloadStatus.Completed)]
    [Arguments(false, false, true, DownloadStatus.Downloading, DownloadStatus.Queued)]
    [Arguments(false, true, true, DownloadStatus.Downloading, DownloadStatus.Queued)]
    [Arguments(false, false, false, DownloadStatus.DownloadFinished, DownloadStatus.Queued)]
    [Arguments(false, false, false, DownloadStatus.Completed, DownloadStatus.MovePaused)]
    public async Task ShouldUndoOnlyProvisionalWork_WhenDownloadHandoffFails(
        bool cancelled,
        bool throwFailure,
        bool stopFails,
        DownloadStatus startedStatusDuringHandoff,
        DownloadStatus siblingStatusDuringHandoff
    )
    {
        // Arrange
        SetupDependencies(builder =>
            builder.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>().SingleInstance()
        );
        await SetupDatabase(
            31831,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieDownloadTasksCount = 1;
                config.TvShowDownloadTasksCount = 2;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeCount = 3;
                config.TvShowEpisodeDownloadTasksCount = 3;
            }
        );
        var dbContext = IDbContext;
        await dbContext.DownloadTaskTvShowEpisodeFile.ExecuteUpdateAsync(
            update => update.SetProperty(file => file.DownloadStatus, DownloadStatus.Completed),
            CancellationToken
        );
        var root = await dbContext.DownloadTaskTvShow.FirstAsync(CancellationToken);
        var children = await dbContext.GetDownloadableChildTasks(root.ToKey(), CancellationToken);
        children.Count.ShouldBeGreaterThanOrEqualTo(3);
        var startedKey = children[0].ToKey();
        var siblingKey = children[1].ToKey();
        var retainedKey = children[2].ToKey();
        var activeKey = (await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken)).ToKey();
        activeKey.PlexServerId.ShouldBe(startedKey.PlexServerId);
        await dbContext.SetDownloadStatus(startedKey, DownloadStatus.Paused);
        await dbContext.SetDownloadStatus(siblingKey, DownloadStatus.AutoPaused);
        await dbContext.SetDownloadStatus(retainedKey, DownloadStatus.MovePaused);
        await dbContext.SetDownloadStatus(activeKey, DownloadStatus.Downloading);
        var before = await dbContext
            .DownloadTaskTvShowEpisodeFile.OrderBy(file => file.Id)
            .Select(file => new { file.Id, file.DownloadStatus })
            .ToListAsync(CancellationToken);
        before.Single(file => file.Id == startedKey.Id).DownloadStatus.ShouldBe(DownloadStatus.Paused);
        before.Single(file => file.Id == siblingKey.Id).DownloadStatus.ShouldBe(DownloadStatus.AutoPaused);
        before.Single(file => file.Id == retainedKey.Id).DownloadStatus.ShouldBe(DownloadStatus.MovePaused);
        var enteredPause = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishPause = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stillActive = startedStatusDuringHandoff is DownloadStatus.Queued or DownloadStatus.Downloading;

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(scheduler => scheduler.IsDownloading(startedKey, CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(scheduler =>
                scheduler.IsDownloading(
                    startedKey,
                    It.Is<CancellationToken>(token =>
                        token != CancellationToken && token.CanBeCanceled && !token.IsCancellationRequested
                    )
                )
            )
            .ReturnsAsync(stillActive)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(scheduler => scheduler.StartDownloadTaskJob(startedKey, CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(scheduler => scheduler.GetCurrentlyDownloadingKeysByServer(startedKey.PlexServerId))
            .ReturnsAsync([startedKey, activeKey])
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(executor => executor.Send(new PauseDownloadTaskCommand(activeKey.Id), CancellationToken))
            .Returns(() =>
            {
                enteredPause.SetResult();
                return finishPause.Task;
            })
            .Verifiable(Times.Once());
        if (stillActive)
        {
            var stopSetup = Mock.Mock<IDownloadTaskScheduler>()
                .Setup(scheduler =>
                    scheduler.StopDownloadTaskJob(
                        startedKey,
                        It.Is<CancellationToken>(token =>
                            token != CancellationToken && token.CanBeCanceled && !token.IsCancellationRequested
                        ),
                        true
                    )
                );
            if (stopFails && throwFailure)
                stopSetup.ThrowsAsync(new InvalidOperationException("Stop failed")).Verifiable(Times.Once());
            else
                stopSetup.ReturnsAsync(stopFails ? Result.Fail("Stop failed") : Result.Ok()).Verifiable(Times.Once());
        }
        if (startedStatusDuringHandoff == DownloadStatus.Downloading && !stopFails)
        {
            Mock.SetupCommand(It.IsAny<UpdateScheduledDownloadLimitsCommand>).ReturnOk().Verifiable(Times.Once());
        }

        // Act — let the real dispatcher queue siblings, then advance records while the old-job pause is pending.
        var execution = TestHandlerExecuteAsync(new StartDownloadTaskCommand(root.Id));
        await enteredPause.Task.WaitAsync(CancellationToken);
        (await dbContext.GetDownloadStatusAsync(startedKey)).ShouldBe(DownloadStatus.Queued);
        (await dbContext.GetDownloadStatusAsync(siblingKey)).ShouldBe(DownloadStatus.Queued);
        await dbContext.SetDownloadStatus(startedKey, startedStatusDuringHandoff);
        await dbContext.SetDownloadStatus(siblingKey, siblingStatusDuringHandoff);
        if (cancelled && throwFailure)
            finishPause.SetException(new OperationCanceledException(new CancellationToken(true)));
        else
            finishPause.SetResult(cancelled ? ResultExtensions.TaskIsCancelled("Pause") : Result.Fail("Pause failed"));
        var result = await execution;

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(stopFails ? 2 : 1);
        if (cancelled)
            result.IsCancelled.ShouldBeTrue();
        else
            result.Errors.ShouldContain(error => error.Message == "Pause failed");
        if (stopFails)
            result.Errors.ShouldContain(error => error.Message.Contains("Stop failed"));
        var expectedStartedStatus = !stopFails && stillActive ? DownloadStatus.Paused : startedStatusDuringHandoff;
        var expectedSiblingStatus =
            siblingStatusDuringHandoff == DownloadStatus.Queued
                ? DownloadStatus.AutoPaused
                : siblingStatusDuringHandoff;
        var after = await dbContext
            .DownloadTaskTvShowEpisodeFile.OrderBy(file => file.Id)
            .Select(file => new { file.Id, file.DownloadStatus })
            .ToListAsync(CancellationToken);
        after.ShouldBe(
            before
                .Select(file => new
                {
                    file.Id,
                    DownloadStatus = file.Id == startedKey.Id ? expectedStartedStatus
                    : file.Id == siblingKey.Id ? expectedSiblingStatus
                    : file.DownloadStatus,
                })
                .ToList()
        );
        (await dbContext.GetDownloadStatusAsync(activeKey)).ShouldBe(DownloadStatus.Downloading);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                scheduler =>
                    scheduler.StopDownloadTaskJob(
                        It.Is<DownloadTaskKey>(key => key != startedKey),
                        It.IsAny<CancellationToken>(),
                        It.IsAny<bool>()
                    ),
                Times.Never()
            );
        if (!stillActive)
            Mock.Mock<IDownloadTaskScheduler>()
                .Verify(
                    scheduler =>
                        scheduler.StopDownloadTaskJob(
                            It.IsAny<DownloadTaskKey>(),
                            It.IsAny<CancellationToken>(),
                            It.IsAny<bool>()
                        ),
                    Times.Never()
                );
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
    }
}
