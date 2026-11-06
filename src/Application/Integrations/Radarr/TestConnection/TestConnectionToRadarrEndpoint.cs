namespace Reaparr.Application;

public record TestConnectionToRadarrEndpointRequest
{
    [QueryParam, BindFrom("integrationId")]
    public Guid? IntegrationId { get; init; }

    [QueryParam, BindFrom("url")]
    public string? Url { get; init; }

    [QueryParam, BindFrom("apiKey")]
    public string? ApiKey { get; init; }
}

public record TestConnectionToRadarrEndpointResponse
{
    public required TestConnectionStatus Result { get; init; }
    public int? HttpStatusCode { get; init; }
    public string? ErrorMessage { get; init; }
    public required DateTime TestedAt { get; init; }
}

public class TestConnectionToRadarrEndpoint
    : Endpoint<TestConnectionToRadarrEndpointRequest, ResultDTO<TestConnectionToRadarrEndpointResponse>>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public TestConnectionToRadarrEndpoint(ILogger log, ICommandExecutor commandExecutor, ITelemetry telemetry)
    {
        _log = log.ForContext<TestConnectionToRadarrEndpoint>();
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Get(ApiRoutes.IntegrationController + "/Radarr/TestConnection");
        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<TestConnectionToRadarrEndpointResponse>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(TestConnectionToRadarrEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var result = await _commandExecutor.Send(
            new TestConnectionToRadarrCommand(req.IntegrationId, req.Url, req.ApiKey),
            ct
        );
        _telemetry.IntegrationConnectionTestFinished(IntegrationType.Radarr, req.IntegrationId, result);
        if (result.IsCancelled)
        {
            await Send.FluentResult(result.ToResult<TestConnectionToRadarrEndpointResponse>().LogWarning(), ct);
            return;
        }
        if (result.IsFailed)
        {
            _telemetry.CaptureErrors(result, TelemetryActivity.ConnectionCheck);
            await Send.FluentResult(result.ToResult<TestConnectionToRadarrEndpointResponse>().LogError(), ct);
            return;
        }

        await Send.FluentResult(
            Result.Ok(
                new TestConnectionToRadarrEndpointResponse
                {
                    Result = result.Value.Status,
                    HttpStatusCode = result.Value.HttpStatusCode,
                    ErrorMessage = result.Value.ErrorMessage,
                    TestedAt = result.Value.TestedAt,
                }
            ),
            ct
        );
    }
}
