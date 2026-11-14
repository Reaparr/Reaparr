namespace Reaparr.Application;

public record RefreshPlexAccountAccessEndpointRequest(int PlexAccountId = 0);

public class RefreshPlexAccountAccessEndpointRequestValidator : Validator<RefreshPlexAccountAccessEndpointRequest>
{
    public RefreshPlexAccountAccessEndpointRequestValidator()
    {
        RuleFor(x => x.PlexAccountId).GreaterThanOrEqualTo(0);
    }
}

public class RefreshPlexAccountAccessEndpoint
    : Endpoint<RefreshPlexAccountAccessEndpointRequest, ResultDTO<List<RefreshPlexAccountAccessRapportDTO>>>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public RefreshPlexAccountAccessEndpoint(ILogger log, ICommandExecutor commandExecutor, ITelemetry telemetry)
    {
        _log = log.ForContext<RefreshPlexAccountAccessEndpoint>();
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexAccountController + "/refresh/{PlexAccountId}");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<List<RefreshPlexAccountAccessRapportDTO>>))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(RefreshPlexAccountAccessEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var result = await _commandExecutor.Send(new RefreshPlexAccountAccessCommand(req.PlexAccountId), ct);
        _telemetry.CaptureErrors(result, TelemetryActivity.PlexAccessRefresh);
        _telemetry.PlexAccessRefreshRequested(
            TelemetryScope.Account,
            result,
            accountId: req.PlexAccountId > 0 ? req.PlexAccountId : null
        );
        await Send.FluentResult(result, ct);
    }
}
