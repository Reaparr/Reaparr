namespace Reaparr.Application;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SendTelemetryDiagnosticRequest
{
    public TelemetryDiagnosticOrigin? Origin { get; init; }
}

public class SendTelemetryDiagnosticRequestValidator : Validator<SendTelemetryDiagnosticRequest>
{
    public SendTelemetryDiagnosticRequestValidator()
    {
        RuleFor(x => x.Origin).NotNull().IsInEnum();
    }
}

public class SendTelemetryDiagnosticEndpoint : Endpoint<SendTelemetryDiagnosticRequest, BaseResultDTO>
{
    private readonly ITelemetry _telemetry;

    public SendTelemetryDiagnosticEndpoint(ITelemetry telemetry)
    {
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.DebugController + "/telemetry/diagnostic");
        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status401Unauthorized, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status403Forbidden, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status503ServiceUnavailable, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(SendTelemetryDiagnosticRequest req, CancellationToken ct)
    {
        var result = await _telemetry.TrackAsync(
            "reaparr diagnostic",
            new Dictionary<string, object?> { ["origin"] = req.Origin!.Value }
        );
        await Send.FluentResult(result, ct);
    }
}
