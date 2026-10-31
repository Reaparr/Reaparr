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

    public RefreshPlexServerAccountsAccessEndpoint(IReaparrDbContext dbContext, ICommandExecutor commandExecutor)
    {
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Post(ApiRoutes.PlexServerController + "/{PlexServerId}/refresh-access");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<List<RefreshPlexAccountAccessRapportDTO>>))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(RefreshPlexServerAccountsAccessEndpointRequest req, CancellationToken ct)
    {
        var server = await _dbContext.PlexServers.IgnoreIsEnabledFilter().GetAsync(req.PlexServerId, ct);
        if (server is null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexServer), req.PlexServerId), ct);
            return;
        }

        if (!server.IsEnabled)
        {
            await Send.FluentResult(
                ResultExtensions.ServerIsDisabled(
                    server.Name,
                    server.Id,
                    nameof(RefreshPlexServerAccountsAccessEndpoint)
                ),
                ct
            );
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
            var result = await _commandExecutor.Send(new RefreshPlexAccountAccessCommand(accountId), ct);

            if (result.IsCancelled)
            {
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

        await Send.FluentResult(Result.Ok(rapports).WithErrors(errors), ct);
    }
}
