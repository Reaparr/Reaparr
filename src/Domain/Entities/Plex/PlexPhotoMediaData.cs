namespace Reaparr.Domain;

/// <summary>
/// A single image or video-clip asset in a Plex photo library, including its one Media/Part payload.
/// The asset is stored once, can belong to several albums, and can be assigned to a source folder.
/// </summary>
public class PlexPhotoMediaData : BaseEntity
{
    /// <summary>
    /// ID of the Plex server that owns this asset.
    /// </summary>
    public required int PlexServerId { get; set; }

    /// <summary>
    /// ID of the photo library that contains this asset.
    /// </summary>
    public required int PlexLibraryId { get; set; }

    /// <summary>
    /// Plex rating key identifying the asset within its library.
    /// </summary>
    public required int PlexApiRatingKey { get; set; }

    /// <summary>
    /// Plex identifier of the asset's single media representation.
    /// </summary>
    public required int PlexApiMediaId { get; set; }

    /// <summary>
    /// Plex identifier of the single file part belonging to this asset.
    /// </summary>
    public required int PlexApiPartId { get; set; }

    /// <summary>
    /// Distinguishes a still image from a video clip within the photo library.
    /// </summary>
    public PhotoAssetKind AssetKind { get; set; } = PhotoAssetKind.Image;

    /// <summary>
    /// Asset title reported by Plex.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Normalized asset title used for searching.
    /// </summary>
    public required string SearchTitle { get; init; }

    /// <summary>
    /// Asset description reported by Plex, when available.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Whether Plex reports a thumbnail for this asset.
    /// </summary>
    public required bool HasThumb { get; set; }

    /// <summary>
    /// Time Plex reports the asset was added to the library.
    /// </summary>
    public required DateTime AddedAt { get; init; }

    /// <summary>
    /// Most recent asset update time reported by Plex, when available.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Original availability date reported by Plex, when available.
    /// </summary>
    public DateTime? OriginallyAvailableAt { get; init; }

    /// <summary>
    /// File path reported by the Plex server, or null when absent; not a path on the Reaparr host.
    /// </summary>
    public string? SourcePath { get; set; }

    /// <summary>
    /// Original file name extracted from the Plex part's source path.
    /// </summary>
    public required string OriginalFilename { get; init; }

    /// <summary>
    /// Plex part endpoint used to retrieve the file, distinct from its source filesystem path.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// File size in bytes reported by Plex.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// File container or image format reported by Plex.
    /// </summary>
    public required string Container { get; init; }

    /// <summary>
    /// Image or video width in pixels, when reported.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    /// Image or video height in pixels, when reported.
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// Playback duration reported by Plex, when available.
    /// </summary>
    public int? Duration { get; init; }

    /// <summary>
    /// Video codec reported by Plex, when available.
    /// </summary>
    public string? VideoCodec { get; init; }

    /// <summary>
    /// Audio codec reported by Plex, when available.
    /// </summary>
    public string? AudioCodec { get; init; }

    /// <summary>
    /// ID of the source folder containing the asset, or null when no folder has been assigned.
    /// </summary>
    public int? PlexPhotoFolderId { get; set; }

    /// <summary>
    /// Navigation to the source folder containing the asset; independent of album membership.
    /// </summary>
    public PlexPhotoFolder? PlexPhotoFolder { get; set; }

    /// <summary>
    /// Navigation to the Plex server that owns this asset.
    /// </summary>
    public PlexServer? PlexServer { get; set; }

    /// <summary>
    /// Navigation to the photo library that contains this asset.
    /// </summary>
    public PlexLibrary? PlexLibrary { get; set; }

    /// <summary>
    /// Album memberships referencing this asset without duplicating its file data.
    /// </summary>
    public ICollection<PlexPhotoAlbumMembership> AlbumMemberships { get; set; } = [];

    /// <summary>
    /// Non-persisted media-family discriminator for both image and clip assets; <see cref="AssetKind"/> distinguishes them.
    /// </summary>
    [NotMapped]
    public PlexMediaType Type => PlexMediaType.PhotoImage;
}
