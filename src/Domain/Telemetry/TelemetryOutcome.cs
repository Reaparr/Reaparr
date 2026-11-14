namespace Reaparr.Domain;

/// <summary>
/// Classifies the business outcome of an instrumented operation. Used by the telemetry facade
/// to describe operation results consistently across event families.
/// </summary>
public enum TelemetryOutcome
{
    Succeeded,
    Rejected,
    Cancelled,
    Failed,
    Partial,
    NoOp,
    Skipped,
    Accepted,
    Unsupported,
    NoUpdate,
    LockedOut,
    NotRequired,
}
