using System.Text.Json;
using Quartz;

namespace Reaparr.Application.UnitTests;

public class QuartzRunningJobUpdatesUnitTests : BaseUnitTest
{
    private const string LocalNode = "running-update-local-node";

    [Test]
    [Arguments(JobTypes.DownloadJob)]
    [Arguments(JobTypes.MoveDownloadFileJob)]
    [Arguments(JobTypes.InspectPlexServerJob)]
    [Arguments(JobTypes.LibrarySyncJob)]
    [Arguments(JobTypes.LibraryComparisonJob)]
    public async Task ShouldReturnTypedStoredPayloadAndUtcStartedStatus_WhenJobIsExecuting(JobTypes jobType)
    {
        // Arrange
        var downloadKey = new DownloadTaskKey
        {
            Id = Guid.Parse("deed97cb-96b8-47c5-92af-a843c044ff73"),
            Type = DownloadTaskType.MovieData,
            PlexServerId = 17,
            PlexLibraryId = 41,
        };
        object payload = jobType switch
        {
            JobTypes.DownloadJob => new DownloadJobPayload(downloadKey),
            JobTypes.MoveDownloadFileJob => new MoveDownloadFileJobPayload(downloadKey),
            JobTypes.InspectPlexServerJob => new InspectPlexServerJobPayload([17, 23]),
            JobTypes.LibrarySyncJob => new LibrarySyncJobPayload(17, 41, true),
            JobTypes.LibraryComparisonJob => new PlexLibraryComparisonJobPayload(41, 73),
            _ => throw new ArgumentOutOfRangeException(nameof(jobType)),
        };
        var key = new JobKey("stored-job-name", jobType.ToString());
        var firedAt = new DateTimeOffset(2026, 11, 18, 14, 31, 19, TimeSpan.FromHours(3));
        var data = payload.ToJobDataMap();
        data[TelemetryProtocol.TraceParentMetadata] = "not-payload";
        data[TelemetryProtocol.SessionIdMetadata] = "not-payload-session";
        data["QRTZ_RECOVERY_DATA"] = "not-payload-recovery";
        var detail = JobBuilder.Create<LibrarySyncJob>().WithIdentity(key).UsingJobData(data).Build();
        var scheduler = new Mock<IScheduler>(MockBehavior.Strict);
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(LocalNode).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query =>
                        query.SchedulerInstanceId == LocalNode
                        && query.State == FireInstanceState.Executing
                        && query.Skip == 0
                        && query.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .Returns(
                new ValueTask<PagedResult<FireInstance>>(
                    new PagedResult<FireInstance>([CreateFiring(key, "distinct-fire-id", firedAt)], false)
                )
            )
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.GetJobDetails(
                    It.Is<IReadOnlyCollection<JobKey>>(keys => keys.SequenceEqual(new[] { key })),
                    CancellationToken
                )
            )
            .Returns(new ValueTask<List<IJobDetail>>([detail]))
            .Verifiable(Times.Once());

        // Act
        var updates = await scheduler.Object.GetRunningJobUpdates(CancellationToken);

