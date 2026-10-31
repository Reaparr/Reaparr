namespace Reaparr.Application;

public record UpdatePlexServerConnectionEndpointRequest
{
    public required int Id { get; init; }

    public required string Url { get; init; }

    public required string Protocol { get; init; }

    public required string Address { get; init; }

    public required int Port { get; init; }

    public required int PlexServerId { get; set; }
};

public class UpdatePlexServerConnectionEndpointRequestValidator : Validator<UpdatePlexServerConnectionEndpointRequest>
{
    public UpdatePlexServerConnectionEndpointRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.PlexServerId).GreaterThan(0);
        RuleFor(x => x.Url)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(url => url is not null && url.Trim().IsValidHttpUrl())
            .WithMessage("string is not a valid URL");
        RuleFor(x => x.Protocol).NotEmpty();
        RuleFor(x => x.Address).NotEmpty();
        RuleFor(x => x.Port).GreaterThan(0);
    }
}

public class UpdatePlexServerConnectionEndpoint
    : Endpoint<UpdatePlexServerConnectionEndpointRequest, ResultDTO<PlexServerConnectionDTO>>
{
    private readonly IReaparrDbContext _dbContext;

    public UpdatePlexServerConnectionEndpoint(IReaparrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Patch(ApiRoutes.PlexServerConnectionController);

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PlexServerConnectionDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(UpdatePlexServerConnectionEndpointRequest req, CancellationToken ct)
    {
        var existing = await _dbContext.PlexServerConnections.GetAsync(req.Id, ct);
        if (existing is null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexServerConnection), req.Id), ct);
            return;
        }

        if (!existing.IsCustom)
        {
            await Send.FluentResult(
                ResultExtensions.Create400BadRequestResult("Only custom connections can be edited"),
                ct
            );
            return;
        }

        if (existing.PlexServerId != req.PlexServerId)
        {
            await Send.FluentResult(
                ResultExtensions.Create400BadRequestResult("A connection cannot be moved to another Plex server"),
                ct
            );
            return;
        }

        var url = req.Url.Trim();
        var connection = new PlexServerConnection
        {
            Id = existing.Id,
            Protocol = req.Protocol.ToLower(),
            Address = req.Address,
            Port = req.Port,
            Url = url,
            Local = url.IsLocalUrl(),
            Relay = req.Address.Contains("plex.direct"),
            IPv4 = req.Address.IsIpv4(),
            IPv6 = req.Address.IsIPv6(),
            PlexServerId = existing.PlexServerId,
            IsCustom = true,
        };

        _dbContext.PlexServerConnections.Update(connection);

        await _dbContext.SaveChangesAsync(ct);

        var connectionDb = await _dbContext.PlexServerConnections.GetAsync(req.Id, ct);

        var result = ResultExtensions.Create200OkResult(connectionDb!.ToDTO());
        await Send.FluentResult(result, ct);
    }
}
