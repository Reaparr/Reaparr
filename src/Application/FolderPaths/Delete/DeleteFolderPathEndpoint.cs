namespace Reaparr.Application;

public record DeleteFolderPathEndpointRequest(int Id);

public class DeleteFolderPathEndpointRequestValidator : Validator<DeleteFolderPathEndpointRequest>
{
    public DeleteFolderPathEndpointRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThanOrEqualTo(10)
            .WithMessage("Cannot delete reserved folder paths with an Id less than 10");
    }
}

public class DeleteFolderPathEndpoint : Endpoint<DeleteFolderPathEndpointRequest, BaseResultDTO>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ITelemetry _telemetry;

    public DeleteFolderPathEndpoint(ILogger log, IReaparrDbContext dbContext, ITelemetry telemetry)
    {
        _log = log.ForContext<DeleteFolderPathEndpoint>();
        _dbContext = dbContext;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Delete(ApiRoutes.FolderPathController + "/{Id}");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(DeleteFolderPathEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var deletedCount = await _dbContext.FolderPaths.Where(x => x.Id == req.Id).ExecuteDeleteAsync(ct);
        _log.Here()
            .Debug("Deleted {FolderPathName} with Id: {CommandId} from the database", nameof(FolderPath), req.Id);

        _telemetry.FolderPathDeleted(req.Id, deletedCount);

        await Send.FluentResult(Result.Ok(), ct);
    }
}
