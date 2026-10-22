using System.Globalization;
using System.Linq.Expressions;

namespace Reaparr.Data.Contracts;

public static class MediaNavigationIndexBuilder
{
    private const string UNKNOWN_LABEL = "#";
    private const long BYTES_PER_GIGABYTE = 1_000_000_000;

    public static List<MediaNavigationIndexDTO> Build(IEnumerable<MediaNavigationIndexRow> rows, string? sortField)
    {
        var result = new List<MediaNavigationIndexDTO>();
        var seenLabels = new HashSet<string>(StringComparer.Ordinal);
        var field = NormalizeSortField(sortField);

        var index = 0;
        foreach (var row in rows)
        {
            var label = GetLabel(row, field);
            if (seenLabels.Add(label))
                result.Add(new MediaNavigationIndexDTO { Label = label, Index = index });

            index++;
        }

        return result;
    }

    public static Task<List<MediaNavigationIndexDTO>> BuildAsync(
        IQueryable<MediaNavigationIndexRow> rows,
        (string? Field, bool Descending) sort,
        CancellationToken cancellationToken
    )
    {
        var field = NormalizeSortField(sort.Field);
        return field switch
        {
            "year" or "videoquality" => BuildGroupedAsync(
                rows,
                (
                    field == "year" ? row => row.Year : row => row.QualityValue,
                    static value => value?.ToString(CultureInfo.InvariantCulture) ?? UNKNOWN_LABEL
                ),
                sort,
                cancellationToken
            ),
            "duration" => BuildGroupedAsync(
                rows,
                (
                    row => row.Duration.HasValue ? Math.Max(0, row.Duration.Value) / 600 : (int?)null,
                    static bucket => bucket.HasValue ? GetDurationLabel(bucket.Value * 600) : UNKNOWN_LABEL
                ),
                sort,
                cancellationToken
            ),
            "addedat" or "updatedat" => BuildGroupedAsync(
                rows,
                (
                    field == "addedat"
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
            "mediasize" => BuildGroupedAsync(
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
    }

    private static async Task<List<MediaNavigationIndexDTO>> BuildGroupedAsync<TKey>(
        IQueryable<MediaNavigationIndexRow> rows,
        (Expression<Func<MediaNavigationIndexRow, TKey>> Key, Func<TKey, string> Label) bucket,
        (string? Field, bool Descending) sort,
        CancellationToken cancellationToken
    )
    {
        var groups = sort.Descending
            ? rows.GroupBy(bucket.Key)
                .Select(group => new { Key = group.Key, FirstRank = group.Max(row => row.SortRank) })
            : rows.GroupBy(bucket.Key)
                .Select(group => new { Key = group.Key, FirstRank = group.Min(row => row.SortRank) });

        var positionedGroups = sort.Descending
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
            sort.Descending
                ? materialized.OrderByDescending(group => group.FirstRank)
                : materialized.OrderBy(group => group.FirstRank)
        )
            .Select(group => new MediaNavigationIndexDTO { Label = bucket.Label(group.Key), Index = group.Index })
            .ToList();
    }

    private static string NormalizeSortField(string? sortField) =>
        string.IsNullOrWhiteSpace(sortField) ? "sortindex" : sortField.Trim().ToLowerInvariant();

    private static string GetLabel(MediaNavigationIndexRow row, string sortField) =>
        sortField switch
        {
            "sortindex" or "title" => GetTitleLabel(row.SearchTitle),
            "year" => row.Year?.ToString(CultureInfo.InvariantCulture) ?? UNKNOWN_LABEL,
            "videoquality" => row.QualityValue?.ToString(CultureInfo.InvariantCulture) ?? UNKNOWN_LABEL,
            "duration" => row.Duration.HasValue ? GetDurationLabel(row.Duration.Value) : UNKNOWN_LABEL,
            "addedat" => row.AddedAt.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            "updatedat" => row.UpdatedAt is null
                ? UNKNOWN_LABEL
                : row.UpdatedAt.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            "mediasize" => GetMediaSizeLabel(row.MediaSize),
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
