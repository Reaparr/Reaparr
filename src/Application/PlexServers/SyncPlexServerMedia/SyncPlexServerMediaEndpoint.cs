namespace Reaparr.Application;

public record SyncPlexServerMediaEndpointRequest
{
    [RouteParam]
    public int PlexServerId { get; init; }

    public bool ForceLibrarySync { get; init; }

    public bool ForceMediaRefresh { get; init; }
}

/// <summary>
/// Sync all media from a Plex server by queuing sync jobs for all its libraries.
/// </summary>
public class SyncPlexServerMediaEndpointRequestValidator : Validator<SyncPlexServerMediaEndpointRequest>
{
    public SyncPlexServerMediaEndpointRequestValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
    }
}

public class SyncPlexServerMediaEndpoint : Endpoint<SyncPlexServerMediaEndpointRequest, BaseResultDTO>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public SyncPlexServerMediaEndpoint(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<SyncPlexServerMediaEndpoint>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.PlexServerController + "/{PlexServerId}/sync");

        Description(x =>
            x.Accepts<SyncPlexServerMediaEndpointRequest>()
                .Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(SyncPlexServerMediaEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);

        if (await _dbContext.IsServerDisabled(req.PlexServerId))
        {
            var serverName = await _dbContext.GetPlexServerNameById(req.PlexServerId);
            var warnResult = _log.Here()
                .WarningResult("Plex server {Name} is disabled and cannot be synced", serverName)
                .Add400BadRequestError();
            _telemetry.ServerMediaSyncRequested(ToTelemetryDTO(req), warnResult);
            await Send.FluentResult(warnResult, ct);
            return;
        }

        var libraryIds = await _dbContext
            .PlexLibraries.Where(x => x.PlexServerId == req.PlexServerId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken: ct);

        if (!libraryIds.Any())
        {
            var name = await _dbContext.GetPlexServerNameById(req.PlexServerId);
            var warnResult = _log.Here()
                .WarningResult("Plex server {Name} has no libraries available to sync", name)
                .Add400BadRequestError();
            _telemetry.ServerMediaSyncRequested(ToTelemetryDTO(req), warnResult);
            await Send.FluentResult(warnResult, ct);
            return;
        }

        var result = await _commandExecutor.Send(
            new QueueLibrarySyncJobCommand(libraryIds, req.ForceLibrarySync, req.ForceMediaRefresh),
            ct
        );
        _telemetry.ServerMediaSyncRequested(ToTelemetryDTO(req), result);
        await Send.FluentResult(result, ct);
    }

    private static ServerMediaSyncTelemetryDTO ToTelemetryDTO(SyncPlexServerMediaEndpointRequest request) =>
        new()
        {
            PlexServerId = request.PlexServerId,
            ForceLibrarySync = request.ForceLibrarySync,
            ForceMediaRefresh = request.ForceMediaRefresh,
        };
}
