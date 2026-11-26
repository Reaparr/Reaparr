namespace Reaparr.Application.Contracts;

/// <summary>
/// Pagination and ordering projected by the photo-search slice for telemetry.
/// </summary>
/// <remarks>The original submitted search text is preserved for telemetry; query parsing remains owned by the search operation.</remarks>
public sealed record PhotoSearchTelemetryDTO
{
    /// <summary>The original submitted search text.</summary>
    public required string Search { get; init; }

    /// <summary>The one-based page number used by the photo search.</summary>
    public required int Page { get; init; }

    /// <summary>The maximum number of photo assets requested for the page.</summary>
    public required int PageSize { get; init; }

    /// <summary>The field used to order the photo search results.</summary>
    public required SortType Sort { get; init; }

    /// <summary>The ascending or descending direction applied to <see cref="Sort"/>.</summary>
    public required SortDirection Direction { get; init; }
}
