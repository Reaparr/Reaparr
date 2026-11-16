namespace Reaparr.Application;

public class AppUserLogOutEndpoint : EndpointWithoutRequest<string>
{
    private readonly IIdentitySignInService _identitySignInService;
    private readonly ITelemetry _telemetry;

    public AppUserLogOutEndpoint(IIdentitySignInService identitySignInService, ITelemetry telemetry)
    {
        _identitySignInService = identitySignInService;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.LogOutEndpoint);
        Description(x =>
        {
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<string>));
            x.Produces(StatusCodes.Status401Unauthorized);
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await _identitySignInService.SignOutAsync();

        var result = Result.Ok("Logout successful");
        _telemetry.AuthenticationProcessed(TelemetryAction.Logout, result);
        await Send.FluentResult(result, x => x, ct);
    }
}
