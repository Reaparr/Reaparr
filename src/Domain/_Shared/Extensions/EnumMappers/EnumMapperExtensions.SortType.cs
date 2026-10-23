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

    private static readonly SortType[] _nonComparisonSortTypes = Enum.GetValues<SortType>()
        .Where(sortType => sortType != SortType.ComparisonState)
        .ToArray();

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

    public static bool IsSupportedSort(this string? value, PlexMediaType mediaType) =>
        IsSupportedSort(
            value,
            mediaType.SupportsComparison() ? ReadOnlySpan<SortType>.Empty : _nonComparisonSortTypes,
            requireSingleSort: false
        );

    // Explicit field lists belong to browse requests that carry a single sort.
    public static bool IsSupportedSort(this string? value, params ReadOnlySpan<SortType> supportedSorts) =>
        IsSupportedSort(value, supportedSorts, requireSingleSort: !supportedSorts.IsEmpty);

    private static bool IsSupportedSort(string? value, ReadOnlySpan<SortType> supportedSorts, bool requireSingleSort)
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
                if (!supportedSorts.IsEmpty && !supportedSorts.Contains(sortType))
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
