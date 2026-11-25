using Quartz;

namespace Reaparr.Application.UnitTests;

public class MoveDownloadFileJobSchedulerSnapshotUnitTests : BaseUnitTest<MoveDownloadFileJobScheduler>
{
    [Test]
    public async Task ShouldReuseExecutingPayloadsWithoutReadingIndividualStoredJobs()
    {
        // Arrange
        var target = CreateKey(1);
        var otherServer = CreateKey(2, 2);
        var targetJobKey = MoveDownloadFileJob.GetJobKey(target.Id);
        var otherJobKey = MoveDownloadFileJob.GetJobKey(otherServer.Id);
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns("snapshot-node");
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == "snapshot-node"
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([CreateFiring(targetJobKey), CreateFiring(otherJobKey)], false))
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
            .ReturnsAsync([CreateJob(target), CreateJob(otherServer)])
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.QueryJobs(It.Is<JobQuery>(q => q.Skip == 0), CancellationToken.None))
            .ReturnsAsync(new PagedResult<JobHeader>([CreateHeader(targetJobKey), CreateHeader(otherJobKey)], false))
            .Verifiable(Times.Once());

        // Act
        var keys = await Sut.GetCurrentlyMovingKeysByServer(target.PlexServerId);

        // Assert
        keys.ShouldBe([target]);
        scheduler.Verify();
        scheduler.Verify(x => x.GetJobDetail(It.IsAny<JobKey>(), CancellationToken.None), Times.Never());
        scheduler.Verify(x => x.QueryTriggers(It.IsAny<TriggerQuery>(), CancellationToken.None), Times.Never());
    }

    [Test]
    public async Task ShouldUseQueuedPayload_WhenAnUnrelatedJobIsRunning()
    {
        // Arrange
        var target = CreateKey(3);
        var key = MoveDownloadFileJob.GetJobKey(target.Id);
        var unrelated = new JobKey("other", nameof(JobTypes.DownloadJob));
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns("snapshot-node");
        scheduler
            .Setup(x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), CancellationToken.None))
            .ReturnsAsync(new PagedResult<FireInstance>([CreateFiring(unrelated)], false))
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.GetJobDetails(It.Is<IReadOnlyCollection<JobKey>>(keys => keys.Count == 0), CancellationToken.None)
            )
            .ReturnsAsync([])
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.QueryJobs(It.IsAny<JobQuery>(), CancellationToken.None))
            .ReturnsAsync(new PagedResult<JobHeader>([CreateHeader(key)], false))
            .Verifiable(Times.Once());
        scheduler.Setup(x => x.Exists(key, CancellationToken.None)).ReturnsAsync(true).Verifiable(Times.Once());
        scheduler
            .Setup(x => x.QueryTriggers(It.Is<TriggerQuery>(q => q.Job == key), CancellationToken.None))
            .ReturnsAsync(new PagedResult<TriggerHeader>([CreateTrigger(key, "SIMPLE", TriggerState.Normal)], false))
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.GetJobDetail(key, CancellationToken.None))
            .ReturnsAsync(CreateJob(target))
            .Verifiable(Times.Once());

        // Act
        var keys = await Sut.GetCurrentlyMovingKeysByServer(target.PlexServerId);

        // Assert
        keys.ShouldBe([target]);
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldExcludePausedCronAndDisappearedJobs_WhenBuildingSnapshot()
    {
        // Arrange
        var queued = CreateKey(4);
        var paused = CreateKey(5);
        var cron = CreateKey(6);
        var disappeared = CreateKey(7);
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns("snapshot-node");
        scheduler
            .Setup(x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), CancellationToken.None))
            .ReturnsAsync(new PagedResult<FireInstance>([], false))
            .Verifiable(Times.Exactly(4));
        scheduler
            .Setup(x =>
                x.GetJobDetails(It.Is<IReadOnlyCollection<JobKey>>(keys => keys.Count == 0), CancellationToken.None)
            )
            .ReturnsAsync([])
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.QueryJobs(It.IsAny<JobQuery>(), CancellationToken.None))
            .ReturnsAsync(
                new PagedResult<JobHeader>(
                    [
                        CreateHeader(MoveDownloadFileJob.GetJobKey(queued.Id)),
                        CreateHeader(MoveDownloadFileJob.GetJobKey(paused.Id)),
                        CreateHeader(MoveDownloadFileJob.GetJobKey(cron.Id)),
                        CreateHeader(MoveDownloadFileJob.GetJobKey(disappeared.Id)),
                    ],
                    false
                )
            )
            .Verifiable(Times.Once());
        foreach (var item in new[] { queued, paused, cron, disappeared })
        {
            var key = MoveDownloadFileJob.GetJobKey(item.Id);
            scheduler
                .Setup(x => x.Exists(key, CancellationToken.None))
                .ReturnsAsync(item != disappeared)
                .Verifiable(Times.Once());
            if (item == disappeared)
                continue;
            scheduler
                .Setup(x => x.QueryTriggers(It.Is<TriggerQuery>(q => q.Job == key), CancellationToken.None))
                .ReturnsAsync(
                    new PagedResult<TriggerHeader>(
                        [
                            CreateTrigger(
                                key,
                                item == cron ? "CRON" : "SIMPLE",
                                item == paused ? TriggerState.Paused : TriggerState.Normal
                            ),
                        ],
                        false
                    )
                )
                .Verifiable(Times.Once());
        }
        scheduler
            .Setup(x => x.GetJobDetail(MoveDownloadFileJob.GetJobKey(queued.Id), CancellationToken.None))
            .ReturnsAsync(CreateJob(queued))
            .Verifiable(Times.Once());

        // Act
        var keys = await Sut.GetCurrentlyMovingKeysByServer(queued.PlexServerId);

        // Assert
        keys.ShouldBe([queued]);
        scheduler.Verify();
        foreach (var excluded in new[] { paused, cron, disappeared })
            scheduler.Verify(
                x => x.GetJobDetail(MoveDownloadFileJob.GetJobKey(excluded.Id), CancellationToken.None),
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

    private static IJobDetail CreateJob(DownloadTaskKey key) =>
        JobBuilder
            .Create<MoveDownloadFileJob>()
            .WithIdentity(MoveDownloadFileJob.GetJobKey(key.Id))
            .UsingJobData(new MoveDownloadFileJobPayload(key).ToJobDataMap())
            .Build();

    private static FireInstance CreateFiring(JobKey key) =>
        new(
            $"fire-{key.Name}",
            new TriggerKey(key.Name, key.Group),
            key,
            "snapshot-node",
            FireInstanceState.Executing,
            DateTimeOffset.UnixEpoch,
            null,
            null
        );

    private static JobHeader CreateHeader(JobKey key) =>
        new(key, null, typeof(MoveDownloadFileJob).FullName!, false, true, false, true);

    private static TriggerHeader CreateTrigger(JobKey key, string type, TriggerState state) =>
        new(
            new TriggerKey(key.Name, key.Group),
            key,
            null,
            type,
            state,
            DateTimeOffset.UnixEpoch,
            null,
            null,
            null,
            null,
            5,
            null,
            null,
            0
        );
}
