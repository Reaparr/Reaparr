using Microsoft.Extensions.DependencyInjection;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace Reaparr.Application.UnitTests;

public class DownloadUpdateEndpointUnitTests : BaseEndpointWithoutRequestUnitTest<DownloadUpdateEndpoint, BaseResultDTO>
{
    [Test]
    public async Task ShouldReturnFailure_WhenDockerMode()
    {
        var mockSource = new Mock<IUpdateSource>();
        var mockLocator = new Mock<IVelopackLocator>();
        var mockManager = new Mock<UpdateManager>(mockSource.Object, null!, mockLocator.Object);
        SetAppBuildInfo(x => x.RuntimeMode = "docker");
        Mock.Mock<ITelemetry>()
            .Setup(x => x.UpdateDownloadFinished(It.Is<ResultBase>(r => r.IsFailed), null, null))
            .Verifiable(Times.Once());
        // Act
        var endpointResult = await TestEndpointHandleAsync(extraServices: s => s.AddSingleton(_ => mockManager.Object));
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem();
        result.Errors[0].Message.ShouldBe("Desktop updates are not supported in the current runtime mode");
        mockManager.Verify(m => m.CheckForUpdatesAsync(), Times.Never);
        mockManager.Verify(
            m =>
                m.DownloadUpdatesAsync(It.IsAny<UpdateInfo>(), It.IsAny<Action<int>?>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        Mock.Mock<IProgressHubService>()
            .Verify(x => x.SendAppUpdateDownloadProgressAsync(It.IsAny<AppUpdateDownloadProgressDTO>()), Times.Never);
        Mock.Mock<ITelemetry>().Verify();
        mockSource.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ShouldReturnSuccess_WhenDesktopModeAndUpdateAvailable()
    {
        // Arrange
        var asset = new VelopackAsset { PackageId = "Reaparr", Version = new SemanticVersion(9, 9, 9) };
        var updateInfo = new UpdateInfo(asset, false);

        var mockSource = new Mock<IUpdateSource>();
        var mockLocator = new Mock<IVelopackLocator>();
        var mockManager = new Mock<UpdateManager>(mockSource.Object, null!, mockLocator.Object);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");
        Mock.Mock<ITelemetry>()
            .Setup(x => x.UpdateDownloadFinished(It.Is<ResultBase>(r => r.IsSuccess), "9.9.9", false))
            .Verifiable(Times.Once());
        mockManager.Setup(m => m.CheckForUpdatesAsync()).ReturnsAsync(updateInfo).Verifiable(Times.Once());
        mockManager
            .Setup(m =>
                m.DownloadUpdatesAsync(It.IsAny<UpdateInfo>(), It.IsAny<Action<int>?>(), It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(extraServices: s => s.AddSingleton(_ => mockManager.Object));
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();

        mockManager.Verify(m => m.CheckForUpdatesAsync(), Times.Once);
        mockManager.Verify(
            m => m.DownloadUpdatesAsync(updateInfo, It.IsAny<Action<int>?>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        Mock.Mock<IProgressHubService>()
            .Verify(x => x.SendAppUpdateDownloadProgressAsync(It.IsAny<AppUpdateDownloadProgressDTO>()), Times.Never);
        Mock.Mock<ITelemetry>().Verify();
        mockSource.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ShouldReturnSuccessWithoutDownload_WhenNoUpdateIsAvailable()
    {
        // Arrange
        var mockSource = new Mock<IUpdateSource>();
        var mockLocator = new Mock<IVelopackLocator>();
        var mockManager = new Mock<UpdateManager>(mockSource.Object, null!, mockLocator.Object);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");
        mockManager.Setup(m => m.CheckForUpdatesAsync()).ReturnsAsync((UpdateInfo?)null).Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.UpdateDownloadFinished(It.Is<ResultBase>(r => r.IsSuccess), null, null))
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(extraServices: s => s.AddSingleton(_ => mockManager.Object));

        // Assert
        endpointResult.Response.ShouldNotBeNull().IsSuccess.ShouldBeTrue();
        mockManager.Verify(m => m.CheckForUpdatesAsync(), Times.Once);
        mockManager.Verify(
            m =>
                m.DownloadUpdatesAsync(It.IsAny<UpdateInfo>(), It.IsAny<Action<int>?>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        Mock.Mock<ITelemetry>().Verify();
        mockSource.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ShouldReportFailure_WhenUpdateDownloadThrows()
    {
        // Arrange
        var asset = new VelopackAsset { PackageId = "Reaparr", Version = new SemanticVersion(9, 9, 9) };
        var updateInfo = new UpdateInfo(asset, false);
        var downloadException = new InvalidOperationException("download failed");
        var mockSource = new Mock<IUpdateSource>();
        var mockLocator = new Mock<IVelopackLocator>();
        var mockManager = new Mock<UpdateManager>(mockSource.Object, null!, mockLocator.Object);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");
        mockManager.Setup(m => m.CheckForUpdatesAsync()).ReturnsAsync(updateInfo).Verifiable(Times.Once());
        mockManager
            .Setup(m =>
                m.DownloadUpdatesAsync(It.IsAny<UpdateInfo>(), It.IsAny<Action<int>?>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(downloadException)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.UpdateDownloadFinished(It.Is<ResultBase>(r => r.IsFailed), "9.9.9", null))
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(It.Is<ResultBase>(r => r.IsFailed), TelemetryActivity.UpdateLauncher))
            .Verifiable(Times.Once());

        // Act
        var outcome = await TestEndpointHandleAsync(extraServices: s => s.AddSingleton(_ => mockManager.Object));

        // Assert
        outcome.Response.ShouldNotBeNull().IsSuccess.ShouldBeFalse();
        mockManager.Verify(m => m.CheckForUpdatesAsync(), Times.Once);
        mockManager.Verify(
            m => m.DownloadUpdatesAsync(updateInfo, It.IsAny<Action<int>?>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        Mock.Mock<ITelemetry>().Verify();
        mockSource.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ShouldReportCancellation_WhenUpdateDownloadIsCancelled()
    {
        // Arrange
        var asset = new VelopackAsset { PackageId = "Reaparr", Version = new SemanticVersion(9, 9, 9) };
        var updateInfo = new UpdateInfo(asset, false);
        var mockSource = new Mock<IUpdateSource>();
        var mockLocator = new Mock<IVelopackLocator>();
        var mockManager = new Mock<UpdateManager>(mockSource.Object, null!, mockLocator.Object);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");
        mockManager.Setup(m => m.CheckForUpdatesAsync()).ReturnsAsync(updateInfo).Verifiable(Times.Once());
        mockManager
            .Setup(m =>
                m.DownloadUpdatesAsync(It.IsAny<UpdateInfo>(), It.IsAny<Action<int>?>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new OperationCanceledException())
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.UpdateDownloadFinished(
                    It.Is<ResultBase>(r =>
                        r.Errors.OfType<ExceptionalError>().Any(error => error.Exception is OperationCanceledException)
                    ),
                    "9.9.9",
                    null
                )
            )
            .Verifiable(Times.Once());

        // Act
        var outcome = await TestEndpointHandleAsync(extraServices: s => s.AddSingleton(_ => mockManager.Object));

        // Assert
        outcome.Response.ShouldNotBeNull().IsSuccess.ShouldBeFalse();
        mockManager.Verify(m => m.CheckForUpdatesAsync(), Times.Once);
        mockManager.Verify(
            m => m.DownloadUpdatesAsync(updateInfo, It.IsAny<Action<int>?>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        Mock.Mock<ITelemetry>().Verify();
        mockSource.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ShouldSendProgressUpdates_WhenDownloadingUpdate()
    {
        // Arrange
        var asset = new VelopackAsset { PackageId = "Reaparr", Version = new SemanticVersion(9, 9, 9) };
        var updateInfo = new UpdateInfo(asset, false);

        var capturedDtos = new List<AppUpdateDownloadProgressDTO>();
        var progressSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mockSource = new Mock<IUpdateSource>();
        var mockLocator = new Mock<IVelopackLocator>();
        var mockManager = new Mock<UpdateManager>(mockSource.Object, null!, mockLocator.Object);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");
        Mock.Mock<ITelemetry>()
            .Setup(x => x.UpdateDownloadFinished(It.Is<ResultBase>(r => r.IsSuccess), "9.9.9", false))
            .Verifiable(Times.Once());
        mockManager.Setup(m => m.CheckForUpdatesAsync()).ReturnsAsync(updateInfo).Verifiable(Times.Once());
        mockManager
            .Setup(m =>
                m.DownloadUpdatesAsync(It.IsAny<UpdateInfo>(), It.IsAny<Action<int>?>(), It.IsAny<CancellationToken>())
            )
            .Callback<UpdateInfo, Action<int>?, CancellationToken>(
                (_, cb, _) =>
                {
                    cb?.Invoke(50);
                    cb?.Invoke(100);
                }
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IProgressHubService>()
            .Setup(s => s.SendAppUpdateDownloadProgressAsync(It.IsAny<AppUpdateDownloadProgressDTO>()))
            .Callback<AppUpdateDownloadProgressDTO>(dto =>
            {
                capturedDtos.Add(dto);
                if (capturedDtos.Count == 2)
                    progressSent.TrySetResult();
            })
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));

        // Act
        var endpointResult = await TestEndpointHandleAsync(extraServices: s => s.AddSingleton(_ => mockManager.Object));
        await progressSent.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        // Assert
        endpointResult.Response.ShouldNotBeNull();
        endpointResult.Response.IsSuccess.ShouldBeTrue();

        capturedDtos.Count.ShouldBe(2);
        capturedDtos[0].Percentage.ShouldBe(50);
        capturedDtos[0].IsComplete.ShouldBeFalse();
        capturedDtos[1].Percentage.ShouldBe(100);
        capturedDtos[1].IsComplete.ShouldBeTrue();

        mockManager.Verify(m => m.CheckForUpdatesAsync(), Times.Once);
        mockManager.Verify(
            m => m.DownloadUpdatesAsync(updateInfo, It.IsAny<Action<int>?>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        Mock.Mock<IProgressHubService>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        mockSource.VerifyNoOtherCalls();
    }
}
