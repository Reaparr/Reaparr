namespace Reaparr.Application;

public record CreateFolderPathEndpointRequest
{
    [FromBody]
    public FolderPathDTO? FolderPathDto { get; init; }
}

public class CreateFolderPathEndpointRequestValidator : Validator<CreateFolderPathEndpointRequest>
{
    public CreateFolderPathEndpointRequestValidator()
    {
        RuleFor(x => x.FolderPathDto)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(x => x.FolderPathDto!.DisplayName).NotEmpty();
                RuleFor(x => x.FolderPathDto!.Directory).NotEmpty();
                RuleFor(x => x.FolderPathDto!.FolderType).NotEqual(FolderType.None).NotEqual(FolderType.Unknown);
                RuleFor(x => x.FolderPathDto!.MediaType)
                    .NotEqual(PlexMediaType.Unknown)
                    .Must(
                        (request, mediaType) =>
                            request.FolderPathDto!.FolderType == FolderType.DownloadFolder
                            || mediaType != PlexMediaType.None
                    )
                    .WithMessage("Media type can only be None for download folders.");
            });
    }
}

public class CreateFolderPathEndpoint : Endpoint<CreateFolderPathEndpointRequest, ResultDTO<FolderPathDTO>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ITelemetry _telemetry;

    public CreateFolderPathEndpoint(ILogger log, IReaparrDbContext dbContext, ITelemetry telemetry)
    {
        _log = log.ForContext<CreateFolderPathEndpoint>();
        _dbContext = dbContext;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.FolderPathController + "/");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<FolderPathDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(CreateFolderPathEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var folderPath = req.FolderPathDto!.ToModel();
        await _dbContext.FolderPaths.AddAsync(folderPath, ct);
        await _dbContext.SaveChangesAsync(ct);

        _telemetry.FolderPathChanged(folderPath, TelemetryAction.Create);

        var folderPathDb = await _dbContext.FolderPaths.GetAsync(folderPath.Id, ct);
        if (folderPathDb is null)
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(FolderPath), folderPath.Id), ct);
        else
            await Send.FluentResult(Result.Ok(folderPathDb), x => x.ToDTO(), ct);
    }
}
