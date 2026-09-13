namespace Reaparr.Application;

public record SetServerEnabledRequest
{
    [RouteParam, BindFrom("PlexServerId")]
    public int PlexServerId { get; init; }

    public bool IsEnabled { get; init; } = true;
}

public class SetServerEnabledRequestValidator : Validator<SetServerEnabledRequest>
{
    public SetServerEnabledRequestValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
    }
}

public class SetServerEnabledEndpoint : Endpoint<SetServerEnabledRequest, ResultDTO<PlexServerDTO>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public SetServerEnabledEndpoint(ILogger log, IReaparrDbContext dbContext, ICommandExecutor commandExecutor)
    {
        _log = log.ForContext<SetServerEnabledEndpoint>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Put(ApiRoutes.PlexServerController + "/{PlexServerId}/set-server-enabled");

        Description(x =>
            x.Accepts<SetServerEnabledRequest>()
                .Produces(StatusCodes.Status200OK, typeof(ResultDTO<PlexServerDTO>))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(SetServerEnabledRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var machineIdentifier = await _dbContext.GetPlexServerMachineIdentifierById(req.PlexServerId);
        if (machineIdentifier == string.Empty)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexServer), req.PlexServerId), ct);
            return;
        }

        var updateCount = await _dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == req.PlexServerId)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.IsEnabled, req.IsEnabled), ct);

        if (updateCount == 0)
        {
            if (await _dbContext.IsServerDisabled(req.PlexServerId))
            {
                var serverName = await _dbContext.GetPlexServerNameById(req.PlexServerId);
                await Send.FluentResult(
                    ResultExtensions.ServerIsDisabled(serverName, req.PlexServerId, nameof(GetPlexServerByIdEndpoint)),
                    ct
                );
                return;
            }
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

        var rebuildResult = await _commandExecutor.Send(new QueueMediaOverviewRebuildCommand(), ct);
        rebuildResult.LogIfFailed();
        if (rebuildResult.IsFailed)
        {
            await Send.FluentResult(rebuildResult, ct);
            return;
        }

        await Send.FluentResult(Result.Ok(plexServer), x => x.ToDTO(), ct);
    }
}
