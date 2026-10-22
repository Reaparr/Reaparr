namespace Reaparr.Domain;

public abstract class BasePlexMediaData : BasePlexMediaQuality
{
    /// <summary>
    /// Unique identifier for the logical media item in Plex.
    /// Maps to Plex API's Metadata.ratingKey field.
    /// </summary>
    [Column(Order = 1)]
    public required int PlexApiRatingKey { get; init; }

    /// <summary>
    /// Unique identifier for a specific media version/quality within a Plex item.
    /// </summary>
    [Column(Order = 2)]
    public required int PlexApiMediaId { get; init; }

    /// <summary>
    /// Unique identifier for a specific file part within a media version.
    /// </summary>
    [Column(Order = 3)]
    public required int PlexApiPartId { get; init; }

    /// <summary>The original filename of the media file as reported by Plex.</summary>
    [Column(Order = 5)]
    public required string OriginalFilename { get; init; }

    /// <summary>Container format of the file (for example mkv, mp4 or flac).</summary>
    [Column(Order = 7)]
    public required string Container { get; init; }

    /// <summary>Duration of the media file in milliseconds.</summary>
    [Column(Order = 8)]
    public required int Duration { get; init; }

    /// <summary>File size in bytes.</summary>
    [Column(Order = 9)]
    public required long Size { get; init; }

    /// <summary>Plex-generated key used to access this specific part.</summary>
    [Column(Order = 10)]
    public required string Key { get; init; }

    /// <summary>
    /// Release source inferred from explicit disc or broadcast container hints.
    /// </summary>
    [Column(Order = 11)]
    public required ReleaseSource Source { get; init; }

    /// <summary>Video codec as reported by Plex.</summary>
    [Column(Order = 12)]
    public required string VideoCodec { get; init; }

    /// <summary>Best available audio codec for the release.</summary>
    [Column(Order = 13)]
    public required string AudioCodec { get; init; }

    #region Relationships

    /// <summary>Identifier of the Plex library section this part belongs to.</summary>
    public required int PlexLibraryId { get; set; }

    /// <summary>Identifier of the Plex server this part was synced from.</summary>
    public required int PlexServerId { get; set; }

    public PlexLibrary? PlexLibrary { get; init; }

    public PlexServer? PlexServer { get; init; }

    #endregion

    #region Helpers

    /// <summary>Returns the canonical source filename used by music and other-video media.</summary>
    public virtual string GetFileName => OriginalFilename;

    #endregion
}
