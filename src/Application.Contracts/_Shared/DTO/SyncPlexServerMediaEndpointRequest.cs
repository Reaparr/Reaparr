namespace Reaparr.Application.Contracts;

public record SyncPlexServerMediaEndpointRequest
{
    [RouteParam]
    public int PlexServerId { get; init; }

    public bool ForceLibrarySync { get; init; }

    public bool ForceMediaRefresh { get; init; }
}
