namespace Reaparr.Domain;

/// <summary>
/// Defines keys for publishing current user settings as PostHog person properties. The telemetry
/// facade uses the prefix and specialized names when constructing settings profile updates.
/// </summary>
public static class TelemetryProfileProperty
{
    public const string SettingsPrefix = "settings.";
    public const string HeaderAuthentication = SettingsPrefix + "authenticationSettings.headerAuthentication";
}
