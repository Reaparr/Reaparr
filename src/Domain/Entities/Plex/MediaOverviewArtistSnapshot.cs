namespace Reaparr.Domain;

public class MediaOverviewArtistSnapshot : BaseMediaOverviewSnapshot
{
    public required int PlexArtistId { get; set; }

    public PlexArtist? PlexArtist { get; set; }
}
