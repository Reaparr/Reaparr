namespace Reaparr.Domain;

/// <summary>
/// Identifies what initiated an operation. Propagated through telemetry calls to distinguish
/// user-requested work from scheduled, recovery, and other system-initiated work.
/// </summary>
public enum TelemetryTrigger
{
    Manual,
    Scheduled,
    Recovery,
    System,
    AccessReconciled,
}
