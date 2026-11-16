namespace Reaparr.Application.UnitTests;

public class StopDownloadTaskEndpointUnitTests
    : BaseEndpointUnitTest<StopDownloadTaskEndpoint, StopDownloadTaskEndpointRequest, BaseResultDTO>
{
    [Test]
    public async Task ShouldReturnSuccessResult_WhenCommandSucceeds()
    {
        // Arrange
        await SetupDatabase(20201);
        var guid = Guid.NewGuid();

        Mock.SetupCommand(It.IsAny<StopDownloadTaskCommand>).ReturnsAsync(Result.Ok());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.DownloadControlProcessed(TelemetryAction.Stop, guid, It.Is<ResultBase>(r => r.IsSuccess), true)
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new StopDownloadTaskEndpointRequest { DownloadTaskGuid = guid }
        );
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StopDownloadTaskCommand>(c => c.DownloadTaskGuid == guid),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenCommandFails()
    {
        // Arrange
        await SetupDatabase(20202);
        var guid = Guid.NewGuid();

        Mock.SetupCommand(It.IsAny<StopDownloadTaskCommand>).ReturnsAsync(Result.Fail("Stop failed"));

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new StopDownloadTaskEndpointRequest { DownloadTaskGuid = guid }
        );
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StopDownloadTaskCommand>(c => c.DownloadTaskGuid == guid),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }
}
