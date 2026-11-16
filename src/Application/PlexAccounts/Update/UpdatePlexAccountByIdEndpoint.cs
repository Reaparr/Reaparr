namespace Reaparr.Application;

public class UpdatePlexAccountByIdEndpointRequest
{
    [FromBody]
    public PlexAccountDTO? PlexAccountDTO { get; init; }
}

public class UpdatePlexAccountByIdEndpointRequestValidator : Validator<UpdatePlexAccountByIdEndpointRequest>
{
    public UpdatePlexAccountByIdEndpointRequestValidator()
    {
        RuleFor(x => x.PlexAccountDTO)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(x => x.PlexAccountDTO!.Id).GreaterThan(0);
                RuleFor(x => x.PlexAccountDTO!.DisplayName).NotEmpty();
                RuleFor(x => x.PlexAccountDTO!.Username)
                    .NotEmpty()
                    .MinimumLength(5)
                    .When(m => string.IsNullOrEmpty(m.PlexAccountDTO!.CustomAuthenticationToken));

                RuleFor(x => x.PlexAccountDTO!.Password)
                    .NotEmpty()
                    .MinimumLength(5)
                    .When(m => string.IsNullOrEmpty(m.PlexAccountDTO!.CustomAuthenticationToken));
            });
    }
}

public class UpdatePlexAccountByIdEndpoint : Endpoint<UpdatePlexAccountByIdEndpointRequest, PlexAccountDTO>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ITelemetry _telemetry;

    public UpdatePlexAccountByIdEndpoint(IReaparrDbContext dbContext, ITelemetry telemetry)
    {
        _dbContext = dbContext;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Put(ApiRoutes.PlexAccountController);

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PlexAccountDTO>))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(UpdatePlexAccountByIdEndpointRequest req, CancellationToken ct)
    {
        var plexAccountDTO = req.PlexAccountDTO!;
        var accountInDb = await _dbContext
            .PlexAccounts.AsTracking()
            .Include(x => x.PlexAccountServers)
            .Include(x => x.PlexAccountLibraries)
            .GetAsync(plexAccountDTO.Id, ct);

        if (accountInDb == null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexAccount), plexAccountDTO.Id), ct);
            return;
        }
        var updatedPlexAccount = plexAccountDTO.ToModel();

        _dbContext.Entry(accountInDb).CurrentValues.SetValues(updatedPlexAccount);
        await _dbContext.SaveChangesAsync(ct);

        _telemetry.PlexAccountChanged(TelemetryAction.Update, accountInDb.Id, accountInDb);

        await Send.FluentResult(Result.Ok(accountInDb), x => x.ToDTO(), ct);
    }
}
