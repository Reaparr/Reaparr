
namespace Reaparr.Build;

internal static class DesktopRuntimeCatalog
{
    private static readonly IReadOnlyDictionary<string, DesktopRuntime> _runtimes = new Dictionary<
        string,
        DesktopRuntime
    >(StringComparer.OrdinalIgnoreCase)
    {
        ["linux-x64"] = new("linux-x64", "Desktop-linux-x64", "Reaparr.AppHost"),
        ["linux-arm64"] = new("linux-arm64", "Desktop-linux-arm64", "Reaparr.AppHost"),
        ["win-x64"] = new("win-x64", "Desktop-win-x64", "Reaparr.AppHost.exe"),
        ["win-arm64"] = new("win-arm64", "Desktop-win-arm64", "Reaparr.AppHost.exe"),
        ["osx-x64"] = new("osx-x64", "Desktop-osx-x64", "Reaparr.AppHost"),
        ["osx-arm64"] = new("osx-arm64", "Desktop-osx-arm64", "Reaparr.AppHost"),
    };

    public static string SupportedRuntimeIdentifiers => string.Join(", ", _runtimes.Keys.Order());

    public static bool TryGet(string runtimeIdentifier, out DesktopRuntime runtime) =>
        _runtimes.TryGetValue(runtimeIdentifier, out runtime!);

    public static DesktopRuntime Get(string runtimeIdentifier)
    {
        if (string.IsNullOrWhiteSpace(runtimeIdentifier))
        {
            throw new ArgumentException(
                $"A runtime identifier is required. Pass --rid <RID>. Supported values: {SupportedRuntimeIdentifiers}.",
                nameof(runtimeIdentifier)
            );
        }

        if (TryGet(runtimeIdentifier, out var runtime))
        {
            return runtime;
        }

        throw new ArgumentOutOfRangeException(
            nameof(runtimeIdentifier),
            $"Unsupported desktop RID '{runtimeIdentifier}'. Supported values: {SupportedRuntimeIdentifiers}."
        );
    }
}
