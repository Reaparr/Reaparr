namespace Reaparr.PublicAPI;

public record TorznabEndpointRequest
{
    /// <summary>
    /// Operation type selector.
    /// Possible values: "caps", "search", "tvsearch", "movie".
    /// </summary>
    [QueryParam, BindFrom("t")]
    public string? Type { get; init; }

    public TorznabQueryType ParsedType => TorznabRequestMappers.ParseType(Type);

    /// <summary>
    /// Free text search query.
    /// Used in search, tvsearch, and movie queries.
    /// </summary>
    [QueryParam, BindFrom("q")]
    public string? Query { get; init; }

    /// <summary>
    /// Season number for tvsearch queries.
    /// </summary>
    [QueryParam, BindFrom("season")]
    public int? Season { get; init; }

    /// <summary>
    /// Episode number for tvsearch queries.
    /// </summary>
    [QueryParam, BindFrom("ep")]
    public int? Episode { get; init; }

    /// <summary>
    /// TheTVDB series ID for tvsearch queries.
    /// </summary>
    [QueryParam, BindFrom("tvdbid")]
    public int? TvdbId { get; init; }

    /// <summary>
    /// IMDb ID for movie queries (e.g. "tt1234567").
    /// </summary>
    [QueryParam, BindFrom("imdbid")]
    public string? ImdbId { get; init; }

    /// <summary>
    /// TMDb ID for movie queries.
    /// </summary>
    [QueryParam, BindFrom("tmdbid")]
    public int? TmdbId { get; init; }

    /// <summary>
    /// API key provided by the requesting client (Sonarr/Radarr).
    /// </summary>
    [QueryParam, BindFrom("apikey")]
    public required string ApiKey { get; init; }

    /// <summary>
    /// Max number of results to return.
    /// Sonarr/Radarr will pass this for pagination.
    /// </summary>
    [QueryParam, BindFrom("limit")]
    public int? Limit { get; init; }

    /// <summary>
    /// Offset of results for pagination.
    /// </summary>
    [QueryParam, BindFrom("offset")]
    public int? Offset { get; init; }

    /// <summary>
    /// One or more category IDs (comma-separated).
    /// Example: "5030,5040".
    /// </summary>
    [QueryParam, BindFrom("cat")]
    public int[]? Categories { get; init; }

    /// <summary>
    /// Comma-separated Torznab attribute names to include when extended output is disabled.
    /// </summary>
    [QueryParam, BindFrom("attrs")]
    public string? Attributes { get; init; }

    /// <summary>
    /// Extended flag (0 = basic, 1 = include extended attributes).
    /// </summary>
    [QueryParam, BindFrom("extended")]
    public int? Extended { get; init; }
}
