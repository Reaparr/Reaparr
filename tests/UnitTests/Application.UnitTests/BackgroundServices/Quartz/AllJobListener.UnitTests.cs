using Quartz;

namespace Reaparr.Application.UnitTests;

public class AllJobListenerUnitTests : BaseUnitTest<AllJobListener>
{
    public AllJobListenerUnitTests()
    {
    }

    [Test]
    public async Task ShouldCaptureTheOriginalEscapingException_AndPublishFailedStatus_WhenJobThrowsUnexpectedly()
    {
        // Arrange
        var failure = new IOException("Unexpected job transport failure");
        var fireTimeUtc = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        var jobDetail = JobBuilder.Create<OptimizeDatabaseJob>()
            .WithIdentity(nameof(OptimizeDatabaseJob), nameof(JobTypes.OptimizeDatabaseJob))
            .Build();
        var dataMap = new JobDataMap();
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail);
        context.SetupGet(x => x.MergedJobDataMap).Returns(dataMap);
        context.SetupGet(x => x.FireInstanceId).Returns("optimize-fire");
        context.SetupGet(x => x.FireTimeUtc).Returns(fireTimeUtc);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        JobStatusUpdate<string>? update = null;
        Mock.Mock<IProgressHubService>()
            .Setup(x =>
                x.SendJobStatusUpdateAsync(
                    It.Is<JobStatusUpdate<string>>(value =>
                        value.Status == JobStatus.Failed
                            && value.JobType == JobTypes.OptimizeDatabaseJob
                            && value.Id == nameof(OptimizeDatabaseJob)
                            && value.JobStartTime == fireTimeUtc.UtcDateTime
                            && value.Data == "{}"
                    )
                )
            )
            .Callback<JobStatusUpdate<string>>(value => update = value)
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureException(failure, TelemetryActivity.BackgroundJob))
            .Verifiable(Times.Once());

        // Act
        await Sut.JobWasExecuted(context.Object, new JobExecutionException(failure));

        // Assert
        update.ShouldNotBeNull();
        update.Status.ShouldBe(JobStatus.Failed);
        update.JobType.ShouldBe(JobTypes.OptimizeDatabaseJob);
        update.Id.ShouldBe(nameof(OptimizeDatabaseJob));
        update.JobStartTime.ShouldBe(fireTimeUtc.UtcDateTime);
        update.Data.ShouldBe("{}");
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<IProgressHubService>().Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldCaptureUnexpectedCancellationOnly_WithoutChangingQuartzTerminalSemantics_WhenRequestCancellationStateVaries(
        bool requestedCancellation
    )
    {
        // Arrange
        var failure = new OperationCanceledException("transport cancelled");
        var fireTimeUtc = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        var jobDetail = JobBuilder.Create<OptimizeDatabaseJob>()
            .WithIdentity(nameof(OptimizeDatabaseJob), nameof(JobTypes.OptimizeDatabaseJob))
            .Build();
        var dataMap = new JobDataMap();
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail);
        context.SetupGet(x => x.MergedJobDataMap).Returns(dataMap);
        context.SetupGet(x => x.FireInstanceId).Returns("optimize-fire");
        context.SetupGet(x => x.FireTimeUtc).Returns(fireTimeUtc);
        context.SetupGet(x => x.CancellationToken).Returns(new CancellationToken(requestedCancellation));
        JobStatusUpdate<string>? update = null;
        Mock.Mock<IProgressHubService>()
            .Setup(x =>
                x.SendJobStatusUpdateAsync(
                    It.Is<JobStatusUpdate<string>>(value =>
                        value.Status == JobStatus.Cancelled
                            && value.JobType == JobTypes.OptimizeDatabaseJob
                            && value.Id == nameof(OptimizeDatabaseJob)
                            && value.JobStartTime == fireTimeUtc.UtcDateTime
                            && value.Data == "{}"
                    )
                )
            )
            .Callback<JobStatusUpdate<string>>(value => update = value)
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        await Sut.JobWasExecuted(context.Object, new JobExecutionException(failure));

        // Assert
        update.ShouldNotBeNull();
        update.Status.ShouldBe(JobStatus.Cancelled);
        update.JobType.ShouldBe(JobTypes.OptimizeDatabaseJob);
        update.Id.ShouldBe(nameof(OptimizeDatabaseJob));
        update.JobStartTime.ShouldBe(fireTimeUtc.UtcDateTime);
        update.Data.ShouldBe("{}");
        Mock.Mock<ITelemetry>().Verify(
            x => x.CaptureException(failure, TelemetryActivity.BackgroundJob),
            requestedCancellation ? Times.Never() : Times.Once()
        );
        Mock.Mock<IProgressHubService>().Verify();
    }

    [Test]
    public async Task ShouldPreserveAnAlreadyReportedBusinessOutcome_WithoutCapturingItAgain_WhenJobProvidedTerminalResult()
    {
        // Arrange
        var fireTimeUtc = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        var jobDetail = JobBuilder.Create<OptimizeDatabaseJob>()
            .WithIdentity(nameof(OptimizeDatabaseJob), nameof(JobTypes.OptimizeDatabaseJob))
            .Build();
        var dataMap = new JobDataMap();
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail);
        context.SetupGet(x => x.MergedJobDataMap).Returns(dataMap);
        context.SetupGet(x => x.FireInstanceId).Returns("optimize-fire");
        context.SetupGet(x => x.FireTimeUtc).Returns(fireTimeUtc);
        context
            .SetupGet(x => x.Result)
            .Returns(new BackgroundJobResult(JobStatus.Failed, "Handled at the job boundary"));
        JobStatusUpdate<string>? update = null;
        Mock.Mock<IProgressHubService>()
            .Setup(x =>
                x.SendJobStatusUpdateAsync(
                    It.Is<JobStatusUpdate<string>>(value =>
                        value.Status == JobStatus.Failed
                            && value.JobType == JobTypes.OptimizeDatabaseJob
                            && value.Id == nameof(OptimizeDatabaseJob)
                            && value.JobStartTime == fireTimeUtc.UtcDateTime
                            && value.Data == "{}"
                    )
                )
            )
            .Callback<JobStatusUpdate<string>>(value => update = value)
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        await Sut.JobWasExecuted(context.Object, null);

        // Assert
        update.ShouldNotBeNull();
        update.Status.ShouldBe(JobStatus.Failed);
        update.JobType.ShouldBe(JobTypes.OptimizeDatabaseJob);
        update.Id.ShouldBe(nameof(OptimizeDatabaseJob));
        update.JobStartTime.ShouldBe(fireTimeUtc.UtcDateTime);
        update.Data.ShouldBe("{}");
        Mock.Mock<ITelemetry>().Verify(x => x.CaptureException(It.IsAny<Exception>(), It.IsAny<TelemetryActivity>()), Times.Never());
        Mock.Mock<IProgressHubService>().Verify();
    }
}
