namespace Reaparr.Domain;

public class PlexMusicTrack : BasePlexMedia
{
    public required int PlexAlbumId { get; set; }

    public PlexMusicAlbum? PlexAlbum { get; set; }

    public string? MusicBrainzRecordingId { get; set; }

    public string? MusicBrainzReleaseTrackId { get; set; }

    public int? DiscNumber { get; set; }

    public int? TrackNumber { get; set; }

    public ICollection<PlexMusicTrackMediaData> MediaDataList { get; set; } = [];
}
