using Quartz;

namespace Reaparr.Application.UnitTests;

public class MoveDownloadFileJobSchedulerUnitTests : BaseUnitTest<MoveDownloadFileJobScheduler>
{
    [Test]
    public async Task ShouldSucceed_WhenJobCompletesBeforeInterrupt()
    {
        // Arrange
        var key = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("f2fbaba3-9073-4294-8041-0b6d76a00e84"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var jobKey = MoveDownloadFileJob.GetJobKey(key.Id);
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.Key).Returns(jobKey);
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        var runningChecks = 0;

        Mock.Mock<IScheduler>()
            .Setup(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => runningChecks++ == 0 ? [context.Object] : [])
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IScheduler>()
            .Setup(x => x.CheckExists(jobKey, CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.Interrupt(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.StopMoveDownloadFileJob(key, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IScheduler>().Verify();
    }

    [Test]
    public async Task ShouldFail_WhenJobRemainsRunningAfterInterruptFails()
    {
        // Arrange
        var key = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("3e280449-84b0-442a-84c5-d31e02b3cbd2"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var jobKey = MoveDownloadFileJob.GetJobKey(key.Id);
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.Key).Returns(jobKey);
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);

        Mock.Mock<IScheduler>()
            .Setup(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()))
            .ReturnsAsync([context.Object])
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IScheduler>()
            .Setup(x => x.CheckExists(jobKey, CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.Interrupt(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.StopMoveDownloadFileJob(key, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(0);
        Mock.Mock<IScheduler>().Verify();
    }

    [Test]
    public async Task ShouldDeleteQueuedJob_WhenStoppingQueuedMove()
    {
        // Arrange
        var key = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("c66a179d-339a-4104-a4f9-993cee0f02ac"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var jobKey = MoveDownloadFileJob.GetJobKey(key.Id);
        var trigger = new Mock<ITrigger>();
        trigger.SetupGet(x => x.Key).Returns(new TriggerKey(jobKey.Name, jobKey.Group));

        Mock.Mock<IScheduler>()
            .Setup(x => x.GetCurrentlyExecutingJobs(CancellationToken))
            .ReturnsAsync([])
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IScheduler>()
            .Setup(x => x.CheckExists(jobKey, CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetTriggersOfJob(jobKey, CancellationToken))
            .ReturnsAsync([trigger.Object])
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetTriggerState(trigger.Object.Key, CancellationToken))
            .ReturnsAsync(TriggerState.Normal)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.DeleteJob(jobKey, CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.StopMoveDownloadFileJob(key, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IScheduler>()
            .Verify(x => x.Interrupt(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IScheduler>().Verify();
    }

    [Test]
    public async Task ShouldReportMoving_WhenMoveJobIsQueued()
    {
        // Arrange
        var key = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("077ce4a7-77c0-446b-866c-ea449476676a"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var jobKey = MoveDownloadFileJob.GetJobKey(key.Id);
        var trigger = new Mock<ITrigger>();
        trigger.SetupGet(x => x.Key).Returns(new TriggerKey(jobKey.Name, jobKey.Group));

        Mock.Mock<IScheduler>()
            .Setup(x => x.CheckExists(jobKey, CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetTriggersOfJob(jobKey, CancellationToken))
            .ReturnsAsync([trigger.Object])
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetTriggerState(trigger.Object.Key, CancellationToken))
            .ReturnsAsync(TriggerState.Normal)
            .Verifiable(Times.Once());

        // Act
        var isMoving = await Sut.IsDownloadFileMoving(key, CancellationToken);

        // Assert
        isMoving.ShouldBeTrue();
        Mock.Mock<IScheduler>().Verify();
    }

    [Test]
    public async Task ShouldReplaceInactiveJob_WhenStartingMove()
    {
        // Arrange
        var key = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("1c017050-9d26-421d-9e8e-10dfaeed3f25"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var jobKey = MoveDownloadFileJob.GetJobKey(key.Id);

        Mock.Mock<IScheduler>()
            .Setup(x => x.GetCurrentlyExecutingJobs(CancellationToken))
            .ReturnsAsync([])
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .SetupSequence(x => x.CheckExists(jobKey, CancellationToken))
            .ReturnsAsync(true)
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetTriggersOfJob(jobKey, CancellationToken))
            .ReturnsAsync([])
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.DeleteJob(jobKey, CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), CancellationToken))
            .ReturnsAsync(DateTimeOffset.UtcNow)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.StartMoveDownloadFileJob(key, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IScheduler>().Verify();
    }

    [Test]
    public async Task ShouldReturnQueuedKeyForRequestedServer()
    {
        // Arrange
        var targetKey = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("1f4864c4-c2e7-42ed-a40e-79e109a960ca"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var otherKey = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("1b5117f1-da02-4112-8fad-efca54e83885"),
            PlexServerId = 2,
            PlexLibraryId = 2,
        };
        var targetJobKey = MoveDownloadFileJob.GetJobKey(targetKey.Id);
        var otherJobKey = MoveDownloadFileJob.GetJobKey(otherKey.Id);
        var targetTrigger = new Mock<ITrigger>();
        targetTrigger.SetupGet(x => x.Key).Returns(new TriggerKey(targetJobKey.Name, targetJobKey.Group));
        var otherTrigger = new Mock<ITrigger>();
        otherTrigger.SetupGet(x => x.Key).Returns(new TriggerKey(otherJobKey.Name, otherJobKey.Group));
        var targetJob = new Mock<IJobDetail>();
        targetJob.SetupGet(x => x.JobDataMap).Returns(new MoveDownloadFileJobPayload(targetKey).ToJobDataMap());
        var otherJob = new Mock<IJobDetail>();
        otherJob.SetupGet(x => x.JobDataMap).Returns(new MoveDownloadFileJobPayload(otherKey).ToJobDataMap());

        Mock.Mock<IScheduler>()
            .Setup(x => x.GetCurrentlyExecutingJobs(CancellationToken.None))
            .ReturnsAsync([])
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetJobKeys(It.IsAny<Quartz.Impl.Matchers.GroupMatcher<JobKey>>(), CancellationToken.None))
            .ReturnsAsync([targetJobKey, otherJobKey])
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.CheckExists(targetJobKey, CancellationToken.None))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetTriggersOfJob(targetJobKey, CancellationToken.None))
            .ReturnsAsync([targetTrigger.Object])
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetTriggerState(targetTrigger.Object.Key, CancellationToken.None))
            .ReturnsAsync(TriggerState.Normal)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.CheckExists(otherJobKey, CancellationToken.None))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetTriggersOfJob(otherJobKey, CancellationToken.None))
            .ReturnsAsync([otherTrigger.Object])
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetTriggerState(otherTrigger.Object.Key, CancellationToken.None))
            .ReturnsAsync(TriggerState.Paused)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetJobDetail(targetJobKey, CancellationToken.None))
            .ReturnsAsync(targetJob.Object)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetJobDetail(otherJobKey, CancellationToken.None))
            .ReturnsAsync(otherJob.Object)
            .Verifiable(Times.Never());

        // Act
        var keys = await Sut.GetCurrentlyMovingKeysByServer(targetKey.PlexServerId);

        // Assert
        keys.ShouldHaveSingleItem();
        keys.Single().Id.ShouldBe(targetKey.Id);
        keys.Single().PlexServerId.ShouldBe(targetKey.PlexServerId);
        keys.Single().PlexLibraryId.ShouldBe(targetKey.PlexLibraryId);
        Mock.Mock<IScheduler>().Verify();
    }
}