        // Assert
        updates.Count.ShouldBe(1);
        var update = updates.Single();
        update.JobType.ShouldBe(jobType);
        update.Status.ShouldBe(JobStatus.Started);
        update.Id.ShouldBe(key.Name);
        update.JobStartTime.ShouldBe(new DateTime(2026, 11, 18, 11, 31, 19, DateTimeKind.Utc));
        update.JobStartTime.Kind.ShouldBe(DateTimeKind.Utc);
        update.Data.ShouldBe(
            JsonSerializer.Serialize(payload, payload.GetType(), DefaultJsonSerializerOptions.ConfigStandard)
        );
        update.Data.ShouldNotContain(TelemetryProtocol.MetadataPrefix);
        update.Data.ShouldNotContain("QRTZ_");
        using var json = JsonDocument.Parse(update.Data);
        var root = json.RootElement;
        switch (jobType)
        {
            case JobTypes.DownloadJob:
            case JobTypes.MoveDownloadFileJob:
                root.EnumerateObject().Select(property => property.Name).ShouldBe(["downloadTaskKey"]);
                var taskKey = root.GetProperty("downloadTaskKey");
                taskKey.GetProperty("id").GetGuid().ShouldBe(downloadKey.Id);
                taskKey.GetProperty("type").GetString().ShouldBe(nameof(DownloadTaskType.MovieData));
                taskKey.GetProperty("plexServerId").GetInt32().ShouldBe(17);
                taskKey.GetProperty("plexLibraryId").GetInt32().ShouldBe(41);
                break;
            case JobTypes.InspectPlexServerJob:
                root.EnumerateObject().Select(property => property.Name).ShouldBe(["plexServerIds"]);
                root.GetProperty("plexServerIds").EnumerateArray().Select(value => value.GetInt32()).ShouldBe([17, 23]);
                break;
            case JobTypes.LibrarySyncJob:
                root.EnumerateObject()
                    .Select(property => property.Name)
                    .Order()
                    .ShouldBe(["forceMediaRefresh", "libraryId", "serverId"]);
                root.GetProperty("serverId").GetInt32().ShouldBe(17);
                root.GetProperty("libraryId").GetInt32().ShouldBe(41);
                root.GetProperty("forceMediaRefresh").GetBoolean().ShouldBeTrue();
                break;
            case JobTypes.LibraryComparisonJob:
                root.EnumerateObject()
                    .Select(property => property.Name)
                    .Order()
                    .ShouldBe(["ownedPlexLibraryId", "remotePlexLibraryId"]);
                root.GetProperty("ownedPlexLibraryId").GetInt32().ShouldBe(41);
                root.GetProperty("remotePlexLibraryId").GetInt32().ShouldBe(73);
                break;
        }
        scheduler.Verify();
        scheduler.Verify(x => x.GetJobDetail(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(JobTypes.CheckAllConnectionsStatusByPlexServerJob)]
    [Arguments(JobTypes.MetadataSyncJob)]
    [Arguments(JobTypes.CheckForUpdateJob)]
    [Arguments(JobTypes.CheckPlexLibrariesForUpdatesJob)]
    [Arguments(JobTypes.RefreshPlexAccountAccessJob)]
    [Arguments(JobTypes.MediaOverviewSnapshotJob)]
    [Arguments(JobTypes.OptimizeDatabaseJob)]
    [Arguments(JobTypes.UpdateScheduledDownloadLimitsJob)]
    public async Task ShouldReturnEmptyJson_WhenExecutingJobHasNoUiPayload(JobTypes jobType)
    {
        // Arrange
        var key = new JobKey("metadata-only-job", jobType.ToString());
        var firedAt = new DateTimeOffset(2026, 11, 18, 11, 32, 20, TimeSpan.Zero);
        var data = new JobDataMap
        {
            [TelemetryProtocol.TraceParentMetadata] = "trace-only",
            [TelemetryProtocol.SessionIdMetadata] = "session-only",
            ["QRTZ_RECOVERY_DATA"] = "recovery-only",
        };
        var detail = JobBuilder.Create<LibrarySyncJob>().WithIdentity(key).UsingJobData(data).Build();
        var scheduler = new Mock<IScheduler>(MockBehavior.Strict);
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(LocalNode).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query =>
                        query.SchedulerInstanceId == LocalNode
                        && query.State == FireInstanceState.Executing
                        && query.Skip == 0
                        && query.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .Returns(
                new ValueTask<PagedResult<FireInstance>>(
                    new PagedResult<FireInstance>([CreateFiring(key, "metadata-fire", firedAt)], false)
                )
            )
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.GetJobDetails(
                    It.Is<IReadOnlyCollection<JobKey>>(keys => keys.SequenceEqual(new[] { key })),
                    CancellationToken
                )
            )
            .Returns(new ValueTask<List<IJobDetail>>([detail]))
            .Verifiable(Times.Once());

        // Act
        var updates = await scheduler.Object.GetRunningJobUpdates(CancellationToken);

        // Assert
        updates.ShouldBe([
            new JobStatusUpdate<string>(jobType, JobStatus.Started, "{}", key.Name, firedAt.UtcDateTime),
        ]);
        updates.Single().JobStartTime.Kind.ShouldBe(DateTimeKind.Utc);
        scheduler.Verify();
    }

