using Quartz;

namespace Reaparr.Application.UnitTests;

public class QueueMediaOverviewRebuildCommandUnitTests : BaseCommandUnitTest<QueueMediaOverviewRebuildCommand>
{
    [Test]
    public async Task ShouldTriggerQuartzOnce_WhenRepeatedInvalidationsAreQueued()
    {
        // Arrange
        SetupDependencies(builder =>
            builder
                .RegisterInstance(new MediaOverviewRebuildCoordinator())
                .As<IMediaOverviewRebuildCoordinator>()
                .SingleInstance()
        );
        Mock.Mock<IScheduler>()
            .Setup(x =>
                x.TriggerJob(
                    It.Is<JobKey>(key => key.Equals(MediaOverviewSnapshotJob.GetJobKey())),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var firstResult = await TestHandlerExecuteAsync(new QueueMediaOverviewRebuildCommand());
        var secondResult = await TestHandlerExecuteAsync(new QueueMediaOverviewRebuildCommand());

        // Assert
        firstResult.IsSuccess.ShouldBeTrue();
        secondResult.IsSuccess.ShouldBeTrue();
        Mock.Mock<IScheduler>().Verify();
    }

    [Test]
    public async Task ShouldPermitRetry_WhenQuartzTriggerFails()
    {
        // Arrange
        SetupDependencies(builder =>
            builder
                .RegisterInstance(new MediaOverviewRebuildCoordinator())
                .As<IMediaOverviewRebuildCoordinator>()
                .SingleInstance()
        );
        Mock.Mock<IScheduler>()
            .SetupSequence(x =>
                x.TriggerJob(
                    It.Is<JobKey>(key => key.Equals(MediaOverviewSnapshotJob.GetJobKey())),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("trigger failed"))
            .Returns(Task.CompletedTask);

        // Act
        var failedResult = await TestHandlerExecuteAsync(new QueueMediaOverviewRebuildCommand());
        var retryResult = await TestHandlerExecuteAsync(new QueueMediaOverviewRebuildCommand());

        // Assert
        failedResult.IsFailed.ShouldBeTrue();
        retryResult.IsSuccess.ShouldBeTrue();
        Mock.Mock<IScheduler>()
            .Verify(
                x =>
                    x.TriggerJob(
                        It.Is<JobKey>(key => key.Equals(MediaOverviewSnapshotJob.GetJobKey())),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Exactly(2)
            );
    }
}
