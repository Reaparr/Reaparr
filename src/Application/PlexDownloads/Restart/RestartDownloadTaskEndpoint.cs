namespace Reaparr.Application;

/// <summary>
/// Restart a currently downloading <see cref="DownloadTaskGeneric"/>.
/// </summary>
public record RestartDownloadTaskEndpointRequest
{
    [RouteParam, BindFrom("DownloadTaskGuid")]
    public required Guid DownloadTaskGuid { get; init; }
}

public class RestartDownloadTaskEndpointRequestValidator : Validator<RestartDownloadTaskEndpointRequest>
{
    public RestartDownloadTaskEndpointRequestValidator()
    {
        RuleFor(x => x.DownloadTaskGuid).NotEmpty();
    }
}

public class RestartDownloadTaskEndpoint
    : Endpoint<RestartDownloadTaskEndpointRequest>
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public RestartDownloadTaskEndpoint(ICommandExecutor commandExecutor, ITelemetry telemetry)
    {
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Put(ApiRoutes.DownloadController + "/restart/{DownloadTaskGuid}");

        Description(x =>
            x.Accepts<RestartDownloadTaskEndpointRequest>()
                .Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(RestartDownloadTaskEndpointRequest req, CancellationToken ct)
    {
        var restartResult = await _commandExecutor.Send(new RestartDownloadTaskCommand(req.DownloadTaskGuid), ct);

        _telemetry.DownloadControlProcessed(TelemetryAction.Restart, req.DownloadTaskGuid, restartResult);

        await Send.FluentResult(restartResult, ct);
    }
}
