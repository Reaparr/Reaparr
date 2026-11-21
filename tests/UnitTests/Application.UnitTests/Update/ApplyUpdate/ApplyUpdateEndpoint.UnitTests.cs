using Microsoft.Extensions.DependencyInjection;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace Reaparr.Application.UnitTests;

public class ApplyUpdateEndpointUnitTests : BaseEndpointWithoutRequestUnitTest<ApplyUpdateEndpoint, BaseResultDTO>
{
    [Test]
    public async Task ShouldReturnFailure_WhenDockerMode()
    {
        // Arrange
        SetAppBuildInfo(x => x.RuntimeMode = "docker");

        var mockSource = new Mock<IUpdateSource>();
        var mockLocator = new Mock<IVelopackLocator>();
        var mockManager = new Mock<UpdateManager>(mockSource.Object, null!, mockLocator.Object);

        // Act
        var endpointResult = await TestEndpointHandleAsync(extraServices: s => s.AddSingleton(_ => mockManager.Object));
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem();
        result.Errors[0].Message.ShouldBe("Desktop updates are not supported in the current runtime mode");
        Mock.Mock<ITelemetry>().Verify(x => x.UpdateApplyAccepted(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        mockManager.Verify(m => m.UpdatePendingRestart, Times.Never);
    }

    [Test]
    public async Task ShouldReturnSuccess_WhenDesktopModeAndUpdatePendingRestart()
    {
        // Arrange
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");

        var asset = new VelopackAsset { PackageId = "Reaparr", Version = new SemanticVersion(9, 9, 9) };

        var mockSource = new Mock<IUpdateSource>();
        var mockLocator = new Mock<IVelopackLocator>();
        var mockManager = new Mock<UpdateManager>(mockSource.Object, null!, mockLocator.Object);
        mockManager.Setup(m => m.UpdatePendingRestart).Returns(asset).Verifiable(Times.Once());
        Mock.Mock<ITelemetry>().Setup(x => x.UpdateApplyAccepted("Reaparr", "9.9.9")).Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(extraServices: s =>
        {
            s.AddSingleton(_ => mockManager.Object);
        });
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();

        mockManager.Verify(m => m.UpdatePendingRestart, Times.Once);
        Mock.Mock<ITelemetry>().Verify();
        mockSource.VerifyNoOtherCalls();
    }
}