    [Test]
    public async Task ShouldSkipNullAndMissingJobsAndRetainEveryFiring_WhenBatchKeysContainDuplicates()
    {
        // Arrange
        var key = new JobKey("repeated-job", nameof(JobTypes.LibrarySyncJob));
        var missingKey = new JobKey("deleted-job", nameof(JobTypes.DownloadJob));
        var firstTime = new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.FromHours(-2));
        var secondTime = firstTime.AddSeconds(37);
        var payload = new LibrarySyncJobPayload(23, 71, false);
        var detail = JobBuilder.Create<LibrarySyncJob>().WithIdentity(key).UsingJobData(payload.ToJobDataMap()).Build();
        var scheduler = new Mock<IScheduler>(MockBehavior.Strict);
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(LocalNode).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query =>
                        query.SchedulerInstanceId == LocalNode
                        && query.State == FireInstanceState.Executing
                        && query.Skip == 0
                        && query.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .Returns(
                new ValueTask<PagedResult<FireInstance>>(
                    new PagedResult<FireInstance>(
                        [
                            CreateFiring(null, "null-key", firstTime),
                            CreateFiring(key, "first-fire", firstTime),
                            CreateFiring(missingKey, "missing-detail", firstTime),
                            CreateFiring(key, "second-fire", secondTime),
                        ],
                        false
                    )
                )
            )
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.GetJobDetails(
                    It.Is<IReadOnlyCollection<JobKey>>(keys => keys.SequenceEqual(new[] { key, missingKey })),
                    CancellationToken
                )
            )
            .Returns(new ValueTask<List<IJobDetail>>([detail]))
            .Verifiable(Times.Once());

        // Act
        var updates = await scheduler.Object.GetRunningJobUpdates(CancellationToken);

        // Assert
        var expectedJson = "{\"serverId\":23,\"libraryId\":71,\"forceMediaRefresh\":false}";
        updates.ShouldBe([
            new JobStatusUpdate<string>(
                JobTypes.LibrarySyncJob,
                JobStatus.Started,
                expectedJson,
                key.Name,
                firstTime.UtcDateTime
            ),
            new JobStatusUpdate<string>(
                JobTypes.LibrarySyncJob,
                JobStatus.Started,
                expectedJson,
                key.Name,
                secondTime.UtcDateTime
            ),
        ]);
        updates.Select(update => update.JobStartTime.Kind).ShouldBe([DateTimeKind.Utc, DateTimeKind.Utc]);
        scheduler.Verify();
        scheduler.Verify(x => x.GetJobDetail(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldReturnNoUpdates_WhenNoFiringsExist()
    {
        // Arrange
        var scheduler = new Mock<IScheduler>(MockBehavior.Strict);
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(LocalNode).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query =>
                        query.SchedulerInstanceId == LocalNode
                        && query.State == FireInstanceState.Executing
                        && query.Skip == 0
                        && query.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .Returns(new ValueTask<PagedResult<FireInstance>>(new PagedResult<FireInstance>([], false)))
            .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.GetJobDetails(It.Is<IReadOnlyCollection<JobKey>>(keys => keys.Count == 0), CancellationToken))
            .Returns(new ValueTask<List<IJobDetail>>([]))
            .Verifiable(Times.Once());

        // Act
        var updates = await scheduler.Object.GetRunningJobUpdates(CancellationToken);

        // Assert
        updates.ShouldBeEmpty();
        scheduler.Verify();
    }

    [Test]
    [Arguments("UnregisteredQuartzJob")]
    [Arguments(nameof(JobTypes.Unknown))]
    [Arguments(nameof(JobTypes.None))]
    [Arguments("999")]
    public async Task ShouldRejectUnknownJobGroup_WhenStoredRunningJobCannotMapToSupportedPayload(string group)
    {
        // Arrange
        var key = new JobKey("unsupported-job", group);
        var firedAt = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        var detail = JobBuilder.Create<LibrarySyncJob>().WithIdentity(key).Build();
        var scheduler = new Mock<IScheduler>(MockBehavior.Strict);
        scheduler.SetupGet(x => x.SchedulerInstanceId).Returns(LocalNode).Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.QueryFireInstances(
                    It.Is<FireInstanceQuery>(query =>
                        query.SchedulerInstanceId == LocalNode
                        && query.State == FireInstanceState.Executing
                        && query.Skip == 0
                        && query.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken
                )
            )
            .Returns(
                new ValueTask<PagedResult<FireInstance>>(
                    new PagedResult<FireInstance>([CreateFiring(key, "unsupported-fire", firedAt)], false)
                )
            )
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.GetJobDetails(
                    It.Is<IReadOnlyCollection<JobKey>>(keys => keys.SequenceEqual(new[] { key })),
                    CancellationToken
                )
            )
            .Returns(new ValueTask<List<IJobDetail>>([detail]))
            .Verifiable(Times.Once());

        // Act
        var exception = await Should.ThrowAsync<Exception>(() =>
            scheduler.Object.GetRunningJobUpdates(CancellationToken)
        );

        // Assert
        if (group == "UnregisteredQuartzJob")
        {
            exception.GetType().ShouldBe(typeof(Exception));
            exception.Message.ShouldBe($"ToJobType => Unknown job type: {group}");
        }
        else
        {
            var unsupported = exception.ShouldBeOfType<ArgumentOutOfRangeException>();
            unsupported.ParamName.ShouldBe("job");
            unsupported.ActualValue.ShouldBe(group);
        }
        scheduler.Verify();
    }

    private static FireInstance CreateFiring(JobKey? key, string fireId, DateTimeOffset firedAt) =>
        new(
            fireId,
            new TriggerKey($"trigger-{fireId}", "trigger-group"),
            key,
            LocalNode,
            FireInstanceState.Executing,
            firedAt,
            firedAt.AddMinutes(-1),
            null
        );
}
