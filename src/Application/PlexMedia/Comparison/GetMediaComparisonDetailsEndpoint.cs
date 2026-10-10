namespace Reaparr.Application;

public class GetMediaComparisonDetailsEndpointRequest
{
    [SetsRequiredMembers]
    public GetMediaComparisonDetailsEndpointRequest(int plexMediaId, PlexMediaType type)
    {
        PlexMediaId = plexMediaId;
        Type = type;
    }

    public required int PlexMediaId { get; init; }

    [QueryParam, BindFrom("type")]
    public required PlexMediaType Type { get; init; }
}

public class GetMediaComparisonDetailsEndpointRequestValidator : Validator<GetMediaComparisonDetailsEndpointRequest>
{
    public GetMediaComparisonDetailsEndpointRequestValidator()
    {
        RuleFor(x => x.PlexMediaId).GreaterThan(0);
        RuleFor(x => x.Type).Must(x => x.SupportsComparison());
    }
}

public class GetMediaComparisonDetailsEndpoint
    : Endpoint<GetMediaComparisonDetailsEndpointRequest, ResultDTO<PlexMediaComparisonDetailsDTO>>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;

    public GetMediaComparisonDetailsEndpoint(ILogger log, ICommandExecutor commandExecutor)
    {
        _log = log.ForContext<GetMediaComparisonDetailsEndpoint>();
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexMediaController + "/comparison-details/{PlexMediaId}");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PlexMediaComparisonDetailsDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(GetMediaComparisonDetailsEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);

        var result = req.Type switch
        {
            PlexMediaType.Movie => await _commandExecutor.Send(
                new GetMovieMediaComparisonDetailsCommand(req.PlexMediaId),
                ct
            ),
            PlexMediaType.TvShow => await _commandExecutor.Send(
                new GetTvShowMediaComparisonDetailsCommand(req.PlexMediaId),
                ct
            ),
            PlexMediaType.MusicArtist => await _commandExecutor.Send(
                new GetMusicMediaComparisonDetailsCommand(req.PlexMediaId),
                ct
            ),
            _ => Result.Fail<PlexMediaComparisonDetailsDTO>("Unsupported media type"),
        };

        await Send.FluentResult(result, ct);
    }
}
