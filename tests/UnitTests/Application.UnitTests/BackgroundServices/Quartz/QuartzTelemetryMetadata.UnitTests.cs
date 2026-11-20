using System.Diagnostics;
using Quartz;
using Quartz.Impl;

namespace Reaparr.Application.UnitTests;

public class QuartzTelemetryMetadataUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldRoundTripNativeMetadataAndLibrarySyncPayload_WhenTypedJobIsScheduled()
    {
        // Arrange
        const int serverId = 17;
        const int libraryId = 42;
        const bool forceMediaRefresh = true;
        const string traceState = "vendor-a=original";
        const string sessionId = "0197af2c-8533-71b3-a351-80da30db084a";
        const string distinctId = "d6d3fd61-601c-4f83-999d-2e9ec6e41113";
        var payload = new LibrarySyncJobPayload(serverId, libraryId, forceMediaRefresh);
        var jobKey = LibrarySyncJob.GetJobKey(serverId, libraryId);
        var scheduledAt = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        using var parentActivity = new Activity("request");
        parentActivity.SetIdFormat(ActivityIdFormat.W3C);
        parentActivity.TraceStateString = traceState;
        parentActivity.Start();
        parentActivity.SetTag(TelemetryProtocol.SessionIdTag, sessionId);
        parentActivity.SetTag(TelemetryProtocol.DistinctIdTag, distinctId);

        IJobDetail? capturedJob = null;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.Exists(jobKey, CancellationToken)).ReturnsAsync(false).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.ScheduleJob(
                    It.Is<IJobDetail>(job => job.Key.Equals(jobKey)),
                    It.Is<ITrigger>(trigger => trigger.JobKey.Equals(jobKey)),
                    It.IsAny<ScheduleJobOptions>(),
                    CancellationToken
                )
            )
            .Callback<IJobDetail, ITrigger, ScheduleJobOptions, CancellationToken>((job, _, _, _) => capturedJob = job)
            .Returns(new ValueTask<DateTimeOffset>(scheduledAt))
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.ExecuteJob<LibrarySyncJob, LibrarySyncJobPayload>(
            jobKey,
            payload,
            executeAt: scheduledAt,
            cancellationToken: CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(scheduledAt);
        capturedJob.ShouldNotBeNull();
        scheduler.Verify();

        var serializer = new SystemTextJsonObjectSerializer();
        var serializedMap = serializer.Serialize(capturedJob.JobDataMap);
        var restoredMap = serializer.Deserialize<JobDataMap>(serializedMap).ShouldNotBeNull();

        restoredMap[TelemetryProtocol.TraceParentMetadata].ShouldBeOfType<string>();
        restoredMap[TelemetryProtocol.TraceStateMetadata].ShouldBeOfType<string>();
        restoredMap[TelemetryProtocol.SessionIdMetadata].ShouldBeOfType<string>();
        restoredMap[TelemetryProtocol.TraceParentMetadata].ShouldBe(parentActivity.Id);
        restoredMap[TelemetryProtocol.TraceStateMetadata].ShouldBe(traceState);
        restoredMap[TelemetryProtocol.SessionIdMetadata].ShouldBe(sessionId);
        restoredMap.Keys.ShouldContain(TelemetryProtocol.TraceParentMetadata);
        restoredMap.Keys.ShouldContain(TelemetryProtocol.TraceStateMetadata);
        restoredMap.Keys.ShouldContain(TelemetryProtocol.SessionIdMetadata);
        restoredMap
            .Keys.Where(key => key.StartsWith(TelemetryProtocol.MetadataPrefix, StringComparison.Ordinal))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ShouldBe(
                new[]
                {
                    TelemetryProtocol.SessionIdMetadata,
                    TelemetryProtocol.TraceParentMetadata,
                    TelemetryProtocol.TraceStateMetadata,
                }.OrderBy(key => key, StringComparer.Ordinal)
            );
        restoredMap.Keys.ShouldNotContain(TelemetryProtocol.DistinctIdTag);
        restoredMap.Values.Select(value => value?.ToString()).ShouldNotContain(distinctId);

        var context = new Mock<IJobExecutionContext>();
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.Key).Returns(jobKey);
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(x => x.MergedJobDataMap).Returns(restoredMap);
        context.SetupGet(x => x.FireInstanceId).Returns("library-sync-fire");

        var (parsedParent, parsedSessionId, parsedJobKey, parsedFireInstanceId) = context.Object.GetTelemetryContext();
        parsedParent.TraceId.ShouldBe(parentActivity.TraceId);
        parsedParent.SpanId.ShouldBe(parentActivity.SpanId);
        parsedParent.TraceState.ShouldBe(traceState);
        parsedParent.TraceFlags.ShouldBe(parentActivity.ActivityTraceFlags);
        parsedParent.IsRemote.ShouldBeTrue();
        parsedSessionId.ShouldBe(sessionId);
        parsedJobKey.ShouldBe(jobKey.ToString());
        parsedFireInstanceId.ShouldBe("library-sync-fire");
        restoredMap.GetPayload<LibrarySyncJobPayload>().ShouldBe(payload);

        var uiPayloadJson = context.Object.GetPayloadAsJson();
        uiPayloadJson.ShouldNotContain(TelemetryProtocol.MetadataPrefix);
        uiPayloadJson.ShouldNotContain(distinctId);
        JsonSerializer
            .Deserialize<LibrarySyncJobPayload>(uiPayloadJson, DefaultJsonSerializerOptions.ConfigStandard)
            .ShouldBe(payload);
    }

    [Test]
    public async Task ShouldPreserveOriginalMetadata_WhenRetryMapIsScheduledUnderUnrelatedAmbientActivity()
    {
        // Arrange
        const int serverId = 23;
        const int libraryId = 71;
        const string originalTraceState = "vendor-a=first";
        const string originalSessionId = "0197af2c-8533-71b3-a351-80da30db084a";
        const string unrelatedTraceState = "vendor-b=unrelated";
        const string unrelatedSessionId = "0197af2c-8533-71b3-a351-80da30db084b";
        const string unrelatedDistinctId = "d6d3fd61-601c-4f83-999d-2e9ec6e41113";
        var jobKey = LibrarySyncJob.GetJobKey(serverId, libraryId);
        var dataMap = new LibrarySyncJobPayload(serverId, libraryId, true).ToJobDataMap();

        using var originalActivity = new Activity("original-request");
        originalActivity.SetIdFormat(ActivityIdFormat.W3C);
        originalActivity.TraceStateString = originalTraceState;
        originalActivity.Start();
        originalActivity.SetTag(TelemetryProtocol.SessionIdTag, originalSessionId);
        var originalTraceParent = originalActivity.Id.ShouldNotBeNull();
        originalActivity.Stop();

        dataMap[TelemetryProtocol.TraceParentMetadata] = originalTraceParent;
        dataMap[TelemetryProtocol.TraceStateMetadata] = originalTraceState;
        dataMap[TelemetryProtocol.SessionIdMetadata] = originalSessionId;

        using var unrelatedActivity = new Activity("unrelated-request");
        unrelatedActivity.SetIdFormat(ActivityIdFormat.W3C);
        unrelatedActivity.TraceStateString = unrelatedTraceState;
        unrelatedActivity.Start();
        unrelatedActivity.SetTag(TelemetryProtocol.SessionIdTag, unrelatedSessionId);
        unrelatedActivity.SetTag(TelemetryProtocol.DistinctIdTag, unrelatedDistinctId);

        IReadOnlyDictionary<IJobDetail, IReadOnlyCollection<ITrigger>>? capturedJobs = null;
        var scheduler = Mock.Mock<IScheduler>();
        scheduler.Setup(x => x.Exists(jobKey, CancellationToken)).ReturnsAsync(false).Verifiable(Times.Once());
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
            >((jobs, _, _) => capturedJobs = jobs)
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await scheduler.Object.ExecuteJobs<LibrarySyncJob>(
            [(Key: jobKey, DataMap: dataMap)],
            cancellationToken: CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        capturedJobs.ShouldNotBeNull();
        scheduler.Verify();

        var serializer = new SystemTextJsonObjectSerializer();
        var serializedMap = serializer.Serialize(capturedJobs.Keys.Single().JobDataMap);
        var restoredMap = serializer.Deserialize<JobDataMap>(serializedMap).ShouldNotBeNull();

        restoredMap[TelemetryProtocol.TraceParentMetadata].ShouldBe(originalTraceParent);
        restoredMap[TelemetryProtocol.TraceStateMetadata].ShouldBe(originalTraceState);
        restoredMap[TelemetryProtocol.SessionIdMetadata].ShouldBe(originalSessionId);
        restoredMap[TelemetryProtocol.TraceParentMetadata].ShouldNotBe(unrelatedActivity.Id);
        restoredMap[TelemetryProtocol.TraceStateMetadata].ShouldNotBe(unrelatedTraceState);
        restoredMap[TelemetryProtocol.SessionIdMetadata].ShouldNotBe(unrelatedSessionId);
        restoredMap.Keys.ShouldNotContain(TelemetryProtocol.DistinctIdTag);
        restoredMap.Values.Select(value => value?.ToString()).ShouldNotContain(unrelatedDistinctId);
        restoredMap.GetPayload<LibrarySyncJobPayload>().ShouldBe(new LibrarySyncJobPayload(serverId, libraryId, true));
    }

    [Test]
    public void ShouldReturnDefaultParent_WhenMetadataIsMissingOrMalformedDespiteAmbientActivity()
    {
        // Arrange
        using var unrelatedActivity = new Activity("unrelated-request");
        _ = CancellationToken;
        unrelatedActivity.SetIdFormat(ActivityIdFormat.W3C);
        unrelatedActivity.Start();
        (JobDataMap Data, string? ExpectedSessionId)[] maps =
        [
            (new JobDataMap(), null),
            (
                new JobDataMap
                {
                    [TelemetryProtocol.TraceParentMetadata] = "not-a-traceparent",
                    [TelemetryProtocol.TraceStateMetadata] = "vendor=orphan",
                    [TelemetryProtocol.SessionIdMetadata] = "not-a-guid",
                },
                "not-a-guid"
            ),
        ];

        foreach (var (dataMap, expectedSessionId) in maps)
        {
            var context = new Mock<IJobExecutionContext>();
            context.SetupGet(x => x.MergedJobDataMap).Returns(dataMap);
            context
                .SetupGet(x => x.JobDetail)
                .Returns(JobBuilder.Create<LibrarySyncJob>().WithIdentity("test-job").Build());
            context.SetupGet(x => x.FireInstanceId).Returns("test-fire");

            // Act
            var (parent, parsedSessionId, _, _) = context.Object.GetTelemetryContext();

            // Assert
            parent.TraceId.ShouldBe(default);
            parent.SpanId.ShouldBe(default);
            parent.TraceState.ShouldBeNull();
            parent.IsRemote.ShouldBeFalse();
            parsedSessionId.ShouldBe(expectedSessionId);
        }
    }
}
