namespace Reaparr.Application;

public record CreateDownloadTasksEndpointRequest
{
    [FromBody]
    public required CreateDownloadTasksRequest Request { get; init; }
}

public class CreateDownloadTasksEndpointRequestValidator : Validator<CreateDownloadTasksEndpointRequest>
{
    public CreateDownloadTasksEndpointRequestValidator()
    {
        RuleFor(x => x.Request).NotNull();
    }
}

public class CreateDownloadTasksEndpoint : Endpoint<CreateDownloadTasksEndpointRequest>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;

    public CreateDownloadTasksEndpoint(ILogger log, ICommandExecutor commandExecutor)
    {
        _log = log.ForContext<CreateDownloadTasksEndpoint>();
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Post(ApiRoutes.DownloadController + "/create");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<DownloadTaskCreationReportDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(CreateDownloadTasksEndpointRequest req, CancellationToken ct)
    {
        _log.Here().Debug("Attempting to add download task orders: ");
        foreach (var downloadMediaDto in req.Request.DownloadMedias)
            _log.Here().Debug("DownloadMediaDTO: {@DownloadMediaDto} ", downloadMediaDto);

        var result = await _commandExecutor.Send(new CreateDownloadTasksCommand(req.Request), ct);

        await Send.FluentResult(
            result,
            report => new DownloadTaskCreationReportDTO
            {
                Movies = report.Movies,
                TvShows = report.TvShows,
                Seasons = report.Seasons,
                Episodes = report.Episodes,
                PhotoAlbums = report.PhotoAlbums,
                PhotoImages = report.PhotoImages,
                MusicArtists = report.MusicArtists,
                MusicAlbums = report.MusicAlbums,
                MusicTracks = report.MusicTracks,
                OtherVideos = report.OtherVideos,
            },
            ct
        );
    }
}
