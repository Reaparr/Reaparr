namespace Reaparr.Application.Contracts;

/// <summary>
/// Authentication validation state projected for telemetry without account details, passwords or tokens.
/// </summary>
/// <remarks>The original operation result is passed separately for failure and cancellation classification.</remarks>
public sealed record PlexAuthenticationTelemetryDTO
{
    /// <summary>
    /// Whether Plex validated the authentication payload; false remains a rejection even in a successful result envelope.
    /// </summary>
    public required bool IsValidated { get; init; }
}
