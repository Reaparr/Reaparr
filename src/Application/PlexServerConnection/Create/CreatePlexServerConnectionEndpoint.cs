namespace Reaparr.Application;

public record CreatePlexServerConnectionEndpointRequest
{
    public required string Url { get; init; }

    public required string Protocol { get; init; }

    public required string Address { get; init; }

    public required int Port { get; init; }

    public required int PlexServerId { get; set; }
};

public class CreatePlexServerConnectionEndpointRequestValidator : Validator<CreatePlexServerConnectionEndpointRequest>
{
    public CreatePlexServerConnectionEndpointRequestValidator()
    {
        RuleFor(x => x.Url)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(url => url is not null && url.Trim().IsValidHttpUrl())
            .WithMessage("string is not a valid URL");
        RuleFor(x => x.Protocol).NotEmpty();
        RuleFor(x => x.Address).NotEmpty();
        RuleFor(x => x.Port).GreaterThan(0);
        RuleFor(x => x.PlexServerId).GreaterThan(0);
    }
}

public class CreatePlexServerConnectionEndpoint
    : Endpoint<CreatePlexServerConnectionEndpointRequest, ResultDTO<PlexServerConnectionDTO>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ITelemetry _telemetry;

    public CreatePlexServerConnectionEndpoint(ILogger log, IReaparrDbContext dbContext, ITelemetry telemetry)
    {
        _log = log.ForContext<CreatePlexServerConnectionEndpoint>();
        _dbContext = dbContext;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.PlexServerConnectionController);

        Description(x =>
            x.ClearDefaultProduces()
                .Produces(StatusCodes.Status201Created, typeof(ResultDTO<PlexServerConnectionDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(CreatePlexServerConnectionEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var url = req.Url.Trim();
        var connection = new PlexServerConnection
        {
            Id = 0,
            Protocol = req.Protocol.ToLower(),
            Address = req.Address,
            Port = req.Port,
            Url = url,
            Local = url.IsLocalUrl(),
            Relay = req.Address.Contains("plex.direct"),
            IPv4 = req.Address.IsIpv4(),
            IPv6 = req.Address.IsIPv6(),
            PlexServerId = req.PlexServerId,
            IsCustom = true,
        };
        _dbContext.PlexServerConnections.Add(connection);
        await _dbContext.SaveChangesAsync(ct);

        _telemetry.PlexConnectionChanged(TelemetryAction.Create, connection, null);

        var result = ResultExtensions.Create201CreatedResult(connection.ToDTO());
        await Send.FluentResult(result, ct);
    }
}
