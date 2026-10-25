namespace Reaparr.Domain;

public static partial class EnumMapperExtensions
{
    private static readonly Dictionary<string, SortType> _sortTypeMap = Enum.GetValues<SortType>()
        .ToDictionary(x => x.ToSortValue(), StringComparer.OrdinalIgnoreCase)
        .Concat(
            new Dictionary<string, SortType>
            {
                [SortType.Title.ToEnumMemberValue()] = SortType.Title,
                [nameof(BasePlexMedia.SearchTitle)] = SortType.Title,
            }
        )
        .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

    private static readonly SortType[] _commonSortTypes =
    [
        SortType.Title,
        SortType.AddedAt,
        SortType.UpdatedAt,
        SortType.Duration,
        SortType.MediaSize,
    ];

    private static readonly SortType[] _videoSortTypes =
    [
        SortType.Title,
        SortType.Year,
        SortType.AddedAt,
        SortType.UpdatedAt,
        SortType.Duration,
        SortType.MediaSize,
        SortType.VideoQuality,
    ];

    /// <summary>
    /// Gets the sort fields implemented by each root media-family query.
    /// </summary>
    public static IReadOnlyDictionary<PlexMediaType, SortType[]> SupportedMediaSorts { get; } =
        new Dictionary<PlexMediaType, SortType[]>
        {
            [PlexMediaType.Movie] = [.. _videoSortTypes, SortType.ComparisonState],
            [PlexMediaType.TvShow] = [.. _videoSortTypes, SortType.ComparisonState],
            [PlexMediaType.MusicArtist] = [.. _commonSortTypes, SortType.AudioQuality, SortType.ComparisonState],
            [PlexMediaType.PhotoAlbum] = _commonSortTypes,
            [PlexMediaType.OtherVideos] = _videoSortTypes,
        };

    /// <summary>
    /// Returns the canonical sort capabilities for a root media family without allocating a new field list.
    /// </summary>
    /// <param name="mediaType">The root media family whose query capabilities are requested.</param>
    /// <returns>The catalog's fields, or an empty span for a media type that has no root overview query.</returns>
    public static IReadOnlyList<SortType> GetSupportedSorts(this PlexMediaType mediaType) =>
        SupportedMediaSorts.TryGetValue(mediaType, out var supportedSorts) ? supportedSorts : [];

    /// <summary>
    /// Converts a string to <see cref="SortType"/> by its canonical JSON field or supported ordering alias.
    /// </summary>
    /// <param name="value">The string representation of <see cref="SortType"/>.</param>
    /// <returns>The converted enum of type <see cref="SortType"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Throws when value is not a supported sort field.</exception>
    public static SortType ToSortType(this string? value)
    {
        if (value is not null && _sortTypeMap.TryGetValue(value.Trim(), out var sortType))
            return sortType;

        throw new ArgumentOutOfRangeException(nameof(value), value, null);
    }

    /// <summary>
    /// Maps a logical sort choice to the query property used for ordering.
    /// Title uses the stored natural-title rank, not a display string or response position.
    /// </summary>
    public static string ToSortValue(this SortType sortType)
    {
        if (sortType == SortType.Title)
            return nameof(BasePlexMedia.SortIndex);

        return sortType.ToEnumMemberValue();
    }

    /// <summary>
    /// Validates all requested sort fields against the same media-family catalog published by the metadata API.
    /// </summary>
    /// <param name="value">The optional comma-separated sort expression; omitted sorting uses the query's default order.</param>
    /// <param name="mediaType">The media family that must implement every requested sort field.</param>
    /// <returns>True when sorting is omitted or every field is recognized and supported by the selected family.</returns>
    public static bool IsSupportedSort(this string? value, PlexMediaType mediaType)
    {
        var supportedSorts = mediaType.GetSupportedSorts();
        return (string.IsNullOrWhiteSpace(value) || supportedSorts.Count > 0)
            && IsSupportedSort(value, supportedSorts, requireSingleSort: false);
    }

    // Explicit field lists belong to browse requests that carry a single sort.
    public static bool IsSupportedSort(this string? value, params IReadOnlyList<SortType> supportedSorts) =>
        IsSupportedSort(value, supportedSorts, requireSingleSort: supportedSorts.Count > 0);

    private static bool IsSupportedSort(string? value, IReadOnlyList<SortType> supportedSorts, bool requireSingleSort)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var nodes = value.Split(',');
        if (requireSingleSort && nodes.Length != 1)
            return false;

        try
        {
            foreach (var node in nodes)
            {
                var sortType = ParseSortNodeField(node);
                if (supportedSorts.Count > 0 && !supportedSorts.Contains(sortType))
                    return false;
            }

            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static SortType ParseSortNodeField(string value)
    {
        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length > 2)
            throw new ArgumentOutOfRangeException(nameof(value), value, null);

        return parts[0].ToSortType();
    }
}
