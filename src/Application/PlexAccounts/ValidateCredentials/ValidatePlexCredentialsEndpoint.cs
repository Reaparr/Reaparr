namespace Reaparr.Application;

/// <summary>
/// Validates the <see cref="PlexAccount"/> by calling the PlexAPI and confirming the PlexAccount can be used to log in.
/// </summary>
public record ValidatePlexCredentialsEndpointRequest
{
    public required string ClientId { get; init; }
    public required string DisplayName { get; init; } = "UnknownDisplayName";

    public required string Username { get; init; }

    public required string Password { get; init; }

    public required string VerificationCode { get; set; }
}

public record ValidatePlexCredentialsDTO
{
    public required bool IsUnAuthorized { get; init; }

    public required string ClientId { get; init; }

    public required string Username { get; init; }

    public required string Password { get; init; }

    public required string Email { get; init; }

    public required string Title { get; init; }

    public required long PlexId { get; init; }

    public required string Uuid { get; init; }

    public required string AuthenticationToken { get; init; }

    public required bool IsValidated { get; init; }

    public required DateTime? ValidatedAt { get; init; }

    public required bool Is2Fa { get; init; }
}

public class ValidatePlexCredentialsEndpointRequestValidator : Validator<ValidatePlexCredentialsEndpointRequest>
{
    public ValidatePlexCredentialsEndpointRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(5);

        RuleFor(x => x.Password).NotEmpty().MinimumLength(5);
    }
}

public class ValidatePlexCredentialsEndpoint
    : Endpoint<ValidatePlexCredentialsEndpointRequest, ResultDTO<ValidatePlexCredentialsDTO>>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;
    public ValidatePlexCredentialsEndpoint(ILogger log, ICommandExecutor commandExecutor, ITelemetry telemetry)
    {
        _log = log.ForContext<ValidatePlexCredentialsEndpoint>();
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.PlexAccountController + "/validate/credentials");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<ValidatePlexCredentialsDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status401Unauthorized, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(ValidatePlexCredentialsEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);

        var clientId = string.IsNullOrWhiteSpace(req.ClientId) ? Guid.NewGuid().ToString() : req.ClientId;

        var signInResult = await _commandExecutor.Send(
            new PlexSignInCommand
            {
                ClientId = clientId,
                Username = req.Username,
                Password = req.Password,
                VerificationCode = req.VerificationCode,
            },
            ct
        );

        // If the PlexAPI returns a 2FA error, we need to return a verification code to the client.
        if (signInResult.HasPlexErrorEnterVerificationCode())
        {
            _log.Here()
                .Warning("The PlexAccount with displayName {Name} requires a verification code (2FA)", req.DisplayName);
            var response = new ValidatePlexCredentialsDTO
            {
                IsUnAuthorized = true,
                ClientId = clientId,
                Username = req.Username,
                Password = req.Password,
                Email = string.Empty,
                Title = string.Empty,
                PlexId = 0,
                Uuid = string.Empty,
                AuthenticationToken = string.Empty,
                IsValidated = false,
                ValidatedAt = null,
                Is2Fa = true,
            };
            _telemetry.PlexAuthenticationProcessed(
                TelemetryAuthenticationMethod.Credentials,
                null,
                signInResult
            );
            await Send.FluentResult(Result.Ok(response), ct);

            return;
        }

        // If the PlexAPI rejects the credentials
        if (signInResult.HasPlex401UnauthorizedError())
        {
            _log.Here().Warning("Invalid Plex credentials provided for username {Username}", req.Username);
            var response = new ValidatePlexCredentialsDTO
            {
                IsUnAuthorized = true,
                ClientId = clientId,
                Username = req.Username,
                Password = req.Password,
                Email = string.Empty,
                Title = string.Empty,
                PlexId = 0,
                Uuid = string.Empty,
                AuthenticationToken = string.Empty,
                IsValidated = false,
                ValidatedAt = null,
                Is2Fa = false,
            };
            _telemetry.PlexAuthenticationProcessed(
                TelemetryAuthenticationMethod.Credentials,
                null,
                signInResult
            );
            await Send.FluentResult(Result.Ok(response), ct);
            return;
        }

        if (signInResult.IsSuccess)
        {
            _log.Here().Debug("The PlexAccount with displayName {Name} has been validated", req.DisplayName);

            var response = new ValidatePlexCredentialsDTO
            {
                IsUnAuthorized = false,
                ClientId = signInResult.Value.ClientId,
                Username = signInResult.Value.Username,
                Password = signInResult.Value.Password,
                Email = signInResult.Value.Email,
                Title = signInResult.Value.Title,
                PlexId = signInResult.Value.PlexId,
                Uuid = signInResult.Value.Uuid,
                AuthenticationToken = signInResult.Value.AuthenticationToken,
                IsValidated = signInResult.Value.IsValidated,
                ValidatedAt = signInResult.Value.ValidatedAt,
                Is2Fa = signInResult.Value.Is2Fa,
            };
            _telemetry.PlexAuthenticationProcessed(
                TelemetryAuthenticationMethod.Credentials,
                null,
                signInResult
            );
            await Send.FluentResult(Result.Ok(response), ct);
            return;
        }

        _telemetry.PlexAuthenticationProcessed(
            TelemetryAuthenticationMethod.Credentials,
            null,
            signInResult
        );
        // Default: return all errors if none of the above conditions matched
        await Send.FluentResult(signInResult.ToResult(), ct);
    }
}
