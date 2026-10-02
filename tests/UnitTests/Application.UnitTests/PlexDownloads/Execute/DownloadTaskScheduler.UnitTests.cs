using Quartz;

namespace Reaparr.Application.UnitTests;

public class DownloadTaskSchedulerUnitTests : BaseUnitTest<DownloadTaskScheduler>
{
    [Test]
    public async Task ShouldSucceed_WhenJobCompletesBeforeInterrupt()
    {
        // Arrange
        var key = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("cf0b8bdb-403d-4627-a015-42683fdd8c30"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var jobKey = DownloadJob.GetJobKey(key.Id);
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
            .Setup(x => x.Interrupt(jobKey, CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.StopDownloadTaskJob(key, CancellationToken);

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
            Id = Guid.Parse("94db012d-91d1-4546-b21a-2201f4e229a0"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var jobKey = DownloadJob.GetJobKey(key.Id);
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
            .Setup(x => x.Interrupt(jobKey, CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.StopDownloadTaskJob(key, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<IScheduler>().Verify();
    }

    [Test]
    public async Task ShouldDeleteQueuedJob_WhenStoppingQueuedDownload()
    {
        // Arrange
        var key = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("f6d4c3a8-4a8f-46f5-a897-7c60e7ea6118"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var jobKey = DownloadJob.GetJobKey(key.Id);
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
        var result = await Sut.StopDownloadTaskJob(key, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IScheduler>()
            .Verify(x => x.Interrupt(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IScheduler>().Verify();
    }

    [Test]
    public async Task ShouldReturnCancelledResult_WhenCompletionWaitIsCancelled()
    {
        // Arrange
        var key = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("e470c719-20ea-48e8-8e7a-5154d5a4d852"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var jobKey = DownloadJob.GetJobKey(key.Id);
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.Key).Returns(jobKey);
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        var interrupted = false;

        Mock.Mock<IScheduler>()
            .Setup(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
                interrupted
                    ? throw new OperationCanceledException()
                    : (IReadOnlyCollection<IJobExecutionContext>)[context.Object]
            );
        Mock.Mock<IScheduler>().Setup(x => x.CheckExists(jobKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Mock.Mock<IScheduler>()
            .Setup(x => x.Interrupt(jobKey, It.IsAny<CancellationToken>()))
            .Callback(() => interrupted = true)
            .ReturnsAsync(true)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.StopDownloadTaskJob(key, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<IScheduler>().Verify();
    }

    [Test]
    public async Task ShouldReportDownloading_WhenDownloadJobIsQueued()
    {
        // Arrange
        var key = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("da50918e-134c-4bc4-a558-3d7a2ced0918"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var jobKey = DownloadJob.GetJobKey(key.Id);
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
        var isDownloading = await Sut.IsDownloading(key, CancellationToken);

        // Assert
        isDownloading.ShouldBeTrue();
        Mock.Mock<IScheduler>().Verify();
    }

    [Test]
    public async Task ShouldReplaceInactiveJob_WhenStartingDownload()
    {
        // Arrange
        var key = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("c3cafc22-45cc-4ea2-90fa-2ca42b550b39"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var jobKey = DownloadJob.GetJobKey(key.Id);

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
        var result = await Sut.StartDownloadTaskJob(key, CancellationToken);

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
            Id = Guid.Parse("38f18e33-85f0-40b1-86c3-242cc141d75d"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var otherKey = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("7086bb55-d370-4281-93a7-14d69d6a4034"),
            PlexServerId = 2,
            PlexLibraryId = 2,
        };
        var targetJobKey = DownloadJob.GetJobKey(targetKey.Id);
        var otherJobKey = DownloadJob.GetJobKey(otherKey.Id);
        var targetTrigger = new Mock<ITrigger>();
        targetTrigger.SetupGet(x => x.Key).Returns(new TriggerKey(targetJobKey.Name, targetJobKey.Group));
        var otherTrigger = new Mock<ITrigger>();
        otherTrigger.SetupGet(x => x.Key).Returns(new TriggerKey(otherJobKey.Name, otherJobKey.Group));
        var targetJob = new Mock<IJobDetail>();
        targetJob.SetupGet(x => x.JobDataMap).Returns(new DownloadJobPayload(targetKey).ToJobDataMap());
        var otherJob = new Mock<IJobDetail>();
        otherJob.SetupGet(x => x.JobDataMap).Returns(new DownloadJobPayload(otherKey).ToJobDataMap());

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
        var keys = await Sut.GetCurrentlyDownloadingKeysByServer(targetKey.PlexServerId);

        // Assert
        keys.ShouldHaveSingleItem();
        keys.Single().Id.ShouldBe(targetKey.Id);
        keys.Single().PlexServerId.ShouldBe(targetKey.PlexServerId);
        keys.Single().PlexLibraryId.ShouldBe(targetKey.PlexLibraryId);
        Mock.Mock<IScheduler>().Verify();
    }
}
