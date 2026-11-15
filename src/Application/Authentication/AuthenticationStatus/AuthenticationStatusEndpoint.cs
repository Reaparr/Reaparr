namespace Reaparr.Application;

public class AuthenticationStatusEndpoint : EndpointWithoutRequest<UserClaimsDTO>
{
    public override void Configure()
    {
        Get(ApiRoutes.AuthenticatedController + "/status");
        AllowAnonymous();
        Description(x =>
        {
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<UserClaimsDTO>));
            x.Produces(StatusCodes.Status401Unauthorized, typeof(BaseResultDTO));
            x.Produces(StatusCodes.Status500InternalServerError);
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Check if the user is authenticated
        if (User.Identity?.IsAuthenticated == true)
        {
            var result = Result.Ok(
                new UserClaimsDTO
                {
                    IsLoggedIn = true,
                    UserName = User.Identity.Name ?? "UNKNOWN USERNAME",
                    Claims = User.Claims.Select(c => c.Type).ToList(),
                }
            );
            await Send.FluentResult(result, x => x, ct);
        }
        else
        {
            await Send.FluentResult(ResultExtensions.Create401UnauthorizedResult(), ct);
        }
    }
}
