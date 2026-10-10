namespace Reaparr.Domain;

public class PlexMusicArtist : BasePlexMedia
{
    public override PlexMediaType Type => PlexMediaType.MusicArtist;

    public string? MusicBrainzArtistId { get; set; }

    public ICollection<PlexMusicAlbum> Albums { get; set; } = [];

    public ICollection<PlexActor> Actors { get; init; } = [];

    public ICollection<PlexGenre> Genres { get; init; } = [];

    public ICollection<PlexCountry> Countries { get; init; } = [];
}
