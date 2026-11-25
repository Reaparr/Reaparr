using Quartz;

namespace Reaparr.Application.UnitTests;

public class DownloadTaskSchedulerUnitTests : BaseUnitTest<DownloadTaskScheduler>
{
    private const string SCHEDULER_INSTANCE_ID = "test-scheduler";

    [Test]
    public async Task ShouldSucceed_WhenJobCompletesBeforeInterrupt()
    {
        Mock.Mock<IScheduler>().SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_INSTANCE_ID);
        // Arrange
        var key = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("cf0b8bdb-403d-4627-a015-42683fdd8c30"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var jobKey = DownloadJob.GetJobKey(key.Id);
        var fire = CreateFireInstance(key);
        var runningChecks = 0;

        Mock.Mock<IScheduler>()
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query => IsRunningQuery(query, 0)),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
                new ValueTask<PagedResult<FireInstance>>(
                    new PagedResult<FireInstance>(runningChecks++ == 0 ? [fire] : [], false)
                )
            )
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IScheduler>()
            .Setup(x => x.Exists(jobKey, CancellationToken))
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
        Mock.Mock<IScheduler>().SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_INSTANCE_ID);
        // Arrange
        var key = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("94db012d-91d1-4546-b21a-2201f4e229a0"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var jobKey = DownloadJob.GetJobKey(key.Id);
        var fire = CreateFireInstance(key);

        Mock.Mock<IScheduler>()
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query => IsRunningQuery(query, 0)),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<PagedResult<FireInstance>>(new PagedResult<FireInstance>([fire], false)))
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IScheduler>()
            .Setup(x => x.Exists(jobKey, CancellationToken))
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
        Mock.Mock<IScheduler>().SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_INSTANCE_ID);
        // Arrange
        var key = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("f6d4c3a8-4a8f-46f5-a897-7c60e7ea6118"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var jobKey = DownloadJob.GetJobKey(key.Id);
        var triggerKey = new TriggerKey(jobKey.Name, jobKey.Group);

        Mock.Mock<IScheduler>()
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query => IsRunningQuery(query, 0)),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<PagedResult<FireInstance>>(new PagedResult<FireInstance>([], false)))
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IScheduler>()
            .Setup(x => x.Exists(jobKey, CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryTriggers(It.Is<TriggerQuery>(query => IsTriggerQuery(query, jobKey)), CancellationToken))
            .Returns(
                new ValueTask<PagedResult<TriggerHeader>>(
                    new PagedResult<TriggerHeader>(
                        [CreateTriggerHeader(triggerKey, jobKey, "SIMPLE", TriggerState.Normal)],
                        false
                    )
                )
            )
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
        Mock.Mock<IScheduler>().SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_INSTANCE_ID);
        // Arrange
        var key = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse("e470c719-20ea-48e8-8e7a-5154d5a4d852"),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };
        var jobKey = DownloadJob.GetJobKey(key.Id);
        var fire = CreateFireInstance(key);
        var interrupted = false;

        Mock.Mock<IScheduler>()
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query => IsRunningQuery(query, 0)),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
                interrupted
                    ? throw new OperationCanceledException()
                    : new ValueTask<PagedResult<FireInstance>>(new PagedResult<FireInstance>([fire], false))
            );
        Mock.Mock<IScheduler>().Setup(x => x.Exists(jobKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
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
        var triggerKey = new TriggerKey(jobKey.Name, jobKey.Group);

        Mock.Mock<IScheduler>()
            .Setup(x => x.Exists(jobKey, CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryTriggers(It.Is<TriggerQuery>(query => IsTriggerQuery(query, jobKey)), CancellationToken))
            .Returns(
                new ValueTask<PagedResult<TriggerHeader>>(
                    new PagedResult<TriggerHeader>(
                        [CreateTriggerHeader(triggerKey, jobKey, "SIMPLE", TriggerState.Normal)],
                        false
                    )
                )
            )
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
        Mock.Mock<IScheduler>().SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_INSTANCE_ID);
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
            .SetupSequence(x => x.Exists(jobKey, CancellationToken))
            .ReturnsAsync(true)
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        Mock.Mock<IScheduler>()
            .Setup(x => x.QueryTriggers(It.Is<TriggerQuery>(query => IsTriggerQuery(query, jobKey)), CancellationToken))
            .Returns(new ValueTask<PagedResult<TriggerHeader>>(new PagedResult<TriggerHeader>([], false)))
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query => IsRunningQuery(query, 0)),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<PagedResult<FireInstance>>(new PagedResult<FireInstance>([], false)))
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.DeleteJob(jobKey, CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x =>
                x.ScheduleJob(
                    It.IsAny<IJobDetail>(),
                    It.IsAny<ITrigger>(),
                    It.IsAny<ScheduleJobOptions>(),
                    CancellationToken
                )
            )
            .Returns(new ValueTask<DateTimeOffset>(DateTimeOffset.UtcNow))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.StartDownloadTaskJob(key, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<IScheduler>().Verify(x => x.Exists(jobKey, CancellationToken), Times.Exactly(3));
    }

    [Test]
    public async Task ShouldReturnQueuedKeyForRequestedServer()
    {
        Mock.Mock<IScheduler>().SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_INSTANCE_ID);
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
        var targetTriggerKey = new TriggerKey(targetJobKey.Name, targetJobKey.Group);
        var otherTriggerKey = new TriggerKey(otherJobKey.Name, otherJobKey.Group);
        var targetJob = CreateJobDetail(targetKey);
        var otherJob = CreateJobDetail(otherKey);

        Mock.Mock<IScheduler>()
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query => IsRunningQuery(query, 0)),
                    CancellationToken.None
                )
            )
            .Returns(new ValueTask<PagedResult<FireInstance>>(new PagedResult<FireInstance>([], false)))
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IScheduler>()
            .Setup(x =>
                x.GetJobDetails(It.Is<IReadOnlyCollection<JobKey>>(keys => keys.Count == 0), CancellationToken.None)
            )
            .ReturnsAsync([])
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x =>
                x.QueryJobs(
                    It.Is<JobQuery>(query => IsJobQuery(query, JobTypes.DownloadJob, 0)),
                    CancellationToken.None
                )
            )
            .Returns(
                new ValueTask<PagedResult<JobHeader>>(
                    new PagedResult<JobHeader>([CreateJobHeader(targetJobKey), CreateJobHeader(otherJobKey)], false)
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.Exists(targetJobKey, CancellationToken.None))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x =>
                x.QueryTriggers(
                    It.Is<TriggerQuery>(query => IsTriggerQuery(query, targetJobKey)),
                    CancellationToken.None
                )
            )
            .Returns(
                new ValueTask<PagedResult<TriggerHeader>>(
                    new PagedResult<TriggerHeader>(
                        [CreateTriggerHeader(targetTriggerKey, targetJobKey, "SIMPLE", TriggerState.Normal)],
                        false
                    )
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.Exists(otherJobKey, CancellationToken.None))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x =>
                x.QueryTriggers(
                    It.Is<TriggerQuery>(query => IsTriggerQuery(query, otherJobKey)),
                    CancellationToken.None
                )
            )
            .Returns(
                new ValueTask<PagedResult<TriggerHeader>>(
                    new PagedResult<TriggerHeader>(
                        [CreateTriggerHeader(otherTriggerKey, otherJobKey, "SIMPLE", TriggerState.Paused)],
                        false
                    )
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetJobDetail(targetJobKey, CancellationToken.None))
            .ReturnsAsync(targetJob)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetJobDetail(otherJobKey, CancellationToken.None))
            .ReturnsAsync(otherJob)
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

    private static bool IsRunningQuery(FireInstanceQuery query, int skip) =>
        query.SchedulerInstanceId == SCHEDULER_INSTANCE_ID
        && query.State == FireInstanceState.Executing
        && query.Skip == skip
        && query.Take == PagedQuery.DefaultTake;

    private static bool IsJobQuery(JobQuery query, JobTypes jobType, int skip) =>
        query.Group?.CompareToValue == jobType.ToString() && query.Skip == skip && query.Take == PagedQuery.DefaultTake;

    private static bool IsTriggerQuery(TriggerQuery query, JobKey jobKey) =>
        query.Job == jobKey && query.Skip == 0 && query.Take == PagedQuery.DefaultTake;

    private static TriggerHeader CreateTriggerHeader(
        TriggerKey triggerKey,
        JobKey jobKey,
        string triggerType,
        TriggerState state
    ) =>
        new(
            triggerKey,
            jobKey,
            null,
            triggerType,
            state,
            DateTimeOffset.UnixEpoch,
            null,
            DateTimeOffset.UnixEpoch,
            null,
            null,
            5,
            null,
            null,
            0
        );

    private static JobHeader CreateJobHeader(JobKey jobKey) =>
        new(jobKey, null, typeof(DownloadJob).FullName!, false, true, false, true);

    private static IJobDetail CreateJobDetail(DownloadTaskKey key, string? group = null)
    {
        var payload = new DownloadJobPayload(key).ToJobDataMap();
        var jobKey = group is null ? DownloadJob.GetJobKey(key.Id) : new JobKey(key.Id.ToString(), group);
        return JobBuilder.Create<DownloadJob>().WithIdentity(jobKey).UsingJobData(payload).Build();
    }

    private static FireInstance CreateFireInstance(DownloadTaskKey key, string? group = null)
    {
        var jobKey = group is null ? DownloadJob.GetJobKey(key.Id) : new JobKey(key.Id.ToString(), group);
        var now = DateTimeOffset.UnixEpoch;
        return new FireInstance(
            $"fire-{key.Id}",
            new TriggerKey(jobKey.Name, jobKey.Group),
            jobKey,
            SCHEDULER_INSTANCE_ID,
            FireInstanceState.Executing,
            now,
            now,
            null
        );
    }
}
