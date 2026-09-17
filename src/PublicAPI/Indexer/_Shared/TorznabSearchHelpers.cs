namespace Reaparr.PublicAPI;

public static class TorznabSearchHelpers
{
    public const int MaxPaginationWindow = 10_000;
    public const int MaxPageSize = 100;

    public static bool IsPageSizeWithinLimit(int limit) => limit >= 0 && limit <= MaxPageSize;

    public static bool IsPaginationWithinLimit(int offset, int limit) =>
        offset >= 0 && limit >= 0 && (long)offset + limit <= MaxPaginationWindow;

    public static IReadOnlySet<string>? GetRequestedAttributes(bool includeAllAttributes, string[] attributes) =>
        includeAllAttributes ? null : attributes.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static TorznabMediaSearchResponseDTO CreateResponse(
        string description,
        int offset,
        int total,
        List<TorznabItem> items
    ) =>
        new()
        {
            Channel = new TorznabChannel
            {
                Title = "Reaparr Indexer",
                Description = description,
                Language = "en-us",
                Category = "search",
                Items = items,
                Response = new TorznabResponseMetadata { Offset = offset, Total = total },
            },
        };
}
