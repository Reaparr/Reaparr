using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Reaparr.AppHost;

/// <summary>Manages the Photino desktop window lifecycle.</summary>
public class DesktopMode : IDesktopMode
{
    private const int DESKTOP_READY_TIMEOUT_SECONDS = 5;

    private readonly Serilog.ILogger _log;
    private readonly IAppRuntimeInfo _appRuntimeInfo;
    private readonly IServer _server;
    private readonly Func<Uri, IDesktopWindow> _windowFactory;
    private readonly TaskCompletionSource _exitCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TaskCompletionSource? _desktopReadyCompletion;

    private IDesktopWindow? _window;
    private bool _isClosingToBackground;
    private bool _isExiting;
    private bool _browserFallbackLaunched;

    /// <summary>Initializes a new instance of <see cref="DesktopMode"/>.</summary>
    public DesktopMode(
        Serilog.ILogger log,
        IAppRuntimeInfo appRuntimeInfo,
        IServer server,
        Func<Uri, IDesktopWindow> windowFactory
    )
    {
        _log = log.ForContext<DesktopMode>();
        _appRuntimeInfo = appRuntimeInfo;
        _server = server;
        _windowFactory = windowFactory;
    }

    /// <inheritdoc />
    public async Task<Result> StartAsync(CancellationToken cancellationToken)
    {
        if (_appRuntimeInfo.IsIntegrationTestMode)
        {
            _log.Here()
                .Warning("DesktopMode startup skipped in integration test mode to avoid launching the embedded window");
            return Result.Ok();
        }

        _log.Here().Information("Starting DesktopMode");
        return await ShowMainWindowAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> ShowMainWindowAsync(CancellationToken cancellationToken)
    {
        if (_appRuntimeInfo.IsIntegrationTestMode)
            return Task.FromResult(Result.Ok());

        if (_window is not null)
        {
            _window.RestoreFromBackground();
            return Task.FromResult(Result.Ok());
        }

        var uriResult = GetReaparrUri();
        if (uriResult.IsFailed)
            return Task.FromResult(uriResult.ToResult());

        _log.Here().Information("Opening Reaparr desktop window at {Uri}", uriResult.Value);

        var window = _windowFactory(uriResult.Value);
        var desktopReadyCompletion = ResetDesktopReadyCompletion();

        _window = window;
        _window.ConfigureWindow();

        _log.Here().Information("Desktop window initialized and navigation requested");
        _window.RegisterWindowClosingHandler(OnWindowClosing);
        _window.RegisterDesktopMessageHandler(message =>
            HandleDesktopMessages(window, desktopReadyCompletion, message)
        );

        _browserFallbackLaunched = false;
        _ = MonitorDesktopReadyTimeoutAsync(uriResult.Value, window, desktopReadyCompletion);

        return Task.FromResult(Result.Ok());
    }

    /// <inheritdoc />
    public Task<Result> CloseMainWindowAsync(CancellationToken cancellationToken)
    {
        if (_window is null)
            return Task.FromResult(Result.Ok());

        _isClosingToBackground = true;

        try
        {
            _window.CloseToBackground();
            _window = null;
            return Task.FromResult(Result.Ok());
        }
        finally
        {
            _isClosingToBackground = false;
        }
    }

    /// <inheritdoc />
    public Task<Result> ExitAsync(CancellationToken cancellationToken)
    {
        _isExiting = true;
        _window?.CloseNativeWindow();
        _window?.DisposeWindow();
        _window = null;
        ClearDesktopReadyCompletion();
        _exitCompletion.TrySetResult();
        return Task.FromResult(Result.Ok());
    }

    /// <inheritdoc />
    public void OpenExternalBrowser(Uri uri)
    {
        _window?.OpenExternalBrowser(uri);
    }

    /// <inheritdoc />
    public async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        if (_appRuntimeInfo.IsIntegrationTestMode)
        {
            await _exitCompletion.Task.WaitAsync(cancellationToken);
            return;
        }

        _window?.WaitForClose();
        await _exitCompletion.Task.WaitAsync(cancellationToken);
    }

    private bool OnWindowClosing(object? sender, EventArgs args)
    {
        if (_isExiting || _isClosingToBackground)
            return false;

        _log.Here().Debug("Closing the Reaparr desktop window to the background");

        _window?.DisposeWindow();
        _window = null;
        return false;
    }

    private void HandleDesktopMessages(
        IDesktopWindow window,
        TaskCompletionSource desktopReadyCompletion,
        DesktopMessageDTO message
    )
    {
        if (!ReferenceEquals(_window, window))
        {
            _log.Here().Warning("Received desktop message for a stale desktop window session: {@Message}", message);
            return;
        }

        switch (message.Type)
        {
            case DesktopMessageType.None:
                break;
            case DesktopMessageType.ExternalLink:
                window.OpenExternalBrowser(new Uri(message.Value));
                break;
            case DesktopMessageType.DesktopReady:
                _log.Here().Information("Reaparr desktop UI reported ready");
                desktopReadyCompletion.TrySetResult();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(message), message.Type, null);
        }
    }

    private Result<Uri> GetReaparrUri()
    {
        if (_appRuntimeInfo.IsDevelopmentEnvironment)
            return Result.Ok(new Uri("http://localhost:3000"));

        var serverAddressesFeature = _server.Features.Get<IServerAddressesFeature>();
        var serverAddress = serverAddressesFeature?.Addresses.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(serverAddress))
            return Result.Fail("Desktop mode could not determine the server address for the embedded window");

        var uri = new Uri(serverAddress);
        return Result.Ok(NormalizeWildcardHost(uri));
    }

    private async Task MonitorDesktopReadyTimeoutAsync(
        Uri uri,
        IDesktopWindow window,
        TaskCompletionSource desktopReadyCompletion
    )
    {
        if (_appRuntimeInfo.IsIntegrationTestMode)
            return;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(DESKTOP_READY_TIMEOUT_SECONDS));
        try
        {
            await desktopReadyCompletion.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            _log.Here()
                .Warning(
                    "Desktop UI did not report ready within {TimeoutSeconds}s after loading {Uri}. Embedded WebView may have failed to render. Launching external browser fallback now",
                    DESKTOP_READY_TIMEOUT_SECONDS,
                    uri
                );

            LaunchBrowserFallback(uri, window);
        }
        finally
        {
            Interlocked.CompareExchange(ref _desktopReadyCompletion, null, desktopReadyCompletion);
        }
    }

    private TaskCompletionSource ResetDesktopReadyCompletion()
    {
        var desktopReadyCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _desktopReadyCompletion, desktopReadyCompletion);
        return desktopReadyCompletion;
    }

    private void ClearDesktopReadyCompletion()
    {
        Interlocked.Exchange(ref _desktopReadyCompletion, null);
    }

    private void LaunchBrowserFallback(Uri uri, IDesktopWindow window)
    {
        if (_browserFallbackLaunched)
            return;

        if (!ReferenceEquals(_window, window))
            return;

        _browserFallbackLaunched = true;

        try
        {
            window.OpenExternalBrowser(uri);
            _log.Here()
                .Information("Launched external browser fallback to {Uri} after embedded desktop render timeout", uri);
        }
        catch (Exception ex)
        {
            _log.Here().Error(ex, "Failed to launch external browser fallback to {Uri}", uri);
        }
    }

    private static Uri NormalizeWildcardHost(Uri uri)
    {
        if (uri.Host is not ("0.0.0.0" or "::" or "[::]"))
            return uri;

        var builder = new UriBuilder(uri) { Host = "localhost" };
        return builder.Uri;
    }
}
