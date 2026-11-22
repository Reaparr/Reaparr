using System.Text.RegularExpressions;

namespace Reaparr.Application;

public record GetAllUniqueMediaTitlesEndpointRequest
{
    [QueryParam, BindFrom("type")]
    public PlexMediaType Type { get; init; } = PlexMediaType.TvShow;

    [QueryParam, BindFrom("count")]
    public int Count { get; init; } = 0;
}

public class GetAllUniqueMediaTitlesEndpoint : Endpoint<GetAllUniqueMediaTitlesEndpointRequest, List<string>>
{
    private readonly IReaparrDbContext _dbContext;

    private readonly Random _random = new();

    public GetAllUniqueMediaTitlesEndpoint(IReaparrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Get(ApiRoutes.DebugController + "/unique-media-titles");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<List<string>>))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(GetAllUniqueMediaTitlesEndpointRequest req, CancellationToken ct)
    {
        var query = req.Type switch
        {
            PlexMediaType.Movie => _dbContext.PlexMovies.Select(x => x.Title),
            PlexMediaType.TvShow => _dbContext.PlexTvShows.Select(x => x.Title),
            PlexMediaType.Season => _dbContext.PlexTvShowSeason.Select(x => x.Title),
            PlexMediaType.Episode => _dbContext.PlexTvShowEpisodes.Select(x => x.Title),
            _ => throw new NotSupportedException($"Type {req.Type} is not supported."),
        };

        var result = query
            .AsEnumerable()
            .Where(x => Regex.IsMatch(x, @"^[a-zA-Z0-9\-: ]+$", RegexOptions.None, TimeSpan.FromSeconds(1)))
            .OrderByNatural(x => x)
            .Distinct()
            .Select(x => x.Trim())
            .ToList();

        var resultCount = result.Count;
        string json;

        if (req.Count > 0)
        {
            var takenList = new HashSet<string>();
            for (var i = 0; i < req.Count; i++)
            {
                string selected;
                do
                {
                    var index = _random.Next(0, resultCount);
                    selected = result[index];
                }
                // Retry if the value is already in the list
                while (takenList.Contains(selected));

                // Add the unique result to the HashSet
                takenList.Add(selected);
            }

            var orderedList = takenList.OrderByNatural(x => x).ToList();
            json = Regex.Unescape(JsonSerializer.Serialize(orderedList, DefaultJsonSerializerOptions.ConfigStandard));
        }
        else
        {
            json = Regex.Unescape(JsonSerializer.Serialize(result, DefaultJsonSerializerOptions.ConfigStandard));
        }

        await Send.StringAsync(json, 200, "application/json", ct);
    }
}
