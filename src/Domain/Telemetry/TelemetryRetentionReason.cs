namespace Reaparr.Domain;

/// <summary>
/// Explains a source-file retention decision. Passed to file-move telemetry alongside the effective
/// retention setting so the resulting move or cleanup behavior can be understood.
/// </summary>
public enum TelemetryRetentionReason
{
    Destination,
    TaskSetting,
    GlobalSetting,
    SamePath,
    MissingDestination,
}
