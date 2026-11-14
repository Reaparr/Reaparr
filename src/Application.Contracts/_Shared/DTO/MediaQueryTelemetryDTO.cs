namespace Reaparr.Application.Contracts;

/// <summary>
/// Search, pagination and scope projected by the media-overview slice for telemetry.
/// </summary>
/// <remarks>
/// The built query filter and operation result are supplied separately to <see cref="ITelemetry"/>.
/// Queries without effective search terms do not emit media-query analytics.
/// </remarks>
public sealed record MediaQueryTelemetryDTO
{
    /// <summary>The requested search text, normalized into effective search terms by telemetry before capture.</summary>
    public string? Search { get; init; }
    /// <summary>The kind of media requested by the query.</summary>
    public required PlexMediaType MediaType { get; init; }
    /// <summary>The requested one-based page number; telemetry records page one when omitted.</summary>
    public int? Page { get; init; }
    /// <summary>The requested page size; telemetry records the media-query page-size limit when omitted.</summary>
    public int? PageSize { get; init; }
    /// <summary>The requested internal Reaparr library ID, or null when no library was specified.</summary>
    public int? PlexLibraryId { get; init; }
    /// <summary>The requested sort expression, such as <c>title:asc</c>, or null when omitted.</summary>
    public string? Sort { get; init; }
}
