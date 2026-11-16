namespace Reaparr.Application;

public record QueueInspectPlexServerJobEndpointRequest(int PlexServerId);

public class QueueInspectPlexServerJobEndpointRequestValidator : Validator<QueueInspectPlexServerJobEndpointRequest>
{
    public QueueInspectPlexServerJobEndpointRequestValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
    }
}

public class QueueInspectPlexServerJobEndpoint
    : Endpoint<QueueInspectPlexServerJobEndpointRequest, ResultDTO<PlexServerDTO>>
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public QueueInspectPlexServerJobEndpoint(ICommandExecutor commandExecutor, ITelemetry telemetry)
    {
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexServerController + "/{PlexServerId}/inspect");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(QueueInspectPlexServerJobEndpointRequest req, CancellationToken ct)
    {
        var result = await _commandExecutor.Send(new QueueInspectPlexServerJobCommand([req.PlexServerId]), ct);
        _telemetry.ServerInspectionRequested(req.PlexServerId, result);
        await Send.FluentResult(result, ct);
    }
}
