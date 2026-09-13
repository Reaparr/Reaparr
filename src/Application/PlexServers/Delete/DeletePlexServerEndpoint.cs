namespace Reaparr.Application;

public record DeletePlexServerEndpointRequest
{
    [RouteParam, BindFrom("PlexServerId")]
    public int PlexServerId { get; init; }
}

public class DeletePlexServerEndpointRequestValidator : Validator<DeletePlexServerEndpointRequest>
{
    public DeletePlexServerEndpointRequestValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
    }
}

public class DeletePlexServerEndpoint : Endpoint<DeletePlexServerEndpointRequest, BaseResultDTO>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IMediaOverviewReadStore _mediaOverviewReadStore;

    public DeletePlexServerEndpoint(
        ILogger log,
        IReaparrDbContext dbContext,
        IMediaOverviewReadStore mediaOverviewReadStore
    )
    {
        _log = log.ForContext<DeletePlexServerEndpoint>();
        _dbContext = dbContext;
        _mediaOverviewReadStore = mediaOverviewReadStore;
    }

    public override void Configure()
    {
        Delete(ApiRoutes.PlexServerController + "/{PlexServerId}");

        Description(x =>
            x.Accepts<DeletePlexServerEndpointRequest>()
                .Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(DeletePlexServerEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);

        var libraryIds = await _dbContext
            .PlexLibraries.IgnoreQueryFilters()
            .Where(x => x.PlexServerId == req.PlexServerId)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var deletedPlexServersCount = await _dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == req.PlexServerId)
            .ExecuteDeleteAsync(ct);

        if (deletedPlexServersCount == 0)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexServer), req.PlexServerId), ct);
            return;
        }

        var rebuildResult = await _mediaOverviewReadStore.RebuildAsync(ct);
        rebuildResult.LogIfFailed();

        await Send.FluentResult(rebuildResult, ct);
    }
}
