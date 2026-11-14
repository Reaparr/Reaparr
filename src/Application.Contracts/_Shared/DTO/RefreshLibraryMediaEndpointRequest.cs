namespace Reaparr.Application.Contracts;

public record RefreshLibraryMediaEndpointRequest
{
    [RouteParam]
    public int PlexLibraryId { get; init; }

    public bool ForceLibrarySync { get; init; }

    public bool ForceMediaRefresh { get; init; }
}
