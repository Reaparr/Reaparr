namespace Reaparr.Application;

public record AppUserLoginEndpointRequest
{
    /// <summary>
    ///  The username of the <see cref="AppUser"/>.
    /// <para> The default username is <see cref="DefaultUserAppCredentials.DefaultPassword"/>. </para>
    /// </summary>
    [DefaultValue(DefaultUserAppCredentials.DefaultUsername)]
    public required string Username { get; init; }

    /// <summary>
    /// The password of the <see cref="AppUser"/>.
    /// <para> The default password is <see cref="DefaultUserAppCredentials.DefaultPassword"/>. </para>
    /// </summary>
    [DefaultValue(DefaultUserAppCredentials.DefaultPassword)]
    public required string Password { get; init; }

    /// <summary>
    /// A value indicating whether the user should be remembered when the browser is closed.
    /// <para> The default value is false </para>
    /// </summary>
    [DefaultValue(false)]
    public required bool RememberMe { get; init; }
};

public class AppUserLoginEndpointRequestValidator : Validator<AppUserLoginEndpointRequest>
{
    public AppUserLoginEndpointRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class AppUserLoginEndpoint : Endpoint<AppUserLoginEndpointRequest>
{
    private readonly ILogger _log;
    private readonly IIdentitySignInService _identitySignInService;
    private readonly ITelemetry _telemetry;

    public AppUserLoginEndpoint(ILogger log, IIdentitySignInService identitySignInService, ITelemetry telemetry)
    {
        _log = log.ForContext<AppUserLoginEndpoint>();
        _identitySignInService = identitySignInService;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.LoginEndpoint);
        AllowAnonymous();
        AllowFormData();

        Summary(s =>
        {
            s.Summary = "User Login";
            s.Description = "Logs in a user.";

            s.ExampleRequest = new AppUserLoginEndpointRequest
            {
                Username = DefaultUserAppCredentials.DefaultUsername,
                Password = DefaultUserAppCredentials.DefaultPassword,
                RememberMe = false,
            };
        });

        Description(x =>
        {
            x.Produces(StatusCodes.Status200OK, typeof(BaseResultDTO));
            x.Produces(StatusCodes.Status401Unauthorized, typeof(BaseResultDTO));
            x.Produces(StatusCodes.Status403Forbidden, typeof(BaseResultDTO));
            x.Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO));
        });
    }

    public override async Task HandleAsync(AppUserLoginEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var username = req.Username;
        var password = req.Password;

        _log.Here().Information("Attempting to sign in user {Username}", username);

        // Attempt to sign in the user
        var signInResult = await _identitySignInService.PasswordSignInAsync(
            username,
            password,
            isPersistent: req.RememberMe,
            lockoutOnFailure: true
        );

        if (signInResult.Succeeded)
        {
            _log.Here().Information("User {Username} signed in successfully", username);

            var result = Result.Ok();
            _telemetry.AuthenticationProcessed(TelemetryAction.Login, result, req.RememberMe);

            await Send.FluentResult(result, ct);
        }
        else if (signInResult.IsLockedOut)
        {
            var result = _log.Here().WarningResult("User {Username} is locked out", username);
            result.Add403ForbiddenError();

            _telemetry.AuthenticationProcessed(TelemetryAction.Login, result, req.RememberMe);

            await Send.FluentResult(result, ct);
        }
        else
        {
            var result = _log.Here().WarningResult("Failed to sign in user {Username}", username);
            result.Add401UnauthorizedError();

            _telemetry.AuthenticationProcessed(TelemetryAction.Login, result, req.RememberMe);

            await Send.FluentResult(result, ct);
        }
    }
}
