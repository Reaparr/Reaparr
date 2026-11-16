using Spectre.Console.Cli;
using ValidationResult = Spectre.Console.ValidationResult;

namespace Reaparr.Build;

internal sealed class DesktopCommandSettings : CommandSettings
{
    [CommandOption("-r|--rid <RID>")]
    public string RuntimeIdentifier { get; init; } = string.Empty;

    [CommandOption("--version <VERSION>")]
    public string? Version { get; init; }

    [CommandOption("--informational-version <VERSION>")]
    public string? InformationalVersion { get; init; }

    [CommandOption("--telemetry-host <HOST>")]
    public string? TelemetryHost { get; init; }

    [CommandOption("--telemetry-project-token <TOKEN>")]
    public string? TelemetryProjectToken { get; init; }

    [CommandOption("--channel <CHANNEL>")]
    public string? Channel { get; init; }

    [CommandOption("--skip-frontend")]
    public bool SkipFrontend { get; init; }

    [CommandOption("--skip-restore")]
    public bool SkipRestore { get; init; }

    [CommandOption("--skip-package")]
    public bool SkipPackage { get; init; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; init; }

    [CommandOption("--frontend-public-dir <PATH>")]
    public string? FrontendPublicDirectory { get; init; }

    [CommandOption("--artifact-dir <PATH>")]
    public string? ArtifactDirectory { get; init; }

    [CommandOption("--preserve-existing-artifacts")]
    public bool PreserveExistingArtifacts { get; init; }

    [CommandOption("--launch-mode <MODE>")]
    public string LaunchMode { get; init; } = "published";

    private const string TELEMETRY_PROJECT_TOKEN_PREFIX = "phc_";

    private static bool IsValidTelemetryHost(string host) =>
        Uri.TryCreate(host, UriKind.Absolute, out var uri)
        && uri.IsWellFormedOriginalString()
        && uri is { Scheme: "https", Host.Length: > 0, UserInfo: "", Query: "", Fragment: "", AbsolutePath: "/" };

    private static bool IsValidTelemetryProjectToken(string projectToken)
    {
        if (!projectToken.StartsWith(TELEMETRY_PROJECT_TOKEN_PREFIX, StringComparison.Ordinal))
            return false;

        var tokenBody = projectToken[TELEMETRY_PROJECT_TOKEN_PREFIX.Length..];
        return tokenBody.Length > 0
            && tokenBody.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    }

    internal static string? GetTelemetryValidationError(string? host, string? projectToken)
    {
        var hasHost = !string.IsNullOrEmpty(host);
        var hasProjectToken = !string.IsNullOrEmpty(projectToken);

        if (!hasHost && !hasProjectToken)
            return null;

        if (!hasHost || !hasProjectToken)
            return "Telemetry host and project token must be supplied together.";

        if (!IsValidTelemetryHost(host!))
            return "Telemetry host must be an absolute HTTPS origin with an optional single trailing slash.";

        var suppliedProjectToken = projectToken!;

        if (!string.Equals(suppliedProjectToken, suppliedProjectToken.Trim(), StringComparison.Ordinal))
            return "Telemetry project token must not include leading or trailing whitespace.";

        return IsValidTelemetryProjectToken(suppliedProjectToken)
            ? null
            : "Telemetry project token must be a public phc_ project token containing only letters, digits, '-' or '_'.";
    }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(RuntimeIdentifier))
        {
            return ValidationResult.Error(
                $"A runtime identifier is required. Pass --rid <RID>. Supported values: {DesktopRuntimeCatalog.SupportedRuntimeIdentifiers}."
            );
        }

        if (!DesktopRuntimeCatalog.TryGet(RuntimeIdentifier, out _))
        {
            return ValidationResult.Error(
                $"Unsupported desktop RID '{RuntimeIdentifier}'. Supported values: {DesktopRuntimeCatalog.SupportedRuntimeIdentifiers}."
            );
        }

        if (string.IsNullOrWhiteSpace(Version))
        {
            return ValidationResult.Error("A build version is required. Pass --version <VERSION>.");
        }

        if (string.IsNullOrWhiteSpace(InformationalVersion))
        {
            return ValidationResult.Error(
                "An informational version is required. Pass --informational-version <VERSION>."
            );
        }
        var telemetryValidationError = GetTelemetryValidationError(TelemetryHost, TelemetryProjectToken);
        if (telemetryValidationError is not null)
            return ValidationResult.Error(telemetryValidationError);

        if (
            RuntimeIdentifier.StartsWith("linux-", StringComparison.OrdinalIgnoreCase)
            && !SkipPackage
            && !string.Equals(LaunchMode, "published", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(LaunchMode, "packaged", StringComparison.OrdinalIgnoreCase)
        )
        {
            return ValidationResult.Error(
                $"Unsupported launch mode '{LaunchMode}'. Supported values are 'published' and 'packaged'."
            );
        }

        return ValidationResult.Success();
    }
}
