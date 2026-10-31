namespace Reaparr.Application.Contracts;

public record PlexMediaSlimDTO
{
    public required int Id { get; init; }

    /// <summary>
    /// Unique key identifying this item by the Plex Api. This is used by the PlexServers to differentiate between media items.
    /// e.g: 28550, 1723, 21898.
    /// </summary>
    public required int PlexApiRatingKey { get; set; }

    /// <summary>
    /// Gets or sets the key used to retrieve thumbnails, art or banners.
    /// E.g. /library/metadata/[PlexApiRatingKey]/art/[PlexApiMetaDataKey] =>  /library/metadata/529367/art/1593898227.
    /// </summary>
    public required int PlexApiMetaDataKey { get; init; }

    public required string Title { get; init; } = string.Empty;

    public required string SearchTitle { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the dynamic one-based response position after filtering, paging and sorting.
    /// This is distinct from the persisted natural-title rank used by logical <see cref="SortType.Title"/> ordering.
    /// </summary>
    public required int SortIndex { get; set; }

    public required int Year { get; init; }

    public required int Duration { get; init; }

    public required long MediaSize { get; init; }

    public required int ChildCount { get; init; }

    public required int GrandChildCount { get; init; }

    public required DateTime AddedAt { get; init; }

    public required DateTime? UpdatedAt { get; init; }

    public required int PlexLibraryId { get; init; }

    public required int PlexServerId { get; init; }

    public required PlexMediaType Type { get; init; }

    // TODO Remove ParentId
    public int? ParentId { get; init; }

    public required bool HasThumb { get; set; }

    public required List<PlexMediaQualityDTO> Qualities { get; init; } = [];

    /// <summary>
    /// Comparison state derived from current stored scope rows.
    /// <see cref="PlexMediaComparisonState.NotCompared"/> when no current scopes exist.
    /// A separate detail endpoint returns exact upgrade/missing information on user action.
    /// </summary>
    public int ComparisonId { get; set; }
}
