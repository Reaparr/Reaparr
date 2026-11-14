namespace Reaparr.Domain;

/// <summary>
/// Identifies the Plex authentication flow being attempted. Used in authentication telemetry
/// to distinguish token generation, credential validation, and token validation.
/// </summary>
public enum TelemetryAuthenticationMethod
{
    GenerateToken,
    Credentials,
    Token,
}
