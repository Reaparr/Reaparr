namespace Reaparr.Application.UnitTests;

public class RefreshLibraryMediaEndpointUnitTests
    : BaseEndpointUnitTest<
        RefreshLibraryMediaEndpoint,
        RefreshLibraryMediaEndpointRequest,
        ResultDTO<LibrarySyncJobQueueDTO>
    >
{
    [Test]
    [Arguments(LibrarySyncJobStatus.Queued)]
    [Arguments(LibrarySyncJobStatus.Processing)]
    public async Task ShouldReturnActualQueueState_WhenLibrarySyncJobAccepted(LibrarySyncJobStatus status)
    {
        // Arrange
        await SetupDatabase(
            1223,
            config =>
            {
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var plexLibrary = dbContext.PlexLibraries.First();
        var createdAt = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        var queueItem = new LibrarySyncJobQueue
        {
            PlexLibraryId = plexLibrary.Id,
            PlexServerId = plexLibrary.PlexServerId,
            Priority = 1,
            Status = status,
            CreatedAt = createdAt,
            StartedAt = status == LibrarySyncJobStatus.Processing ? createdAt.AddMinutes(1) : null,
        };
        await dbContext.LibrarySyncJobQueues.AddAsync(queueItem, CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<QueueLibrarySyncJobCommand>(command =>
                        command.ForceLibrarySync
                        && command.ForceMediaRefresh
                        && command.PlexLibraryIds.SequenceEqual(new[] { plexLibrary.Id })
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        LibraryRefreshTelemetryDTO? telemetryRequest = null;
        ResultBase? telemetryResult = null;
        LibrarySyncJobStatus? telemetryQueueStatus = null;
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.LibraryRefreshRequested(
                    It.IsAny<LibraryRefreshTelemetryDTO>(),
                    It.IsAny<ResultBase>(),
                    It.IsAny<LibrarySyncJobStatus?>()
                )
            )
            .Callback<LibraryRefreshTelemetryDTO, ResultBase, LibrarySyncJobStatus?>(
                (request, result, queueStatus) =>
                {
                    telemetryRequest = request;
                    telemetryResult = result;
                    telemetryQueueStatus = queueStatus;
                }
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new RefreshLibraryMediaEndpointRequest
            {
                PlexLibraryId = plexLibrary.Id,
                ForceLibrarySync = true,
                ForceMediaRefresh = true,
            }
        );
        var resultDTO = endpointResult.Response;

        // Assert
        resultDTO.ShouldNotBeNull();
        resultDTO.IsSuccess.ShouldBeTrue();
        resultDTO.Errors.Count.ShouldBe(0);
        resultDTO.Value.ShouldNotBeNull();
        resultDTO.Value.PlexLibraryId.ShouldBe(plexLibrary.Id);
        resultDTO.Value.PlexServerId.ShouldBe(plexLibrary.PlexServerId);
        resultDTO.Value.Priority.ShouldBe(queueItem.Priority);
        resultDTO.Value.Status.ShouldBe(status);
        resultDTO.Value.CreatedAt.ShouldBe(createdAt);
        resultDTO.Value.StartedAt.ShouldBe(queueItem.StartedAt);
        resultDTO.Value.CompletedAt.ShouldBeNull();
        resultDTO.Value.IsServerOffline.ShouldBeFalse();
        resultDTO.Value.ErrorMessage.ShouldBeNull();
        Mock.Mock<ICommandExecutor>().Verify();
        telemetryRequest.ShouldNotBeNull();
        telemetryRequest.PlexLibraryId.ShouldBe(plexLibrary.Id);
        telemetryRequest.ForceLibrarySync.ShouldBeTrue();
        telemetryRequest.ForceMediaRefresh.ShouldBeTrue();
        telemetryResult.ShouldNotBeNull().IsSuccess.ShouldBeTrue();
        telemetryQueueStatus.ShouldBe(status);
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldReturnError_WhenRefreshCommandFails()
    {
        // Arrange
        await SetupDatabase(
            1226,
            config =>
            {
                config.PlexMovieLibraryCount = 1;
            }
        );

        var plexLibrary = IDbContext.PlexLibraries.First();

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<QueueLibrarySyncJobCommand>(command =>
                        command.ForceLibrarySync
                        && command.ForceMediaRefresh
                        && command.PlexLibraryIds.SequenceEqual(new[] { plexLibrary.Id })
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Fail("Failed to refresh library"))
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new RefreshLibraryMediaEndpointRequest
            {
                PlexLibraryId = plexLibrary.Id,
                ForceLibrarySync = true,
                ForceMediaRefresh = true,
            }
        );
        var resultDTO = endpointResult.Response;

        resultDTO.ShouldNotBeNull();
        resultDTO.IsSuccess.ShouldBeFalse();
        resultDTO.Errors.Count.ShouldBe(1);
        resultDTO.Value.ShouldBeNull();
        resultDTO.Errors.ShouldContain(x => x.Message.Contains("Failed to refresh library"));
        (await IDbContext.LibrarySyncJobQueues.AsNoTracking().ToListAsync(CancellationToken)).ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldReturnValidationFailure_WhenPlexLibraryIdIsInvalid()
    {
        // Arrange
        var request = new RefreshLibraryMediaEndpointRequest { PlexLibraryId = 0 };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never());

        // Act
        var result = await TestEndpointHandleAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ValidationResult.ShouldNotBeNull();
        result.ValidationResult.Errors.ShouldContain(x =>
            x.PropertyName == nameof(RefreshLibraryMediaEndpointRequest.PlexLibraryId)
        );

        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
