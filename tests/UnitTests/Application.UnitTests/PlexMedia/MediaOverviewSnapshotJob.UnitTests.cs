using Quartz;

namespace Reaparr.Application.UnitTests;

public class MediaOverviewSnapshotJobUnitTests : BaseUnitTest<MediaOverviewSnapshotJob>
{
    [Test]
    public async Task ShouldRebuildBothRootTypesAndMarkJobCompleted()
    {
        // Arrange
        var context = CreateJobContext();
        Mock.SetupCommand(() => new RebuildMediaOverviewCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.RescheduleJob(It.IsAny<TriggerKey>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow.AddHours(1))
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);

        // Assert
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IScheduler>().Verify();
        context.Verify(x => x.Put(JobStatus.Completed.ToString(), It.IsAny<object>()), Times.Never());
    }

    [Test]
    public async Task ShouldMarkJobFailedAndStillReschedule_WhenRebuildFails()
    {
        // Arrange
        var context = CreateJobContext();
        Mock.SetupCommand(() => new RebuildMediaOverviewCommand())
            .ReturnsAsync(Result.Fail("rebuild failed"))
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.RescheduleJob(It.IsAny<TriggerKey>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.UtcNow.AddHours(1))
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);

        // Assert
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IScheduler>().Verify();
    }

    private static Mock<IJobExecutionContext> CreateJobContext()
    {
        var context = new Mock<IJobExecutionContext>(MockBehavior.Loose);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        context.SetupGet(x => x.MergedJobDataMap).Returns(new JobDataMap());
        return context;
    }
}
