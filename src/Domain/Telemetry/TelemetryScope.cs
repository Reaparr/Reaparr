namespace Reaparr.Domain;

/// <summary>
/// Identifies the target grouping of an operation, such as a server or a selection. Passed through
/// telemetry calls to describe which set of resources an event applies to.
/// </summary>
public enum TelemetryScope
{
    Server,
    Selection,
    Account,
    Library,
}
