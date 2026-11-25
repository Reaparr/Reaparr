using System.Diagnostics;
using Quartz;

namespace Reaparr.Application.UnitTests;

public class CheckForUpdateJobUnitTests : BaseUnitTest<CheckForUpdateJob>
{
    private const string CURRENT_VERSION = "1.2.3";

    private static AppUpdateCheckResult CreateCheckResult(bool updateAvailable) =>
        new()
        {
            IsUpdateAvailable = updateAvailable,
            NewestVersion = updateAvailable ? "1.2.4" : CURRENT_VERSION,
            CurrentVersion = CURRENT_VERSION,
            ReleaseNotes = [],
        };

    private static IJobExecutionContext SetupJobContext(CancellationToken cancellationToken, JobDataMap? jobData = null)
    {
        var mergedJobDataMap = jobData ?? new JobDataMap();
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.Key).Returns(new JobKey(nameof(CheckForUpdateJob), nameof(CheckForUpdateJob)));
        jobDetail.SetupGet(x => x.JobDataMap).Returns(mergedJobDataMap);

        var context = new Mock<IJobExecutionContext>();
        context.SetupProperty(x => x.Result);
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(x => x.MergedJobDataMap).Returns(mergedJobDataMap);
        context.SetupGet(x => x.CancellationToken).Returns(cancellationToken);
        context.SetupGet(x => x.FireInstanceId).Returns("update-check-test");
        return context.Object;
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldPublishOnlyWhenSuccessfulCheckFindsAnUpdate(bool updateAvailable)
    {
        // Arrange
        var context = SetupJobContext(CancellationToken);
        var notificationCount = updateAvailable ? 1 : 0;
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.BeginJob(TelemetryActivity.UpdateCheck, It.IsAny<(ActivityContext, string?, string, string)>())
            )
            .Returns((IDisposable?)null)
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckForUpdatesCommand>(), context.CancellationToken))
            .ReturnsAsync(Result.Ok(CreateCheckResult(updateAvailable)))
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(RefreshDataType.UpdateAvailable))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(notificationCount));

        // Act
        await Sut.Execute(context, context.CancellationToken);

        // Assert
        context.Result.ShouldBeNull();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    [Arguments(JobStatus.Failed, false)]
    [Arguments(JobStatus.Cancelled, false)]
    [Arguments(JobStatus.Failed, true)]
    public async Task ShouldPreserveFailureStatusAndNotPublish_WhenCheckDoesNotSucceed(
        JobStatus expectedStatus,
        bool commandThrows
    )
    {
        // Arrange
        var context = SetupJobContext(CancellationToken);
        InvalidOperationException? commandException = null;
        Result<AppUpdateCheckResult>? returnedFailure = null;
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.BeginJob(TelemetryActivity.UpdateCheck, It.IsAny<(ActivityContext, string?, string, string)>())
            )
            .Returns((IDisposable?)null)
            .Verifiable(Times.Once());
        var commandSetup = Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckForUpdatesCommand>(), context.CancellationToken));
        if (commandThrows)
        {
            commandException = new InvalidOperationException("Update check failed");
            commandSetup.ThrowsAsync(commandException).Verifiable(Times.Once());
        }
        else
        {
            returnedFailure =
                expectedStatus == JobStatus.Cancelled
                    ? ResultExtensions.TaskIsCancelled(nameof(CheckForUpdatesCommand)).ToResult<AppUpdateCheckResult>()
                    : Result.Fail<AppUpdateCheckResult>("Update check failed");
            commandSetup.ReturnsAsync(returnedFailure).Verifiable(Times.Once());
        }

        if (expectedStatus == JobStatus.Failed)
        {
            Mock.Mock<ITelemetry>()
                .Setup(x =>
                    x.CaptureErrors(
                        It.Is<ResultBase>(result =>
                            result.IsFailed
                            && result.Errors.Count == 1
                            && (
                                commandThrows
                                    ? result
                                        .Errors.OfType<ExceptionalError>()
                                        .Any(error => ReferenceEquals(error.Exception, commandException))
                                    : ReferenceEquals(result.Errors.Single(), returnedFailure!.Errors.Single())
                                        && result.Errors.Single().Message == "Update check failed"
                            )
                        ),
                        TelemetryActivity.UpdateCheck
                    )
                )
                .Verifiable(Times.Once());
        }

        // Act
        await Sut.Execute(context, context.CancellationToken);

        // Assert
        var result = context.Result.ShouldBeOfType<BackgroundJobResult>();
        result.Status.ShouldBe(expectedStatus);
        if (expectedStatus == JobStatus.Failed)
            result.ErrorSummary.ShouldBe("Update check failed");
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(RefreshDataType.UpdateAvailable), Times.Never());
    }

    [Test]
    public async Task ShouldContainPublisherFailureAsFailedResult_WhenUpdateNotificationThrows()
    {
        // Arrange
        var context = SetupJobContext(CancellationToken);
        var notificationException = new InvalidOperationException("Update notification failed");
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.BeginJob(TelemetryActivity.UpdateCheck, It.IsAny<(ActivityContext, string?, string, string)>())
            )
            .Returns((IDisposable?)null)
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckForUpdatesCommand>(), context.CancellationToken))
            .ReturnsAsync(Result.Ok(CreateCheckResult(true)))
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(RefreshDataType.UpdateAvailable))
            .ThrowsAsync(notificationException)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.CaptureErrors(
                    It.Is<ResultBase>(result =>
                        result.IsFailed
                        && result.Errors.Count == 1
                        && result
                            .Errors.OfType<ExceptionalError>()
                            .Any(error => ReferenceEquals(error.Exception, notificationException))
                    ),
                    TelemetryActivity.UpdateCheck
                )
            )
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context, context.CancellationToken);

        // Assert
        var result = context.Result.ShouldBeOfType<BackgroundJobResult>();
        result.Status.ShouldBe(JobStatus.Failed);
        result.ErrorSummary.ShouldBe("Update notification failed");
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldPassSerializedContextToTelemetry_WhenScheduledCheckRuns()
    {
        // Arrange
        var traceParent = $"00-{ActivityTraceId.CreateRandom()}-{ActivitySpanId.CreateRandom()}-01";
        ActivityContext.TryParse(traceParent, null, isRemote: true, out var expectedParent).ShouldBeTrue();
        var sessionId = "6d3f5d2b-9d5b-4f64-a1f7-d8a4e1d29e0a";
        var context = SetupJobContext(
            CancellationToken,
            new JobDataMap
            {
                [TelemetryProtocol.TraceParentMetadata] = traceParent,
                [TelemetryProtocol.SessionIdMetadata] = sessionId,
            }
        );
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckForUpdatesCommand>(), context.CancellationToken))
            .ReturnsAsync(Result.Ok(CreateCheckResult(false)))
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.BeginJob(
                    TelemetryActivity.UpdateCheck,
                    It.Is<(ActivityContext Parent, string? SessionId, string JobKey, string FireInstanceId)>(value =>
                        value.Parent.TraceId == expectedParent.TraceId
                        && value.Parent.SpanId == expectedParent.SpanId
                        && value.SessionId == sessionId
                        && value.JobKey == $"{nameof(CheckForUpdateJob)}.{nameof(CheckForUpdateJob)}"
                        && value.FireInstanceId == "update-check-test"
                    )
                )
            )
            .Returns((IDisposable?)null)
            .Verifiable(Times.Once());

        using var ambientActivity = new Activity("unrelated").Start();

        // Act
        await Sut.Execute(context, context.CancellationToken);

        // Assert
        Activity.Current.ShouldBe(ambientActivity);
        context.Result.ShouldBeNull();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
    }
}
