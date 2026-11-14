namespace Reaparr.Application;

public class RefreshLibraryMediaEndpointRequestValidator : Validator<RefreshLibraryMediaEndpointRequest>
{
    public RefreshLibraryMediaEndpointRequestValidator()
    {
        RuleFor(x => x.PlexLibraryId).GreaterThan(0);
    }
}

public class RefreshLibraryMediaEndpoint
    : Endpoint<RefreshLibraryMediaEndpointRequest, ResultDTO<LibrarySyncJobQueueDTO>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public RefreshLibraryMediaEndpoint(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<RefreshLibraryMediaEndpoint>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.PlexLibraryController + "/refresh/{PlexLibraryId}");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<LibrarySyncJobQueueDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(RefreshLibraryMediaEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);

        var result = await _commandExecutor.Send(
            new QueueLibrarySyncJobCommand([req.PlexLibraryId], req.ForceLibrarySync, req.ForceMediaRefresh),
            ct
        );
        if (result.IsCancelledOrFailed)
        {
            _telemetry.LibraryRefreshRequested(req, result);
            await Send.FluentResult(result, ct);
            return;
        }

        var plexLibrary = await _dbContext.PlexLibraries.GetAsync(req.PlexLibraryId, cancellationToken: ct);

        if (plexLibrary is null)
        {
            var notFound = ResultExtensions.EntityNotFound(nameof(PlexLibrary), req.PlexLibraryId);
            _telemetry.LibraryRefreshRequested(req, notFound);
            await Send.FluentResult(notFound, ct: ct);
            return;
        }

        var queueItem = await _dbContext
            .LibrarySyncJobQueues.Where(x => x.PlexLibraryId == req.PlexLibraryId)
            .FirstOrDefaultAsync(ct);
        if (queueItem is null)
        {
            var notFound = ResultExtensions.EntityNotFound(nameof(LibrarySyncJobQueue), req.PlexLibraryId);
            _telemetry.LibraryRefreshRequested(req, notFound);
            await Send.FluentResult(notFound, ct: ct);
            return;
        }

        _telemetry.LibraryRefreshRequested(req, result, queueItem.Status);
        await Send.FluentResult(Result.Ok(queueItem), x => x.ToDTO(), ct);
    }
}
