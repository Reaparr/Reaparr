namespace Reaparr.Application;

public record GeneratePlexTokenEndpointRequest
{
    /// <summary>
    /// NOTE: This constructor is needed to make the query param optional in the front-end typescript-api generation.
    /// </summary>
    public GeneratePlexTokenEndpointRequest(string verificationCode = "")
    {
        VerificationCode = verificationCode;
    }

    public required int PlexAccountId { get; init; }

    [QueryParam, BindFrom("verificationCode")]
    public string VerificationCode { get; init; }
}

public class GeneratePlexTokenEndpointRequestValidator : Validator<GeneratePlexTokenEndpointRequest>
{
    public GeneratePlexTokenEndpointRequestValidator()
    {
        RuleFor(x => x.PlexAccountId).GreaterThan(0);
    }
}

public class GeneratePlexTokenResponse
{
    public required bool IsUnAuthorized { get; init; }

    public required bool NeedsVerificationCode { get; init; }

    public required string PlexAuthToken { get; init; }
}

public class GeneratePlexTokenEndpoint
    : Endpoint<GeneratePlexTokenEndpointRequest, ResultDTO<GeneratePlexTokenResponse>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;
    public GeneratePlexTokenEndpoint(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        ITelemetry telemetry
    )
    {
        _log = log.ForContext<GeneratePlexTokenEndpoint>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexAccountController + "/generate-token/{PlexAccountId}");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<GeneratePlexTokenResponse>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status401Unauthorized, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(GeneratePlexTokenEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var plexAccount = await _dbContext.PlexAccounts.GetAsync(req.PlexAccountId, ct);
        if (plexAccount is null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexAccount), req.PlexAccountId), ct);
            return;
        }

        if (!string.IsNullOrEmpty(req.VerificationCode))
        {
            plexAccount.Is2Fa = true;
            plexAccount.VerificationCode = req.VerificationCode;
        }

        var validateResult = await _commandExecutor.Send(
            new PlexSignInCommand
            {
                ClientId = plexAccount.ClientId,
                Username = plexAccount.Username,
                Password = plexAccount.Password,
                VerificationCode = plexAccount.VerificationCode,
            },
            ct
        );

        if (validateResult.IsSuccess)
        {
            var response = new GeneratePlexTokenResponse
            {
                IsUnAuthorized = false,
                NeedsVerificationCode = false,
                PlexAuthToken = validateResult.Value.AuthenticationToken,
            };
            _telemetry.PlexAuthenticationProcessed(
                TelemetryAuthenticationMethod.GenerateToken,
                req.PlexAccountId,
                validateResult,
                new PlexAuthenticationTelemetryDTO { IsValidated = validateResult.Value.IsValidated }
            );
            await Send.FluentResult(Result.Ok(response), ct);
            return;
        }

        if (validateResult.HasPlexErrorEnterVerificationCode())
        {
            var response = new GeneratePlexTokenResponse
            {
                IsUnAuthorized = false,
                NeedsVerificationCode = true,
                PlexAuthToken = "",
            };
            _telemetry.PlexAuthenticationProcessed(
                TelemetryAuthenticationMethod.GenerateToken,
                req.PlexAccountId,
                validateResult
            );
            await Send.FluentResult(Result.Ok(response), ct);
            return;
        }

        if (validateResult.Has401UnauthorizedError())
        {
            var response = new GeneratePlexTokenResponse
            {
                IsUnAuthorized = true,
                NeedsVerificationCode = false,
                PlexAuthToken = "",
            };
            _telemetry.PlexAuthenticationProcessed(
                TelemetryAuthenticationMethod.GenerateToken,
                req.PlexAccountId,
                validateResult
            );
            await Send.FluentResult(Result.Ok(response), ct);
            return;
        }

        _telemetry.PlexAuthenticationProcessed(
            TelemetryAuthenticationMethod.GenerateToken,
            req.PlexAccountId,
            validateResult
        );
        await Send.FluentResult(validateResult.ToResult(), ct);
    }
}
