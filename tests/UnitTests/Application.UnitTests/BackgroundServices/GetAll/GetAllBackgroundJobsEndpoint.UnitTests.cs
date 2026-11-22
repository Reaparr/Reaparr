using Quartz;
using Quartz.Impl.Matchers;

namespace Reaparr.Application.UnitTests;

public class GetAllBackgroundJobsEndpointUnitTests
    : BaseEndpointUnitTest<
        GetAllBackgroundJobsEndpoint,
        GetAllBackgroundJobsEndpointRequest,
        ResultDTO<List<JobStatusUpdateDTO>>
    >
{
    private string ToJsonString<T>(T value) =>
        value is null ? string.Empty : JsonSerializer.Serialize(value, DefaultJsonSerializerOptions.ConfigStandard);

    private void SetupScheduler(IEnumerable<IJobExecutionContext> executingJobs)
    {
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()))
            .ReturnsAsync(executingJobs.ToArray());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<JobKey>());
    }

    private static IJobExecutionContext CreateExecutingJob(JobStatusUpdate<string> update)
    {
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.Key).Returns(new JobKey(update.Id, update.JobType.ToString()));

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(x => x.MergedJobDataMap).Returns(CreateJobDataMap(update));
        context
            .SetupGet(x => x.FireTimeUtc)
            .Returns(new DateTimeOffset(DateTime.SpecifyKind(update.JobStartTime, DateTimeKind.Utc)));
        return context.Object;
    }

    private static DownloadTaskKey GetDownloadTaskKey(string data)
    {
        if (Guid.TryParse(data, out var id))
        {
            return new DownloadTaskKey
            {
                Type = DownloadTaskType.TvShow,
                Id = id,
                PlexServerId = 1,
                PlexLibraryId = 1,
            };
        }

        return JsonSerializer.Deserialize<DownloadJobUpdateDTO>(data, DefaultJsonSerializerOptions.ConfigStandard)?.Id
            ?? throw new JsonException("Download job data did not contain a task key.");
    }

    private static JobDataMap CreateJobDataMap(JobStatusUpdate<string> update)
    {
        return update.JobType switch
        {
            JobTypes.DownloadJob => new DownloadJobPayload(GetDownloadTaskKey(update.Data)).ToJobDataMap(),
            JobTypes.MoveDownloadFileJob => new MoveDownloadFileJobPayload(
                JsonSerializer
                    .Deserialize<MoveDownloadFileJobUpdateDTO>(
                        update.Data,
                        DefaultJsonSerializerOptions.ConfigStandard
                    )!
                    .DownloadTaskId
            ).ToJobDataMap(),
            JobTypes.InspectPlexServerJob => new InspectPlexServerJobPayload(
                JsonSerializer
                    .Deserialize<InspectPlexServerJobUpdateDTO>(
                        update.Data,
                        DefaultJsonSerializerOptions.ConfigStandard
                    )!
                    .PlexServerIds
            ).ToJobDataMap(),
            _ => new JobDataMap(),
        };
    }

    private static string ExpectedPayloadJson(JobStatusUpdate<string> update) =>
        update.JobType switch
        {
            JobTypes.DownloadJob => JsonSerializer.Serialize(
                new DownloadJobPayload(Deserialize<DownloadJobUpdateDTO>(update.Data).Id),
                DefaultJsonSerializerOptions.ConfigStandard
            ),
            JobTypes.MoveDownloadFileJob => JsonSerializer.Serialize(
                new MoveDownloadFileJobPayload(Deserialize<MoveDownloadFileJobUpdateDTO>(update.Data).DownloadTaskId),
                DefaultJsonSerializerOptions.ConfigStandard
            ),
            JobTypes.InspectPlexServerJob => JsonSerializer.Serialize(
                new InspectPlexServerJobPayload(Deserialize<InspectPlexServerJobUpdateDTO>(update.Data).PlexServerIds),
                DefaultJsonSerializerOptions.ConfigStandard
            ),
            _ => "{}",
        };

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, DefaultJsonSerializerOptions.ConfigStandard)!;

    [Test]
    public async Task ShouldReturnEmptyList_WhenNoBackgroundJobIsRunning()
    {
        // Arrange
        SetupScheduler([]);

        // Act
        var endpointResult = await TestEndpointHandleAsync(new GetAllBackgroundJobsEndpointRequest());
        var resultDTO = endpointResult.Response;

        // Assert
        resultDTO.ShouldNotBeNull();
        resultDTO.IsSuccess.ShouldBeTrue();
        var responseValue = resultDTO.Value;
        responseValue.ShouldNotBeNull();
        responseValue.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnJobStatusUpdate_WhenABackgroundJobIsRunning()
    {
        // Arrange
        var jobUpdate = new JobStatusUpdate<string>(JobTypes.DownloadJob, JobStatus.Started, Guid.NewGuid().ToString());
        SetupScheduler([CreateExecutingJob(jobUpdate)]);

        // Act
        var endpointResult = await TestEndpointHandleAsync(new GetAllBackgroundJobsEndpointRequest());
        var resultDTO = endpointResult.Response;

        // Assert
        resultDTO.ShouldNotBeNull();
        resultDTO.IsSuccess.ShouldBeTrue();
        var responseValue = resultDTO.Value;
        responseValue.ShouldNotBeNull();
        responseValue.Count.ShouldBe(1);

        foreach (var value in responseValue)
        {
            value.Id.ShouldBe(jobUpdate.Id);
            value.JobType.ShouldBe(jobUpdate.JobType);
            value.Status.ShouldBe(jobUpdate.Status);
            value.JobStartTime.ShouldBe(jobUpdate.JobStartTime);
        }
    }

    [Test]
    public async Task ShouldReturnTypedJobStatusUpdate_WhenABackgroundJobIsRunning()
    {
        // Arrange
        var downloadJobUpdatePayload = new DownloadJobUpdateDTO
        {
            Id = new DownloadTaskKey
            {
                Type = DownloadTaskType.TvShow,
                Id = Guid.NewGuid(),
                PlexServerId = 1,
                PlexLibraryId = 1,
            },
        };
        var downloadJobUpdate = new JobStatusUpdate<string>(
            JobTypes.DownloadJob,
            JobStatus.Started,
            ToJsonString(downloadJobUpdatePayload),
            Guid.NewGuid().ToString()
        );

        var inspectPlexServerJobUpdatePayload = new InspectPlexServerJobUpdateDTO { PlexServerIds = [1, 2, 3, 4, 5] };
        var inspectPlexServerJobUpdate = new JobStatusUpdate<string>(
            JobTypes.InspectPlexServerJob,
            JobStatus.Started,
            ToJsonString(inspectPlexServerJobUpdatePayload),
            Guid.NewGuid().ToString()
        );

        var moveDownloadJobUpdatePayload = new MoveDownloadFileJobUpdateDTO
        {
            DownloadTaskId = new DownloadTaskKey
            {
                Type = DownloadTaskType.TvShow,
                Id = Guid.NewGuid(),
                PlexServerId = 1,
                PlexLibraryId = 1,
            },
        };

        var moveDownloadJobUpdate = new JobStatusUpdate<string>(
            JobTypes.MoveDownloadFileJob,
            JobStatus.Started,
            ToJsonString(moveDownloadJobUpdatePayload),
            Guid.NewGuid().ToString()
        );

        var checkAllConnectionsStatusJobUpdatePayload = new CheckAllConnectionStatusUpdateDTO
        {
            PlexServersWithConnectionIds = new Dictionary<int, List<int>>(),
        };
        var checkAllConnectionsStatusJobUpdate = new JobStatusUpdate<string>(
            JobTypes.CheckAllConnectionsStatusByPlexServerJob,
            JobStatus.Started,
            ToJsonString(checkAllConnectionsStatusJobUpdatePayload),
            Guid.NewGuid().ToString()
        );

        var list = new List<JobStatusUpdate<string>>
        {
            downloadJobUpdate,
            inspectPlexServerJobUpdate,
            moveDownloadJobUpdate,
            checkAllConnectionsStatusJobUpdate,
        };

        SetupScheduler(list.Select(CreateExecutingJob));

        // Act
        var endpointResult = await TestEndpointHandleAsync(new GetAllBackgroundJobsEndpointRequest());
        var resultDTO = endpointResult.Response;

        // Assert
        resultDTO.ShouldNotBeNull();
        resultDTO.IsSuccess.ShouldBeTrue();
        var responseValue = resultDTO.Value;
        responseValue.ShouldNotBeNull();
        responseValue.Count.ShouldBe(list.Count);

        // Validate each job type
        ValidateJobStatusUpdate(responseValue[0], downloadJobUpdate);
        ValidateJobStatusUpdate(responseValue[1], inspectPlexServerJobUpdate);
        ValidateJobStatusUpdate(responseValue[2], moveDownloadJobUpdate);
        ValidateJobStatusUpdate(responseValue[3], checkAllConnectionsStatusJobUpdate);

        static void ValidateJobStatusUpdate(JobStatusUpdateDTO actual, JobStatusUpdate<string> expected)
        {
            actual.ShouldNotBeNull();
            actual.Id.ShouldBe(expected.Id);
            actual.JobType.ShouldBe(expected.JobType);
            actual.Status.ShouldBe(expected.Status);
            actual.JobStartTime.ShouldBe(expected.JobStartTime);
            actual.JsonString.ShouldBe(ExpectedPayloadJson(expected));
        }
    }
}
