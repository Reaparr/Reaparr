namespace Reaparr.Domain;

public static class SortTypeExtensions
{
    public static bool TryParse(string? value, out SortType sortType)
    {
        sortType = SortType.Title;
        var field = value.AsSpan().Trim();
        // Legacy query and persisted UI names represent the same title ordering intent.
        if (field.Equals(nameof(BasePlexMedia.SortIndex), StringComparison.OrdinalIgnoreCase)
            || field.Equals(nameof(BasePlexMedia.SearchTitle), StringComparison.OrdinalIgnoreCase)
            || field.Equals("sortTitle", StringComparison.OrdinalIgnoreCase))
        {
            sortType = SortType.Title;
            return true;
        }

        if (field.Equals("quality", StringComparison.OrdinalIgnoreCase))
        {
            sortType = SortType.VideoQuality;
            return true;
        }

        // Reject numeric and comma-separated enum inputs: this boundary accepts one named field.
        if (Enum.TryParse<SortType>(field, ignoreCase: true, out var parsed)
            && field.Equals(Enum.GetName(parsed), StringComparison.OrdinalIgnoreCase))
        {
            sortType = parsed;
            return true;
        }

        return false;
    }

    public static string ToQueryField(this SortType sortType) => sortType switch
    {
        SortType.Title => nameof(BasePlexMedia.SortIndex),
        SortType.Year => nameof(SortType.Year),
        SortType.AddedAt => nameof(BasePlexMedia.AddedAt),
        SortType.UpdatedAt => nameof(BasePlexMedia.UpdatedAt),
        SortType.Duration => nameof(BasePlexMedia.Duration),
        SortType.MediaSize => nameof(BasePlexMedia.MediaSize),
        SortType.VideoQuality => nameof(BasePlexMedia.VideoQuality),
        SortType.ComparisonState => nameof(BasePlexMedia.ComparisonState),
        _ => throw new ArgumentOutOfRangeException(nameof(sortType), sortType, "Unsupported media sort type"),
    };
}
