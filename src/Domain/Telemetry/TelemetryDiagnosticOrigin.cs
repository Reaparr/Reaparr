namespace Reaparr.Domain;

/// <summary>
/// Identifies whether a diagnostic originated in the backend or frontend. Supplied when sending
/// diagnostic telemetry so delivery checks can be attributed to their source.
/// </summary>
public enum TelemetryDiagnosticOrigin
{
    Backend = 0,
    Frontend = 1,
}
