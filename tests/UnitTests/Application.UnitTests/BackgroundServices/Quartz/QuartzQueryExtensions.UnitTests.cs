using Quartz;

namespace Reaparr.Application.UnitTests;

public class QuartzQueryExtensionsUnitTests : BaseUnitTest
{
    private const string LocalNode = "query-test-node";

    [Test]
    [Arguments(false, TriggerState.Normal, true)]
    [Arguments(false, TriggerState.Blocked, true)]
    [Arguments(false, TriggerState.Paused, false)]
    [Arguments(false, TriggerState.Complete, false)]
    [Arguments(false, TriggerState.Error, false)]
    [Arguments(false, TriggerState.None, false)]
    [Arguments(true, TriggerState.Normal, true)]
    [Arguments(true, TriggerState.Blocked, true)]
    [Arguments(true, TriggerState.Paused, true)]
    [Arguments(true, TriggerState.Complete, false)]
    [Arguments(true, TriggerState.Error, false)]
    [Arguments(true, TriggerState.None, false)]
    public async Task ShouldClassifySimpleTriggerState_WhenCheckingQueueOrCancellation(
        bool cancellable,
        TriggerState state,
        bool expected
    )
    {
        // Arrange
        var key = new JobKey("target", nameof(JobTypes.DownloadJob));
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.Exists(key, CancellationToken)).ReturnsAsync(true).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryTriggers(
                    It.Is<TriggerQuery>(q => q.Job == key && q.Skip == 0 && q.Take == PagedQuery.DefaultTake),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<TriggerHeader>([CreateTrigger(key, "SIMPLE", state)], false))
            .Verifiable(Times.Once());

        // Act
        var result = cancellable
            ? await scheduler.Object.IsCancellable(key, CancellationToken)
            : await scheduler.Object.IsQueued(key, CancellationToken);

