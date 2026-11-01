using Quartz;

namespace Reaparr.Application.UnitTests;

public class DownloadTaskSchedulerSnapshotUnitTests : BaseUnitTest<DownloadTaskScheduler>
{
    [Test]
    public async Task ShouldReuseExecutingPayloadsWithoutReadingTheirStoredJobs()
    {
        var target = CreateKey(1);
        var otherServer = CreateKey(2, 2);
        var unrelated = CreateKey(3);
        var targetContext = CreateContext(target);
        var unrelatedContext = CreateContext(unrelated, group: "OtherJob");
        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .Setup(x => x.GetCurrentlyExecutingJobs(CancellationToken.None))
            .ReturnsAsync([targetContext, CreateContext(otherServer), targetContext, unrelatedContext])
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.GetJobKeys(It.IsAny<Quartz.Impl.Matchers.GroupMatcher<JobKey>>(), CancellationToken.None))
            .ReturnsAsync([DownloadJob.GetJobKey(target.Id), DownloadJob.GetJobKey(otherServer.Id)])
            .Verifiable(Times.Once());

        var keys = await Sut.GetCurrentlyDownloadingKeysByServer(target.PlexServerId);

        keys.ShouldHaveSingleItem();
        keys.Single().ShouldBeEquivalentTo(target);
        scheduler.Verify();
        scheduler.Verify(x => x.CheckExists(It.IsAny<JobKey>(), CancellationToken.None), Times.Never());
        scheduler.Verify(x => x.GetTriggersOfJob(It.IsAny<JobKey>(), CancellationToken.None), Times.Never());
        scheduler.Verify(x => x.GetJobDetail(It.IsAny<JobKey>(), CancellationToken.None), Times.Never());
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldRetainStoredPayloadFallbackWhenTheExecutionSnapshotCannotSupplyIt(bool wasAlreadyExecuting)
    {
        var target = CreateKey(4);
        var jobKey = DownloadJob.GetJobKey(target.Id);
        var context = CreateContext(target, includePayload: false);
        IReadOnlyCollection<IJobExecutionContext> initialContexts = wasAlreadyExecuting ? [context] : [];
        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .SetupSequence(x => x.GetCurrentlyExecutingJobs(CancellationToken.None))
            .ReturnsAsync(initialContexts)
            .ReturnsAsync([context]);
        scheduler
            .Setup(x => x.GetJobKeys(It.IsAny<Quartz.Impl.Matchers.GroupMatcher<JobKey>>(), CancellationToken.None))
            .ReturnsAsync([jobKey]);
        scheduler.Setup(x => x.CheckExists(jobKey, CancellationToken.None)).ReturnsAsync(true);
        scheduler.Setup(x => x.GetTriggersOfJob(jobKey, CancellationToken.None)).ReturnsAsync([]);
        scheduler
            .Setup(x => x.GetJobDetail(jobKey, CancellationToken.None))
            .ReturnsAsync(context.JobDetail)
            .Verifiable(Times.Once());

        var keys = await Sut.GetCurrentlyDownloadingKeysByServer(target.PlexServerId);

        keys.ShouldHaveSingleItem();
        keys.Single().ShouldBeEquivalentTo(target);
        scheduler.Verify();
        scheduler.Verify(x => x.GetCurrentlyExecutingJobs(CancellationToken.None), Times.Exactly(2));
    }

    [Test]
    public async Task ShouldIncludeBlockedJobsButExcludePausedCronDisappearedAndMissingPayloadJobs()
    {
        var queued = CreateKey(5);
        var paused = CreateKey(6);
        var cron = CreateKey(7);
        var disappeared = CreateKey(8);
        var missingPayload = CreateKey(9);
        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .Setup(x => x.GetCurrentlyExecutingJobs(CancellationToken.None))
            .ReturnsAsync([])
            .Verifiable(Times.Exactly(4));
        scheduler
            .Setup(x => x.GetJobKeys(It.IsAny<Quartz.Impl.Matchers.GroupMatcher<JobKey>>(), CancellationToken.None))
            .ReturnsAsync([
                DownloadJob.GetJobKey(queued.Id),
                DownloadJob.GetJobKey(paused.Id),
                DownloadJob.GetJobKey(cron.Id),
                DownloadJob.GetJobKey(disappeared.Id),
                DownloadJob.GetJobKey(missingPayload.Id),
            ]);
        foreach (var key in new[] { queued, paused, cron, disappeared, missingPayload })
        {
            var jobKey = DownloadJob.GetJobKey(key.Id);
            scheduler.Setup(x => x.CheckExists(jobKey, CancellationToken.None)).ReturnsAsync(key != disappeared);
            if (key == disappeared)
                continue;

            var trigger = new Mock<ITrigger>();
            if (key == cron)
                trigger.As<ICronTrigger>();
            var triggerKey = new TriggerKey(jobKey.Name, jobKey.Group);
            trigger.SetupGet(x => x.Key).Returns(triggerKey);
            scheduler.Setup(x => x.GetTriggersOfJob(jobKey, CancellationToken.None)).ReturnsAsync([trigger.Object]);
            scheduler
                .Setup(x => x.GetTriggerState(triggerKey, CancellationToken.None))
                .ReturnsAsync(key == paused ? TriggerState.Paused : TriggerState.Blocked);
        }
        scheduler
            .Setup(x => x.GetJobDetail(DownloadJob.GetJobKey(queued.Id), CancellationToken.None))
            .ReturnsAsync(CreateContext(queued).JobDetail)
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.GetJobDetail(DownloadJob.GetJobKey(missingPayload.Id), CancellationToken.None))
            .ReturnsAsync(JobBuilder.Create<DownloadJob>().WithIdentity(DownloadJob.GetJobKey(missingPayload.Id)).Build())
            .Verifiable(Times.Once());

        var keys = await Sut.GetCurrentlyDownloadingKeysByServer(queued.PlexServerId);

        keys.ShouldHaveSingleItem();
        keys.Single().ShouldBeEquivalentTo(queued);
        scheduler.Verify();
        foreach (var excluded in new[] { paused, cron, disappeared })
            scheduler.Verify(x => x.GetJobDetail(DownloadJob.GetJobKey(excluded.Id), CancellationToken.None), Times.Never());
        scheduler.Verify(x => x.GetTriggersOfJob(DownloadJob.GetJobKey(disappeared.Id), CancellationToken.None), Times.Never());
        var cronJobKey = DownloadJob.GetJobKey(cron.Id);
        scheduler.Verify(x => x.GetTriggerState(new TriggerKey(cronJobKey.Name, cronJobKey.Group), CancellationToken.None), Times.Never());
    }

    private static DownloadTaskKey CreateKey(int id, int serverId = 1) =>
        new()
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.Parse($"00000000-0000-0000-0000-{id:D12}"),
            PlexServerId = serverId,
            PlexLibraryId = serverId,
        };

    private static IJobExecutionContext CreateContext(DownloadTaskKey key, bool includePayload = true, string? group = null)
    {
        var payload = new DownloadJobPayload(key).ToJobDataMap();
        var jobKey = group is null ? DownloadJob.GetJobKey(key.Id) : new JobKey(key.Id.ToString(), group);
        var detail = JobBuilder.Create<DownloadJob>().WithIdentity(jobKey).UsingJobData(payload).Build();
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(detail);
        context.SetupGet(x => x.MergedJobDataMap).Returns(includePayload ? payload : new JobDataMap());
        return context.Object;
    }
}
