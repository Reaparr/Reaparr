using System.Diagnostics;
using Quartz;

namespace Reaparr.Application.UnitTests;

public class QuartzCancellationExtensionsUnitTests : BaseUnitTest
{
    private const string SCHEDULER_NODE = "cancellation-test-node";

    [Test]
    public async Task ShouldFailWithoutDeletingOrInterrupting_WhenTheRequestedJobIsNotScheduled()
    {
        // Arrange
        var key = new JobKey("requested", nameof(JobTypes.LibrarySyncJob));
        var unrelatedKey = new JobKey(key.Name, nameof(JobTypes.DownloadJob));
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([CreateFire(unrelatedKey)], false))
            .Verifiable(Times.Once());
        scheduler.Setup(x => x.Exists(key, CancellationToken)).ReturnsAsync(false).Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.CancelJob(key, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().Message.ShouldContain("not scheduled");
        scheduler.Verify();
        scheduler.Verify(x => x.QueryTriggers(It.IsAny<TriggerQuery>(), It.IsAny<CancellationToken>()), Times.Never());
        scheduler.Verify(x => x.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        scheduler.Verify(x => x.Interrupt(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(true, false)]
    [Arguments(false, false)]
    [Arguments(false, true)]
    public async Task ShouldVerifyQueuedDeletion_WhenQuartzReportsWhetherTheJobWasDeleted(
        bool deleted,
        bool stillCancellable
    )
    {
        // Arrange
        var key = new JobKey("queued", nameof(JobTypes.LibrarySyncJob));
        var existenceChecks = 0;
        var triggerQueries = 0;
        var expectedSucceeded = deleted || !stillCancellable;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .SetupGet(x => x.SchedulerInstanceId)
            .Returns(SCHEDULER_NODE)
            .Verifiable(Times.Exactly(expectedSucceeded ? 2 : 1));
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([], false))
            .Verifiable(Times.Exactly(expectedSucceeded ? 2 : 1));
        scheduler
            .Setup(x => x.Exists(key, CancellationToken))
            .ReturnsAsync(() => ++existenceChecks == 1 || stillCancellable)
            .Verifiable(Times.Exactly(deleted ? 1 : 2));
        scheduler
            .Setup(x =>
                x.QueryTriggers(
                    It.Is<TriggerQuery>(q => q.Job == key && q.Skip == 0 && q.Take == PagedQuery.DefaultTake),
                    CancellationToken
                )
            )
            .Callback(() => triggerQueries++)
            .ReturnsAsync(new PagedResult<TriggerHeader>([CreateTrigger(key)], false))
            .Verifiable(Times.Exactly(!deleted && stillCancellable ? 2 : 1));
        scheduler.Setup(x => x.DeleteJob(key, CancellationToken)).ReturnsAsync(deleted).Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.CancelJob(key, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBe(expectedSucceeded);
        result.IsFailed.ShouldBe(!expectedSucceeded);
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(expectedSucceeded ? 0 : 1);
        if (!expectedSucceeded)
            result.Errors.Single().Message.ShouldContain("Failed to cancel queued Quartz job");
        existenceChecks.ShouldBe(deleted ? 1 : 2);
        triggerQueries.ShouldBe(!deleted && stillCancellable ? 2 : 1);
        scheduler.Verify();
        scheduler.Verify(x => x.Interrupt(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldAcceptAFalseDelete_WhenTheJobStillExistsButItsCancellableTriggerDisappeared()
    {
        // Arrange
        var key = new JobKey("completed-trigger", nameof(JobTypes.LibrarySyncJob));
        var triggerQueries = 0;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Exactly(2));
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([], false))
            .Verifiable(Times.Exactly(2));
        scheduler.Setup(x => x.Exists(key, CancellationToken)).ReturnsAsync(true).Verifiable(Times.Exactly(2));
        scheduler
            .Setup(x =>
                x.QueryTriggers(
                    It.Is<TriggerQuery>(q => q.Job == key && q.Skip == 0 && q.Take == PagedQuery.DefaultTake),
                    CancellationToken
                )
            )
            .ReturnsAsync(() =>
                new PagedResult<TriggerHeader>(++triggerQueries == 1 ? [CreateTrigger(key)] : [], false)
            )
            .Verifiable(Times.Exactly(2));
        scheduler.Setup(x => x.DeleteJob(key, CancellationToken)).ReturnsAsync(false).Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.CancelJob(key, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(0);
        triggerQueries.ShouldBe(2);
        scheduler.Verify();
        scheduler.Verify(x => x.Interrupt(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldInterruptTheExactJob_WhenItStartsRunningDuringQueuedDeletion()
    {
        // Arrange
        var key = new JobKey("started-during-delete", nameof(JobTypes.LibrarySyncJob));
        var executionQueries = 0;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Exactly(2));
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(() => new PagedResult<FireInstance>(++executionQueries == 1 ? [] : [CreateFire(key)], false))
            .Verifiable(Times.Exactly(2));
        scheduler.Setup(x => x.Exists(key, CancellationToken)).ReturnsAsync(true).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryTriggers(
                    It.Is<TriggerQuery>(q => q.Job == key && q.Skip == 0 && q.Take == PagedQuery.DefaultTake),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<TriggerHeader>([CreateTrigger(key)], false))
            .Verifiable(Times.Once());
        scheduler.Setup(x => x.DeleteJob(key, CancellationToken)).ReturnsAsync(true).Verifiable(Times.Once());
        scheduler.Setup(x => x.Interrupt(key, CancellationToken)).ReturnsAsync(true).Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.CancelJob(key, CancellationToken, waitForCompletion: false);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(0);
        executionQueries.ShouldBe(2);
        scheduler.Verify();
        scheduler.Verify(
            x =>
                x.QueryFireInstances(
                    It.IsAny<FireInstanceQuery>(),
                    It.Is<CancellationToken>(token => token != CancellationToken)
                ),
            Times.Never()
        );
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ShouldDeleteCancellableTriggersAndWaitOnlyWhenRequested_WhenInterruptingARunningJob(
        bool isCancellable,
        bool waitForCompletion
    )
    {
        // Arrange
        var key = new JobKey("running", nameof(JobTypes.LibrarySyncJob));
        using var caller = new CancellationTokenSource();
        var callerToken = caller.Token;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .SetupGet(x => x.SchedulerInstanceId)
            .Returns(SCHEDULER_NODE)
            .Verifiable(Times.Exactly(waitForCompletion ? 2 : 1));
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    callerToken
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([CreateFire(key)], false))
            .Verifiable(Times.Once());
        scheduler.Setup(x => x.Exists(key, callerToken)).ReturnsAsync(isCancellable).Verifiable(Times.Once());
        if (isCancellable)
        {
            scheduler
                .Setup(x =>
                    x.QueryTriggers(
                        It.Is<TriggerQuery>(q => q.Job == key && q.Skip == 0 && q.Take == PagedQuery.DefaultTake),
                        callerToken
                    )
                )
                .ReturnsAsync(new PagedResult<TriggerHeader>([CreateTrigger(key)], false))
                .Verifiable(Times.Once());
            scheduler.Setup(x => x.DeleteJob(key, callerToken)).ReturnsAsync(true).Verifiable(Times.Once());
        }
        scheduler.Setup(x => x.Interrupt(key, callerToken)).ReturnsAsync(true).Verifiable(Times.Once());
        if (waitForCompletion)
            scheduler
                .Setup(x =>
                    x.QueryFireInstances(
                        It.Is<FireInstanceQuery>(q =>
                            q.SchedulerInstanceId == SCHEDULER_NODE
                            && q.State == FireInstanceState.Executing
                            && q.Skip == 0
                            && q.Take == PagedQuery.DefaultTake
                        ),
                        It.Is<CancellationToken>(token => token.CanBeCanceled && token != callerToken)
                    )
                )
                .ReturnsAsync(new PagedResult<FireInstance>([], false))
                .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.CancelJob(key, callerToken, waitForCompletion);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(0);
        scheduler.Verify();
        scheduler.Verify(x => x.DeleteJob(key, callerToken), isCancellable ? Times.Once() : Times.Never());
        scheduler.Verify(
            x => x.QueryTriggers(It.IsAny<TriggerQuery>(), It.IsAny<CancellationToken>()),
            isCancellable ? Times.Once() : Times.Never()
        );
        scheduler.Verify(
            x =>
                x.QueryFireInstances(
                    It.IsAny<FireInstanceQuery>(),
                    It.Is<CancellationToken>(token => token != callerToken)
                ),
            waitForCompletion ? Times.Once() : Times.Never()
        );
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldVerifyARejectedInterrupt_WhenTheRunningJobMayHaveAlreadyFinished(bool stillRunning)
    {
        // Arrange
        var key = new JobKey("running", nameof(JobTypes.LibrarySyncJob));
        var executionQueries = 0;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Exactly(2));
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(() =>
                new PagedResult<FireInstance>(++executionQueries == 1 || stillRunning ? [CreateFire(key)] : [], false)
            )
            .Verifiable(Times.Exactly(2));
        scheduler.Setup(x => x.Exists(key, CancellationToken)).ReturnsAsync(false).Verifiable(Times.Once());
        scheduler.Setup(x => x.Interrupt(key, CancellationToken)).ReturnsAsync(false).Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.CancelJob(key, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBe(!stillRunning);
        result.IsFailed.ShouldBe(stillRunning);
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(stillRunning ? 1 : 0);
        if (stillRunning)
            result.Errors.Single().Message.ShouldContain("Failed to interrupt running Quartz job");
        executionQueries.ShouldBe(2);
        scheduler.Verify();
        scheduler.Verify(x => x.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        scheduler.Verify(
            x =>
                x.QueryFireInstances(
                    It.IsAny<FireInstanceQuery>(),
                    It.Is<CancellationToken>(token => token != CancellationToken)
                ),
            Times.Never()
        );
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldPreserveCompletionFailureOrCancellation_WhenTheInterruptedJobCannotBeAwaited(
        bool completionCancelled
    )
    {
        // Arrange
        var key = new JobKey("completion-failed", nameof(JobTypes.LibrarySyncJob));
        using var caller = new CancellationTokenSource();
        var callerToken = caller.Token;
        Exception failure = completionCancelled
            ? new OperationCanceledException("Completion was cancelled")
            : new InvalidOperationException("Completion lookup failed");
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Exactly(2));
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    callerToken
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([CreateFire(key)], false))
            .Verifiable(Times.Once());
        scheduler.Setup(x => x.Exists(key, callerToken)).ReturnsAsync(false).Verifiable(Times.Once());
        scheduler.Setup(x => x.Interrupt(key, callerToken)).ReturnsAsync(true).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    It.Is<CancellationToken>(token => token.CanBeCanceled && token != callerToken)
                )
            )
            .ThrowsAsync(failure)
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.CancelJob(key, callerToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBe(completionCancelled);
        result.Errors.Count.ShouldBe(1);
        var returnedException = result.Errors.Single().ShouldBeOfType<ExceptionalError>().Exception;
        if (completionCancelled)
        {
            returnedException.ShouldBeOfType<OperationCanceledException>();
            returnedException.Message.ShouldContain(nameof(QuartzExtensions.CancelJob));
        }
        else
            returnedException.ShouldBeSameAs(failure);
        caller.IsCancellationRequested.ShouldBeFalse();
        scheduler.Verify();
        scheduler.Verify(x => x.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldReturnCancellation_WhenTheCallerCancelsEvenThoughTheCompletionLookupSucceeds()
    {
        // Arrange
        var key = new JobKey("caller-cancelled", nameof(JobTypes.LibrarySyncJob));
        using var caller = new CancellationTokenSource();
        var callerToken = caller.Token;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Exactly(2));
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    callerToken
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([CreateFire(key)], false))
            .Verifiable(Times.Once());
        scheduler.Setup(x => x.Exists(key, callerToken)).ReturnsAsync(false).Verifiable(Times.Once());
        scheduler
            .Setup(x => x.Interrupt(key, callerToken))
            .Callback(caller.Cancel)
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    It.Is<CancellationToken>(token => token != callerToken && token.IsCancellationRequested)
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([], false))
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.CancelJob(key, callerToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result
            .Errors.Single()
            .ShouldBeOfType<ExceptionalError>()
            .Exception.ShouldBeOfType<OperationCanceledException>()
            .Message.ShouldContain(nameof(QuartzExtensions.CancelJob));
        caller.IsCancellationRequested.ShouldBeTrue();
        scheduler.Verify();
        scheduler.Verify(x => x.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldPropagateCallerCancellationBeforeMutation_WhenTheInitialExecutionLookupIsCancelled()
    {
        // Arrange
        var key = new JobKey("not-mutated", nameof(JobTypes.LibrarySyncJob));
        using var caller = new CancellationTokenSource();
        await caller.CancelAsync();
        var callerToken = caller.Token;
        var failure = new OperationCanceledException(callerToken);
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    callerToken
                )
            )
            .ThrowsAsync(failure)
            .Verifiable(Times.Once());

        // Act
        var exception = await Should.ThrowAsync<OperationCanceledException>(() =>
            scheduler.Object.CancelJob(key, callerToken)
        );

        // Assert
        exception.CancellationToken.ShouldBe(callerToken);
        scheduler.Verify();
        scheduler.Verify(x => x.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        scheduler.Verify(x => x.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        scheduler.Verify(x => x.Interrupt(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldCompleteWithoutPolling_WhenOnlyADifferentJobKeyIsExecuting()
    {
        // Arrange
        var key = new JobKey("requested", nameof(JobTypes.LibrarySyncJob));
        var unrelatedKey = new JobKey(key.Name, nameof(JobTypes.DownloadJob));
        using var caller = new CancellationTokenSource();
        var callerToken = caller.Token;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    It.Is<CancellationToken>(token => token.CanBeCanceled && token != callerToken)
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([CreateFire(unrelatedKey)], false))
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.AwaitJobCompletion(key, callerToken, timeoutSeconds: 0);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(0);
        caller.IsCancellationRequested.ShouldBeFalse();
        scheduler.Verify();
        scheduler.Verify(x => x.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        scheduler.Verify(x => x.Interrupt(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldPollUntilTheExactJobStopsExecuting_WhenAwaitingJobCompletion()
    {
        // Arrange
        var key = new JobKey("finishing", nameof(JobTypes.LibrarySyncJob));
        var unrelatedKey = new JobKey("retained", key.Group);
        using var caller = new CancellationTokenSource();
        var callerToken = caller.Token;
        var executionQueries = 0;
        var queriedTokens = new List<CancellationToken>();
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Exactly(2));
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    It.Is<CancellationToken>(token => token.CanBeCanceled && token != callerToken)
                )
            )
            .Callback<FireInstanceQuery, CancellationToken>((_, token) => queriedTokens.Add(token))
            .ReturnsAsync(() =>
                new PagedResult<FireInstance>(
                    ++executionQueries == 1 ? [CreateFire(key), CreateFire(unrelatedKey)] : [CreateFire(unrelatedKey)],
                    false
                )
            )
            .Verifiable(Times.Exactly(2));

        // Act
        var result = await scheduler.Object.AwaitJobCompletion(key, callerToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(0);
        executionQueries.ShouldBe(2);
        queriedTokens.Count.ShouldBe(2);
        queriedTokens[0].ShouldBe(queriedTokens[1]);
        caller.IsCancellationRequested.ShouldBeFalse();
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldReturnCancellation_WhenAZeroSecondCompletionTimeoutExpiresWhileTheJobRuns()
    {
        // Arrange
        var key = new JobKey("timed-out", nameof(JobTypes.LibrarySyncJob));
        CancellationToken timeoutToken = default;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    It.Is<CancellationToken>(token => token.CanBeCanceled)
                )
            )
            .Callback<FireInstanceQuery, CancellationToken>((_, token) => timeoutToken = token)
            .ReturnsAsync(new PagedResult<FireInstance>([CreateFire(key)], false))
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.AwaitJobCompletion(key, timeoutSeconds: 0);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result
            .Errors.Single()
            .ShouldBeOfType<ExceptionalError>()
            .Exception.ShouldBeAssignableTo<OperationCanceledException>();
        timeoutToken.IsCancellationRequested.ShouldBeTrue();
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldReturnCallerCancellation_WhenCompletionPollingIsCancelled()
    {
        // Arrange
        var key = new JobKey("caller-cancelled", nameof(JobTypes.LibrarySyncJob));
        using var caller = new CancellationTokenSource();
        await caller.CancelAsync();
        var callerToken = caller.Token;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    It.Is<CancellationToken>(token => token != callerToken && token.IsCancellationRequested)
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([CreateFire(key)], false))
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.AwaitJobCompletion(key, callerToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result
            .Errors.Single()
            .ShouldBeOfType<ExceptionalError>()
            .Exception.ShouldBeAssignableTo<OperationCanceledException>();
        caller.IsCancellationRequested.ShouldBeTrue();
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldPreserveTheSchedulerException_WhenCompletionLookupFails()
    {
        // Arrange
        var key = new JobKey("lookup-failed", nameof(JobTypes.LibrarySyncJob));
        var failure = new InvalidOperationException("Cannot inspect execution");
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    It.Is<CancellationToken>(token => token.CanBeCanceled)
                )
            )
            .ThrowsAsync(failure)
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.AwaitJobCompletion(key, timeoutSeconds: 0);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ShouldBeOfType<ExceptionalError>().Exception.ShouldBeSameAs(failure);
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldSucceedWithoutSchedulerCalls_WhenThereAreNoJobsToWaitFor()
    {
        // Arrange
        var scheduler = Mock.Mock<IScheduler>();

        // Act
        var result = await scheduler.Object.WaitForJobsToFinish([], TimeSpan.Zero, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(0);
        scheduler.VerifyGet(x => x.SchedulerInstanceId, Times.Never());
        scheduler.Verify(
            x => x.QueryFireInstances(It.IsAny<FireInstanceQuery>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
    }

    [Test]
    public async Task ShouldWaitOnceForEachDistinctJobKey_WhenAllRequestedJobsHaveFinished()
    {
        // Arrange
        var first = new JobKey("first", nameof(JobTypes.LibrarySyncJob));
        var equivalentFirst = new JobKey(first.Name, first.Group);
        var second = new JobKey("second", first.Group);
        using var caller = new CancellationTokenSource();
        var callerToken = caller.Token;
        var queriedTokens = new List<CancellationToken>();
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Exactly(2));
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    It.Is<CancellationToken>(token => token.CanBeCanceled && token != callerToken)
                )
            )
            .Callback<FireInstanceQuery, CancellationToken>((_, token) => queriedTokens.Add(token))
            .ReturnsAsync(new PagedResult<FireInstance>([], false))
            .Verifiable(Times.Exactly(2));

        // Act
        var result = await scheduler.Object.WaitForJobsToFinish(
            [first, equivalentFirst, second],
            TimeSpan.FromMilliseconds(1),
            callerToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(0);
        queriedTokens.Count.ShouldBe(2);
        queriedTokens.Distinct().Count().ShouldBe(2);
        caller.IsCancellationRequested.ShouldBeFalse();
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldReturnTheFirstFailedWaitAndStillInspectAllDistinctKeys_WhenMultipleWaitsFail()
    {
        // Arrange
        var successful = new JobKey("successful", nameof(JobTypes.LibrarySyncJob));
        var failedFirst = new JobKey("failed-first", successful.Group);
        var failedLast = new JobKey("failed-last", successful.Group);
        var firstFailure = new InvalidOperationException("First failing wait");
        var lastFailure = new InvalidOperationException("Last failing wait");
        var executionQueries = 0;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Exactly(3));
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    It.Is<CancellationToken>(token => token.CanBeCanceled)
                )
            )
            .ReturnsAsync(() =>
                ++executionQueries switch
                {
                    1 => new PagedResult<FireInstance>([], false),
                    2 => throw firstFailure,
                    _ => throw lastFailure,
                }
            )
            .Verifiable(Times.Exactly(3));

        // Act
        var result = await scheduler.Object.WaitForJobsToFinish(
            [successful, failedFirst, failedFirst, failedLast],
            TimeSpan.Zero
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ShouldBeOfType<ExceptionalError>().Exception.ShouldBeSameAs(firstFailure);
        executionQueries.ShouldBe(3);
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldPreserveCancelledWaits_WhenTheCallerCancelsARequestedJob()
    {
        // Arrange
        var cancelled = new JobKey("cancelled", nameof(JobTypes.LibrarySyncJob));
        var completed = new JobKey("completed", cancelled.Group);
        using var caller = new CancellationTokenSource();
        await caller.CancelAsync();
        var callerToken = caller.Token;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Exactly(2));
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    It.Is<CancellationToken>(token => token != callerToken && token.IsCancellationRequested)
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([CreateFire(cancelled)], false))
            .Verifiable(Times.Exactly(2));

        // Act
        var result = await scheduler.Object.WaitForJobsToFinish(
            [cancelled, cancelled, completed],
            TimeSpan.Zero,
            callerToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result
            .Errors.Single()
            .ShouldBeOfType<ExceptionalError>()
            .Exception.ShouldBeAssignableTo<OperationCanceledException>();
        caller.IsCancellationRequested.ShouldBeTrue();
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldCompleteImmediately_WhenTheSchedulerHasNoExecutingJobs()
    {
        // Arrange
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([], false))
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.AwaitScheduler(CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(0);
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldPollUntilNoJobsRemain_WhenAwaitingTheScheduler()
    {
        // Arrange
        var key = new JobKey("finishing", nameof(JobTypes.LibrarySyncJob));
        var executionQueries = 0;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Exactly(2));
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(() => new PagedResult<FireInstance>(++executionQueries == 1 ? [CreateFire(key)] : [], false))
            .Verifiable(Times.Exactly(2));

        // Act
        var result = await scheduler.Object.AwaitScheduler(CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(0);
        executionQueries.ShouldBe(2);
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldReturnCancellation_WhenTheCallerCancelsWhileAwaitingTheScheduler()
    {
        // Arrange
        var key = new JobKey("still-running", nameof(JobTypes.LibrarySyncJob));
        using var caller = new CancellationTokenSource();
        await caller.CancelAsync();
        var callerToken = caller.Token;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    callerToken
                )
            )
            .ReturnsAsync(new PagedResult<FireInstance>([CreateFire(key)], false))
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.AwaitScheduler(callerToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result
            .Errors.Single()
            .ShouldBeOfType<ExceptionalError>()
            .Exception.ShouldBeAssignableTo<OperationCanceledException>();
        caller.IsCancellationRequested.ShouldBeTrue();
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldPreserveTheSchedulerException_WhenAwaitingAllJobsFails()
    {
        // Arrange
        var failure = new InvalidOperationException("Scheduler lookup failed");
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .ThrowsAsync(failure)
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.AwaitScheduler(CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ShouldBeOfType<ExceptionalError>().Exception.ShouldBeSameAs(failure);
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldReturnTimeoutAfterThirtySeconds_WhenTheSchedulerStillHasExecutingJobs()
    {
        // Arrange
        var key = new JobKey("never-finishes", nameof(JobTypes.LibrarySyncJob));
        var executionQueries = 0;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(SCHEDULER_NODE).Verifiable(Times.AtLeastOnce());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .Callback(() => executionQueries++)
            .ReturnsAsync(new PagedResult<FireInstance>([CreateFire(key)], false))
            .Verifiable(Times.AtLeastOnce());
        var elapsed = Stopwatch.StartNew();

        // Act
        var result = await scheduler.Object.AwaitScheduler(CancellationToken);
        elapsed.Stop();

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().Message.ShouldBe("Timed out waiting for Quartz scheduler jobs to complete");
        elapsed.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(30));
        executionQueries.ShouldBeGreaterThan(1);
        scheduler.Verify();
        scheduler.VerifyGet(x => x.SchedulerInstanceId, Times.Exactly(executionQueries));
        scheduler.Verify(
            x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(q =>
                        q.SchedulerInstanceId == SCHEDULER_NODE
                        && q.State == FireInstanceState.Executing
                        && q.Skip == 0
                        && q.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                ),
            Times.Exactly(executionQueries)
        );
        scheduler.Verify(x => x.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        scheduler.Verify(x => x.Interrupt(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    private static FireInstance CreateFire(JobKey key) =>
        new(
            $"fire-{key.Group}-{key.Name}",
            new TriggerKey($"trigger-{key.Name}", key.Group),
            key,
            SCHEDULER_NODE,
            FireInstanceState.Executing,
            DateTimeOffset.UnixEpoch,
            null,
            null
        );

    private static TriggerHeader CreateTrigger(JobKey key) =>
        new(
            new TriggerKey($"trigger-{key.Name}", key.Group),
            key,
            null,
            "SIMPLE",
            TriggerState.Paused,
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
