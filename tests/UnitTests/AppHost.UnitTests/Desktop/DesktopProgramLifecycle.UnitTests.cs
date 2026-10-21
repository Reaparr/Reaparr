using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Reaparr.Environment;

namespace Reaparr.AppHost.UnitTests;

[NotInParallel]
public class DesktopProgramLifecycleUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldSignalPrimaryAndSkipHostStartup_WhenDesktopProcessIsSecondaryInstance()
    {
        // Arrange
        var hostProbe = new HostProbe();
        var desktopMode = new Mock<IDesktopMode>(MockBehavior.Strict);
        var coordinator = new Mock<IDesktopSingleInstanceCoordinator>(MockBehavior.Strict);
        var app = CreateApp(hostProbe, desktopMode.Object, coordinator.Object);

        coordinator.Setup(x => x.TryAcquirePrimaryOwnership()).Returns(false).Verifiable(Times.Once());
        coordinator
            .Setup(x => x.SignalPrimaryInstanceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Program.RunDesktopLifecycleAsync(app, CancellationToken);

        // Assert
        result.Errors.Count.ShouldBe(0);
        result.IsSuccess.ShouldBeTrue();
        hostProbe.StartCalls.ShouldBe(0);
        hostProbe.StopCalls.ShouldBe(0);
        coordinator.Verify();
    }

    [Test]
    public async Task ShouldStartHostAndWaitForExit_WhenDesktopProcessIsPrimaryInstance()
    {
        // Arrange
        var hostProbe = new HostProbe();
        var desktopMode = new Mock<IDesktopMode>(MockBehavior.Strict);
        var coordinator = new Mock<IDesktopSingleInstanceCoordinator>(MockBehavior.Strict);
        var app = CreateApp(hostProbe, desktopMode.Object, coordinator.Object);

        coordinator.Setup(x => x.TryAcquirePrimaryOwnership()).Returns(true).Verifiable(Times.Once());
        coordinator
            .Setup(x =>
                x.StartListener(It.IsAny<Func<CancellationToken, Task<Result>>>(), It.IsAny<CancellationToken>())
            )
            .Returns(Result.Ok())
            .Verifiable(Times.Once());
        desktopMode
            .Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        desktopMode
            .Setup(x => x.WaitForExitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await Program.RunDesktopLifecycleAsync(app, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        hostProbe.StartCalls.ShouldBe(1);
        hostProbe.StopCalls.ShouldBe(1);
        coordinator.Verify();
        desktopMode.Verify();
        desktopMode.Verify(x => x.ShowMainWindowAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ShouldStopHostAndReturnFailure_WhenListenerStartupFails()
    {
        // Arrange
        var hostProbe = new HostProbe();
        var desktopMode = new Mock<IDesktopMode>(MockBehavior.Strict);
        var coordinator = new Mock<IDesktopSingleInstanceCoordinator>(MockBehavior.Strict);
        var app = CreateApp(hostProbe, desktopMode.Object, coordinator.Object);

        coordinator.Setup(x => x.TryAcquirePrimaryOwnership()).Returns(true).Verifiable(Times.Once());
        coordinator
            .Setup(x =>
                x.StartListener(It.IsAny<Func<CancellationToken, Task<Result>>>(), It.IsAny<CancellationToken>())
            )
            .Returns(Result.Fail("listener failed"))
            .Verifiable(Times.Once());

        // Act
        var result = await Program.RunDesktopLifecycleAsync(app, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        hostProbe.StartCalls.ShouldBe(1);
        hostProbe.StopCalls.ShouldBe(1);
        coordinator.Verify();
        desktopMode.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ShouldStopHostAndReturnFailure_WhenDesktopModeStartFails()
    {
        // Arrange
        var hostProbe = new HostProbe();
        var desktopMode = new Mock<IDesktopMode>(MockBehavior.Strict);
        var coordinator = new Mock<IDesktopSingleInstanceCoordinator>(MockBehavior.Strict);
        var app = CreateApp(hostProbe, desktopMode.Object, coordinator.Object);

        coordinator.Setup(x => x.TryAcquirePrimaryOwnership()).Returns(true).Verifiable(Times.Once());
        coordinator
            .Setup(x =>
                x.StartListener(It.IsAny<Func<CancellationToken, Task<Result>>>(), It.IsAny<CancellationToken>())
            )
            .Returns(Result.Ok())
            .Verifiable(Times.Once());
        desktopMode
            .Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("desktop failed"))
            .Verifiable(Times.Once());

        // Act
        var result = await Program.RunDesktopLifecycleAsync(app, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        hostProbe.StartCalls.ShouldBe(1);
        result.Errors.Count.ShouldBe(1);
        hostProbe.StopCalls.ShouldBe(1);
        coordinator.Verify();
        desktopMode.Verify();
        desktopMode.Verify(x => x.ShowMainWindowAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ShouldStopHostBeforeDesktopWaitCompletes_WhenApplicationShutdownIsRequested()
    {
        // Arrange
        var hostProbe = new HostProbe();
        var windowWaitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWindowWait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var applicationStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationToken = CancellationToken;
        var deadline = TimeSpan.FromSeconds(10);
        var desktopMode = new Mock<IDesktopMode>(MockBehavior.Strict);
        var coordinator = new Mock<IDesktopSingleInstanceCoordinator>(MockBehavior.Strict);
        await using var app = CreateApp(hostProbe, desktopMode.Object, coordinator.Object);
        using var stoppedRegistration = app.Lifetime.ApplicationStopped.Register(() => applicationStopped.TrySetResult());

        coordinator.Setup(x => x.TryAcquirePrimaryOwnership()).Returns(true).Verifiable(Times.Once());
        coordinator
            .Setup(x => x.StartListener(It.IsAny<Func<CancellationToken, Task<Result>>>(), cancellationToken))
            .Returns(Result.Ok())
            .Verifiable(Times.Once());
        desktopMode.Setup(x => x.StartAsync(cancellationToken)).ReturnsAsync(Result.Ok()).Verifiable(Times.Once());
        desktopMode
            .Setup(x => x.WaitForExitAsync(cancellationToken))
            .Returns(() =>
            {
                windowWaitStarted.TrySetResult();
                releaseWindowWait.Task.GetAwaiter().GetResult();
                return Task.CompletedTask;
            })
            .Verifiable(Times.Once());

        // Act
        var lifecycle = Task.Run(() => Program.RunDesktopLifecycleAsync(app, cancellationToken), cancellationToken);
        Result result;
        try
        {
            await windowWaitStarted.Task.WaitAsync(deadline, cancellationToken);
            app.Lifetime.StopApplication();
            await applicationStopped.Task.WaitAsync(deadline, cancellationToken);

            // Assert: cleanup must finish while the native window wait is still blocked.
            hostProbe.StartCalls.ShouldBe(1);
            hostProbe.StopCalls.ShouldBe(1);
            app.Lifetime.ApplicationStopped.IsCancellationRequested.ShouldBeTrue();
            lifecycle.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            releaseWindowWait.TrySetResult();
            result = await lifecycle.WaitAsync(deadline, cancellationToken);
        }

        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        hostProbe.StopCalls.ShouldBe(1);
        coordinator.Verify();
        desktopMode.Verify();
        desktopMode.Verify(x => x.ShowMainWindowAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static WebApplication CreateApp(
        HostProbe hostProbe,
        IDesktopMode desktopMode,
        IDesktopSingleInstanceCoordinator coordinator
    )
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IHostedService>(hostProbe);
        builder.Services.AddSingleton(desktopMode);
        builder.Services.AddSingleton(coordinator);
        builder.Services.AddSingleton<IAppRuntimeInfo>(new MockAppRuntimeInfo());
        return builder.Build();
    }

    private sealed class HostProbe : IHostedService
    {
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCalls++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCalls++;
            return Task.CompletedTask;
        }
    }
}
