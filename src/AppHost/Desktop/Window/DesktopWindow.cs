using System.Diagnostics;
using System.Text.Json;
using Photino.NET;

namespace Reaparr.AppHost;

/// <summary>Wraps a Photino window.</summary>
public class DesktopWindow : IDesktopWindow
{
    private readonly Uri _uri;
    private readonly IAppBuildInfo _appBuildInfo;
    private bool _isInitialized;
    private PhotinoWindow? _window;

    /// <summary>Initializes a new instance of <see cref="DesktopWindow"/>.</summary>
    public DesktopWindow(Uri uri, IAppBuildInfo appBuildInfo)
    {
        _uri = uri;
        _appBuildInfo = appBuildInfo;
    }

    /// <inheritdoc />
    public void ConfigureWindow()
    {
        _window = new PhotinoWindow()
            .SetTitle("Reaparr - " + _appBuildInfo.InformationalVersion)
            .SetUseOsDefaultSize(true)
            .Center()
            .SetMinSize(1920, 1080)
            .SetMaximized(true)
            .SetResizable(true)
            .SetLogVerbosity(2)
            .Load(_uri);
        _isInitialized = true;
    }

    /// <inheritdoc />
    public void RegisterWindowClosingHandler(Func<object?, EventArgs, bool> handler)
    {
        _window?.RegisterWindowClosingHandler((sender, args) => handler(sender, args));
    }

    /// <inheritdoc />
    public void RegisterDesktopMessageHandler(Action<DesktopMessageDTO> handler)
    {
        _window?.RegisterWebMessageReceivedHandler(
            (_, message) =>
            {
                var msg = JsonSerializer.Deserialize<DesktopMessageDTO>(
                    message,
                    DefaultJsonSerializerOptions.ConfigStandard
                ) ?? new DesktopMessageDTO { Type = DesktopMessageType.None, Value = "" };
                handler(msg);
            }
        );
    }

    /// <inheritdoc />
    public void OpenExternalBrowser(Uri uri)
    {
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    /// <inheritdoc />
    public void CloseToBackground()
    {
        if (!IsInitialized)
            return;

        _window?.Close();
        _isInitialized = false;
    }

    /// <inheritdoc />
    public bool IsInitialized => _isInitialized;

    /// <inheritdoc />
    public void RestoreFromBackground()
    {
        if (_window is not null && IsInitialized)
            _window.Minimized = false;
    }

    /// <inheritdoc />
    public void CloseNativeWindow()
    {
        if (!IsInitialized)
            return;

        _window?.Close();
        _isInitialized = false;
    }

    /// <inheritdoc />
    public void DisposeWindow()
    {
        _window = null;
        _isInitialized = false;
    }

    /// <inheritdoc />
    public void WaitForClose()
    {
        if (_window is null)
            return;

        _window.WaitForClose();
    }
}
