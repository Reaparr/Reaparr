using Quartz;

namespace Reaparr.Application.UnitTests;

public class MediaOverviewSnapshotJobUnitTests : BaseUnitTest<MediaOverviewSnapshotJob>
{
    [Test]
    public async Task ShouldRebuildBothRootTypesAndMarkJobCompleted()
    {
        // Arrange
        var context = CreateJobContext();
        SetupDependencies(builder =>
            builder
                .RegisterInstance(new MediaOverviewRebuildCoordinator())
                .As<IMediaOverviewRebuildCoordinator>()
                .SingleInstance()
        );
        Mock.SetupCommand(() => new RebuildMediaOverviewCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);

        // Assert
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IScheduler>()
            .Verify(x => x.TriggerJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IScheduler>()
            .Verify(
                x => x.RescheduleJob(It.IsAny<TriggerKey>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        context.Verify(x => x.Put(JobStatus.Completed.ToString(), It.IsAny<object>()), Times.Never());
    }

    [Test]
    public async Task ShouldMarkJobFailedWithoutChangingRecurringSchedule_WhenRebuildFails()
    {
        // Arrange
        var context = CreateJobContext();
        SetupDependencies(builder =>
            builder
                .RegisterInstance(new MediaOverviewRebuildCoordinator())
                .As<IMediaOverviewRebuildCoordinator>()
                .SingleInstance()
        );
        Mock.SetupCommand(() => new RebuildMediaOverviewCommand())
            .ReturnsAsync(Result.Fail("rebuild failed"))
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);

        // Assert
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IScheduler>()
            .Verify(x => x.TriggerJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IScheduler>()
            .Verify(
                x => x.RescheduleJob(It.IsAny<TriggerKey>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldReleaseCoordinatorAndAllowRetry_WhenRebuildFails()
    {
        // Arrange
        var context = CreateJobContext();
        var coordinator = new MediaOverviewRebuildCoordinator();
        SetupDependencies(builder =>
            builder.RegisterInstance(coordinator).As<IMediaOverviewRebuildCoordinator>().SingleInstance()
        );
        Mock.SetupCommand(() => new RebuildMediaOverviewCommand())
            .ReturnsAsync(Result.Fail("rebuild failed"))
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);
        var retryAccepted = coordinator.RequestRebuild(DateTimeOffset.UtcNow);

        // Assert
        retryAccepted.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldReleaseCoordinatorAndAllowRetry_WhenRebuildIsCancelled()
    {
        // Arrange
        var context = CreateJobContext();
        var coordinator = new MediaOverviewRebuildCoordinator();
        SetupDependencies(builder =>
            builder.RegisterInstance(coordinator).As<IMediaOverviewRebuildCoordinator>().SingleInstance()
        );
        Mock.SetupCommand(() => new RebuildMediaOverviewCommand())
            .ReturnsAsync(ResultExtensions.TaskIsCancelled(nameof(RebuildMediaOverviewCommand)))
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);
        var retryAccepted = coordinator.RequestRebuild(DateTimeOffset.UtcNow);

        // Assert
        retryAccepted.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldContainExceptionAndReleaseCoordinator_WhenCommandDispatchThrows()
    {
        // Arrange
        var context = CreateJobContext();
        var coordinator = new MediaOverviewRebuildCoordinator();
        SetupDependencies(builder =>
            builder.RegisterInstance(coordinator).As<IMediaOverviewRebuildCoordinator>().SingleInstance()
        );
        Mock.SetupCommand(() => new RebuildMediaOverviewCommand())
            .ThrowsAsync(new InvalidOperationException("dispatch failed"))
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);
        var retryAccepted = coordinator.RequestRebuild(DateTimeOffset.UtcNow);

        // Assert
        retryAccepted.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldTriggerFollowUpRebuild_WhenInvalidatedDuringSuccessfulRebuild()
    {
        // Arrange
        var context = CreateJobContext();
        var coordinator = new MediaOverviewRebuildCoordinator();
        var rebuildStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishRebuild = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetupDependencies(builder =>
            builder.RegisterInstance(coordinator).As<IMediaOverviewRebuildCoordinator>().SingleInstance()
        );
        Mock.SetupCommand(() => new RebuildMediaOverviewCommand())
            .Returns(() =>
            {
                rebuildStarted.SetResult();
                return finishRebuild.Task;
            })
            .Verifiable(Times.Once());

        // Act
        var executeTask = Sut.Execute(context.Object);
        await rebuildStarted.Task;
        coordinator.RequestRebuild(DateTimeOffset.UtcNow).ShouldBeFalse();
        coordinator.RequestRebuild(DateTimeOffset.UtcNow).ShouldBeFalse();
        finishRebuild.SetResult(Result.Ok());
        await executeTask;

        // Assert
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IScheduler>()
            .Verify(x => x.TriggerJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    private static Mock<IJobExecutionContext> CreateJobContext()
    {
        var context = new Mock<IJobExecutionContext>(MockBehavior.Loose);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        context.SetupGet(x => x.MergedJobDataMap).Returns(new JobDataMap());
        return context;
    }
}
