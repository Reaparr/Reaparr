namespace Reaparr.Application.Contracts;

/// <summary>
/// Library scope and requested refresh options projected by the library-refresh slice for telemetry.
/// </summary>
/// <remarks>Describes the request, not whether work was queued or completed; the result is passed separately.</remarks>
public sealed record LibraryRefreshTelemetryDTO
{
    /// <summary>The internal Reaparr ID of the library requested for refresh.</summary>
    public required int PlexLibraryId { get; init; }

    /// <summary>Whether the request asks to bypass the normal library-sync freshness window.</summary>
    public required bool ForceLibrarySync { get; init; }

    /// <summary>Whether the request asks the sync to force a media metadata refresh.</summary>
    public required bool ForceMediaRefresh { get; init; }
}
