namespace Reaparr.Application.UnitTests;

public class ValidatePlexServerConnectionEndpointUnitTests
    : BaseEndpointUnitTest<
        ValidatePlexServerConnectionEndpoint,
        ValidatePlexServerConnectionEndpointRequest,
        ResultDTO<ServerIdentityDTO>
    >
{
    [Test]
    public async Task ShouldEmitConnectionCheckTelemetryOnce_WhenValidationSucceeds()
    {
        // Arrange
        const string url = "https://plex.example";
        var result = Result.Ok(
            new ServerIdentityDTO
            {
                Claimed = true,
                MachineIdentifier = "machine-id",
                Version = "1.0.0",
            }
        );

        Mock.SetupCommand(() => It.Is<ValidatePlexConnectionUrlCommand>(command => command.PlexConnectionUrl == url))
            .ReturnsAsync(result)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.ConnectionCheckFinished(It.Is<ResultBase>(value => value.IsSuccess)))
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new ValidatePlexServerConnectionEndpointRequest { Url = url }
        );

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response!.IsSuccess.ShouldBeTrue();
        endpointResult.Response.Errors.Count.ShouldBe(0);
        endpointResult.Response.Value.ShouldNotBeNull();
        endpointResult.Response.Value!.MachineIdentifier.ShouldBe("machine-id");
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(
                    It.Is<ValidatePlexConnectionUrlCommand>(command => command.PlexConnectionUrl == url),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once()
            );
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ITelemetry>().VerifyNoOtherCalls();
    }
}
