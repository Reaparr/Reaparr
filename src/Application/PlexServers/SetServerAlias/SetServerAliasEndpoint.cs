namespace Reaparr.Application;

public record SetServerAliasRequest
{
    [RouteParam, BindFrom("PlexServerId")]
    public required int PlexServerId { get; init; }

    public required string ServerAlias { get; init; }
}

public class SetServerAliasRequestValidator : Validator<SetServerAliasRequest>
{
    public SetServerAliasRequestValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
        RuleFor(x => x.ServerAlias).Must(x => !string.IsNullOrWhiteSpace(x));
    }
}

public class SetServerAlias : Endpoint<SetServerAliasRequest, BaseResultDTO>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IServerSettingsModule _serverSettingsModule;
    private readonly ITelemetry _telemetry;
    public SetServerAlias(
        ILogger log,
        IReaparrDbContext dbContext,
        IServerSettingsModule serverSettingsModule,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<SetServerAlias>();
        _dbContext = dbContext;
        _serverSettingsModule = serverSettingsModule;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Put(ApiRoutes.PlexServerController + "/{PlexServerId}/set-server-alias");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(SetServerAliasRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var machineIdentifier = await _dbContext.GetPlexServerMachineIdentifierById(req.PlexServerId);
        if (machineIdentifier == string.Empty)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexServer), req.PlexServerId), ct);
            return;
        }

        var previousAlias = _serverSettingsModule.GetServerNameAlias(machineIdentifier);
        _serverSettingsModule.SetServerName(machineIdentifier, req.ServerAlias);
        var alias = _serverSettingsModule.GetServerNameAlias(machineIdentifier);
        if (!string.Equals(previousAlias, alias, StringComparison.Ordinal))
            _telemetry.PlexServerAliasChanged(req.PlexServerId, machineIdentifier, alias);
        await Send.FluentResult(Result.Ok(), ct);

    }
}
