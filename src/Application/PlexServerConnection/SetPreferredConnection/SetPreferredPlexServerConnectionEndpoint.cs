namespace Reaparr.Application;

/// <summary>
/// Sets the preferred connection for a <see cref="PlexServer"/>
/// </summary>
/// <param name="PlexServerId"> The id of the <see cref="PlexServer"/> to set the preferred connection for.</param>
/// <param name="PlexServerConnectionId"> The id of the <see cref="PlexServerConnection"/> to set as preferred.</param>
/// <returns></returns>
public record SetPreferredPlexServerConnectionEndpointRequest(int PlexServerId, int PlexServerConnectionId);

public class SetPreferredPlexServerConnectionEndpointRequestValidator
    : Validator<SetPreferredPlexServerConnectionEndpointRequest>
{
    public SetPreferredPlexServerConnectionEndpointRequestValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
        RuleFor(x => x.PlexServerConnectionId).GreaterThan(0);
    }
}

public class SetPreferredPlexServerConnectionEndpoint
    : Endpoint<SetPreferredPlexServerConnectionEndpointRequest, BaseResultDTO>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ITelemetry _telemetry;

    public SetPreferredPlexServerConnectionEndpoint(ILogger log, IReaparrDbContext dbContext, ITelemetry telemetry)
    {
        _log = log.ForContext<SetPreferredPlexServerConnectionEndpoint>();
        _dbContext = dbContext;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexServerController + "/{PlexServerId}/preferred-connection/{PlexServerConnectionId}");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(SetPreferredPlexServerConnectionEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var plexServerConnectionId = req.PlexServerConnectionId;
        var plexServerId = req.PlexServerId;

        _log.Here()
            .Debug(
                "Setting the preferred {NameOfPlexServerConnection} for {PlexServerIdName}: {PlexServerId}",
                nameof(PlexServerConnection),
                nameof(plexServerId),
                plexServerId
            );

        var plexServer = await _dbContext
            .PlexServers.Include(x => x.PlexServerConnections)
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == plexServerId, ct);

        if (plexServer is null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexServer), plexServerId).LogError(), ct);
            return;
        }

        var connectionIds = plexServer.PlexServerConnections.Select(x => x.Id).ToList();
        if (!connectionIds.Contains(plexServerConnectionId))
        {
            await Send.FluentResult(
                Result
                    .Fail(
                        "PlexServer with id {PlexServerId} has no connections with id {PlexServerConnectionId} and can not set that as preferred",
                        plexServerId,
                        plexServerConnectionId
                    )
                    .LogError(),
                ct
            );
            return;
        }

        var preferredConnection = plexServer.PlexServerConnections.First(x => x.Id == plexServerConnectionId);

        plexServer.PreferredConnectionId = plexServerConnectionId;

        await _dbContext.SaveChangesAsync(ct);

        _telemetry.PlexConnectionChanged(TelemetryAction.Preferred, preferredConnection, plexServerConnectionId);

        await Send.FluentResult(Result.Ok(), ct);
    }
}
