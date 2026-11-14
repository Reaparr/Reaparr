namespace Reaparr.Application.UnitTests;

public class CheckAllConnectionsStatusByPlexServerEndpointUnitTests
    : BaseEndpointUnitTest<
        CheckAllConnectionsStatusByPlexServerEndpoint,
        CheckAllConnectionsStatusByPlexServerRequest,
        List<PlexServerStatusDTO>
    >
{
    [Test]
    public async Task ShouldEmitAggregateConnectionTelemetryOnce_WhenManualCheckSucceeds()
    {
        // Arrange
        const int serverId = 7;
        var result = Result.Ok(
            new List<PlexServerStatus>
            {
                new()
                {
                    Id = 501,
                    IsSuccessful = true,
                    StatusCode = 200,
                    StatusMessage = "OK",
                    LastChecked = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    PlexServerId = serverId,
                    PlexServerConnectionId = 41,
                },
            }
        );

        Mock.SetupCommand(It.IsAny<CheckAllConnectionsStatusByPlexServerCommand>)
            .ReturnsAsync(result)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.ConnectionCheckFinished(
                    serverId,
                    It.Is<Result<List<PlexServerStatus>>>(value =>
                        value.IsSuccess
                        && value.Errors.Count == 0
                        && value.Value.Count == 1
                        && value.Value[0].IsSuccessful
                        && value.Value[0].PlexServerId == serverId
                    )
                )
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(new CheckAllConnectionsStatusByPlexServerRequest(serverId));

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        endpointResult.Endpoint.HttpContext.Response.Body.Position = 0;
        var response = (
            await JsonSerializer.DeserializeAsync<ResultDTO<List<PlexServerStatusDTO>>>(
                endpointResult.Endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        response.Value.ShouldNotBeNull();
        response.Value!.Count.ShouldBe(1);
        response.Value![0].PlexServerId.ShouldBe(serverId);
        response.Value![0].IsSuccessful.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(
                It.Is<CheckAllConnectionsStatusByPlexServerCommand>(command => command.PlexServerId == serverId),
                It.IsAny<CancellationToken>()
            ),
            Times.Once()
        );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ITelemetry>().VerifyNoOtherCalls();
    }

    [Test]
    public async Task ShouldEmitAggregateConnectionTelemetryOnce_WhenAllConnectionsAreUnhealthy()
    {
        // Arrange
        const int serverId = 8;
        var result = Result.Ok(
                new List<PlexServerStatus>
                {
                    new()
                    {
                        Id = 502,
                        IsSuccessful = false,
                        StatusCode = 503,
                        StatusMessage = "Unavailable",
                        LastChecked = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        PlexServerId = serverId,
                        PlexServerConnectionId = 42,
                    },
                }
            )
            .WithError("all connections failed");

        Mock.SetupCommand(It.IsAny<CheckAllConnectionsStatusByPlexServerCommand>)
            .ReturnsAsync(result)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.ConnectionCheckFinished(
                    serverId,
                    It.Is<Result<List<PlexServerStatus>>>(value =>
                        value.IsFailed
                        && value.Errors.Count == 1
                        && value.ValueOrDefault != null
                        && value.ValueOrDefault.Count == 1
                        && !value.ValueOrDefault![0].IsSuccessful
                    )
                )
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(new CheckAllConnectionsStatusByPlexServerRequest(serverId));

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        endpointResult.Endpoint.HttpContext.Response.Body.Position = 0;
        var response = (
            await JsonSerializer.DeserializeAsync<BaseResultDTO>(
                endpointResult.Endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeFalse();
        response.Errors.Count.ShouldBe(1);
        response.Errors.Any(error => error.Message == "all connections failed").ShouldBeTrue();
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(
                It.Is<CheckAllConnectionsStatusByPlexServerCommand>(command => command.PlexServerId == serverId),
                It.IsAny<CancellationToken>()
            ),
            Times.Once()
        );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ITelemetry>().VerifyNoOtherCalls();
    }
}
