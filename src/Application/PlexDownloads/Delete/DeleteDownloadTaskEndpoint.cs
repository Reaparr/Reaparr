namespace Reaparr.Application;

public record DeleteDownloadTaskEndpointRequest
{
    [FromBody]
    public required List<Guid> DownloadTaskIds { get; init; }
}

public class DeleteDownloadTaskEndpointRequestValidator : Validator<DeleteDownloadTaskEndpointRequest>
{
    public DeleteDownloadTaskEndpointRequestValidator()
    {
        RuleFor(x => x.DownloadTaskIds).NotEmpty();
    }
}

public class DeleteDownloadTaskEndpoint : Endpoint<DeleteDownloadTaskEndpointRequest, BaseResultDTO>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public DeleteDownloadTaskEndpoint(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<DeleteDownloadTaskEndpoint>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Delete(ApiRoutes.DownloadController + "/delete");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(DeleteDownloadTaskEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);

        // Resolve keys upfront for stop handling and to avoid sending an empty delete command.
        var keys = await _dbContext.GetDownloadTaskKeysAsync(req.DownloadTaskIds, ct);
        if (keys.Count == 0)
        {
            var noOpResult = Result.Ok();
            _telemetry.DownloadDeletionProcessed(req.DownloadTaskIds, keys, noOpResult);
            await Send.FluentResult(noOpResult, ct);
            return;
        }

        foreach (var key in keys)
        {
            var stopResult = await _commandExecutor.Send(new StopDownloadTaskCommand(key.Id), ct);
            if (stopResult.IsFailed)
            {
                _telemetry.DownloadDeletionProcessed(req.DownloadTaskIds, keys, stopResult);
                await Send.FluentResult(stopResult, ct);
                return;
            }
        }

        var deleteResult = await _commandExecutor.Send(new DeleteDownloadTasksByKeyCommand(keys), ct);

        _telemetry.DownloadDeletionProcessed(req.DownloadTaskIds, keys, deleteResult);
        await Send.FluentResult(deleteResult, ct);
    }
}
