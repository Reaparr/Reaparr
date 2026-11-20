using Autofac;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;

namespace Reaparr.AppHost.UnitTests;

public class DesktopModeUnitTests : BaseUnitTest<DesktopMode>
{
    [Test]
    public async Task ShouldReturnFailure_WhenServerAddressIsMissingInProduction()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsProductionEnvironment = true);

        SetAppBuildInfo(x => x.RuntimeMode = "desktop");

        var windowFactory = new FakeDesktopWindowFactory(new MockDesktopWindow());
        var server = CreateServer(null);
        var sut = CreateSut(server, _ => windowFactory.Create());

        // Act
        var result = await sut.StartAsync(CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x =>
            x.Message.Contains("could not determine the server address", StringComparison.OrdinalIgnoreCase)
        );
        windowFactory.CreateCalls.ShouldBe(0);
    }

    [Test]
    public async Task ShouldSkipWindowCreation_WhenIntegrationTestModeIsEnabled()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsIntegrationTestMode = true);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");

        var windowFactory = new FakeDesktopWindowFactory(new MockDesktopWindow());
        var server = CreateServer("http://localhost:5000");
        var sut = CreateSut(server, _ => windowFactory.Create());

        // Act
        var startResult = await sut.StartAsync(CancellationToken);
        var showResult = await sut.ShowMainWindowAsync(CancellationToken);

        // Assert
        startResult.IsSuccess.ShouldBeTrue();
        showResult.IsSuccess.ShouldBeTrue();
        windowFactory.CreateCalls.ShouldBe(0);
    }

    [Test]
    public async Task ShouldKeepDesktopRuntimeAlive_WhenMainWindowIsClosed()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsProductionEnvironment = true);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");

        var window = new MockDesktopWindow();
        var windowFactory = new FakeDesktopWindowFactory(window);
        var server = CreateServer("http://localhost:5000");
        var sut = CreateSut(server, _ => windowFactory.Create());

        await sut.StartAsync(CancellationToken);
        var waitForExitTask = sut.WaitForExitAsync(CancellationToken);

        // Act
        var result = await sut.CloseMainWindowAsync(CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        waitForExitTask.IsCompleted.ShouldBeFalse();
        window.IsClosedToBackground.ShouldBeTrue();
        window.NativeClosePrevented.ShouldBeFalse();
        window.IsDisposed.ShouldBeFalse();

        await sut.ExitAsync(CancellationToken);
        await waitForExitTask;
    }

    [Test]
    public async Task ShouldStartWindowMessageLoop_WhenWaitingForExit()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsProductionEnvironment = true);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");

        var window = new MockDesktopWindow();
        var windowFactory = new FakeDesktopWindowFactory(window);
        var server = CreateServer("http://localhost:5000");
        var sut = CreateSut(server, _ => windowFactory.Create());

        // Act
        var startResult = await sut.StartAsync(CancellationToken);
        window.WaitForCloseCalls.ShouldBe(0);
        var waitForExitTask = sut.WaitForExitAsync(CancellationToken);
        await window.MessageLoopStarted.Task.WaitAsync(CancellationToken);
        var exitResult = await sut.ExitAsync(CancellationToken);
        await waitForExitTask;

        // Assert
        startResult.IsSuccess.ShouldBeTrue();
        exitResult.IsSuccess.ShouldBeTrue();
        window.WaitForCloseCalls.ShouldBe(1);
    }

    [Test]
    public async Task ShouldAllowNativeClose_WhenExitClosesNativeWindow()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsProductionEnvironment = true);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");

        var window = new MockDesktopWindow();
        var windowFactory = new FakeDesktopWindowFactory(window);
        var server = CreateServer("http://localhost:5000");
        var sut = CreateSut(server, _ => windowFactory.Create());

        await sut.StartAsync(CancellationToken);

        // Act
        var result = await sut.ExitAsync(CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        window.NativeClosePrevented.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldRegisterExternalLinkHandler_WhenMainWindowStarts()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsProductionEnvironment = true);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");

        var window = new MockDesktopWindow();
        var windowFactory = new FakeDesktopWindowFactory(window);
        var server = CreateServer("http://localhost:5000");
        var sut = CreateSut(server, _ => windowFactory.Create());

        // Act
        var result = await sut.StartAsync(CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        window.ExternalLinkHandler.ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldOpenExternalBrowser_WhenExternalLinkMessageIsReceived()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsProductionEnvironment = true);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");

        var window = new MockDesktopWindow();
        var windowFactory = new FakeDesktopWindowFactory(window);
        var server = CreateServer("http://localhost:5000");
        var sut = CreateSut(server, _ => windowFactory.Create());

        await sut.StartAsync(CancellationToken);

        // Act
        window.ExternalLinkHandler!.Invoke(
            new DesktopMessageDTO
            {
                Type = DesktopMessageType.ExternalLink,
                Value = "https://github.com/Reaparr/Reaparr",
            }
        );

        // Assert
        window.OpenedExternalUrls.ShouldBe([new Uri("https://github.com/Reaparr/Reaparr")]);
    }

    [Test]
    public async Task ShouldAcceptDesktopReadyMessage_WhenMainWindowStarts()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsProductionEnvironment = true);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");

        var window = new MockDesktopWindow();
        var windowFactory = new FakeDesktopWindowFactory(window);
        var server = CreateServer("http://localhost:5000");
        var sut = CreateSut(server, _ => windowFactory.Create());

        await sut.StartAsync(CancellationToken);

        // Act
        window.ExternalLinkHandler!.Invoke(
            new DesktopMessageDTO { Type = DesktopMessageType.DesktopReady, Value = "ready" }
        );

        // Assert
        window.OpenedExternalUrls.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldOpenExternalBrowserFallback_WhenDesktopReadyIsNotReportedWithinTimeout()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsProductionEnvironment = true);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");

        var window = new MockDesktopWindow();
        var windowFactory = new FakeDesktopWindowFactory(window);
        var server = CreateServer("http://localhost:5000");
        var sut = CreateSut(server, _ => windowFactory.Create());

        // Act
        var startResult = await sut.StartAsync(CancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(6), CancellationToken);

        // Assert
        startResult.IsSuccess.ShouldBeTrue();
        window.OpenedExternalUrls.ShouldContain(new Uri("http://localhost:5000/"));

        await sut.ExitAsync(CancellationToken);
    }

    [Test]
    public async Task ShouldUseFreshDesktopReadyCompletion_WhenMainWindowReopens()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsProductionEnvironment = true);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");

        var firstWindow = new MockDesktopWindow();
        var secondWindow = new MockDesktopWindow();
        var windowFactory = new FakeDesktopWindowFactory(firstWindow, secondWindow);
        var server = CreateServer("http://localhost:5000");
        var sut = CreateSut(server, _ => windowFactory.Create());

        await sut.StartAsync(CancellationToken);
        await sut.CloseMainWindowAsync(CancellationToken);

        // Act
        var showResult = await sut.ShowMainWindowAsync(CancellationToken);
        firstWindow.ExternalLinkHandler!.Invoke(
            new DesktopMessageDTO { Type = DesktopMessageType.DesktopReady, Value = "ready" }
        );
        await Task.Delay(TimeSpan.FromSeconds(6), CancellationToken);

        // Assert
        showResult.IsSuccess.ShouldBeTrue();
        windowFactory.CreateCalls.ShouldBe(2);
        firstWindow.OpenedExternalUrls.ShouldBeEmpty();
        secondWindow.OpenedExternalUrls.ShouldContain(new Uri("http://localhost:5000/"));

        await sut.ExitAsync(CancellationToken);
    }

    [Test]
    public async Task ShouldCompleteDesktopRuntime_WhenExitCommandRuns()
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsProductionEnvironment = true);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");

        var windowFactory = new FakeDesktopWindowFactory(new MockDesktopWindow());
        var server = CreateServer("http://localhost:5000");
        var sut = CreateSut(server, _ => windowFactory.Create());

        await sut.StartAsync(CancellationToken);
        var waitForExitTask = sut.WaitForExitAsync(CancellationToken);

        // Act
        var result = await sut.ExitAsync(CancellationToken);
        await waitForExitTask;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        waitForExitTask.IsCompleted.ShouldBeTrue();
    }

    private DesktopMode CreateSut(IServer server, Func<Uri, IDesktopWindow> windowFactory) =>
        Mock.Create<DesktopMode>(
            new TypedParameter(typeof(ILogger), Log),
            new TypedParameter(typeof(IServer), server),
            new TypedParameter(typeof(Func<Uri, IDesktopWindow>), windowFactory)
        );

    private static IServer CreateServer(string? address)
    {
        var featureCollection = new FeatureCollection();
        var serverAddresses = new ServerAddressesFeature();
        if (!string.IsNullOrWhiteSpace(address))
            serverAddresses.Addresses.Add(address);

        featureCollection.Set<IServerAddressesFeature>(serverAddresses);

        var server = new Mock<IServer>(MockBehavior.Strict);
        server.SetupGet(x => x.Features).Returns(featureCollection);
        return server.Object;
    }

    private sealed class FakeDesktopWindowFactory
    {
        private readonly Queue<MockDesktopWindow> _windows;

        public FakeDesktopWindowFactory(params MockDesktopWindow[] windows)
        {
            _windows = new Queue<MockDesktopWindow>(windows);
        }

        public int CreateCalls { get; private set; }

        public IDesktopWindow Create()
        {
            CreateCalls++;
            return _windows.Dequeue();
        }
    }
}
