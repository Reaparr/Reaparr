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

    private void SetupBeginJob()
    {
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.BeginJob(
                    TelemetryActivity.PlexAccessRefresh,
                    It.IsAny<(ActivityContext Parent, string? SessionId, string JobKey, string FireInstanceId)>()
                )
            )
            .Returns((IDisposable?)null)
            .Verifiable(Times.Once());
    }

    [Test]
    public async Task ShouldComplete_WhenAccountAccessRefreshSucceeds()
    {
        // Arrange
        var context = SetupJobContext();
        SetupBeginJob();
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
        var cancellationToken = cancellationSource.Token;
        cancellationSource.Cancel();
        var context = SetupJobContext(cancellationToken);
        var exception = new OperationCanceledException(cancellationToken);
        ResultBase? capturedResult = null;
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshPlexAccountAccessCommand>(command => command.Trigger == TelemetryTrigger.Scheduled),
                    cancellationToken
                )
            )
            .ThrowsAsync(exception)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.CaptureErrors(
                    It.Is<ResultBase>(result =>
                        result.Errors.Count == 1
                        && result.HasError<ExceptionalError>(error => ReferenceEquals(error.Exception, exception))
                    ),
                    TelemetryActivity.PlexAccessRefresh
                )
            )
            .Callback<ResultBase, TelemetryActivity>((result, _) => capturedResult = result)
            .Verifiable(Times.Once());

        SetupBeginJob();
        // Act
        await Sut.Execute(context, context.CancellationToken);

        // Assert
        context.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Cancelled);
        capturedResult.ShouldNotBeNull().IsCancelled.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureException(It.IsAny<Exception>(), It.IsAny<TelemetryActivity>()), Times.Never());
    }

    [Test]
    public async Task ShouldCancel_WhenAccountAccessRefreshReturnsCancellation()
    {
        // Arrange
        var context = SetupJobContext();
        var cancellation = ResultExtensions
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

        SetupBeginJob();
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

        SetupBeginJob();
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
        SetupBeginJob();
        var capturedResult = (ResultBase?)null;
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
                x.CaptureErrors(
                    It.Is<ResultBase>(result =>
                        result.IsFailed
                        && result.Errors.Count == 1
                        && result.HasError<ExceptionalError>(error => ReferenceEquals(error.Exception, exception))
                    ),
                    TelemetryActivity.PlexAccessRefresh
                )
            )
            .Callback<ResultBase, TelemetryActivity>((result, _) => capturedResult = result)
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context);

        // Assert
        var terminalResult = context.Result.ShouldBeOfType<BackgroundJobResult>();
        terminalResult.Status.ShouldBe(JobStatus.Failed);
        terminalResult.ErrorSummary.ShouldBe(exception.Message);
        capturedResult.ShouldNotBeNull().IsCancelled.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldFail_WhenAccountAccessRefreshThrowsUnrequestedCancellation()
    {
        // Arrange
        var context = SetupJobContext();
        var exception = new OperationCanceledException("account access refresh timed out");
        var capturedResult = (ResultBase?)null;
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
                x.CaptureErrors(
                    It.Is<ResultBase>(result =>
                        result.IsFailed
                        && result.Errors.Count == 1
                        && result.HasError<ExceptionalError>(error => ReferenceEquals(error.Exception, exception))
                    ),
                    TelemetryActivity.PlexAccessRefresh
                )
            )
            .Callback<ResultBase, TelemetryActivity>((result, _) => capturedResult = result)
            .Verifiable(Times.Once());

        SetupBeginJob();
        // Act
        await Sut.Execute(context);

        // Assert
        var result = context.Result.ShouldBeOfType<BackgroundJobResult>();
        result.Status.ShouldBe(JobStatus.Failed);
        result.ErrorSummary.ShouldBe(exception.Message);
        capturedResult.ShouldNotBeNull().IsCancelled.ShouldBeFalse();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
    }
}
