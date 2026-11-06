namespace Reaparr.Application;

/// <summary>
/// Start a currently downloading <see cref="DownloadTaskGeneric"/>.
/// </summary>
public record StartDownloadTaskEndpointRequest
{
    [RouteParam, BindFrom("DownloadTaskGuid")]
    public required Guid DownloadTaskGuid { get; init; }
}

public class StartDownloadTaskEndpointRequestValidator : Validator<StartDownloadTaskEndpointRequest>
{
    public StartDownloadTaskEndpointRequestValidator()
    {
        RuleFor(x => x.DownloadTaskGuid).NotEmpty();
    }
}

public class StartDownloadTaskEndpoint : Endpoint<StartDownloadTaskEndpointRequest, BaseResultDTO>
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public StartDownloadTaskEndpoint(ICommandExecutor commandExecutor, ITelemetry telemetry)
    {
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Put(ApiRoutes.DownloadController + "/start/{DownloadTaskGuid}");

        Description(x =>
            x.Accepts<StartDownloadTaskEndpointRequest>()
                .Produces(StatusCodes.Status202Accepted, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(StartDownloadTaskEndpointRequest req, CancellationToken ct)
    {
        var startResult = await _commandExecutor.Send(new StartDownloadTaskCommand(req.DownloadTaskGuid), ct);

        _telemetry.DownloadControlProcessed(TelemetryAction.Start, req.DownloadTaskGuid, startResult);

        await Send.FluentResult(startResult, ct);
    }
}
