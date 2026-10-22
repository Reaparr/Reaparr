namespace Reaparr.Domain;

public class PlexMusicArtist : BasePlexMedia
{
    public override PlexMediaType Type => PlexMediaType.MusicArtist;

    [Column(Order = 10)]
    public required string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Native Plex artist identifier normalized from Plex metadata.
    /// Stores the lowercase identifier suffix without a Plex URI prefix or library-local <see cref="BasePlexMedia.PlexApiRatingKey"/>.
    /// Null when Plex does not provide a valid native artist identifier.
    /// </summary>
    public string? PlexApiIdentityId { get; set; }

    public string? MusicBrainzArtistId { get; set; }

    public ICollection<PlexMusicAlbum> Albums { get; set; } = [];

    public ICollection<PlexMusicArtistMediaQuality> Qualities { get; set; } = [];

    public ICollection<PlexActor> Actors { get; init; } = [];

    public ICollection<PlexGenreAudio> Genres { get; init; } = [];

    public ICollection<PlexCountry> Countries { get; init; } = [];
}
