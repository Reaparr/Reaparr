namespace Reaparr.Application.UnitTests;

public class StartDownloadTaskEndpointUnitTests
    : BaseEndpointUnitTest<StartDownloadTaskEndpoint, StartDownloadTaskEndpointRequest, BaseResultDTO>
{
    [Test]
    public async Task ShouldReturnSuccessResult_WhenCommandSucceeds()
    {
        // Arrange
        await SetupDatabase(20101);
        var guid = Guid.NewGuid();

        Mock.SetupCommand(It.IsAny<StartDownloadTaskCommand>).ReturnsAsync(Result.Ok());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.DownloadControlProcessed(TelemetryAction.Start, guid, It.Is<ResultBase>(r => r.IsSuccess), false)
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new StartDownloadTaskEndpointRequest { DownloadTaskGuid = guid }
        );
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StartDownloadTaskCommand>(c => c.DownloadTaskGuid == guid),
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
        await SetupDatabase(20102);
        var guid = Guid.NewGuid();

        Mock.SetupCommand(It.IsAny<StartDownloadTaskCommand>).ReturnsAsync(Result.Fail("Start failed"));

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new StartDownloadTaskEndpointRequest { DownloadTaskGuid = guid }
        );
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StartDownloadTaskCommand>(c => c.DownloadTaskGuid == guid),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }
}