        // Assert
        result.ShouldBe(expected);
        scheduler.Verify();
    }

    [Test]
    [Arguments(false, TriggerState.Normal)]
    [Arguments(false, TriggerState.Blocked)]
    [Arguments(false, TriggerState.Paused)]
    [Arguments(true, TriggerState.Normal)]
    [Arguments(true, TriggerState.Blocked)]
    [Arguments(true, TriggerState.Paused)]
    public async Task ShouldExcludeCronTriggers_WhenCheckingQueueOrCancellation(bool cancellable, TriggerState state)
    {
        // Arrange
        var key = new JobKey("recurring", nameof(JobTypes.LibrarySyncJob));
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.Exists(key, CancellationToken)).ReturnsAsync(true).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryTriggers(
                    It.Is<TriggerQuery>(q => q.Job == key && q.Skip == 0 && q.Take == PagedQuery.DefaultTake),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<TriggerHeader>([CreateTrigger(key, "CRON", state)], false))
            .Verifiable(Times.Once());

        // Act
        var result = cancellable
            ? await scheduler.Object.IsCancellable(key, CancellationToken)
            : await scheduler.Object.IsQueued(key, CancellationToken);

        // Assert
        result.ShouldBeFalse();
        scheduler.Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldSkipTriggerQuery_WhenJobDoesNotExist(bool cancellable)
    {
        // Arrange
        var key = new JobKey("absent", nameof(JobTypes.DownloadJob));
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.Exists(key, CancellationToken)).ReturnsAsync(false).Verifiable(Times.Once());

        // Act
        var result = cancellable
            ? await scheduler.Object.IsCancellable(key, CancellationToken)
            : await scheduler.Object.IsQueued(key, CancellationToken);

        // Assert
        result.ShouldBeFalse();
        scheduler.Verify();
        scheduler.Verify(x => x.QueryTriggers(It.IsAny<TriggerQuery>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldAdvanceByReturnedTriggerCount_WhenMatchIsOnLaterPage(bool cancellable)
    {
        // Arrange
        var key = new JobKey("paged", nameof(JobTypes.DownloadJob));
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.Exists(key, CancellationToken)).ReturnsAsync(true).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryTriggers(
                    It.Is<TriggerQuery>(q => q.Job == key && q.Skip == 0 && q.Take == PagedQuery.DefaultTake),
                    CancellationToken
                )
            )
            .ReturnsAsync(
                new PagedResult<TriggerHeader>(
                    [
                        CreateTrigger(key, "CRON", TriggerState.Normal),
                        CreateTrigger(key, "SIMPLE", TriggerState.Complete),
                    ],
                    true
                )
            )
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryTriggers(
                    It.Is<TriggerQuery>(q => q.Job == key && q.Skip == 2 && q.Take == PagedQuery.DefaultTake),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<TriggerHeader>([CreateTrigger(key, "SIMPLE", TriggerState.Blocked)], true))
            .Verifiable(Times.Once());

        // Act
        var result = cancellable
            ? await scheduler.Object.IsCancellable(key, CancellationToken)
            : await scheduler.Object.IsQueued(key, CancellationToken);

        // Assert
        result.ShouldBeTrue();
        scheduler.Verify();
        scheduler.Verify(x => x.QueryTriggers(It.IsAny<TriggerQuery>(), CancellationToken), Times.Exactly(2));
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ShouldReturnFalseOrRejectEmptyTriggerPage_WhenContinuationFlagChanges(
        bool cancellable,
        bool hasMore
    )
    {
        // Arrange
        var key = new JobKey("empty", nameof(JobTypes.DownloadJob));
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.Exists(key, CancellationToken)).ReturnsAsync(true).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryTriggers(
                    It.Is<TriggerQuery>(q => q.Job == key && q.Skip == 0 && q.Take == PagedQuery.DefaultTake),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<TriggerHeader>([], hasMore))
            .Verifiable(Times.Once());

        // Act
        var query = cancellable
            ? scheduler.Object.IsCancellable(key, CancellationToken)
            : scheduler.Object.IsQueued(key, CancellationToken);

        // Assert
        if (hasMore)
        {
            var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await query);
            exception.Message.ShouldBe("Quartz returned an empty trigger page while reporting more results");
        }
        else
            (await query).ShouldBeFalse();
        scheduler.Verify();
    }

    [Test]
    [Arguments(0, false)]
    [Arguments(1, false)]
    [Arguments(2, false)]
    [Arguments(3, true)]
    public async Task ShouldMatchExactJobKey_WhenInspectingLocalExecutingFirings(int scenario, bool expected)
    {
        // Arrange
        var key = new JobKey("target", nameof(JobTypes.DownloadJob));
        var firingKey = scenario switch
        {
            0 => null,
            1 => new JobKey("other", key.Group),
            2 => new JobKey(key.Name, nameof(JobTypes.LibrarySyncJob)),
            _ => new JobKey(key.Name, key.Group),
        };
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(LocalNode).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == LocalNode
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([CreateFire(firingKey, "firing")], false))
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.IsJobExecuting(key, CancellationToken);

        // Assert
        result.ShouldBe(expected);
        scheduler.Verify();
    }

    [Test]
    [Arguments(true, false, true)]
    [Arguments(false, true, true)]
    [Arguments(false, false, false)]
    public async Task ShouldShortCircuitQueuedJobsOrConsultExecution_WhenCheckingScheduledOrExecuting(
        bool queued,
        bool executing,
        bool expected
    )
    {
        // Arrange
        var key = new JobKey("target", nameof(JobTypes.DownloadJob));
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.Exists(key, CancellationToken)).ReturnsAsync(queued).Verifiable(Times.Once());
        if (queued)
        {
            scheduler
                .Setup(x =>
                    x.QueryTriggers(
                        It.Is<TriggerQuery>(q => q.Job == key && q.Skip == 0 && q.Take == PagedQuery.DefaultTake),
                        CancellationToken
                    )
                )
                .ReturnsAsync(
                    new PagedResult<TriggerHeader>([CreateTrigger(key, "SIMPLE", TriggerState.Normal)], false)
                )
                .Verifiable(Times.Once());
        }
        else
        {
            scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(LocalNode).Verifiable(Times.Once());
            scheduler
                .Setup(x =>
                    x.QueryFireInstances(
                        It.Is<FireInstanceQuery>(q =>
                            q.SchedulerInstanceId == LocalNode
                            && q.State == FireInstanceState.Executing
                            && q.Skip == 0
                            && q.Take == PagedQuery.DefaultTake
                        ),
                        CancellationToken
                    )
                )
                .ReturnsAsync(new PagedResult<FireInstance>(executing ? [CreateFire(key, "running")] : [], false))
                .Verifiable(Times.Once());
        }

        // Act
        var result = await scheduler.Object.IsJobScheduledOrExecuting(key, CancellationToken);

        // Assert
        result.ShouldBe(expected);
        scheduler.Verify();
        if (queued)
            scheduler.Verify(
                x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        else
            scheduler.Verify(
                x => x.QueryTriggers(It.IsAny<TriggerQuery>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldReturnEveryJobKeyInOrder_WhenGroupQuerySpansPages()
    {
        // Arrange
        var first = new JobKey("first", nameof(JobTypes.LibrarySyncJob));
        var second = new JobKey("second", first.Group);
        var third = new JobKey("third", first.Group);
        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .Setup(x =>
                x.QueryJobs(
                    It.Is<JobQuery>(q =>
                        q.Group != null
                        && q.Group.IsMatch(first)
                        && !q.Group.IsMatch(new JobKey(first.Name, nameof(JobTypes.DownloadJob)))
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<JobHeader>([CreateHeader(first), CreateHeader(second)], true))
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryJobs(
                    It.Is<JobQuery>(q =>
                        q.Group != null
                        && q.Group.IsMatch(third)
                        && !q.Group.IsMatch(new JobKey(third.Name, nameof(JobTypes.DownloadJob)))
                        && q.Skip == 2
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<JobHeader>([CreateHeader(third)], false))
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.GetJobKeys(JobTypes.LibrarySyncJob, CancellationToken);

        // Assert
        result.ShouldBe([first, second, third]);
        scheduler.Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReturnEmptyKeysOrRejectEmptyJobPage_WhenGroupQueryContinuationFlagChanges(bool hasMore)
    {
        // Arrange
        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .Setup(x =>
                x.QueryJobs(
                    It.Is<JobQuery>(q =>
                        q.Group != null
                        && q.Group.CompareToValue == nameof(JobTypes.LibrarySyncJob)
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<JobHeader>([], hasMore))
            .Verifiable(Times.Once());

        // Act
        var query = scheduler.Object.GetJobKeys(JobTypes.LibrarySyncJob, CancellationToken);

        // Assert
        if (hasMore)
        {
            var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await query);
            exception.Message.ShouldBe("Quartz returned an empty job page while reporting more results");
        }
        else
            (await query).ShouldBeEmpty();
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldReturnAllLocalExecutingFiringsInOrder_WhenQuerySpansPages()
    {
        // Arrange
        var key = new JobKey("target", nameof(JobTypes.DownloadJob));
        var first = CreateFire(key, "first");
        var second = CreateFire(null, "second");
        var third = CreateFire(key, "third");
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(LocalNode).Verifiable(Times.Exactly(2));
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == LocalNode
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([first, second], true))
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == LocalNode
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 2
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([third], false))
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.GetLocalExecutingFireInstances(CancellationToken);

        // Assert
        result.ShouldBe([first, second, third]);
        scheduler.Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReturnEmptyFiringsOrRejectEmptyPage_WhenExecutionQueryContinuationFlagChanges(bool hasMore)
    {
        // Arrange
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(LocalNode).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == LocalNode
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([], hasMore))
            .Verifiable(Times.Once());

        // Act
        var query = scheduler.Object.GetLocalExecutingFireInstances(CancellationToken);

        // Assert
        if (hasMore)
        {
            var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await query);
            exception.Message.ShouldBe("Quartz returned an empty firing page while reporting more results");
        }
        else
            (await query).ShouldBeEmpty();
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldAvoidSchedulerQueries_WhenNoActiveJobKeysAreRequested()
    {
        // Arrange
        var scheduler = Mock.Mock<IScheduler>();

        // Act
        var result = await scheduler.Object.HasActiveJobs([], CancellationToken);

        // Assert
        result.ShouldBeFalse();
        scheduler.Verify(
            x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
        scheduler.Verify(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()), Times.Never());
        scheduler.VerifyGet(x => x.SchedulerInstanceId, Times.Never());
    }

    [Test]
    public async Task ShouldSkipStoredJobs_WhenRequestedKeyIsExecutingLocally()
    {
        // Arrange
        var key = new JobKey("target", nameof(JobTypes.DownloadJob));
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(LocalNode).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == LocalNode
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(
                new PagedResult<FireInstance>(
                    [
                        CreateFire(null, "null-key"),
                        CreateFire(new JobKey("other", key.Group), "unrelated"),
                        CreateFire(key, "target"),
                    ],
                    false
                )
            )
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.HasActiveJobs([key, key], CancellationToken);

        // Assert
        result.ShouldBeTrue();
        scheduler.Verify();
        scheduler.Verify(x => x.QueryJobs(It.IsAny<JobQuery>(), It.IsAny<CancellationToken>()), Times.Never());
        scheduler.Verify(x => x.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldSearchAllStoredJobGroupsAcrossPages_WhenRequestedKeyIsNotExecuting(bool stored)
    {
        // Arrange
        var key = new JobKey("target", nameof(JobTypes.DownloadJob));
        var sameNameOtherGroup = new JobKey(key.Name, nameof(JobTypes.LibrarySyncJob));
        var other = new JobKey("other", key.Group);
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(LocalNode).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == LocalNode
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(
                new PagedResult<FireInstance>([CreateFire(null, "null-key"), CreateFire(other, "unrelated")], false)
            )
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryJobs(
                    It.Is<JobQuery>(q => q.Group == null && q.Skip == 0 && q.Take == PagedQuery.DefaultTake),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<JobHeader>([CreateHeader(sameNameOtherGroup), CreateHeader(other)], true))
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryJobs(
                    It.Is<JobQuery>(q => q.Group == null && q.Skip == 2 && q.Take == PagedQuery.DefaultTake),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<JobHeader>([CreateHeader(stored ? key : other)], stored))
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.HasActiveJobs([key, key], CancellationToken);

        // Assert
        result.ShouldBe(stored);
        scheduler.Verify();
        scheduler.Verify(x => x.QueryJobs(It.IsAny<JobQuery>(), CancellationToken), Times.Exactly(2));
        scheduler.Verify(x => x.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReturnInactiveOrRejectEmptyJobPage_WhenActiveJobQueryContinuationFlagChanges(bool hasMore)
    {
        // Arrange
        var key = new JobKey("target", nameof(JobTypes.DownloadJob));
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(LocalNode).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == LocalNode
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([], false))
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryJobs(
                    It.Is<JobQuery>(q => q.Group == null && q.Skip == 0 && q.Take == PagedQuery.DefaultTake),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<JobHeader>([], hasMore))
            .Verifiable(Times.Once());

        // Act
        var query = scheduler.Object.HasActiveJobs([key], CancellationToken);

        // Assert
        if (hasMore)
        {
            var exception = await Should.ThrowAsync<InvalidOperationException>(async () => await query);
            exception.Message.ShouldBe("Quartz returned an empty job page while reporting more results");
        }
        else
            (await query).ShouldBeFalse();
        scheduler.Verify();
    }

    private static TriggerHeader CreateTrigger(JobKey key, string type, TriggerState state) =>
        new(
            new TriggerKey($"{type}-{state}", key.Group),
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

    private static FireInstance CreateFire(JobKey? key, string id) =>
        new(
            id,
            new TriggerKey(id, "test"),
            key!,
            LocalNode,
            FireInstanceState.Executing,
            DateTimeOffset.UnixEpoch,
            null,
            null
        );

    private static JobHeader CreateHeader(JobKey key) =>
        new(key, null, typeof(DownloadJob).FullName!, false, true, false, true);
}
