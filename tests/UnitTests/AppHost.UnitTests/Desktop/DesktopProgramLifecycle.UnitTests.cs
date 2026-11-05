using System.Collections.Concurrent;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Reaparr.Environment;
using Reaparr.Data.Contracts;
using Reaparr.Settings.Contracts;

namespace Reaparr.AppHost.UnitTests;

[NotInParallel]
public class DesktopProgramLifecycleUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldSignalPrimaryWithoutInitializingState_WhenDesktopProcessIsSecondaryInstance(bool signalSucceeds)
    {
        // Arrange
        var hostProbe = new HostProbe();
        var coordinator = new Mock<IDesktopSingleInstanceCoordinator>(MockBehavior.Strict);
        var configManager = new Mock<IConfigManager>(MockBehavior.Strict);
        var databaseManager = new Mock<IReaparrDbContextManager>(MockBehavior.Strict);
        await using var app = CreateStartupApp(hostProbe, coordinator.Object, configManager.Object, databaseManager.Object);
        var signalResult = signalSucceeds ? Result.Ok() : Result.Fail("primary unavailable");
        coordinator.Setup(x => x.TryAcquirePrimaryOwnership()).Returns(false).Verifiable(Times.Once());
        coordinator.Setup(x => x.SignalPrimaryInstanceAsync(CancellationToken))
            .ReturnsAsync(signalResult).Verifiable(Times.Once());
        configManager.Setup(x => x.Setup()).Throws(new InvalidOperationException("configuration must not initialize"));
        databaseManager.Setup(x => x.SetupAsync()).Throws(new InvalidOperationException("database must not initialize"));

        // Act
        var result = await Program.RunApplicationAsync(
            app, new MockAppBuildInfo { RuntimeMode = "desktop" }, new MockAppRuntimeInfo(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBe(signalSucceeds);
        result.Errors.Select(error => error.Message).ShouldBe(signalSucceeds ? [] : new[] { "primary unavailable" });
        hostProbe.StartCalls.ShouldBe(0);
        hostProbe.StopCalls.ShouldBe(0);
        configManager.Verify(x => x.Setup(), Times.Never);
        databaseManager.Verify(x => x.SetupAsync(), Times.Never);
        coordinator.Verify();
    }

    [Test]
    [Arguments("desktop", false, true)]
    [Arguments("desktop", false, false)]
    [Arguments("docker", false, true)]
    [Arguments("desktop", true, true)]
    public async Task ShouldPreserveInitializationOrderAndStopBeforeHost_WhenStartupInitializationFails(
        string runtimeMode, bool integrationTestMode, bool configFails)
    {
        // Arrange
        var hostProbe = new HostProbe();
        var coordinator = new Mock<IDesktopSingleInstanceCoordinator>(MockBehavior.Strict);
        var configManager = new Mock<IConfigManager>(MockBehavior.Strict);
        var databaseManager = new Mock<IReaparrDbContextManager>(MockBehavior.Strict);
        await using var app = CreateStartupApp(hostProbe, coordinator.Object, configManager.Object, databaseManager.Object);
        var failure = new Error(configFails ? "configuration failed" : "database failed");
        var sequence = new MockSequence();
        if (runtimeMode == "desktop" && !integrationTestMode)
            coordinator.InSequence(sequence).Setup(x => x.TryAcquirePrimaryOwnership()).Returns(true);
        configManager.InSequence(sequence).Setup(x => x.Setup())
            .Returns(configFails ? Result.Fail(failure) : Result.Ok());
        if (!configFails)
            databaseManager.InSequence(sequence).Setup(x => x.SetupAsync()).ReturnsAsync(Result.Fail(failure));

        // Act
        var result = await Program.RunApplicationAsync(
            app,
            new MockAppBuildInfo { RuntimeMode = runtimeMode },
            new MockAppRuntimeInfo { IsIntegrationTestMode = integrationTestMode },
            CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldBe([failure]);
        configManager.Verify(x => x.Setup(), Times.Once);
        databaseManager.Verify(x => x.SetupAsync(), configFails ? Times.Never() : Times.Once());
        coordinator.Verify(x => x.TryAcquirePrimaryOwnership(),
            runtimeMode == "desktop" && !integrationTestMode ? Times.Once() : Times.Never());
        coordinator.Verify(x => x.SignalPrimaryInstanceAsync(It.IsAny<CancellationToken>()), Times.Never);
        hostProbe.StartCalls.ShouldBe(0);
        hostProbe.StopCalls.ShouldBe(0);
    }

    [Test]
    public async Task ShouldStartHostAndWaitForExit_WhenDesktopProcessIsPrimaryInstance()
    {
        // Arrange
        var hostProbe = new HostProbe();
        var desktopMode = new Mock<IDesktopMode>(MockBehavior.Strict);
        var coordinator = new Mock<IDesktopSingleInstanceCoordinator>(MockBehavior.Strict);
        var app = CreateApp(hostProbe, desktopMode.Object, coordinator.Object);

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
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldStopHostAndReturnFailure_WhenListenerStartupFails(bool embeddedDisabled)
    {
        // Arrange
        var hostProbe = new HostProbe();
        var desktopMode = new Mock<IDesktopMode>(MockBehavior.Strict);
        var coordinator = new Mock<IDesktopSingleInstanceCoordinator>(MockBehavior.Strict);
        var browserRequests = new List<Uri>();
        await using var app = CreateApp(
            hostProbe,
            desktopMode.Object,
            coordinator.Object,
            new MockAppRuntimeInfo { IsDesktopEmbeddedDisabled = embeddedDisabled }
        );

        coordinator
            .Setup(x =>
                x.StartListener(It.IsAny<Func<CancellationToken, Task<Result>>>(), It.IsAny<CancellationToken>())
            )
            .Returns(Result.Fail("listener failed"))
            .Verifiable(Times.Once());

        // Act
        var result = await Program.RunDesktopLifecycleAsync(app, CancellationToken, browserRequests.Add);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        hostProbe.StartCalls.ShouldBe(1);
        hostProbe.StopCalls.ShouldBe(1);
        coordinator.Verify();
        desktopMode.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
        browserRequests.ShouldBeEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReopenBrowserAndStopHost_WhenBrowserOnlyPrimaryReceivesRelaunchSignal(bool development)
    {
        // Arrange
        var hostProbe = new HostProbe();
        var firstOpen = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextOpen = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var browserRequests = new ConcurrentQueue<Uri>();
        var deadline = TimeSpan.FromSeconds(10);
        var cancellationToken = CancellationToken;
        var desktopMode = new Mock<IDesktopMode>(MockBehavior.Strict);
        var coordinator = new Mock<IDesktopSingleInstanceCoordinator>(MockBehavior.Strict);
        using var pipe = new DesktopSingleInstanceCoordinator(
            Log,
            new MockAppRuntimeInfo(),
            $"Reaparr.Desktop.Browser.Tests.{Guid.NewGuid():N}"
        );
        await using var app = CreateApp(
            hostProbe,
            desktopMode.Object,
            coordinator.Object,
            new MockAppRuntimeInfo { IsDesktopEmbeddedDisabled = true, IsDevelopmentEnvironment = development }
        );

        coordinator
            .Setup(x => x.StartListener(It.IsAny<Func<CancellationToken, Task<Result>>>(), cancellationToken))
            .Returns((Func<CancellationToken, Task<Result>> onSignal, CancellationToken token) =>
                pipe.StartListener(onSignal, token)
            )
            .Verifiable(Times.Once());
        var secondaryProbe = new HostProbe();
        var secondaryCoordinator = new Mock<IDesktopSingleInstanceCoordinator>(MockBehavior.Strict);
        var secondaryConfig = new Mock<IConfigManager>(MockBehavior.Strict);
        var secondaryDatabase = new Mock<IReaparrDbContextManager>(MockBehavior.Strict);
        await using var secondaryApp = CreateStartupApp(
            secondaryProbe, secondaryCoordinator.Object, secondaryConfig.Object, secondaryDatabase.Object);
        secondaryCoordinator.Setup(x => x.TryAcquirePrimaryOwnership()).Returns(false).Verifiable(Times.Once());
        secondaryCoordinator.Setup(x => x.SignalPrimaryInstanceAsync(cancellationToken))
            .Returns(() => pipe.SignalPrimaryInstanceAsync(cancellationToken)).Verifiable(Times.Once());

        // Act
        var lifecycle = Program.RunDesktopLifecycleAsync(
            app,
            cancellationToken,
            uri =>
            {
                browserRequests.Enqueue(uri);
                if (!firstOpen.TrySetResult(uri))
                    nextOpen.TrySetResult(uri);
            }
        );
        Result result;
        try
        {
            var initialUri = await firstOpen.Task.WaitAsync(deadline, cancellationToken);
            var expectedUri = new Uri(development ? "http://localhost:3000/" : app.Urls.Single());
            initialUri.ShouldBe(expectedUri);
            browserRequests.ToArray().ShouldBe([expectedUri]);
            nextOpen.Task.IsCompleted.ShouldBeFalse();
            var signalResult = await Program.RunApplicationAsync(
                secondaryApp, new MockAppBuildInfo { RuntimeMode = "desktop" }, new MockAppRuntimeInfo(), cancellationToken);
            var reopenedUri = await nextOpen.Task.WaitAsync(deadline, cancellationToken);

            // Assert
            signalResult.IsSuccess.ShouldBeTrue();
            signalResult.Errors.Count.ShouldBe(0);
            reopenedUri.ShouldBe(expectedUri);
            browserRequests.ToArray().ShouldBe([expectedUri, expectedUri]);
            secondaryProbe.StartCalls.ShouldBe(0);
            secondaryConfig.Verify(x => x.Setup(), Times.Never);
            secondaryDatabase.Verify(x => x.SetupAsync(), Times.Never);
            secondaryCoordinator.Verify();
            lifecycle.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            app.Lifetime.StopApplication();
            result = await lifecycle.WaitAsync(deadline, cancellationToken);
        }

        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        hostProbe.StartCalls.ShouldBe(1);
        hostProbe.StopCalls.ShouldBe(1);
        coordinator.Verify();
        desktopMode.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
        desktopMode.Verify(x => x.ShowMainWindowAsync(It.IsAny<CancellationToken>()), Times.Never);
        desktopMode.Verify(x => x.WaitForExitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [Arguments(true, "http://127.0.0.1:45000", 6001, "http://localhost:3000/")]
    [Arguments(true, null, 6001, "http://localhost:3000/")]
    [Arguments(false, "http://127.0.0.1:45000", 6001, "http://127.0.0.1:45000/")]
    [Arguments(false, null, 6001, "http://localhost:6001/")]
    [Arguments(false, "http://0.0.0.0:45000", 6001, "http://localhost:45000/")]
    [Arguments(false, "http://[::]:45000", 6001, "http://localhost:45000/")]
    public void ShouldResolveBrowserUri_WhenEnvironmentAndBoundAddressDiffer(
        bool development,
        string? boundAddress,
        int fallbackPort,
        string expectedUri
    )
    {
        // Arrange
        string[] serverAddresses = boundAddress is null ? [] : [boundAddress];
        var runtimeInfo = new MockAppRuntimeInfo { IsDevelopmentEnvironment = development, AppPort = fallbackPort };

        // Act
        var browserUri = Program.ResolveBrowserUri(serverAddresses, runtimeInfo);

        // Assert
        browserUri.AbsoluteUri.ShouldBe(expectedUri);
    }

    [Test]
    public async Task ShouldStopHostAndReturnFailure_WhenDesktopModeStartFails()
    {
        // Arrange
        var hostProbe = new HostProbe();
        var desktopMode = new Mock<IDesktopMode>(MockBehavior.Strict);
        var coordinator = new Mock<IDesktopSingleInstanceCoordinator>(MockBehavior.Strict);
        var app = CreateApp(hostProbe, desktopMode.Object, coordinator.Object);

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
    public async Task ShouldStopHostAndPropagateFailure_WhenRuntimeInfoThrowsAfterStartup()
    {
        // Arrange
        var hostProbe = new HostProbe();
        var desktopMode = new Mock<IDesktopMode>(MockBehavior.Strict);
        var coordinator = new Mock<IDesktopSingleInstanceCoordinator>(MockBehavior.Strict);
        var runtimeInfo = new Mock<IAppRuntimeInfo>(MockBehavior.Strict);
        var failure = new InvalidOperationException("runtime-info-unavailable");
        await using var app = CreateApp(hostProbe, desktopMode.Object, coordinator.Object, runtimeInfo.Object);

        runtimeInfo.SetupGet(x => x.IsDesktopEmbeddedDisabled).Throws(failure).Verifiable(Times.Once());

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => Program.RunDesktopLifecycleAsync(app, CancellationToken)
        );

        // Assert
        exception.ShouldBeSameAs(failure);
        hostProbe.StartCalls.ShouldBe(1);
        hostProbe.StopCalls.ShouldBe(1);
        app.Lifetime.ApplicationStopped.IsCancellationRequested.ShouldBeTrue();
        coordinator.Verify();
        runtimeInfo.Verify();
        coordinator.Verify(
            x => x.StartListener(It.IsAny<Func<CancellationToken, Task<Result>>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        desktopMode.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
        desktopMode.Verify(x => x.ShowMainWindowAsync(It.IsAny<CancellationToken>()), Times.Never);
        desktopMode.Verify(x => x.WaitForExitAsync(It.IsAny<CancellationToken>()), Times.Never);
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

    private static WebApplication CreateStartupApp(
        HostProbe hostProbe,
        IDesktopSingleInstanceCoordinator coordinator,
        IConfigManager configManager,
        IReaparrDbContextManager databaseManager)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IHostedService>(hostProbe);
        builder.Services.AddSingleton(coordinator);
        builder.Services.AddSingleton(configManager);
        builder.Services.AddSingleton(databaseManager);
        return builder.Build();
    }

    private static WebApplication CreateApp(
        HostProbe hostProbe,
        IDesktopMode desktopMode,
        IDesktopSingleInstanceCoordinator coordinator,
        IAppRuntimeInfo? appRuntimeInfo = null
    )
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IHostedService>(hostProbe);
        builder.Services.AddSingleton(desktopMode);
        builder.Services.AddSingleton(coordinator);
        builder.Services.AddSingleton<IAppRuntimeInfo>(appRuntimeInfo ?? new MockAppRuntimeInfo());
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
