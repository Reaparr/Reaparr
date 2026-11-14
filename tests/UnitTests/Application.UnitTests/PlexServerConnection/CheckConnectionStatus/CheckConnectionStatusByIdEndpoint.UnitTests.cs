namespace Reaparr.Application.UnitTests;

public class CheckConnectionStatusByIdEndpointUnitTests
    : BaseEndpointUnitTest<
        CheckConnectionStatusByIdEndpoint,
        CheckConnectionStatusByIdRequest,
        PlexServerStatusDTO
    >
{
    [Test]
    public async Task ShouldEmitConnectionTelemetryOnce_WhenManualCheckSucceeds()
    {
        // Arrange
        const int connectionId = 41;
        var status = new PlexServerStatus
        {
            Id = 501,
            IsSuccessful = true,
            StatusCode = 200,
            StatusMessage = "OK",
            LastChecked = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PlexServerId = 7,
            PlexServerConnectionId = connectionId,
        };
        var result = Result.Ok(status);

        Mock.SetupCommand(It.IsAny<CheckConnectionStatusByIdCommand>)
            .ReturnsAsync(result)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.ConnectionCheckFinished(
                    connectionId,
                    It.Is<Result<PlexServerStatus>>(value =>
                        value.IsSuccess
                        && value.Errors.Count == 0
                        && value.Value.IsSuccessful
                        && value.Value.PlexServerConnectionId == connectionId
                    )
                )
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(new CheckConnectionStatusByIdRequest(connectionId));

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var response = (
            await JsonSerializer.DeserializeAsync<ResultDTO<PlexServerStatusDTO>>(
                endpointResult.Endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        response.Value.ShouldNotBeNull();
        response.Value!.Id.ShouldBe(status.Id);
        response.Value.PlexServerConnectionId.ShouldBe(connectionId);
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(
                It.Is<CheckConnectionStatusByIdCommand>(command => command.PlexServerConnectionId == connectionId),
                It.IsAny<CancellationToken>()
            ),
            Times.Once()
        );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ITelemetry>().VerifyNoOtherCalls();
    }

    [Test]
    public async Task ShouldEmitConnectionTelemetryOnce_WhenManualCheckFails()
    {
        // Arrange
        const int connectionId = 42;
        var result = Result
            .Fail<PlexServerStatus>("probe failed")
            .AddStatusCode(System.Net.HttpStatusCode.ServiceUnavailable, "probe failed");

        Mock.SetupCommand(It.IsAny<CheckConnectionStatusByIdCommand>)
            .ReturnsAsync(result)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.ConnectionCheckFinished(
                    connectionId,
                    It.Is<Result<PlexServerStatus>>(value => value.IsFailed && value.ValueOrDefault == null)
                )
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(new CheckConnectionStatusByIdRequest(connectionId));

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        var response = (
            await JsonSerializer.DeserializeAsync<BaseResultDTO>(
                endpointResult.Endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeFalse();
        response.Errors.Count.ShouldBe(2);
        response.Errors.Select(error => error.Message).ShouldBe(["probe failed", "probe failed"]);
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(
                It.Is<CheckConnectionStatusByIdCommand>(command => command.PlexServerConnectionId == connectionId),
                It.IsAny<CancellationToken>()
            ),
            Times.Once()
        );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ITelemetry>().VerifyNoOtherCalls();
    }
}
