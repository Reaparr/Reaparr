namespace Reaparr.Domain;

public class PlexMusicAlbum : BasePlexMedia
{
    public override PlexMediaType Type => PlexMediaType.MusicAlbum;

    [Column(Order = 3)]
    public required int Year { get; set; }

    [Column(Order = 16)]
    public required DateTime? OriginallyAvailableAt { get; init; }

    [Column(Order = 10)]
    public required string Summary { get; init; } = string.Empty;

    [Column(Order = 12)]
    public required double Rating { get; init; }

    /// <summary>
    /// The Plex key of the <see cref="PlexTvShow"/> this belongs too.
    /// </summary>
    public required int ParentKey { get; set; }

    public required int PlexArtistId { get; set; }

    public PlexMusicArtist? PlexArtist { get; set; }

    /// <summary>
    /// Native Plex album identifier normalized from Plex metadata.
    /// Stores the lowercase identifier suffix without a Plex URI prefix or library-local <see cref="BasePlexMedia.PlexApiRatingKey"/>.
    /// Null when Plex does not provide a valid native album identifier.
    /// </summary>
    public required string? PlexApiIdentityId { get; set; }

    public required string? MusicBrainzReleaseId { get; set; }

    public required string? RecordLabel { get; set; }

    /// <summary>
    /// Gets the countries associated with this album from Plex metadata.
    /// </summary>
    public ICollection<PlexCountry> Countries { get; init; } = [];

    public ICollection<PlexMusicTrack> Tracks { get; set; } = [];

    public ICollection<PlexMusicAlbumMediaQuality> Qualities { get; set; } = [];
}
