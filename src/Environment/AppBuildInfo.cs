using System.Reflection;

namespace Reaparr.Environment;

/// <inheritdoc/>
public sealed class AppBuildInfo : IAppBuildInfo
{
    private readonly List<AssemblyMetadataAttribute> _attributes;

    /// <inheritdoc/>
    public string Version { get; }

    /// <inheritdoc/>
    public string InformationalVersion { get; }

    /// <inheritdoc/>
    public string RuntimeMode => GetAssemblyMetadataValue("ReaparrRuntimeMode");

    /// <inheritdoc/>
    public string RuntimeIdentifier => GetAssemblyMetadataValue("ReaparrRuntimeIdentifier");

    /// <inheritdoc/>
    public bool IsDesktopMode => string.Equals(RuntimeMode, "desktop", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public bool IsDockerMode => string.Equals(RuntimeMode, "docker", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public bool IsDevRelease => InformationalVersion.Contains("dev", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public OperatingSystemPlatform CurrentOS
    {
        get
        {
            if (OperatingSystem.IsWindows())
                return OperatingSystemPlatform.Windows;

            if (OperatingSystem.IsMacOS())
                return OperatingSystemPlatform.Osx;

            if (OperatingSystem.IsLinux())
                return OperatingSystemPlatform.Linux;

            return OperatingSystemPlatform.Unknown;
        }
    }

    /// <inheritdoc/>
    public bool IsWindows => OperatingSystem.IsWindows();

    /// <inheritdoc/>
    public bool IsLinux => OperatingSystem.IsLinux();

    /// <inheritdoc/>
    public bool IsMacOS => OperatingSystem.IsMacOS();

    /// <summary>
    /// Initializes a new instance of <see cref="AppBuildInfo"/> by reading build metadata from the entry assembly.
    /// </summary>
    public AppBuildInfo()
    {
        var assembly = Assembly.GetEntryAssembly();
        if (assembly is null)
            throw new InvalidOperationException("Unable to determine entry assembly.");

        _attributes = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToList();
        Version = assembly.GetName().Version?.ToString() ?? "0.0.0";
        InformationalVersion =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
    }

    private string GetAssemblyMetadataValue(string key, string fallback = "unknown") =>
        _attributes.FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))?.Value
        ?? fallback;
}
