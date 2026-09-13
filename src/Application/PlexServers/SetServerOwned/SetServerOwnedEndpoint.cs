namespace Reaparr.Application;

public record SetServerOwnedRequest
{
    [RouteParam, BindFrom("PlexServerId")]
    public int PlexServerId { get; init; }

    public bool IsOwned { get; init; }
}

public class SetServerOwnedRequestValidator : Validator<SetServerOwnedRequest>
{
    public SetServerOwnedRequestValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
    }
}

public class SetServerOwnedEndpoint : Endpoint<SetServerOwnedRequest, ResultDTO<PlexServerDTO>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public SetServerOwnedEndpoint(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor
    )
    {
        _log = log.ForContext<SetServerOwnedEndpoint>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Put(ApiRoutes.PlexServerController + "/{PlexServerId}/set-server-owned");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PlexServerDTO>))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(SetServerOwnedRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);

        var plexServerExists = await _dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .AnyAsync(x => x.Id == req.PlexServerId, ct);
        if (!plexServerExists)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexServer), req.PlexServerId), ct);
            return;
        }

        var updateCount = await _dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == req.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, req.IsOwned), ct);

        if (updateCount == 0)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexServer), req.PlexServerId), ct);
            return;
        }

        var plexServer = await _dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Include(x => x.PlexAccountServers)
            .GetAsync(req.PlexServerId, ct);

        if (plexServer is null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexServer), req.PlexServerId), ct);
            return;
        }

        var libraries = await _dbContext
            .PlexLibraries.IgnoreQueryFilters()
            .Where(x => x.PlexServerId == req.PlexServerId)
            .Select(x => new { x.Id, x.IsEnabled })
            .ToListAsync(ct);
        var libraryIds = libraries.Select(x => x.Id).ToList();


        var invalidationResult = await _commandExecutor.Send(
            new InvalidateLibraryComparisonJobsCommand(libraryIds),
            ct
        );
        if (invalidationResult.IsFailed)
        {
            await Send.FluentResult(invalidationResult.ToResult(), ct);
            return;
        }

        var failedResults = new List<ResultBase>();
        foreach (var lib in libraries.Where(x => x.IsEnabled))
        {
            var queueResult = await _commandExecutor.Send(new ScheduleAffectedLibraryComparisonJobsCommand(lib.Id), ct);
            if (queueResult.IsCancelled)
            {
                await Send.FluentResult(queueResult, ct);
                return;
            }

            if (queueResult.IsFailed)
                failedResults.Add(queueResult);
        }

        if (failedResults.Count > 0)
        {
            await Send.FluentResult(Result.Merge(failedResults.ToArray()).LogError(), ct);
            return;
        }

        await Send.FluentResult(Result.Ok(plexServer), x => x.ToDTO(), ct);
    }
}
