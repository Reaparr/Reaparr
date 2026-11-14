namespace Reaparr.Application.Contracts;

/// <summary>
/// Server scope and requested refresh options projected by the server media-sync slice for telemetry.
/// </summary>
/// <remarks>Describes the request across the server's libraries, not completed work; the result is passed separately.</remarks>
public sealed record ServerMediaSyncTelemetryDTO
{
    /// <summary>The internal Reaparr ID of the server whose libraries were requested for sync.</summary>
    public required int PlexServerId { get; init; }
    /// <summary>Whether the request asks to bypass the normal library-sync freshness window.</summary>
    public required bool ForceLibrarySync { get; init; }
    /// <summary>Whether the request asks the library syncs to force a media metadata refresh.</summary>
    public required bool ForceMediaRefresh { get; init; }
}
