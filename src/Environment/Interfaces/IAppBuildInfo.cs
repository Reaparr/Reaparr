namespace Reaparr.Environment;

/// <summary>
/// Provides build-time and assembly-stamped metadata for the running Reaparr application.
/// </summary>
public interface IAppBuildInfo
{
    /// <summary>
    /// Gets the application runtime mode stamped into the build metadata, for example <c>desktop</c> or <c>docker</c>.
    /// </summary>
    string RuntimeMode { get; }

    /// <summary>
    /// Gets the runtime identifier stamped into the build metadata, for example <c>linux-arm64</c>.
    /// </summary>
    string RuntimeIdentifier { get; }

    /// <summary>
    /// Gets the embedded telemetry ingestion host, or an empty string when telemetry is not configured.
    /// </summary>
    string TelemetryHost { get; }

    /// <summary>
    /// Gets the embedded public telemetry project ingestion token, or an empty string when telemetry is not configured.
    /// </summary>
    string TelemetryProjectToken { get; }

    /// <summary>
    /// Gets the application release version from the entry assembly version metadata.
    /// This should be the stable product version, for example <c>0.36.1</c>.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Gets the application informational version from the entry assembly informational version metadata.
    /// This is typically a more detailed build string than <c>Version</c>, for example <c>0.36.0-dev.1</c>.
    /// </summary>
    string InformationalVersion { get; }

    /// <summary>
    /// Returns true if the application runtime mode is desktop.
    /// </summary>
    bool IsDesktopMode { get; }

    /// <summary>
    /// Returns true if the application runtime mode is docker.
    /// </summary>
    bool IsDockerMode { get; }

    /// <summary>
    /// Returns true if the current version indicates a development build (contains <c>dev</c>).
    /// </summary>
    bool IsDevRelease { get; }

    /// <summary>
    /// Gets the current operating system platform as an <see cref="OperatingSystemPlatform"/> enum value.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    OperatingSystemPlatform CurrentOS { get; }

    /// <summary>
    /// Returns true if the current operating system is Windows.
    /// </summary>
    bool IsWindows { get; }

    /// <summary>
    /// Returns true if the current operating system is Linux.
    /// </summary>
    bool IsLinux { get; }

    /// <summary>
    /// Returns true if the current operating system is macOS.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    bool IsMacOS { get; }

    bool HasValidTelemetry { get; }
}
