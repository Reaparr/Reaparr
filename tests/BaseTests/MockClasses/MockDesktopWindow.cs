using Reaparr.AppHost;

namespace Reaparr.BaseTests;

public sealed class MockDesktopWindow : IDesktopWindow
{
    public bool IsClosedToBackground { get; private set; }
    public bool NativeClosePrevented { get; private set; }
    public bool IsDisposed { get; private set; }
    public int WaitForCloseCalls { get; private set; }
    public bool IsInitialized { get; private set; }
    public List<Uri> OpenedExternalUrls { get; } = [];

    public TaskCompletionSource CloseToBackgroundCompletion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource MessageLoopStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<Uri> ExternalBrowserOpened { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Func<object?, EventArgs, bool>? WindowClosingHandler { get; private set; }
    public Action<DesktopMessageDTO>? ExternalLinkHandler { get; private set; }

    public void ConfigureWindow() { }

    public void RegisterWindowClosingHandler(Func<object?, EventArgs, bool> handler)
    {
        WindowClosingHandler = handler;
    }

    public void RegisterDesktopMessageHandler(Action<DesktopMessageDTO> handler)
    {
        ExternalLinkHandler = handler;
    }

    public void OpenExternalBrowser(Uri uri)
    {
        OpenedExternalUrls.Add(uri);
        ExternalBrowserOpened.TrySetResult(uri);
    }

    public void CloseToBackground()
    {
        IsClosedToBackground = true;
        CloseToBackgroundCompletion.TrySetResult();
    }

    public void RestoreFromBackground() { }

    public void CloseNativeWindow()
    {
        NativeClosePrevented = WindowClosingHandler?.Invoke(this, EventArgs.Empty) ?? false;
        IsDisposed = true;
        IsInitialized = false;
    }

    public void DisposeWindow()
    {
        IsDisposed = true;
        IsInitialized = false;
    }

    public void WaitForClose()
    {
        WaitForCloseCalls++;
        IsInitialized = true;
        MessageLoopStarted.TrySetResult();
    }
}
