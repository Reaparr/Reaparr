using System.Globalization;
using Reaparr.Application.Contracts;

namespace Reaparr.PublicAPI;

public static class TorznabRequestMappers
{
    private static readonly ILogger _log = LogFactory.Create(typeof(TorznabRequestMappers));

    public static TorznabRequest ToTorznabRequest(
        this TorznabEndpointRequest request,
        IntegrationIdentity integration
    )
    {
        TryParseEpisodeSelector(request.Season, request.Episode, out var episode, out var airDate);
        return new TorznabRequest
        {
            Type = ParseType(request.Type),
            Query = request.Query ?? string.Empty,
            Season = airDate.HasValue ? null : request.Season,
            Episode = episode,
            AirDate = airDate,
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
    }

    public static bool TryParseEpisodeSelector(
        int? season,
        string? value,
        out int episode,
        out DateOnly? airDate
    )
    {
        episode = 0;
        airDate = null;
        if (string.IsNullOrEmpty(value))
            return true;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out episode))
            return episode >= 0;
        if (season is not (>= 1 and <= 9999) || value.Length != 5)
            return false;

        Span<char> dateText = stackalloc char[10];
        season.Value.TryFormat(dateText[..4], out _, "D4", CultureInfo.InvariantCulture);
        dateText[4] = '/';
        value.AsSpan().CopyTo(dateText[5..]);
        if (!DateOnly.TryParseExact(dateText, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return false;

        airDate = date;
        return true;
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
