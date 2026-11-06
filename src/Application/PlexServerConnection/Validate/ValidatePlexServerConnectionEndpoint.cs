namespace Reaparr.Application;

public record ValidatePlexServerConnectionEndpointRequest
{
    public required string Url { get; init; }
}

public class ValidatePlexServerConnectionEndpointRequestValidator
    : Validator<ValidatePlexServerConnectionEndpointRequest>
{
    public ValidatePlexServerConnectionEndpointRequestValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty()
            .WithMessage("URL cannot be empty.")
            .Must(url => url.IsValidHttpUrl())
            .WithMessage("string is not a valid URL");
    }
}

public class ValidatePlexServerConnectionEndpoint
    : Endpoint<ValidatePlexServerConnectionEndpointRequest, ResultDTO<ServerIdentityDTO>>
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public ValidatePlexServerConnectionEndpoint(ICommandExecutor commandExecutor, ITelemetry telemetry)
    {
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.PlexServerConnectionController + "/validate");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<ServerIdentityDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(ValidatePlexServerConnectionEndpointRequest req, CancellationToken ct)
    {
        var result = await _commandExecutor.Send(new ValidatePlexConnectionUrlCommand(req.Url), ct);
        _telemetry.ConnectionCheckFinished(
            result,
            result.IsSuccess ? TelemetryHealth.Healthy : TelemetryHealth.Unknown
        );
        await Send.FluentResult(result, ct);
    }
}
