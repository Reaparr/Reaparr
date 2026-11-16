namespace Reaparr.Application;

/// <summary>
/// Stop a currently downloading <see cref="DownloadTaskGeneric"/>.
/// </summary>
public record StopDownloadTaskEndpointRequest
{
    [RouteParam, BindFrom("DownloadTaskGuid")]
    public required Guid DownloadTaskGuid { get; init; }
}

public class StopDownloadTaskEndpointRequestValidator : Validator<StopDownloadTaskEndpointRequest>
{
    public StopDownloadTaskEndpointRequestValidator()
    {
        RuleFor(x => x.DownloadTaskGuid).NotEmpty();
    }
}

public class StopDownloadTaskEndpoint : Endpoint<StopDownloadTaskEndpointRequest, BaseResultDTO>
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public StopDownloadTaskEndpoint(ICommandExecutor commandExecutor, ITelemetry telemetry)
    {
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Put(ApiRoutes.DownloadController + "/stop/{DownloadTaskGuid}");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(StopDownloadTaskEndpointRequest req, CancellationToken ct)
    {
        var stopResult = await _commandExecutor.Send(new StopDownloadTaskCommand(req.DownloadTaskGuid), ct);

        _telemetry.DownloadControlProcessed(TelemetryAction.Stop, req.DownloadTaskGuid, stopResult, deleteFiles: true);

        await Send.FluentResult(stopResult, ct);
    }
}
