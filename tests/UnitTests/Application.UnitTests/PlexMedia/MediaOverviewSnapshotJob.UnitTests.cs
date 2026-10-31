using Quartz;

namespace Reaparr.Application.UnitTests;

public class MediaOverviewSnapshotJobUnitTests : BaseUnitTest<MediaOverviewSnapshotJob>
{
    [Test]
    public async Task ShouldDispatchRebuildAndMarkJobCompleted()
    {
        // Arrange
        var context = CreateJobContext();
        var coordinator = new MediaOverviewRebuildCoordinator();
        SetupDependencies(builder =>
            builder.RegisterInstance(coordinator).As<IMediaOverviewRebuildCoordinator>().SingleInstance()
        );
        coordinator.RequestRebuild(null, DateTimeOffset.UtcNow).ShouldBeTrue();
        Mock.SetupCommand(() => new RebuildMediaOverviewCommand()).ReturnsAsync(Result.Ok()).Verifiable(Times.Once());

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
    public async Task ShouldDispatchOnlyRequestedMediaType_WhenSpecificRebuildIsQueued()
    {
        // Arrange
        var context = CreateJobContext();
        var coordinator = new MediaOverviewRebuildCoordinator();
        SetupDependencies(builder =>
            builder.RegisterInstance(coordinator).As<IMediaOverviewRebuildCoordinator>().SingleInstance()
        );
        coordinator.RequestRebuild(PlexMediaType.PhotoAlbum, DateTimeOffset.UtcNow).ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RebuildMediaOverviewCommand>(command =>
                        command.MediaTypes != null
                        && command.MediaTypes.SequenceEqual(new[] { PlexMediaType.PhotoAlbum })
                    ),
                    System.Threading.CancellationToken.None
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);

        // Assert
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IScheduler>()
            .Verify(x => x.TriggerJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldMarkJobFailedWithoutChangingRecurringSchedule_WhenRebuildFails()
    {
        // Arrange
        var context = CreateJobContext();
        var coordinator = new MediaOverviewRebuildCoordinator();
        SetupDependencies(builder =>
            builder.RegisterInstance(coordinator).As<IMediaOverviewRebuildCoordinator>().SingleInstance()
        );
        coordinator.RequestRebuild(null, DateTimeOffset.UtcNow).ShouldBeTrue();
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
        coordinator.RequestRebuild(null, DateTimeOffset.UtcNow).ShouldBeTrue();
        Mock.SetupCommand(() => new RebuildMediaOverviewCommand())
            .ReturnsAsync(Result.Fail("rebuild failed"))
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);
        var retryAccepted = coordinator.RequestRebuild(null, DateTimeOffset.UtcNow);

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
        coordinator.RequestRebuild(null, DateTimeOffset.UtcNow).ShouldBeTrue();
        Mock.SetupCommand(() => new RebuildMediaOverviewCommand())
            .ReturnsAsync(ResultExtensions.TaskIsCancelled(nameof(RebuildMediaOverviewCommand)))
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);
        var retryAccepted = coordinator.RequestRebuild(null, DateTimeOffset.UtcNow);

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
        coordinator.RequestRebuild(null, DateTimeOffset.UtcNow).ShouldBeTrue();
        Mock.SetupCommand(() => new RebuildMediaOverviewCommand())
            .ThrowsAsync(new InvalidOperationException("dispatch failed"))
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context.Object);
        var retryAccepted = coordinator.RequestRebuild(null, DateTimeOffset.UtcNow);

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

        coordinator.RequestRebuild(null, DateTimeOffset.UtcNow).ShouldBeTrue();
        var executeTask = Sut.Execute(context.Object);
        await rebuildStarted.Task;
        coordinator.RequestRebuild(null, DateTimeOffset.UtcNow).ShouldBeFalse();
        coordinator.RequestRebuild(null, DateTimeOffset.UtcNow).ShouldBeFalse();
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
