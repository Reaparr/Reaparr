namespace Reaparr.Application;

/// <summary>
/// Pause a currently downloading <see cref="DownloadTaskGeneric"/>.
/// </summary>
public record PauseDownloadTaskEndpointRequest
{
    /// <summary>
    /// The id of the <see cref="DownloadTaskGeneric"/> to pause.
    /// </summary>
    [RouteParam, BindFrom("DownloadTaskGuid")]
    public required Guid DownloadTaskGuid { get; init; }
}

public class PauseDownloadTaskEndpointRequestValidator : Validator<PauseDownloadTaskEndpointRequest>
{
    public PauseDownloadTaskEndpointRequestValidator()
    {
        RuleFor(x => x.DownloadTaskGuid).NotEmpty();
    }
}

public class PauseDownloadTaskEndpoint : Endpoint<PauseDownloadTaskEndpointRequest>
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public PauseDownloadTaskEndpoint(ICommandExecutor commandExecutor, ITelemetry telemetry)
    {
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Put(ApiRoutes.DownloadController + "/pause/{DownloadTaskGuid}");

        Description(x =>
            x.Accepts<PauseDownloadTaskEndpointRequest>()
                .Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(PauseDownloadTaskEndpointRequest req, CancellationToken ct)
    {
        var pauseResult = await _commandExecutor.Send(new PauseDownloadTaskCommand(req.DownloadTaskGuid), ct);

        _telemetry.DownloadControlProcessed(TelemetryAction.Pause, req.DownloadTaskGuid, pauseResult);

        await Send.FluentResult(pauseResult, ct);
    }
}
