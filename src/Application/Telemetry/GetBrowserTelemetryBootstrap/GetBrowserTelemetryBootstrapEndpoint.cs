namespace Reaparr.Application;

/// <summary>
/// Returns the public browser telemetry bootstrap for anonymous application startup.
/// </summary>
public sealed class GetBrowserTelemetryBootstrapEndpoint
    : EndpointWithoutRequest<ResultDTO<BrowserTelemetryBootstrapDTO?>>
{
    private readonly ITelemetry _telemetry;

    public GetBrowserTelemetryBootstrapEndpoint(ITelemetry telemetry)
    {
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Get(ApiRoutes.TelemetryController + "/bootstrap");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "Gets the browser telemetry bootstrap configuration.";
            s.Description =
                "Returns the public browser telemetry configuration and existing application account identity, or null when telemetry is unconfigured.";
        });

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<BrowserTelemetryBootstrapDTO?>))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.FluentResult(Result.Ok(_telemetry.GetBrowserBootstrap()), ct);
    }
}
