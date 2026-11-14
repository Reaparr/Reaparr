namespace Reaparr.Application;

public record RefreshPlexServerConnectionsEndpointRequest(int PlexServerId);

public class RefreshPlexServerConnectionsEndpointRequestValidator
    : Validator<RefreshPlexServerConnectionsEndpointRequest>
{
    public RefreshPlexServerConnectionsEndpointRequestValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
    }
}

public class RefreshPlexServerConnectionsEndpoint : Endpoint<RefreshPlexServerConnectionsEndpointRequest>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public RefreshPlexServerConnectionsEndpoint(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<RefreshPlexServerConnectionsEndpoint>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexServerController + "/{PlexServerId}/refresh");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PlexServerDTO>))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(RefreshPlexServerConnectionsEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        // Pick an account that has access to the PlexServer to connect with
        var plexAccountResult = await _dbContext.ChoosePlexAccountToConnect(req.PlexServerId, ct);
        if (plexAccountResult.IsFailed)
        {
            _telemetry.PlexAccessRefreshRequested(
                TelemetryScope.Account,
                plexAccountResult,
                serverId: req.PlexServerId
            );
            await Send.FluentResult(plexAccountResult.ToResult(), ct);
            return;
        }

        var refreshResult = await _commandExecutor.Send(
            new RefreshPlexAccountAccessCommand(plexAccountResult.Value.Id, TelemetryTrigger.Manual),
            ct
        );
        _telemetry.PlexAccessRefreshRequested(
            TelemetryScope.Account,
            refreshResult,
            accountId: plexAccountResult.Value.Id,
            serverId: req.PlexServerId
        );
        await Send.FluentResult(refreshResult.ToResult(), ct);
    }
}
