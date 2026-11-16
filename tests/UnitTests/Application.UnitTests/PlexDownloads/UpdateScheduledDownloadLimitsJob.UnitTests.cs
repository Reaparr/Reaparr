using System.Diagnostics;
using Quartz;

namespace Reaparr.Application.UnitTests;

public class UpdateScheduledDownloadLimitsJobUnitTests : BaseUnitTest<UpdateScheduledDownloadLimitsJob>
{
    private static IJobDetail CreateJobDetail() =>
        JobBuilder
            .Create<UpdateScheduledDownloadLimitsJob>()
            .WithIdentity(UpdateScheduledDownloadLimitsJob.GetJobKey())
            .Build();

    [Test]
    [Arguments(false, JobStatus.Completed)]
    [Arguments(true, JobStatus.Failed)]
    public async Task ShouldRecordRecalculationOutcome_WhenCommandCompletes(bool failed, JobStatus expectedStatus)
    {
        // Arrange
        var commandResult = failed ? Result.Fail("Schedule recalculation failed") : Result.Ok();
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(CreateJobDetail());
        context.SetupGet(x => x.FireInstanceId).Returns("scheduled-limits-test");

        context.SetupProperty(x => x.Result);
        context.SetupGet(x => x.MergedJobDataMap).Returns(new JobDataMap());
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<UpdateScheduledDownloadLimitsCommand>(command => command.LocalTime == null),
                    CancellationToken
                )
            )
            .ReturnsAsync(commandResult)
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);

        // Assert
        ((BackgroundJobResult)context.Object.Result!).Status.ShouldBe(expectedStatus);
        Mock.Mock<ICommandExecutor>().Verify();
        context.Verify();
    }

    [Test]
    public async Task ShouldUseScheduledLimitsTelemetryBoundary()
    {
        // Arrange
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(CreateJobDetail());

        context.SetupProperty(x => x.Result);
        context.SetupGet(x => x.MergedJobDataMap).Returns(new JobDataMap());
        context.SetupGet(x => x.FireInstanceId).Returns("scheduled-limits-test");
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<UpdateScheduledDownloadLimitsCommand>(command => command.LocalTime == null),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(Result.Ok());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.BeginJob(
                    TelemetryActivity.ScheduledDownloadLimits,
                    It.IsAny<(ActivityContext Parent, string? SessionId, string JobKey, string FireInstanceId)>()
                )
            )
            .Returns((IDisposable?)null)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CompleteScheduledDownloadLimits(It.IsAny<Activity?>(), It.Is<ResultBase>(r => r.IsSuccess)))
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);

        // Assert
        context.Object.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Completed);
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldCaptureOriginalCommandFailureOnce_WhenCommandReturnsExceptionalResult()
    {
        // Arrange
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(CreateJobDetail());
        context.SetupGet(x => x.FireInstanceId).Returns("scheduled-limits-test");
        context.SetupProperty(x => x.Result);
        context.SetupGet(x => x.MergedJobDataMap).Returns(new JobDataMap());
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        var exception = new InvalidOperationException("scheduled limit update failed");
        var commandResult = Result.Fail(new ExceptionalError(exception));
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<UpdateScheduledDownloadLimitsCommand>(command => command.LocalTime == null),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(commandResult)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(commandResult, TelemetryActivity.ScheduledDownloadLimits))
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);

        // Assert
        var outcome = context.Object.Result.ShouldBeOfType<BackgroundJobResult>();
        outcome.Status.ShouldBe(JobStatus.Failed);
        outcome.ErrorSummary.ShouldBe(exception.Message);
        commandResult.IsFailed.ShouldBeTrue();
        commandResult.Errors.Count.ShouldBe(1);
        commandResult.Errors.Single().ShouldBeOfType<ExceptionalError>().Exception.ShouldBeSameAs(exception);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureErrors(commandResult, TelemetryActivity.ScheduledDownloadLimits), Times.Once());
    }

    [Test]
    [Arguments("2026-10-04T18:00:00Z", "2026-10-04T18:15:00Z")]
    [Arguments("2026-10-04T18:15:01Z", "2026-10-04T18:45:00Z")]
    public void ShouldFireAtBothLocalHalfHourBoundaries_WhenTheTimezoneHasAQuarterHourOffset(
        string after,
        string expected
    )
    {
        // Arrange
        var trigger = UpdateScheduledDownloadLimitsJob
            .CreateTrigger("Asia/Kathmandu")
            .GetTriggerBuilder()
            .StartAt(DateTimeOffset.UnixEpoch)
            .Build();

        // Act
        var nextFireTime = trigger.GetFireTimeAfter(DateTimeOffset.Parse(after));

        // Assert
        nextFireTime.ShouldBe(DateTimeOffset.Parse(expected));
    }
}
