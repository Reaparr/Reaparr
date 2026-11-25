using FlexQuery.NET.Models;

namespace Reaparr.Data;

public static class QueryOptionsExtensions
{
    public static bool HasFiltersApplied(this QueryOptions options) =>
        options.Filter is { } filter && (filter.Filters.Count > 0 || filter.Groups.Count > 0);
}
