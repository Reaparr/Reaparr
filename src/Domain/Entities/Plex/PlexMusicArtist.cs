namespace Reaparr.Domain;

public class PlexMusicArtist : BasePlexMedia
{
    public override PlexMediaType Type => PlexMediaType.MusicArtist;

    public string? MusicBrainzArtistId { get; set; }

    public ICollection<PlexMusicAlbum> Albums { get; set; } = [];
}
