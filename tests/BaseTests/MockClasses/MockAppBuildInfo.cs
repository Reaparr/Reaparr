namespace Reaparr.BaseTests;

public class MockAppBuildInfo : IAppBuildInfo
{
    /// <inheritdoc/>
    public string RuntimeMode { get; set; } = "docker";

    /// <inheritdoc/>
    public string RuntimeIdentifier { get; set; } = "linux-x64";

    /// <inheritdoc/>
    public string TelemetryHost { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string TelemetryProjectToken { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string Version { get; set; } = "0.0.0";

    /// <inheritdoc/>
    public string InformationalVersion { get; set; } = "0.0.0";

    /// <inheritdoc/>
    public bool IsDesktopMode => string.Equals(RuntimeMode, "desktop", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public bool IsDockerMode => string.Equals(RuntimeMode, "docker", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public bool IsDevRelease => InformationalVersion.Contains("dev", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public OperatingSystemPlatform CurrentOS { get; set; } = OperatingSystemPlatform.Linux;

    /// <inheritdoc/>
    public bool IsWindows => CurrentOS == OperatingSystemPlatform.Windows;

    public bool IsLinux => CurrentOS == OperatingSystemPlatform.Linux;

    public bool IsMacOS => CurrentOS == OperatingSystemPlatform.Osx;

    public bool HasValidTelemetry =>
        !string.IsNullOrEmpty(TelemetryHost) && !string.IsNullOrEmpty(TelemetryProjectToken);
}
