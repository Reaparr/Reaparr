namespace Reaparr.Application;

public record UpdateCredentialsEndpointRequest
{
    /// <summary>
    ///  The username of the <see cref="AppUser"/>.
    /// <para> The default username is <see cref="DefaultUserAppCredentials.DefaultPassword"/>. </para>
    /// </summary>
    public required string? Username { get; init; }

    /// <summary>
    /// The password of the <see cref="AppUser"/>.
    /// <para> The default password is <see cref="DefaultUserAppCredentials.DefaultPassword"/>. </para>
    /// </summary>
    public required string? Password { get; init; }
}

public class UpdateCredentialsEndpointRequestValidator : Validator<UpdateCredentialsEndpointRequest>
{
    public UpdateCredentialsEndpointRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => !(x.Username == null && x.Password == null))
            .WithMessage("Both Username and Password cannot be null.");
        RuleFor(x => x.Username).NotEmpty().MinimumLength(8).When(x => x.Username != null);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).When(x => x.Password != null);
    }
}

public class UpdateCredentialsEndpoint : Endpoint<UpdateCredentialsEndpointRequest>
{
    private readonly ILogger _log;
    private readonly IUserService _userService;
    private readonly ITelemetry _telemetry;

    public UpdateCredentialsEndpoint(ILogger log, IUserService userService, ITelemetry telemetry)
    {
        _log = log.ForContext<UpdateCredentialsEndpoint>();
        _userService = userService;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Put(ApiRoutes.AuthenticatedController);

        Summary(s =>
        {
            s.Summary = "Updates the credentials of a user.";
            s.ExampleRequest = new UpdateCredentialsEndpointRequest
            {
                Username = DefaultUserAppCredentials.DefaultUsername,
                Password = DefaultUserAppCredentials.DefaultPassword,
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

    public override async Task HandleAsync(UpdateCredentialsEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        // There is only 1 app user in the database
        var user = await _userService.GetFirstUserAsync(ct);
        if (user is null)
        {
            var result = Result.Fail("No app user found in the database").LogError();
            _telemetry.CredentialsUpdateProcessed(result, false, false);
            await Send.FluentResult(result, ct);
            return;
        }

        var newUsername = req.Username;
        var newPassword = req.Password;
        var usernameChanged = false;
        var passwordChanged = false;
        // Validate before saving the username so password-policy failures leave both credentials unchanged.
        var passwordValidation = newPassword is null
            ? null
            : await _userService.ValidatePasswordAsync(user, newPassword);
        if (passwordValidation is { Succeeded: false })
        {
            var result = Result.Fail(passwordValidation.Errors.Select(e => e.Description)).LogError();
            result.Add400BadRequestError("Failed to update password");
            _telemetry.CredentialsUpdateProcessed(result, false, false);
            await Send.FluentResult(result, ct);
            return;
        }

        if (newUsername is not null)
        {
            // Update the username
            var previousUsername = user.UserName;
            user.UserName = newUsername;
            user.NormalizedUserName = newUsername.ToUpperInvariant();

            var usernameResult = await _userService.UpdateAsync(user);
            if (!usernameResult.Succeeded)
            {
                var result = Result.Fail(usernameResult.Errors.Select(e => e.Description)).LogError();
                result.Add400BadRequestError("Failed to update username");
                _telemetry.CredentialsUpdateProcessed(result, false, false);
                await Send.FluentResult(result, ct);
                return;
            }

            usernameChanged = !string.Equals(previousUsername, newUsername, StringComparison.Ordinal);
        }

        if (newPassword is not null)
        {
            // Update the password
            var token = await _userService.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userService.ResetPasswordAsync(user, token, newPassword);
            if (!passwordResult.Succeeded)
            {
                var result = Result.Fail(passwordResult.Errors.Select(e => e.Description)).LogError();
                result.Add400BadRequestError("Failed to update password");
                _telemetry.CredentialsUpdateProcessed(result, usernameChanged, false);
                await Send.FluentResult(result, ct);
                return;
            }

            passwordChanged = true;
        }

        _log.Here().Warning("The Reaparr app credentials have been updated! Make sure this is intended");

        var successResult = Result.Ok();
        _telemetry.CredentialsUpdateProcessed(successResult, usernameChanged, passwordChanged);

        // Respond with success
        await Send.FluentResult(successResult, ct);
    }
}
