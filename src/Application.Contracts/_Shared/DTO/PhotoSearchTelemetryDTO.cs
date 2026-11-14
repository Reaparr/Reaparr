namespace Reaparr.Application.Contracts;

/// <summary>
/// Pagination and ordering projected by the photo-search slice for telemetry.
/// </summary>
/// <remarks>Normalized search terms and the operation result are passed separately to <see cref="ITelemetry"/>.</remarks>
public sealed record PhotoSearchTelemetryDTO
{
    /// <summary>The one-based page number used by the photo search.</summary>
    public required int Page { get; init; }
    /// <summary>The maximum number of photo assets requested for the page.</summary>
    public required int PageSize { get; init; }
    /// <summary>The field used to order the photo search results.</summary>
    public required SortType Sort { get; init; }
    /// <summary>The ascending or descending direction applied to <see cref="Sort"/>.</summary>
    public required SortDirection Direction { get; init; }
}
