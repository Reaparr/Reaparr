namespace Reaparr.Application;

public record CheckConnectionStatusByIdRequest(int PlexServerConnectionId);

public class CheckConnectionStatusByIdRequestValidator : Validator<CheckConnectionStatusByIdRequest>
{
    public CheckConnectionStatusByIdRequestValidator()
    {
        RuleFor(x => x.PlexServerConnectionId).GreaterThan(0);
    }
}

public class CheckConnectionStatusByIdEndpoint : Endpoint<CheckConnectionStatusByIdRequest, PlexServerStatusDTO>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public CheckConnectionStatusByIdEndpoint(
        ILogger log,
        ICommandExecutor commandExecutor,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<CheckConnectionStatusByIdEndpoint>();
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexServerConnectionController + "/check/{PlexServerConnectionId}");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PlexServerStatusDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(CheckConnectionStatusByIdRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var result = await _commandExecutor.Send(new CheckConnectionStatusByIdCommand(req.PlexServerConnectionId), ct);
        _telemetry.ConnectionCheckFinished(req.PlexServerConnectionId, result);
        if (result.IsFailed)
            await Send.FluentResult(result.ToResult(), ct);
        else
            await Send.FluentResult(result, x => x.ToDTO(), ct);
    }
}
