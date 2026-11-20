using System.Diagnostics;
using Quartz;

namespace Reaparr.Application.UnitTests;

public class QuartzSchedulingExtensionsUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldScheduleRecoverableTypedJobWithMatchingTrigger_WhenJobIsMissing(bool delayed)
    {
        // Arrange
        var key = LibrarySyncJob.GetJobKey(17, 42);
        var payload = new LibrarySyncJobPayload(17, 42, true);
        var before = DateTimeOffset.UtcNow;
        DateTimeOffset? executeAt = delayed ? before.AddHours(2) : null;
        var scheduledAt = before.AddMinutes(1);
        using var activity = new Activity("schedule-single").SetIdFormat(ActivityIdFormat.W3C).Start();
        activity.SetTag(TelemetryProtocol.SessionIdTag, "single-session");
        IJobDetail? capturedJob = null;
        ITrigger? capturedTrigger = null;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.Exists(key, CancellationToken)).ReturnsAsync(false).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.ScheduleJob(
                    It.Is<IJobDetail>(job => job.Key.Equals(key)),
                    It.Is<ITrigger>(trigger => trigger.JobKey.Equals(key)),
                    It.IsAny<ScheduleJobOptions>(),
                    CancellationToken
                )
            )
            .Callback<IJobDetail, ITrigger, ScheduleJobOptions, CancellationToken>(
                (job, trigger, _, _) =>
                {
                    capturedJob = job;
                    capturedTrigger = trigger;
                }
            )
            .Returns(new ValueTask<DateTimeOffset>(scheduledAt))
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.ExecuteJob<LibrarySyncJob, LibrarySyncJobPayload>(
            key,
            payload,
            CancellationToken,
            executeAt
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(scheduledAt);
        capturedJob.ShouldNotBeNull();
        capturedJob.Key.ShouldBe(key);
        capturedJob.JobType.ShouldBe(typeof(LibrarySyncJob));
        capturedJob.ConcurrentExecutionDisallowed.ShouldBeTrue();
        capturedJob.RequestsRecovery.ShouldBeTrue();
        capturedJob.JobDataMap.GetPayload<LibrarySyncJobPayload>().ShouldBe(payload);
        capturedJob.JobDataMap[TelemetryProtocol.TraceParentMetadata].ShouldBe(activity.Id);
        capturedJob.JobDataMap[TelemetryProtocol.SessionIdMetadata].ShouldBe("single-session");
        capturedTrigger.ShouldNotBeNull();
        capturedTrigger.Key.ShouldBe(new TriggerKey(key.Name, key.Group));
        capturedTrigger.JobKey.ShouldBe(key);
        var simpleTrigger = capturedTrigger.ShouldBeAssignableTo<ISimpleTrigger>();
        simpleTrigger.MisfireInstruction.ShouldBe(SimpleTriggerMisfireInstruction.FireNow);
        simpleTrigger.RepeatCount.ShouldBe(0);
        if (executeAt.HasValue)
            capturedTrigger.StartTimeUtc.ShouldBe(executeAt.Value);
        else
        {
            capturedTrigger.StartTimeUtc.ShouldBeGreaterThanOrEqualTo(before);
            capturedTrigger.StartTimeUtc.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
        }
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldReturnSentinelWithoutScheduling_WhenSingleJobAlreadyExists()
    {
        // Arrange
        var key = LibrarySyncJob.GetJobKey(17, 42);
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.Exists(key, CancellationToken)).ReturnsAsync(true).Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.ExecuteJob<LibrarySyncJob, LibrarySyncJobPayload>(
            key,
            new(17, 42, true),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(DateTimeOffset.MinValue);
        scheduler.Verify();
        scheduler.Verify(
            x =>
                x.ScheduleJob(
                    It.IsAny<IJobDetail>(),
                    It.IsAny<ITrigger>(),
                    It.IsAny<ScheduleJobOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never()
        );
    }

    [Test]
    public async Task ShouldRejectInvalidGroupBeforeQueryingScheduler_WhenSingleJobGroupIsUnknown()
    {
        // Arrange
        var key = new JobKey("invalid-job", "not-a-job-type");
        var scheduler = Mock.Mock<IScheduler>();

        // Act
        var result = await scheduler.Object.ExecuteJob<LibrarySyncJob, LibrarySyncJobPayload>(
            key,
            new(17, 42, true),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Message.ShouldContain(key.Group);
        scheduler.Verify(x => x.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        scheduler.Verify(
            x =>
                x.ScheduleJob(
                    It.IsAny<IJobDetail>(),
                    It.IsAny<ITrigger>(),
                    It.IsAny<ScheduleJobOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never()
        );
    }

    [Test]
    public async Task ShouldReturnOriginalSchedulerExceptionAsFailure_WhenSingleJobSchedulingFails()
    {
        // Arrange
        var key = LibrarySyncJob.GetJobKey(17, 42);
        var exception = new SchedulerException("schedule rejected");
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.Exists(key, CancellationToken)).ReturnsAsync(false).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.ScheduleJob(
                    It.Is<IJobDetail>(job => job.Key.Equals(key)),
                    It.Is<ITrigger>(trigger => trigger.JobKey.Equals(key)),
                    It.IsAny<ScheduleJobOptions>(),
                    CancellationToken
                )
            )
            .Throws(exception)
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.ExecuteJob<LibrarySyncJob, LibrarySyncJobPayload>(
            key,
            new(17, 42, true),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldBeOfType<ExceptionalError>().Exception.ShouldBeSameAs(exception);
        scheduler.Verify();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ShouldScheduleOnlyMissingJobsWithExactPayloadsAndNoReplacement_WhenBatchContainsExistingJob(
        bool typed,
        bool delayed
    )
    {
        // Arrange
        var existingKey = LibrarySyncJob.GetJobKey(17, 41);
        var firstKey = LibrarySyncJob.GetJobKey(17, 42);
        var secondKey = LibrarySyncJob.GetJobKey(18, 43);
        var existingPayload = new LibrarySyncJobPayload(17, 41, false);
        var firstPayload = new LibrarySyncJobPayload(17, 42, true);
        var secondPayload = new LibrarySyncJobPayload(18, 43, false);
        var before = DateTimeOffset.UtcNow;
        DateTimeOffset? executeAt = delayed ? before.AddHours(2) : null;
        using var activity = new Activity("schedule-batch").SetIdFormat(ActivityIdFormat.W3C).Start();
        activity.SetTag(TelemetryProtocol.SessionIdTag, "batch-session");
        IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>? captured = null;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.Exists(existingKey, CancellationToken)).ReturnsAsync(true).Verifiable(Times.Once());
        scheduler.Setup(x => x.Exists(firstKey, CancellationToken)).ReturnsAsync(false).Verifiable(Times.Once());
        scheduler.Setup(x => x.Exists(secondKey, CancellationToken)).ReturnsAsync(false).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.ScheduleJobs(
                    It.IsAny<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>>(),
                    It.Is<ScheduleJobOptions>(options => !options.Replace),
                    CancellationToken
                )
            )
            .Callback<
                IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>,
                ScheduleJobOptions,
                CancellationToken
            >((jobs, _, _) => captured = jobs)
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = typed
            ? await scheduler.Object.ExecuteJobs<LibrarySyncJob, LibrarySyncJobPayload>(
                [(existingKey, existingPayload), (firstKey, firstPayload), (secondKey, secondPayload)],
                CancellationToken,
                executeAt
            )
            : await scheduler.Object.ExecuteJobs<LibrarySyncJob>(
                [
                    (existingKey, existingPayload.ToJobDataMap()),
                    (firstKey, firstPayload.ToJobDataMap()),
                    (secondKey, secondPayload.ToJobDataMap()),
                ],
                CancellationToken,
                executeAt
            );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        captured.ShouldNotBeNull();
        captured
            .Keys.Select(job => job.Key.ToString())
            .Order()
            .ShouldBe(new[] { firstKey.ToString(), secondKey.ToString() }.Order());
        foreach (var (job, triggers) in captured)
        {
            job.JobType.ShouldBe(typeof(LibrarySyncJob));
            job.ConcurrentExecutionDisallowed.ShouldBeTrue();
            job.RequestsRecovery.ShouldBeTrue();
            job.JobDataMap.GetPayload<LibrarySyncJobPayload>()
                .ShouldBe(job.Key.Equals(firstKey) ? firstPayload : secondPayload);
            job.JobDataMap[TelemetryProtocol.TraceParentMetadata].ShouldBe(activity.Id);
            job.JobDataMap[TelemetryProtocol.SessionIdMetadata].ShouldBe("batch-session");
            triggers.Count.ShouldBe(1);
            var trigger = triggers.Single();
            trigger.Key.ShouldBe(new TriggerKey(job.Key.Name, job.Key.Group));
            trigger.JobKey.ShouldBe(job.Key);
            trigger
                .ShouldBeAssignableTo<ISimpleTrigger>()
                .MisfireInstruction.ShouldBe(SimpleTriggerMisfireInstruction.FireNow);
            trigger.ShouldBeAssignableTo<ISimpleTrigger>().RepeatCount.ShouldBe(0);
            if (executeAt.HasValue)
                trigger.StartTimeUtc.ShouldBe(executeAt.Value);
            else
            {
                trigger.StartTimeUtc.ShouldBeGreaterThanOrEqualTo(before);
                trigger.StartTimeUtc.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
            }
        }
        scheduler.Verify();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ShouldSkipBulkScheduling_WhenBatchIsEmptyOrEveryJobExists(bool typed, bool existing)
    {
        // Arrange
        var key = LibrarySyncJob.GetJobKey(17, 42);
        var payload = new LibrarySyncJobPayload(17, 42, true);
        var scheduler = Mock.Mock<IScheduler>();
        if (existing)
            scheduler.Setup(x => x.Exists(key, CancellationToken)).ReturnsAsync(true).Verifiable(Times.Once());

        // Act
        var result = typed
            ? await scheduler.Object.ExecuteJobs<LibrarySyncJob, LibrarySyncJobPayload>(
                existing ? [(key, payload)] : [],
                CancellationToken
            )
            : await scheduler.Object.ExecuteJobs<LibrarySyncJob>(
                existing ? [(key, payload.ToJobDataMap())] : [],
                CancellationToken
            );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        scheduler.Verify();
        if (!existing)
            scheduler.Verify(x => x.Exists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        scheduler.Verify(
            x =>
                x.ScheduleJobs(
                    It.IsAny<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>>(),
                    It.IsAny<ScheduleJobOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never()
        );
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldPropagateOriginalException_WhenBatchSchedulingFails(bool typed)
    {
        // Arrange
        var key = LibrarySyncJob.GetJobKey(17, 42);
        var payload = new LibrarySyncJobPayload(17, 42, true);
        var exception = new SchedulerException("batch rejected");
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.Exists(key, CancellationToken)).ReturnsAsync(false).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.ScheduleJobs(
                    It.Is<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>>(jobs =>
                        jobs.Count == 1 && jobs.Keys.Single().Key.Equals(key)
                    ),
                    It.Is<ScheduleJobOptions>(options => !options.Replace),
                    CancellationToken
                )
            )
            .Throws(exception)
            .Verifiable(Times.Once());

        // Act
        var thrown = await Should.ThrowAsync<SchedulerException>(async () =>
        {
            if (typed)
                await scheduler.Object.ExecuteJobs<LibrarySyncJob, LibrarySyncJobPayload>(
                    [(key, payload)],
                    CancellationToken
                );
            else
                await scheduler.Object.ExecuteJobs<LibrarySyncJob>([(key, payload.ToJobDataMap())], CancellationToken);
        });

        // Assert
        thrown.ShouldBeSameAs(exception);
        scheduler.Verify();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task ShouldPropagateLookupExceptionWithoutScheduling_WhenExistenceLookupFails(int overload)
    {
        // Arrange
        var key = LibrarySyncJob.GetJobKey(17, 42);
        var payload = new LibrarySyncJobPayload(17, 42, true);
        var exception = new SchedulerException("lookup rejected");
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.Exists(key, CancellationToken)).Throws(exception).Verifiable(Times.Once());

        // Act
        var thrown = await Should.ThrowAsync<SchedulerException>(async () =>
        {
            if (overload == 0)
                await scheduler.Object.ExecuteJob<LibrarySyncJob, LibrarySyncJobPayload>(
                    key,
                    payload,
                    CancellationToken
                );
            else if (overload == 1)
                await scheduler.Object.ExecuteJobs<LibrarySyncJob, LibrarySyncJobPayload>(
                    [(key, payload)],
                    CancellationToken
                );
            else
                await scheduler.Object.ExecuteJobs<LibrarySyncJob>([(key, payload.ToJobDataMap())], CancellationToken);
        });

        // Assert
        thrown.ShouldBeSameAs(exception);
        scheduler.Verify();
        scheduler.Verify(
            x =>
                x.ScheduleJob(
                    It.IsAny<IJobDetail>(),
                    It.IsAny<ITrigger>(),
                    It.IsAny<ScheduleJobOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never()
        );
        scheduler.Verify(
            x =>
                x.ScheduleJobs(
                    It.IsAny<IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>>(),
                    It.IsAny<ScheduleJobOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never()
        );
    }
}
