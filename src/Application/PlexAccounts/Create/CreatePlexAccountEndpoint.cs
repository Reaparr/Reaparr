namespace Reaparr.Application;

/// <summary>
/// Creates an <see cref="PlexAccount"/> in the Database and performs an QueueInspectPlexServerByPlexAccountIdJob().
/// </summary>
/// <returns>Returns the created <see cref="PlexAccount"/> as a DTO</returns>
public record CreatePlexAccountEndpointRequest
{
    public required string DisplayName { get; init; }

    public required string Username { get; init; }

    public required string Password { get; init; }

    public required bool IsEnabled { get; init; }

    public required bool IsMain { get; init; }

    public required bool IsValidated { get; init; }

    public required DateTime? ValidatedAt { get; init; }

    public required string Uuid { get; init; }

    public required long PlexId { get; init; }

    public required string Email { get; init; }

    public required string Title { get; init; }

    public required string ClientId { get; set; }

    public required bool Is2Fa { get; init; }

    /// <summary>
    /// The user has the option to provide their own token to authenticate with plex.tv.
    /// This is not the same as the auto filled AuthenticationToken when provided by the username and password
    /// </summary>
    public required string CustomAuthenticationToken { get; init; }

    public required string AuthenticationToken { get; init; }
}

public class CreatePlexAccountEndpointRequestValidator : Validator<CreatePlexAccountEndpointRequest>
{
    public CreatePlexAccountEndpointRequestValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.DisplayName).NotEmpty();

        RuleFor(x => x.ClientId).NotEmpty();

        RuleFor(x => x.PlexId).NotEmpty();

        RuleFor(x => x.Uuid).NotEmpty();

        RuleFor(x => x.IsValidated).Equal(true);
        RuleFor(x => x.ValidatedAt).NotNull();

        RuleFor(x => x.Username)
            .NotEmpty()
            .MinimumLength(5)
            .When(m => string.IsNullOrEmpty(m!.CustomAuthenticationToken));

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(5)
            .When(m => string.IsNullOrEmpty(m!.CustomAuthenticationToken));

        RuleFor(x => x.AuthenticationToken)
            .NotEmpty()
            .MinimumLength(5)
            .When(m => string.IsNullOrEmpty(m!.CustomAuthenticationToken));
    }
}

public class CreatePlexAccountEndpoint : Endpoint<CreatePlexAccountEndpointRequest, ResultDTO<PlexAccountDTO>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public CreatePlexAccountEndpoint(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<CreatePlexAccountEndpoint>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.PlexAccountController + "/");

        Description(x =>
            x.Produces(StatusCodes.Status201Created, typeof(ResultDTO<PlexAccountDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(CreatePlexAccountEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);

        var isAuthTokenMode = !(req.Username != string.Empty && req.Password != string.Empty);

        if (!isAuthTokenMode)
        {
            // Check if an account with the same username already exists
            var isAvailable = await _dbContext.IsUsernameAvailable(req.Username, ct);
            if (!isAvailable)
            {
                var msg =
                    $"Account with username {req.Username} cannot be created due to an account with the same username already existing";
                await Send.FluentResult(ResultExtensions.Create400BadRequestResult(msg).LogError(), ct);
                return;
            }

            // Check if account with the same UUID already exists
            var uuidResult = await _dbContext.PlexAccounts.Where(x => x.Uuid == req.Uuid).FirstOrDefaultAsync(ct);
            if (uuidResult is not null)
            {
                var badResult = ResultExtensions
                    .Create400BadRequestResult("Account with the same UUID {plexAccount.Uuid} already exists")
                    .LogWarning();
                await Send.FluentResult(badResult, ct);
                return;
            }
        }

        _log.Here().Debug("Creating account with username {DisplayName}", req.DisplayName);

        // Generate plexAccount clientId
        if (req.ClientId == string.Empty)
            req.ClientId = Guid.NewGuid().ToString();

        var plexAccountDb = new PlexAccount
        {
            Id = 0,
            DisplayName = req.DisplayName,
            Username = req.Username,
            Password = req.Password,
            IsEnabled = req.IsEnabled,
            IsValidated = req.IsValidated,
            ValidatedAt = req.ValidatedAt,
            PlexId = req.PlexId,
            Uuid = req.Uuid,
            ClientId = req.ClientId,
            Title = req.Title,
            Email = req.Email,
            HasPassword = true,
            CustomAuthenticationToken = req.CustomAuthenticationToken,
            AuthenticationToken = req.AuthenticationToken,
            IsMain = req.IsMain,
            Is2Fa = req.Is2Fa,
            VerificationCode = "",
        };

        await _dbContext.PlexAccounts.AddAsync(plexAccountDb, ct);

        await _dbContext.SaveChangesAsync(ct);
        await _dbContext.Entry(plexAccountDb).GetDatabaseValuesAsync(ct);

        plexAccountDb = await _dbContext
            .PlexAccounts.Include(x => x.PlexAccountServers)
            .Include(x => x.PlexAccountLibraries)
            .GetAsync(plexAccountDb.Id, ct);

        if (plexAccountDb is null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexAccount), 0), ct);
            return;
        }

        _telemetry.PlexAccountChanged(TelemetryAction.Create, plexAccountDb.Id, plexAccountDb);
        var result = Result.Ok(plexAccountDb).Add201CreatedRequestSuccess("PlexAccount created successfully.");

        await Send.FluentResult(result, model => model.ToDTO(), ct);

        // Return the Ok result and then kick off the inspecting job
        var inspectResult = await _commandExecutor.Send(
            new InspectAllPlexServersByAccountIdCommand(plexAccountDb.Id),
            ct
        );

        if (inspectResult.IsFailed)
            _log.Here()
                .Error("Failed to queue inspect server job for PlexAccount with id {PlexAccountId}", plexAccountDb.Id);
    }
}
