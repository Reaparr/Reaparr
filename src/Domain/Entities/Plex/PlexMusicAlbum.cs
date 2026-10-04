namespace Reaparr.Domain;

public class PlexMusicAlbum : BasePlexMedia
{
    public override PlexMediaType Type => PlexMediaType.Album;

    public required int PlexArtistId { get; set; }

    public PlexMusicArtist? PlexArtist { get; set; }

    public string? MusicBrainzReleaseId { get; set; }

    public string? MusicBrainzReleaseGroupId { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public string? RecordLabel { get; set; }

    public string? Country { get; set; }

    public int? DiscCount { get; set; }

    public int? TrackCount { get; set; }

    public ICollection<PlexMusicTrack> Tracks { get; set; } = [];
}
