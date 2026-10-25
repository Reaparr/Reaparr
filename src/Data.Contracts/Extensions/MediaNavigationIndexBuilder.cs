using System.Globalization;
using System.Linq.Expressions;

namespace Reaparr.Data.Contracts;

public static class MediaNavigationIndexBuilder
{
    private const string UNKNOWN_LABEL = "#";
    private const long BYTES_PER_GIGABYTE = 1_000_000_000;

    public static List<MediaNavigationIndexDTO> Build(IEnumerable<MediaNavigationIndexRow> rows, SortType sortType)
    {
        var result = new List<MediaNavigationIndexDTO>();
        var seenLabels = new HashSet<string>(StringComparer.Ordinal);

        var index = 0;
        foreach (var row in rows)
        {
            var label = GetLabel(row, sortType);
            if (seenLabels.Add(label))
                result.Add(new MediaNavigationIndexDTO { Label = label, Index = index });

            index++;
        }

        return result;
    }

    public static Task<List<MediaNavigationIndexDTO>> BuildAsync(
        IQueryable<MediaNavigationIndexRow> rows,
        Sort sort,
        CancellationToken cancellationToken
    ) =>
        sort.Field switch
        {
            SortType.Year or SortType.VideoQuality or SortType.AudioQuality => BuildGroupedAsync<int?>(
                rows,
                (
                    sort.Field == SortType.Year ? row => row.Year : row => row.QualityValue,
                    static value => value?.ToString(CultureInfo.InvariantCulture) ?? UNKNOWN_LABEL
                ),
                sort,
                cancellationToken
            ),
            SortType.Duration => BuildGroupedAsync(
                rows,
                (
                    row => row.Duration.HasValue ? Math.Max(0, row.Duration.Value) / 600 : (int?)null,
                    static bucket => bucket.HasValue ? GetDurationLabel(bucket.Value * 600) : UNKNOWN_LABEL
                ),
                sort,
                cancellationToken
            ),
            SortType.AddedAt or SortType.UpdatedAt => BuildGroupedAsync<int?>(
                rows,
                (
                    sort.Field == SortType.AddedAt
                        ? row => row.AddedAt.Year * 100 + row.AddedAt.Month
                        : row =>
                            row.UpdatedAt.HasValue
                                ? row.UpdatedAt.Value.Year * 100 + row.UpdatedAt.Value.Month
                                : (int?)null,
                    bucket =>
                    {
                        if (bucket is null)
                            return UNKNOWN_LABEL;

                        var year = bucket.Value / 100;
                        var month = bucket.Value % 100;
                        return $"{year:D4}-{month:D2}";
                    }
                ),
                sort,
                cancellationToken
            ),
            SortType.MediaSize => BuildGroupedAsync(
                rows,
                (
                    row => Math.Max(0L, row.MediaSize) / BYTES_PER_GIGABYTE,
                    static bucket => GetMediaSizeLabel(bucket * BYTES_PER_GIGABYTE)
                ),
                sort,
                cancellationToken
            ),
            _ => BuildGroupedAsync(
                rows,
                ((Expression<Func<MediaNavigationIndexRow, string?>>)(row => row.SearchTitle), GetTitleLabel),
                sort,
                cancellationToken
            ),
        };

    private static async Task<List<MediaNavigationIndexDTO>> BuildGroupedAsync<TKey>(
        IQueryable<MediaNavigationIndexRow> rows,
        (Expression<Func<MediaNavigationIndexRow, TKey>> Key, Func<TKey, string> Label) bucket,
        Sort sort,
        CancellationToken cancellationToken
    )
    {
        var groups = sort.Direction == SortDirection.Desc
            ? rows.GroupBy(bucket.Key)
                .Select(group => new { Key = group.Key, FirstRank = group.Max(row => row.SortRank) })
            : rows.GroupBy(bucket.Key)
                .Select(group => new { Key = group.Key, FirstRank = group.Min(row => row.SortRank) });

        var positionedGroups = sort.Direction == SortDirection.Desc
            ? groups.Select(group => new NavigationGroupResult<TKey>(
                group.Key,
                group.FirstRank,
                rows.Count(row => row.SortRank > group.FirstRank)
            ))
            : groups.Select(group => new NavigationGroupResult<TKey>(
                group.Key,
                group.FirstRank,
                rows.Count(row => row.SortRank < group.FirstRank)
            ));

        var materialized = await positionedGroups.ToListAsync(cancellationToken);
        return (
            sort.Direction == SortDirection.Desc
                ? materialized.OrderByDescending(group => group.FirstRank)
                : materialized.OrderBy(group => group.FirstRank)
        )
            .Select(group => new MediaNavigationIndexDTO { Label = bucket.Label(group.Key), Index = group.Index })
            .ToList();
    }

    private static string GetLabel(MediaNavigationIndexRow row, SortType sortType) =>
        sortType switch
        {
            SortType.Title => GetTitleLabel(row.SearchTitle),
            SortType.Year => row.Year?.ToString(CultureInfo.InvariantCulture) ?? UNKNOWN_LABEL,
            SortType.VideoQuality or SortType.AudioQuality => row.QualityValue?.ToString(CultureInfo.InvariantCulture) ?? UNKNOWN_LABEL,
            SortType.Duration => row.Duration.HasValue ? GetDurationLabel(row.Duration.Value) : UNKNOWN_LABEL,
            SortType.AddedAt => row.AddedAt.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            SortType.UpdatedAt => row.UpdatedAt is null
                ? UNKNOWN_LABEL
                : row.UpdatedAt.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            SortType.MediaSize => GetMediaSizeLabel(row.MediaSize),
            _ => GetTitleLabel(row.SearchTitle),
        };

    public static string GetTitleLabel(string? title)
    {
        var first = title?.Trim().FirstOrDefault();
        if (first is null or '\0' || !char.IsAsciiLetter(first.Value))
            return UNKNOWN_LABEL;

        return char.ToUpperInvariant(first.Value).ToString();
    }

    private static string GetDurationLabel(int duration)
    {
        var start = Math.Max(0, duration) / 600 * 10;
        return $"{start}–{start + 10} min";
    }

    private static string GetMediaSizeLabel(long mediaSize)
    {
        var start = Math.Max(0, mediaSize) / BYTES_PER_GIGABYTE;
        return $"{start}–{start + 1} GB";
    }

    private sealed record NavigationGroupResult<TKey>(TKey Key, int FirstRank, int Index);
}
