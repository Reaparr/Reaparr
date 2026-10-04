namespace Reaparr.Domain;

public class PlexMusicTrack : BasePlexMedia
{
    public override PlexMediaType Type => PlexMediaType.Track;

    /// <summary>
    /// The Plex key of the <see cref="PlexTvShow"/> this belongs too.
    /// </summary>
    public required int ParentKey { get; set; }

    public required int PlexAlbumId { get; set; }

    public PlexMusicAlbum? PlexAlbum { get; set; }

    public string? MusicBrainzRecordingId { get; set; }

    public string? MusicBrainzReleaseTrackId { get; set; }

    public int? DiscNumber { get; set; }

    public int? TrackNumber { get; set; }

    public ICollection<PlexMusicTrackMediaData> MediaDataList { get; set; } = [];
}
