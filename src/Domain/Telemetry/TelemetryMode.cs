namespace Reaparr.Domain;

/// <summary>
/// Distinguishes execution modes within an event family. Used for indexer search and RSS activity,
/// and for connection checks using stored or explicitly supplied integration credentials.
/// </summary>
public enum TelemetryMode
{
    Search,
    Rss,
    Stored,
    Explicit,
}
