namespace Reaparr.Domain;

/// <summary>
/// Describes observed server or connection health. Recorded alongside connection-check outcomes
/// to distinguish reachability from whether the check itself completed successfully.
/// </summary>
public enum TelemetryHealth
{
    Healthy,
    Unhealthy,
    Unknown,
}
