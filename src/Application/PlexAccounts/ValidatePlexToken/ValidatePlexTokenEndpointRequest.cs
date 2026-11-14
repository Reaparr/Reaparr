namespace Reaparr.Application;

public record ValidatePlexTokenEndpointRequest
{
    /// <summary>
    /// The database account to update after validation. A value of 0 indicates an account that has not been created yet.
    /// </summary>
    public int PlexAccountId { get; init; }

    public required string DisplayName { get; init; } = "UnknownDisplayName";
    public required string ManualAuthenticationToken { get; init; }
}

public class ValidatePlexTokenEndpointResponse
{
    public required bool IsUnAuthorized { get; set; }

    public required string ClientId { get; init; }

    public required string Username { get; init; }

    public required string Email { get; init; }

    public required string Title { get; init; }

    public required long PlexId { get; init; }

    public required string Uuid { get; init; }

    public required string CustomAuthenticationToken { get; init; }

    public required bool IsValidated { get; init; }

    public required DateTime? ValidatedAt { get; init; }

    public required bool Is2Fa { get; init; }
}

public class ValidatePlexTokenPlexAccountRequestValidator : Validator<ValidatePlexTokenEndpointRequest>
{
    public ValidatePlexTokenPlexAccountRequestValidator()
    {
        RuleFor(x => x.PlexAccountId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ManualAuthenticationToken).NotEmpty().MinimumLength(5);
    }
}

public class ValidatePlexTokenEndpoint
    : Endpoint<ValidatePlexTokenEndpointRequest, ResultDTO<ValidatePlexTokenEndpointResponse>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly INotificationHubService _notificationHubService;
    private readonly ITelemetry _telemetry;

    public ValidatePlexTokenEndpoint(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        INotificationHubService notificationHubService,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<ValidatePlexTokenEndpoint>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _notificationHubService = notificationHubService;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.PlexAccountController + "/validate/token");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<ValidatePlexTokenEndpointResponse>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status401Unauthorized, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(ValidatePlexTokenEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);

        var validateResult = await _commandExecutor.Send(
            new ValidatePlexTokenCommand(req.ManualAuthenticationToken),
            ct
        );

        // If the PlexAPI rejects the token with 401 Unauthorized
        if (validateResult.HasPlex401UnauthorizedError())
        {
            await PersistValidationResult(req.PlexAccountId, false, null, ct);

            _log.Here()
                .Warning(
                    "Failed to validate the PlexAccount Authentication Token for user {PlexAccountDisplayName} from the PlexApi",
                    req.DisplayName
                );

            var response = new ValidatePlexTokenEndpointResponse
            {
                IsUnAuthorized = true,
                ClientId = string.Empty,
                Username = string.Empty,
                Email = string.Empty,
                Title = string.Empty,
                PlexId = 0,
                Uuid = string.Empty,
                CustomAuthenticationToken = string.Empty,
                IsValidated = false,
                ValidatedAt = null,
                Is2Fa = false,
            };
            _telemetry.PlexAuthenticationProcessed(req.PlexAccountId, null, validateResult);
            await Send.FluentResult(Result.Ok(response), ct);
            return;
        }

        if (validateResult.IsSuccess)
        {
            await PersistValidationResult(
                req.PlexAccountId,
                validateResult.Value.IsValidated,
                validateResult.Value.ValidatedAt,
                ct
            );

            _log.Here()
                .Information(
                    "Successfully validated the PlexAccount Authentication Token for user {PlexAccountDisplayName} from the PlexApi",
                    req.DisplayName
                );

            var response = new ValidatePlexTokenEndpointResponse
            {
                IsUnAuthorized = false,
                ClientId = validateResult.Value.ClientId,
                Username = validateResult.Value.Username,
                Email = validateResult.Value.Email,
                Title = validateResult.Value.Title,
                PlexId = validateResult.Value.PlexId,
                Uuid = validateResult.Value.Uuid,
                CustomAuthenticationToken = validateResult.Value.AuthenticationToken,
                IsValidated = validateResult.Value.IsValidated,
                ValidatedAt = validateResult.Value.ValidatedAt,
                Is2Fa = validateResult.Value.Is2Fa,
            };
            _telemetry.PlexAuthenticationProcessed(
                req.PlexAccountId,
                new PlexAuthenticationTelemetryDTO { IsValidated = validateResult.Value.IsValidated },
                validateResult
            );
            await Send.FluentResult(Result.Ok(response), ct);
            return;
        }

        _telemetry.PlexAuthenticationProcessed(req.PlexAccountId, null, validateResult);
        // Default: return all errors if none of the above conditions matched
        await Send.FluentResult(validateResult, ct);
    }

    private async Task PersistValidationResult(
        int plexAccountId,
        bool isValidated,
        DateTime? validatedAt,
        CancellationToken ct
    )
    {
        // Validation is also used before account creation, when there is no database row to update yet.
        if (plexAccountId <= 0)
            return;

        var plexAccount = await _dbContext.PlexAccounts.AsTracking().GetAsync(plexAccountId, ct);
        if (plexAccount is null)
        {
            _log.Here()
                .Warning(
                    "Could not persist token validation result because PlexAccount with id {PlexAccountId} was not found",
                    plexAccountId
                );
            return;
        }

        // Only persist validation state. Data returned by the external API must not overwrite other account fields.
        plexAccount.IsValidated = isValidated;
        plexAccount.ValidatedAt = isValidated ? validatedAt : null;
        await _dbContext.SaveChangesAsync(ct);

        await _notificationHubService.SendRefreshNotificationAsync(RefreshDataType.PlexAccount);
    }
}
