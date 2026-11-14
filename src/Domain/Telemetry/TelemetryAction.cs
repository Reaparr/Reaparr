namespace Reaparr.Domain;

/// <summary>
/// Classifies the action associated with a business event. Passed to the telemetry facade to distinguish
/// operations on the same resource without defining a separate event for each action.
/// </summary>
public enum TelemetryAction
{
    Create,
    Update,
    Delete,
    Start,
    Pause,
    Stop,
    Restart,
    Resume,
    Login,
    Logout,
    Owned,
    Enabled,
    Disabled,
    Alias,
    Preferred,
    DefaultDestination,
}
