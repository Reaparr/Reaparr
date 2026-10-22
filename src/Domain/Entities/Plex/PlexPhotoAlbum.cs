namespace Reaparr.Domain;

/// <summary>
/// A named collection of assets in a Plex photo library. Membership is many-to-many and does not
/// determine an asset's source folder or download destination; albums do not form a nested hierarchy.
/// </summary>
public class PlexPhotoAlbum : BaseEntity
{
    /// <summary>
    /// ID of the Plex server that owns this album.
    /// </summary>
    public required int PlexServerId { get; set; }

    /// <summary>
    /// ID of the photo library that contains this album.
    /// </summary>
    public required int PlexLibraryId { get; set; }

    /// <summary>
    /// Plex rating key identifying the album within its library.
    /// </summary>
    public required int PlexApiRatingKey { get; set; }

    /// <summary>
    /// Album title reported by Plex.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Normalized album title used for searching.
    /// </summary>
    public required string SearchTitle { get; init; }

    /// <summary>
    /// Album description reported by Plex, when available.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Whether Plex reports a thumbnail for this album.
    /// </summary>
    public required bool HasThumb { get; set; }

    /// <summary>
    /// Time Plex reports the album was added to the library.
    /// </summary>
    public required DateTime AddedAt { get; init; }

    /// <summary>
    /// Most recent album update time reported by Plex, when available.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Navigation to the server that owns this album.
    /// </summary>
    public PlexServer? PlexServer { get; set; }

    /// <summary>
    /// Navigation to the photo library that contains this album.
    /// </summary>
    public PlexLibrary? PlexLibrary { get; set; }

    /// <summary>
    /// Links to the image and clip assets in this album, which may also belong to other albums.
    /// </summary>
    public ICollection<PlexPhotoAlbumMembership> Memberships { get; set; } = [];

    /// <summary>
    /// Non-persisted media discriminator identifying this entity as a photo album.
    /// </summary>
    [NotMapped]
    public virtual PlexMediaType Type => PlexMediaType.PhotoAlbum;
}
