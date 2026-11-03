namespace Reaparr.Environment;

public sealed class DesignTimeAppBuildInfo : IAppBuildInfo
{
    public string RuntimeMode => "desktop";

    public string RuntimeIdentifier => "design-time";

    public string TelemetryHost => string.Empty;

    public string TelemetryProjectToken => string.Empty;

    public string Version => "0.0.0";

    public string InformationalVersion => "0.0.0-design-time";

    public bool IsDesktopMode => true;

    public bool IsDockerMode => false;

    public bool IsDevRelease => true;

    public OperatingSystemPlatform CurrentOS =>
        true switch
        {
            _ when OperatingSystem.IsWindows() => OperatingSystemPlatform.Windows,
            _ when OperatingSystem.IsMacOS() => OperatingSystemPlatform.Osx,
            _ when OperatingSystem.IsLinux() => OperatingSystemPlatform.Linux,
            _ => OperatingSystemPlatform.Unknown,
        };

    public bool IsWindows => OperatingSystem.IsWindows();

    public bool IsLinux => OperatingSystem.IsLinux();

    public bool IsMacOS => OperatingSystem.IsMacOS();
}
