using Quartz;

namespace Reaparr.Application.UnitTests;

public class MoveDownloadFileJobRetryUnitTests : BaseUnitTest<MoveDownloadFileJob>
{
    private static Mock<IJobExecutionContext> CreateContext(
        DownloadTaskKey key,
        IScheduler scheduler,
        CancellationToken cancellationToken,
        string fireInstanceId,
        int? retryCount = null
    )
    {
        var jobDataMap = new MoveDownloadFileJobPayload(key).ToJobDataMap();
        var mergedJobDataMap = new MoveDownloadFileJobPayload(key).ToJobDataMap();
        if (retryCount.HasValue)
            mergedJobDataMap[MoveDownloadFileJob.RetryCountKey] = retryCount.Value;

        var jobDetail = JobBuilder
            .Create<MoveDownloadFileJob>()
            .WithIdentity(MoveDownloadFileJob.GetJobKey(key.Id))
            .UsingJobData(jobDataMap)
            .Build();
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail);
        context.SetupGet(x => x.MergedJobDataMap).Returns(mergedJobDataMap);
        context.SetupGet(x => x.CancellationToken).Returns(cancellationToken);
        context.SetupGet(x => x.FireInstanceId).Returns(fireInstanceId);
        context.SetupGet(x => x.Scheduler).Returns(scheduler);
        context.SetupProperty(x => x.Result);
        return context;
    }

    [Test]
    public async Task ShouldScheduleExactlyFiveDelayedRetriesWithIncrementingCounts_WhenMoveFails()
    {
        // Arrange
        await SetupDatabase(78181, config => config.MovieDownloadTasksCount = 1);
        MoveDownloadFileJob.MaxRetryCount.ShouldBe(5);
        var dbContext = IDbContext;
        var target = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        await dbContext.SetDownloadStatus(target.ToKey(), DownloadStatus.MoveError);
        var key = target.ToKey();
        var jobKey = MoveDownloadFileJob.GetJobKey(key.Id);
        var scheduler = Mock.Mock<IScheduler>();
        var moveException = new IOException("retryable move failure");
        var moveFailure = Result.Fail(new ExceptionalError(moveException));
        var pending = new List<ITrigger>();
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(It.Is<ResultBase>(r => r.IsFailed), TelemetryActivity.MoveDownloadJob))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<MoveDownloadFileFromFileTaskCommand>(c => c.Key == key), CancellationToken))
            .ReturnsAsync(moveFailure)
            .Verifiable(Times.Exactly(MoveDownloadFileJob.MaxRetryCount + 1));
        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(MoveDownloadFileJob.MaxRetryCount + 1));
        scheduler
            .Setup(x => x.Exists(It.Is<TriggerKey>(triggerKey => triggerKey.Group == jobKey.Group), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Exactly(MoveDownloadFileJob.MaxRetryCount));
        scheduler
            .Setup(x =>
                x.ScheduleJob(
                    It.Is<ITrigger>(trigger => trigger.JobKey.Equals(jobKey)),
                    It.IsAny<ScheduleJobOptions>(),
                    CancellationToken
                )
            )
            .Callback<ITrigger, ScheduleJobOptions, CancellationToken>((trigger, _, _) => pending.Add(trigger))
            .Returns(new ValueTask<DateTimeOffset>(DateTimeOffset.UtcNow))
            .Verifiable(Times.Exactly(MoveDownloadFileJob.MaxRetryCount));

        var contexts = Enumerable
            .Range(0, MoveDownloadFileJob.MaxRetryCount + 1)
            .Select(attempt =>
                CreateContext(
                    key,
                    scheduler.Object,
                    CancellationToken,
                    $"move-job-attempt-{attempt}",
                    attempt == 0 ? null : attempt
                )
            )
            .ToArray();
        var before = DateTimeOffset.UtcNow;

        // Act
        foreach (var context in contexts)
            await Sut.Execute(context.Object, context.Object.CancellationToken);

        // Assert
        foreach (var context in contexts)
        {
            var outcome = context.Object.Result.ShouldBeOfType<BackgroundJobResult>();
            outcome.Status.ShouldBe(JobStatus.Failed);
        }
        Mock.Mock<ITelemetry>()
            .Verify(
                x => x.CaptureErrors(It.Is<ResultBase>(r => r.IsFailed), TelemetryActivity.MoveDownloadJob),
                Times.Once()
            );

        pending.Count.ShouldBe(MoveDownloadFileJob.MaxRetryCount);
        pending
            .Select(trigger => trigger.JobDataMap.GetInt(MoveDownloadFileJob.RetryCountKey))
            .ShouldBe(Enumerable.Range(1, MoveDownloadFileJob.MaxRetryCount).ToArray());
        pending
            .Select(trigger => trigger.Key.Name)
            .ShouldBe(
                Enumerable
                    .Range(0, MoveDownloadFileJob.MaxRetryCount)
                    .Select(attempt => $"{jobKey.Name}_Retry_move-job-attempt-{attempt}")
                    .ToArray()
            );
        foreach (var trigger in pending)
        {
            trigger.JobKey.ShouldBe(jobKey);
            trigger.StartTimeUtc.ShouldBeInRange(before.AddMinutes(1), DateTimeOffset.UtcNow.AddMinutes(1));
            ((ISimpleTrigger)trigger).RepeatCount.ShouldBe(0);
            ((ISimpleTrigger)trigger).MisfireInstruction.ShouldBe(SimpleTriggerMisfireInstruction.FireNow);
        }

        (await dbContext.GetDownloadTaskStatusAsync(key, CancellationToken)).ShouldBe(DownloadStatus.MoveError);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IMoveDownloadFileQueue>().Verify();
        scheduler.Verify();
        scheduler.Verify(
            x => x.ScheduleJob(It.IsAny<ITrigger>(), It.IsAny<ScheduleJobOptions>(), It.IsAny<CancellationToken>()),
            Times.Exactly(MoveDownloadFileJob.MaxRetryCount)
        );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CleanUpDownloadTaskFoldersCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldCaptureSchedulingExceptionInsteadOfMoveFailure_WhenRetrySchedulingThrows()
    {
        // Arrange
        await SetupDatabase(78184, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var target = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        await dbContext.SetDownloadStatus(target.ToKey(), DownloadStatus.MoveError);
        var key = target.ToKey();
        var scheduler = Mock.Mock<IScheduler>();
        var moveException = new IOException("retryable move failure");
        var schedulingException = new InvalidOperationException("retry scheduling failed");
        ResultBase? captured = null;
        var context = CreateContext(key, scheduler.Object, CancellationToken, "move-job-schedule-failure");

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<MoveDownloadFileFromFileTaskCommand>(c => c.Key == key), CancellationToken))
            .ReturnsAsync(Result.Fail(new ExceptionalError(moveException)))
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.Exists(It.IsAny<TriggerKey>(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.ScheduleJob(It.IsAny<ITrigger>(), It.IsAny<ScheduleJobOptions>(), CancellationToken))
            .ThrowsAsync(schedulingException)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(It.IsAny<ResultBase>(), TelemetryActivity.MoveDownloadJob))
            .Callback<ResultBase, TelemetryActivity>((result, _) => captured = result)
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object, context.Object.CancellationToken);

        // Assert
        context.Object.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Failed);
        captured.ShouldNotBeNull();
        captured!.Errors.Count.ShouldBe(1);
        captured.Errors.Single().ShouldBeOfType<ExceptionalError>().Exception.ShouldBeSameAs(schedulingException);
        (await dbContext.GetDownloadTaskStatusAsync(key, CancellationToken)).ShouldBe(DownloadStatus.MoveError);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IMoveDownloadFileQueue>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        scheduler.Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CleanUpDownloadTaskFoldersCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldLeaveMoveErrorAndScheduleNothing_WhenRetryAllowanceIsExhausted()
    {
        // Arrange
        await SetupDatabase(78182, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var target = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        await dbContext.SetDownloadStatus(target.ToKey(), DownloadStatus.MoveError);
        var key = target.ToKey();
        var scheduler = Mock.Mock<IScheduler>();
        var context = CreateContext(
            key,
            scheduler.Object,
            CancellationToken,
            "move-job-exhausted",
            MoveDownloadFileJob.MaxRetryCount
        );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<MoveDownloadFileFromFileTaskCommand>(c => c.Key == key), CancellationToken))
            .ReturnsAsync(Result.Fail("move failed after retries"))
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.Exists(It.IsAny<TriggerKey>(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Never());
        scheduler
            .Setup(x => x.ScheduleJob(It.IsAny<ITrigger>(), It.IsAny<ScheduleJobOptions>(), CancellationToken))
            .Returns(new ValueTask<DateTimeOffset>(DateTimeOffset.UtcNow))
            .Verifiable(Times.Never());

        // Act
        await Sut.Execute(context.Object, context.Object.CancellationToken);

        // Assert
        var outcome = context.Object.Result.ShouldBeOfType<BackgroundJobResult>();
        outcome.Status.ShouldBe(JobStatus.Failed);
        (await dbContext.GetDownloadTaskStatusAsync(key, CancellationToken)).ShouldBe(DownloadStatus.MoveError);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IMoveDownloadFileQueue>().Verify();
        scheduler.Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CleanUpDownloadTaskFoldersCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldStartWithFreshRetryAllowance_WhenNewJobHasNoRetryData()
    {
        // Arrange
        await SetupDatabase(78183, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var target = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        await dbContext.SetDownloadStatus(target.ToKey(), DownloadStatus.MoveError);
        var key = target.ToKey();
        var jobKey = MoveDownloadFileJob.GetJobKey(key.Id);
        var scheduler = Mock.Mock<IScheduler>();
        var pending = new List<ITrigger>();

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<MoveDownloadFileFromFileTaskCommand>(c => c.Key == key), CancellationToken))
            .ReturnsAsync(Result.Fail("move failed"))
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(2));
        scheduler
            .Setup(x => x.Exists(It.IsAny<TriggerKey>(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.ScheduleJob(
                    It.Is<ITrigger>(trigger => trigger.JobKey.Equals(jobKey)),
                    It.IsAny<ScheduleJobOptions>(),
                    CancellationToken
                )
            )
            .Callback<ITrigger, ScheduleJobOptions, CancellationToken>((trigger, _, _) => pending.Add(trigger))
            .Returns(new ValueTask<DateTimeOffset>(DateTimeOffset.UtcNow))
            .Verifiable(Times.Once());

        var exhaustedContext = CreateContext(
            key,
            scheduler.Object,
            CancellationToken,
            "move-job-exhausted",
            MoveDownloadFileJob.MaxRetryCount
        );
        var restartContext = CreateContext(key, scheduler.Object, CancellationToken, "manual-restart");
        await Sut.Execute(exhaustedContext.Object, exhaustedContext.Object.CancellationToken);
        await Sut.Execute(restartContext.Object, restartContext.Object.CancellationToken);

        // Assert
        pending.ShouldHaveSingleItem();
        pending[0].JobDataMap[MoveDownloadFileJob.RetryCountKey].ShouldBe(1);
        (await dbContext.GetDownloadTaskStatusAsync(key, CancellationToken)).ShouldBe(DownloadStatus.MoveError);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IMoveDownloadFileQueue>().Verify();
        scheduler.Verify();
    }
}
