namespace Reaparr.Application.Contracts;

/// <summary>Describes the request fields and result counts captured for one external indexer query.</summary>
/// <remarks>
/// The Torznab mapper counts items on the returned page separately from all matching items before pagination, and uses zero when the result is not successful.
/// </remarks>
public sealed record ExternalIndexerQueryReport
{
    /// <summary>Provider and stored integration identifier associated with the query.</summary>
    public required IntegrationIdentity Integration { get; init; }
    /// <summary>Torznab query type mapped for telemetry.</summary>
    public required ExternalIndexerQueryType Type { get; init; }
    /// <summary>Execution mode recorded for the query; the mapper records RSS mode as <see cref="TelemetryMode.Rss"/> and all other modes as <see cref="TelemetryMode.Search"/>.</summary>
    public required TelemetryMode Mode { get; init; }
    /// <summary>Search text supplied by the requester; the endpoint mapper uses an empty string when omitted.</summary>
    public required string Query { get; init; }
    /// <summary>IMDb identifier supplied by the requester; the endpoint mapper uses an empty string when omitted.</summary>
    public required string ImdbId { get; init; }
    /// <summary>TMDB identifier supplied by the requester; the endpoint mapper uses zero when omitted.</summary>
    public required int TmdbId { get; init; }
    /// <summary>TVDB identifier supplied by the requester; the endpoint mapper uses zero when omitted.</summary>
    public required int TvdbId { get; init; }
    /// <summary>Season number supplied by the requester; the endpoint mapper uses zero when omitted.</summary>
    public required int Season { get; init; }
    /// <summary>Episode number supplied by the requester; the endpoint mapper uses zero when omitted.</summary>
    public required int Episode { get; init; }
    /// <summary>Torznab category identifiers supplied by the requester; the endpoint mapper uses an empty array when omitted.</summary>
    public required int[] Categories { get; init; }
    /// <summary>Number of items on the returned page; the mapper uses the response channel's item count.</summary>
    public required int ReturnedItemCount { get; init; }
    /// <summary>Total number of matching items before pagination; the mapper uses the response channel's reported total.</summary>
    public required int TotalCount { get; init; }
}

/// <summary>Classifies the supported Torznab query types captured in indexer telemetry.</summary>
public enum ExternalIndexerQueryType
{
    /// <summary>No supported Torznab query type was mapped.</summary>
    Unknown,
    /// <summary>Capabilities request.</summary>
    Caps,
    /// <summary>Generic search request.</summary>
    Search,
    /// <summary>TV search request.</summary>
    TvSearch,
    /// <summary>Movie search request.</summary>
    Movie,
}
