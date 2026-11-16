namespace Reaparr.Application;

public record RefreshPlexServerAccountsAccessEndpointRequest
{
    [RouteParam, BindFrom("PlexServerId")]
    public int PlexServerId { get; init; }
}

public class RefreshPlexServerAccountsAccessEndpointRequestValidator
    : Validator<RefreshPlexServerAccountsAccessEndpointRequest>
{
    public RefreshPlexServerAccountsAccessEndpointRequestValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
    }
}

public class RefreshPlexServerAccountsAccessEndpoint
    : Endpoint<RefreshPlexServerAccountsAccessEndpointRequest, ResultDTO<List<RefreshPlexAccountAccessRapportDTO>>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;
    public RefreshPlexServerAccountsAccessEndpoint(
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        ITelemetry telemetry
    )
    {
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.PlexServerController + "/{PlexServerId}/refresh-access");

        Description(x =>
            x.Produces<ResultDTO<List<RefreshPlexAccountAccessRapportDTO>>>()
                .Produces<BaseResultDTO>(StatusCodes.Status404NotFound)
                .Produces<BaseResultDTO>(StatusCodes.Status500InternalServerError)
        );
    }

    public override async Task HandleAsync(RefreshPlexServerAccountsAccessEndpointRequest req, CancellationToken ct)
    {
        var server = await _dbContext.PlexServers.IgnoreIsEnabledFilter().GetAsync(req.PlexServerId, ct);
        if (server is null)
        {
            var result = ResultExtensions.EntityNotFound(nameof(PlexServer), req.PlexServerId);
            _telemetry.PlexAccessRefreshRequested(TelemetryScope.Server, result, serverId: req.PlexServerId);
            await Send.FluentResult(result, ct);
            return;
        }

        if (!server.IsEnabled)
        {
            var result = ResultExtensions.ServerIsDisabled(
                server.Name,
                server.Id,
                nameof(RefreshPlexServerAccountsAccessEndpoint)
            );
            _telemetry.PlexAccessRefreshRequested(TelemetryScope.Server, result, serverId: req.PlexServerId);
            await Send.FluentResult(result, ct);
            return;
        }

        var accountIds = await _dbContext
            .PlexAccountServers.Where(x => x.PlexServerId == req.PlexServerId && x.PlexAccount!.IsEnabled)
            .Select(x => x.PlexAccountId)
            .Distinct()
            .ToListAsync(ct);

        var rapports = new List<RefreshPlexAccountAccessRapportDTO>();
        var errors = new List<IError>();
        foreach (var accountId in accountIds)
        {
            var result = await _commandExecutor.Send(
                new RefreshPlexAccountAccessCommand(accountId),
                ct
            );

            if (result.IsCancelled)
            {
                _telemetry.PlexAccessRefreshRequested(
                    TelemetryScope.Server,
                    result,
                    serverId: req.PlexServerId,
                    accountCount: accountIds.Count
                );
                await Send.FluentResult(result, ct);
                return;
            }

            if (result.IsFailed)
            {
                result.LogError();
                errors.AddRange(result.Errors);
                continue;
            }

            rapports.AddRange(result.Value);
        }

        var refreshResult = Result.Ok(rapports).WithErrors(errors);
        _telemetry.PlexAccessRefreshRequested(
            TelemetryScope.Server,
            refreshResult,
            serverId: req.PlexServerId,
            accountCount: accountIds.Count
        );
        await Send.FluentResult(refreshResult, ct);
    }
}
