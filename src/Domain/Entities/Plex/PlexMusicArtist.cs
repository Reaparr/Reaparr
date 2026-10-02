namespace Reaparr.Domain;

public class PlexMusicArtist : BasePlexMedia
{
    public string? MusicBrainzArtistId { get; set; }

    public ICollection<PlexMusicAlbum> Albums { get; set; } = [];
}
