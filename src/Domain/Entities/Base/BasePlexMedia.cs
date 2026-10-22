namespace Reaparr.Domain;

/// <summary>
///     Plex stores media in one generic type, while Reaparr stores each media family in its own entity.
///     This base contains only fields shared by every catalog media family.
/// </summary>
public class BasePlexMedia : BaseEntity
{
    #region Properties

    /// <summary>
    /// Unique key identifying this item by the Plex API. This is used by Plex servers to differentiate media items.
    /// </summary>
    [Column(Order = 1)]
    public required int PlexApiRatingKey { get; set; }

    [Column(Order = 2)]
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets the sort index of the media based on the alphabetical sort order.
    /// </summary>
    [Column(Order = 4)]
    public required int SortIndex { get; init; }

    [Column(Order = 5)]
    public required string SearchTitle { get; init; }

    /// <summary>
    /// Gets or sets the duration in seconds of the nested media.
    /// </summary>
    [Column(Order = 6)]
    public required int Duration { get; set; }

    /// <summary>
    /// Gets or sets the total filesize of the nested media.
    /// </summary>
    [Column(Order = 7)]
    public required long MediaSize { get; set; }

    /// <summary>
    /// Gets or sets the highest available video quality for this media item or based on its children.
    /// </summary>
    [Column(Order = 17)]
    public VideoQuality VideoQuality { get; set; } = VideoQuality.Unknown;

    /// <summary>
    /// Gets or sets the key used to retrieve thumbnails, art or banners.
    /// </summary>
    [Column(Order = 8)]
    public required int PlexApiMetaDataKey { get; init; }

    /// <summary>
    /// Gets or sets the number of direct children.
    /// </summary>
    [Column(Order = 13)]
    public required int ChildCount { get; set; }

    /// <summary>
    /// Gets or sets when this media was added to the Plex library.
    /// </summary>
    [Column(Order = 14)]
    public required DateTime AddedAt { get; init; }

    /// <summary>
    /// Gets or sets when this media was last updated in the Plex library.
    /// </summary>
    [Column(Order = 15)]
    public required DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this media has a thumbnail.
    /// </summary>
    [Column(Order = 18)]
    public required bool HasThumb { get; set; }

    /// <summary>
    /// Gets or sets whether this media has art or a banner.
    /// </summary>
    [Column(Order = 19)]
    public required bool HasArt { get; init; }

    #endregion

    #region Relationships

    public required int PlexLibraryId { get; set; }

    public required int PlexServerId { get; set; }

    public PlexLibrary? PlexLibrary { get; set; }

    public PlexServer? PlexServer { get; init; }

    #endregion

    #region Helpers

    [NotMapped]
    public virtual PlexMediaType Type { get; init; }

    [NotMapped]
    public PlexMediaComparisonState ComparisonState { get; set; } = PlexMediaComparisonState.NotCompared;

    [NotMapped]
    public string MetaDataUrl => $"/library/metadata/{PlexApiRatingKey}";

    [NotMapped]
    public string ThumbUrl => HasThumb ? $"{MetaDataUrl}/thumb/{PlexApiMetaDataKey}" : string.Empty;

    [NotMapped]
    public string FullBannerUrl { get; init; } = string.Empty;

    [NotMapped]
    public string ArtUrl => HasArt ? $"{MetaDataUrl}/art/{PlexApiMetaDataKey}" : string.Empty;

    #endregion
}
