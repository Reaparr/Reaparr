namespace Reaparr.PublicAPI;

public static class TorznabRequestMappers
{
    private static readonly ILogger _log = LogFactory.Create(typeof(TorznabRequestMappers));

    public static TorznabRequest ToTorznabRequest(
        this TorznabEndpointRequest request,
        IntegrationIdentity integration
    ) =>
        new()
        {
            Type = ParseType(request.Type),
            Query = request.Query ?? string.Empty,
            Season = request.Season ?? 0,
            Episode = request.Episode ?? 0,
            TvdbId = request.TvdbId ?? 0,
            ImdbId = request.ImdbId ?? string.Empty,
            TmdbId = request.TmdbId ?? 0,
            ApiKey = request.ApiKey,
            Limit = request.Limit ?? 50,
            Offset = request.Offset ?? 0,
            Categories = request.Categories ?? Array.Empty<int>(),
            Extended = request.Extended,
            Attributes =
                request.Attributes?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? [],
            Integration = integration,
        };

    public static ExternalIndexerQueryReport ToTelemetryReport(
        this TorznabRequest request,
        Result<TorznabMediaSearchResponseDTO> result
    )
    {
        var response = result.IsSuccess ? result.Value : null;
        return new ExternalIndexerQueryReport
        {
            Integration = request.Integration,
            Type = request.Type switch
            {
                TorznabQueryType.Caps => ExternalIndexerQueryType.Caps,
                TorznabQueryType.Search => ExternalIndexerQueryType.Search,
                TorznabQueryType.TvSearch => ExternalIndexerQueryType.TvSearch,
                TorznabQueryType.Movie => ExternalIndexerQueryType.Movie,
                _ => ExternalIndexerQueryType.Unknown,
            },
            Mode = request.Mode == TorznabRequestMode.Rss ? TelemetryMode.Rss : TelemetryMode.Search,
            Query = request.Query,
            ImdbId = request.ImdbId,
            TmdbId = request.TmdbId,
            TvdbId = request.TvdbId,
            Season = request.Season,
            Episode = request.Episode,
            Categories = request.Categories,
            ReturnedItemCount = response?.Channel.Items.Count ?? 0,
            TotalCount = response?.Channel.Response.Total ?? 0,
        };
    }

    public static TorznabQueryType ParseType(string? value)
    {
        if (
            !string.IsNullOrWhiteSpace(value)
            && !int.TryParse(value, out _)
            && Enum.TryParse<TorznabQueryType>(value, true, out var type)
            && Enum.IsDefined(type)
        )
            return type;

        _log.Here().Error("Received unknown Torznab request type: {Type}", value);
        return TorznabQueryType.Unknown;
    }
}
