using System.Diagnostics;
using Quartz;

namespace Reaparr.Application.UnitTests;

public class RefreshPlexAccountAccessJobUnitTests : BaseUnitTest<RefreshPlexAccountAccessJob>
{
    private static IJobExecutionContext SetupJobContext(CancellationToken cancellationToken = default)
    {
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.Key).Returns(RefreshPlexAccountAccessJob.GetJobKey());

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(x => x.MergedJobDataMap).Returns(new JobDataMap());
        context.SetupGet(x => x.FireInstanceId).Returns("account-access-test");
        context.SetupProperty(x => x.Result);
        context.SetupGet(x => x.CancellationToken).Returns(cancellationToken);
        return context.Object;
    }
    private void SetupRunJob()
    {
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.RunJobAsync(
                    It.IsAny<TelemetryActivity>(),
                    It.IsAny<(ActivityContext Parent, string? SessionId, string JobKey, string FireInstanceId)>(),
                    It.IsAny<Func<Task>>()
                )
            )
            .Returns(
                (
                    TelemetryActivity operation,
                    (ActivityContext Parent, string? SessionId, string JobKey, string FireInstanceId) telemetryContext,
                    Func<Task> execute
                ) => execute()
            )
            .Verifiable(Times.Once());
    }

    [Test]
    public async Task ShouldComplete_WhenAccountAccessRefreshSucceeds()
    {
        // Arrange
        var context = SetupJobContext();
        SetupRunJob();
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshPlexAccountAccessCommand>(command => command.Trigger == TelemetryTrigger.Scheduled),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(Result.Ok(new List<RefreshPlexAccountAccessRapportDTO>()))
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context);

        // Assert
        context.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Completed);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldCancel_WhenAccountAccessRefreshObservesCallerCancellation()
    {
        // Arrange
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var context = SetupJobContext(cancellationSource.Token);
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshPlexAccountAccessCommand>(command => command.Trigger == TelemetryTrigger.Scheduled),
                    cancellationSource.Token
                )
            )
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token))
            .Verifiable(Times.Once());

        SetupRunJob();
        // Act
        await Sut.Execute(context);

        // Assert
        context.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Cancelled);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureErrors(It.IsAny<ResultBase>(), TelemetryActivity.PlexAccessRefresh), Times.Never());
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureException(It.IsAny<Exception>(), It.IsAny<TelemetryActivity>()), Times.Never());
    }

    [Test]
    public async Task ShouldCancel_WhenAccountAccessRefreshReturnsCancellation()
    {
        // Arrange
        var context = SetupJobContext();
        var cancellation =
            ResultExtensions
                .TaskIsCancelled(nameof(RefreshPlexAccountAccessJob))
                .ToResult<List<RefreshPlexAccountAccessRapportDTO>>();
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshPlexAccountAccessCommand>(command => command.Trigger == TelemetryTrigger.Scheduled),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(cancellation)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(cancellation, TelemetryActivity.PlexAccessRefresh))
            .Verifiable(Times.Once());

        SetupRunJob();
        // Act
        await Sut.Execute(context);

        // Assert
        var result = context.Result.ShouldBeOfType<BackgroundJobResult>();
        result.Status.ShouldBe(JobStatus.Cancelled);
        result.ErrorSummary.ShouldBe(cancellation.Errors.Single().Message);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureException(It.IsAny<Exception>(), It.IsAny<TelemetryActivity>()), Times.Never());
    }

    [Test]
    public async Task ShouldFail_WhenAccountAccessRefreshFails()
    {
        // Arrange
        var context = SetupJobContext();
        var failure = Result.Fail<List<RefreshPlexAccountAccessRapportDTO>>("Refresh failed");
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshPlexAccountAccessCommand>(command => command.Trigger == TelemetryTrigger.Scheduled),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(failure)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(failure, TelemetryActivity.PlexAccessRefresh))
            .Verifiable(Times.Once());

        SetupRunJob();
        // Act
        await Sut.Execute(context);

        // Assert
        var result = context.Result.ShouldBeOfType<BackgroundJobResult>();
        result.Status.ShouldBe(JobStatus.Failed);
        result.ErrorSummary.ShouldBe("Refresh failed");
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldContainUnexpectedRefreshExceptionAndMarkJobFailed()
    {
        // Arrange
        var context = SetupJobContext();
        var exception = new InvalidOperationException("refresh access failed");
        Exception? capturedException = null;
        SetupRunJob();
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshPlexAccountAccessCommand>(command => command.Trigger == TelemetryTrigger.Scheduled),
                    CancellationToken.None
                )
            )
            .ThrowsAsync(exception)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.CaptureException(
                    It.Is<Exception>(captured => ReferenceEquals(captured, exception)),
                    TelemetryActivity.PlexAccessRefresh
                )
            )
            .Callback<Exception, TelemetryActivity>((captured, _) => capturedException = captured)
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context);

        // Assert
        var terminalResult = context.Result.ShouldBeOfType<BackgroundJobResult>();
        terminalResult.Status.ShouldBe(JobStatus.Failed);
        terminalResult.ErrorSummary.ShouldBe(exception.Message);
        capturedException.ShouldBeSameAs(exception);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureErrors(It.IsAny<ResultBase>(), TelemetryActivity.PlexAccessRefresh), Times.Never());
    }

    [Test]
    public async Task ShouldFail_WhenAccountAccessRefreshThrowsUnrequestedCancellation()
    {
        // Arrange
        var context = SetupJobContext();
        var exception = new OperationCanceledException("account access refresh timed out");
        Exception? capturedException = null;
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshPlexAccountAccessCommand>(command => command.Trigger == TelemetryTrigger.Scheduled),
                    CancellationToken.None
                )
            )
            .ThrowsAsync(exception)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.CaptureException(
                    It.Is<Exception>(captured => ReferenceEquals(captured, exception)),
                    TelemetryActivity.PlexAccessRefresh
                )
            )
            .Callback<Exception, TelemetryActivity>((captured, _) => capturedException = captured)
            .Verifiable(Times.Once());

        SetupRunJob();
        // Act
        await Sut.Execute(context);

        // Assert
        var result = context.Result.ShouldBeOfType<BackgroundJobResult>();
        result.Status.ShouldBe(JobStatus.Failed);
        result.ErrorSummary.ShouldBe(exception.Message);
        capturedException.ShouldBeSameAs(exception);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureErrors(It.IsAny<ResultBase>(), TelemetryActivity.PlexAccessRefresh), Times.Never());
    }


}
