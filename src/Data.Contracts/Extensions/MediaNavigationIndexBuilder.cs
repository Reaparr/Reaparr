using System.Globalization;

namespace Reaparr.Data.Contracts;

public static class MediaNavigationIndexBuilder
{
    private const string UNKNOWN_LABEL = "#";
    private const long BYTES_PER_GIGABYTE = 1_000_000_000;

    public static List<MediaNavigationIndexDTO> Build(IEnumerable<MediaNavigationIndexRow> rows, string? sortField)
    {
        var result = new List<MediaNavigationIndexDTO>();
        var seenLabels = new HashSet<string>(StringComparer.Ordinal);
        var field = string.IsNullOrWhiteSpace(sortField) ? nameof(BasePlexMedia.SortIndex) : sortField.Trim();

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

    private static string GetLabel(MediaNavigationIndexRow row, string sortField) =>
        sortField switch
        {
            nameof(BasePlexMedia.SortIndex) or "sortIndex" or nameof(BasePlexMedia.Title) or "title" => GetTitleLabel(
                row.SearchTitle
            ),
            nameof(BasePlexMedia.Year) or "year" => row.Year.ToString(CultureInfo.InvariantCulture),
            "quality" => row.QualityValue?.ToString(CultureInfo.InvariantCulture) ?? UNKNOWN_LABEL,
            nameof(BasePlexMedia.Duration) or "duration" => GetDurationLabel(row.Duration),
            nameof(BasePlexMedia.AddedAt) or "addedAt" => GetMonthLabel(row.AddedAt),
            nameof(BasePlexMedia.UpdatedAt) or "updatedAt" => row.UpdatedAt is null
                ? UNKNOWN_LABEL
                : GetMonthLabel(row.UpdatedAt.Value),
            nameof(BasePlexMedia.MediaSize) or "mediaSize" => GetMediaSizeLabel(row.MediaSize),
            _ => GetTitleLabel(row.SearchTitle),
        };

    private static string GetTitleLabel(string? title)
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

    private static string GetMonthLabel(DateTime value) => value.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static string GetMediaSizeLabel(long mediaSize)
    {
        var start = Math.Max(0, mediaSize) / BYTES_PER_GIGABYTE;
        return $"{start}–{start + 1} GB";
    }
}
