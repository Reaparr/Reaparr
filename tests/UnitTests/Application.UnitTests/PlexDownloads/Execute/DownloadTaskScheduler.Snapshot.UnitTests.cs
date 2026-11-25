using Quartz;

namespace Reaparr.Application.UnitTests;

public class DownloadTaskSchedulerSnapshotUnitTests : BaseUnitTest<DownloadTaskScheduler>
{
    private const string SCHEDULER_INSTANCE_ID = "test-scheduler";

    [Test]
    public async Task ShouldReuseExecutingPayloadsAcrossPagesWithoutReadingIndividualStoredJobs()
    {
        // Arrange
        var target = CreateKey(1);
        var otherServer = CreateKey(2, 2);
        var unrelated = CreateKey(3);
        var targetJobKey = DownloadJob.GetJobKey(target.Id);
        var otherJobKey = DownloadJob.GetJobKey(otherServer.Id);
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_INSTANCE_ID);
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query => IsRunningQuery(query, 0)),
                    CancellationToken.None
                )
            )
            .Returns(
                new ValueTask<PagedResult<FireInstance>>(
                    new PagedResult<FireInstance>([CreateFireInstance(target)], true)
                )
            )
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query => IsRunningQuery(query, 1)),
                    CancellationToken.None
                )
            )
            .Returns(
                new ValueTask<PagedResult<FireInstance>>(
                    new PagedResult<FireInstance>(
                        [CreateFireInstance(otherServer), CreateFireInstance(unrelated, "OtherJob")],
                        false
                    )
                )
            )
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.GetJobDetails(
                    It.Is<IReadOnlyCollection<JobKey>>(keys =>
                        keys.Count == 2 && keys.Contains(targetJobKey) && keys.Contains(otherJobKey)
                    ),
                    CancellationToken.None
                )
            )
            .Returns(new ValueTask<List<IJobDetail>>([CreateJobDetail(target), CreateJobDetail(otherServer)]))
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.QueryJobs(It.Is<JobQuery>(query => IsJobQuery(query, 0)), CancellationToken.None))
            .Returns(
                new ValueTask<PagedResult<JobHeader>>(new PagedResult<JobHeader>([CreateJobHeader(targetJobKey)], true))
            )
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.QueryJobs(It.Is<JobQuery>(query => IsJobQuery(query, 1)), CancellationToken.None))
            .Returns(
                new ValueTask<PagedResult<JobHeader>>(new PagedResult<JobHeader>([CreateJobHeader(otherJobKey)], false))
            )
            .Verifiable(Times.Once());

        // Act
        var keys = await Sut.GetCurrentlyDownloadingKeysByServer(target.PlexServerId);

        // Assert
        keys.ShouldHaveSingleItem();
        keys.Single().ShouldBeEquivalentTo(target);
        scheduler.Verify();
        scheduler.Verify(x => x.Exists(It.IsAny<JobKey>(), CancellationToken.None), Times.Never());
        scheduler.Verify(x => x.QueryTriggers(It.IsAny<TriggerQuery>(), CancellationToken.None), Times.Never());
        scheduler.Verify(x => x.GetJobDetail(It.IsAny<JobKey>(), CancellationToken.None), Times.Never());
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldRetainStoredPayloadFallbackWhenTheExecutionSnapshotCannotSupplyIt(bool wasAlreadyExecuting)
    {
        // Arrange
        var target = CreateKey(4);
        var jobKey = DownloadJob.GetJobKey(target.Id);
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_INSTANCE_ID);
        var queryCount = 0;
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query => IsRunningQuery(query, 0)),
                    CancellationToken.None
                )
            )
            .Returns(() =>
            {
                var hasExecution = wasAlreadyExecuting ? queryCount == 0 : queryCount == 1;
                queryCount++;
                return new ValueTask<PagedResult<FireInstance>>(
                    new PagedResult<FireInstance>(hasExecution ? [CreateFireInstance(target)] : [], false)
                );
            });
        scheduler
            .Setup(x =>
                x.GetJobDetails(
                    It.Is<IReadOnlyCollection<JobKey>>(keys => keys.Count == (wasAlreadyExecuting ? 1 : 0)),
                    CancellationToken.None
                )
            )
            .Returns(new ValueTask<List<IJobDetail>>(wasAlreadyExecuting ? [CreateJobDetail(target)] : []))
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.QueryJobs(It.Is<JobQuery>(query => IsJobQuery(query, 0)), CancellationToken.None))
            .Returns(
                new ValueTask<PagedResult<JobHeader>>(new PagedResult<JobHeader>([CreateJobHeader(jobKey)], false))
            )
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.Exists(jobKey, CancellationToken.None))
            .ReturnsAsync(true)
            .Verifiable(Times.Exactly(wasAlreadyExecuting ? 0 : 1));
        scheduler
            .Setup(x =>
                x.QueryTriggers(It.Is<TriggerQuery>(query => IsTriggerQuery(query, jobKey, 0)), CancellationToken.None)
            )
            .Returns(new ValueTask<PagedResult<TriggerHeader>>(new PagedResult<TriggerHeader>([], false)))
            .Verifiable(Times.Exactly(wasAlreadyExecuting ? 0 : 1));
        scheduler
            .Setup(x => x.GetJobDetail(jobKey, CancellationToken.None))
            .ReturnsAsync(CreateJobDetail(target))
            .Verifiable(Times.Exactly(wasAlreadyExecuting ? 0 : 1));

        // Act
        var keys = await Sut.GetCurrentlyDownloadingKeysByServer(target.PlexServerId);

        // Assert
        keys.ShouldHaveSingleItem();
        keys.Single().ShouldBeEquivalentTo(target);
        scheduler.Verify();
        scheduler.Verify(
            x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), CancellationToken.None),
            Times.Exactly(wasAlreadyExecuting ? 1 : 2)
        );
    }

    [Test]
    public async Task ShouldIncludeBlockedJobsButExcludePausedCronDisappearedAndMissingPayloadJobs()
    {
        // Arrange
        var queued = CreateKey(5);
        var paused = CreateKey(6);
        var cron = CreateKey(7);
        var disappeared = CreateKey(8);
        var missingPayload = CreateKey(9);
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_INSTANCE_ID);
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query => IsRunningQuery(query, 0)),
                    CancellationToken.None
                )
            )
            .Returns(new ValueTask<PagedResult<FireInstance>>(new PagedResult<FireInstance>([], false)))
            .Verifiable(Times.Exactly(4));
        scheduler
            .Setup(x =>
                x.GetJobDetails(It.Is<IReadOnlyCollection<JobKey>>(keys => keys.Count == 0), CancellationToken.None)
            )
            .Returns(new ValueTask<List<IJobDetail>>([]))
            .Verifiable(Times.Once());
        var jobKeys = new[] { queued, paused, cron, disappeared, missingPayload }
            .Select(x => DownloadJob.GetJobKey(x.Id))
            .ToList();
        scheduler
            .Setup(x => x.QueryJobs(It.Is<JobQuery>(query => IsJobQuery(query, 0)), CancellationToken.None))
            .Returns(
                new ValueTask<PagedResult<JobHeader>>(
                    new PagedResult<JobHeader>(jobKeys.Select(CreateJobHeader).ToList(), false)
                )
            )
            .Verifiable(Times.Once());
        foreach (var key in new[] { queued, paused, cron, disappeared, missingPayload })
        {
            var jobKey = DownloadJob.GetJobKey(key.Id);
            scheduler
                .Setup(x => x.Exists(jobKey, CancellationToken.None))
                .ReturnsAsync(key != disappeared)
                .Verifiable(Times.Once());
            if (key == disappeared)
                continue;

            var triggerKey = new TriggerKey(jobKey.Name, jobKey.Group);
            scheduler
                .Setup(x =>
                    x.QueryTriggers(
                        It.Is<TriggerQuery>(query => IsTriggerQuery(query, jobKey, 0)),
                        CancellationToken.None
                    )
                )
                .Returns(
                    new ValueTask<PagedResult<TriggerHeader>>(
                        new PagedResult<TriggerHeader>(
                            [
                                CreateTriggerHeader(
                                    triggerKey,
                                    jobKey,
                                    key == cron ? "CRON" : "SIMPLE",
                                    key == paused ? TriggerState.Paused : TriggerState.Blocked
                                ),
                            ],
                            false
                        )
                    )
                )
                .Verifiable(Times.Once());
        }
        scheduler
            .Setup(x => x.GetJobDetail(DownloadJob.GetJobKey(queued.Id), CancellationToken.None))
            .ReturnsAsync(CreateJobDetail(queued))
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.GetJobDetail(DownloadJob.GetJobKey(missingPayload.Id), CancellationToken.None))
            .ReturnsAsync(CreateEmptyJobDetail(DownloadJob.GetJobKey(missingPayload.Id)))
            .Verifiable(Times.Once());

        // Act
        var keys = await Sut.GetCurrentlyDownloadingKeysByServer(queued.PlexServerId);

        // Assert
        keys.ShouldHaveSingleItem();
        keys.Single().ShouldBeEquivalentTo(queued);
        scheduler.Verify();
        foreach (var excluded in new[] { paused, cron, disappeared })
            scheduler.Verify(
                x => x.GetJobDetail(DownloadJob.GetJobKey(excluded.Id), CancellationToken.None),
                Times.Never()
            );
        scheduler.Verify(
            x =>
                x.QueryTriggers(
                    It.Is<TriggerQuery>(query => IsTriggerQuery(query, DownloadJob.GetJobKey(disappeared.Id), 0)),
                    CancellationToken.None
                ),
            Times.Never()
        );
    }

    private static DownloadTaskKey CreateKey(int id, int serverId = 1) =>
        new()
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse($"00000000-0000-0000-0000-{id:D12}"),
            PlexServerId = serverId,
            PlexLibraryId = serverId,
        };

    private static bool IsRunningQuery(FireInstanceQuery query, int skip) =>
        query.SchedulerInstanceId == SCHEDULER_INSTANCE_ID
        && query.State == FireInstanceState.Executing
        && query.Skip == skip
        && query.Take == PagedQuery.DefaultTake;

    private static bool IsJobQuery(JobQuery query, int skip) =>
        query.Group?.CompareToValue == nameof(JobTypes.DownloadJob)
        && query.Skip == skip
        && query.Take == PagedQuery.DefaultTake;

    private static bool IsTriggerQuery(TriggerQuery query, JobKey jobKey, int skip) =>
        query.Job == jobKey && query.Skip == skip && query.Take == PagedQuery.DefaultTake;

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

    private static IJobDetail CreateJobDetail(DownloadTaskKey key) =>
        JobBuilder
            .Create<DownloadJob>()
            .WithIdentity(DownloadJob.GetJobKey(key.Id))
            .UsingJobData(new DownloadJobPayload(key).ToJobDataMap())
            .Build();

    private static IJobDetail CreateEmptyJobDetail(JobKey jobKey) =>
        JobBuilder.Create<DownloadJob>().WithIdentity(jobKey).Build();

    private static JobHeader CreateJobHeader(JobKey jobKey) =>
        new(jobKey, null, typeof(DownloadJob).FullName!, false, true, false, true);

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
}
