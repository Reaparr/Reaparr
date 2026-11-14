namespace Reaparr.Application;

public record CheckAllConnectionsStatusByPlexServerRequest(int PlexServerId);

public class CheckAllConnectionsStatusByPlexServerRequestValidator
    : Validator<CheckAllConnectionsStatusByPlexServerRequest>
{
    public CheckAllConnectionsStatusByPlexServerRequestValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
    }
}

public class CheckAllConnectionsStatusByPlexServerEndpoint
    : Endpoint<CheckAllConnectionsStatusByPlexServerRequest, List<PlexServerStatusDTO>>
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public CheckAllConnectionsStatusByPlexServerEndpoint(
        ICommandExecutor commandExecutor,
        ITelemetry telemetry
    )
    {
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexServerConnectionController + "/check/by-server/{PlexServerId}");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<List<PlexServerStatusDTO>>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(CheckAllConnectionsStatusByPlexServerRequest req, CancellationToken ct)
    {
        var result = await _commandExecutor.Send(
            new CheckAllConnectionsStatusByPlexServerCommand(req.PlexServerId),
            ct
        );
        _telemetry.ConnectionCheckFinished(req.PlexServerId, result);
        if (result.IsFailed)
            await Send.FluentResult(result.ToResult(), ct);
        else
            await Send.FluentResult(result, x => x.ToDTO(), ct);
    }
}
