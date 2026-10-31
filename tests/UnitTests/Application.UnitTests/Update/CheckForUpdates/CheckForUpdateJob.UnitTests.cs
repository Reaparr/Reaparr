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

    private static IJobExecutionContext SetupJobContext(CancellationToken cancellationToken)
    {
        var context = new Mock<IJobExecutionContext>();
        context.SetupProperty(x => x.Result);
        context.SetupGet(x => x.CancellationToken).Returns(cancellationToken);
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
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckForUpdatesCommand>(), CancellationToken))
            .ReturnsAsync(Result.Ok(CreateCheckResult(updateAvailable)))
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(RefreshDataType.UpdateAvailable))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(notificationCount));

        // Act
        await Sut.Execute(context);

        // Assert
        context.Result.ShouldBeNull();
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
        var commandSetup = Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckForUpdatesCommand>(), CancellationToken));
        if (commandThrows)
        {
            commandSetup.ThrowsAsync(new InvalidOperationException("Update check failed")).Verifiable(Times.Once());
        }
        else
        {
            var checkResult =
                expectedStatus == JobStatus.Cancelled
                    ? ResultExtensions.TaskIsCancelled(nameof(CheckForUpdatesCommand)).ToResult<AppUpdateCheckResult>()
                    : Result.Fail<AppUpdateCheckResult>("Update check failed");
            commandSetup.ReturnsAsync(checkResult).Verifiable(Times.Once());
        }

        // Act
        await Sut.Execute(context);

        // Assert
        var result = context.Result.ShouldBeOfType<BackgroundJobResult>();
        result.Status.ShouldBe(expectedStatus);
        if (expectedStatus == JobStatus.Failed)
            result.ErrorSummary.ShouldBe("Update check failed");
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(RefreshDataType.UpdateAvailable), Times.Never());
    }

    [Test]
    public async Task ShouldContainPublisherFailureAsFailedResult_WhenUpdateNotificationThrows()
    {
        // Arrange
        var context = SetupJobContext(CancellationToken);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckForUpdatesCommand>(), CancellationToken))
            .ReturnsAsync(Result.Ok(CreateCheckResult(true)))
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(RefreshDataType.UpdateAvailable))
            .ThrowsAsync(new InvalidOperationException("Update notification failed"))
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context);

        // Assert
        var result = context.Result.ShouldBeOfType<BackgroundJobResult>();
        result.Status.ShouldBe(JobStatus.Failed);
        result.ErrorSummary.ShouldBe("Update notification failed");
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<CheckForUpdatesCommand>(), CancellationToken), Times.Once());
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(RefreshDataType.UpdateAvailable), Times.Once());
    }
}
