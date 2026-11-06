namespace Reaparr.Application;

public record SetNotificationVisibilityEndpointRequest
{
    public int Id { get; init; }

    public bool Hidden { get; init; } = true;
}

public class SetNotificationVisibilityEndpointRequestValidator : Validator<SetNotificationVisibilityEndpointRequest>
{
    public SetNotificationVisibilityEndpointRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Hidden).Equal(true);
    }
}

public class SetNotificationVisibilityEndpoint : Endpoint<SetNotificationVisibilityEndpointRequest, BaseResultDTO>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ITelemetry _telemetry;

    public SetNotificationVisibilityEndpoint(ILogger log, IReaparrDbContext dbContext, ITelemetry telemetry)
    {
        _log = log.ForContext<SetNotificationVisibilityEndpoint>();
        _dbContext = dbContext;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Patch(ApiRoutes.NotificationController);

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(SetNotificationVisibilityEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var affectedCount = await _dbContext
            .Notifications.Where(x => x.Id == req.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Hidden, req.Hidden), ct);

        _telemetry.NotificationVisibilityProcessed(req.Id, req.Hidden, affectedCount);

        if (affectedCount == 0)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(Notification), req.Id), ct);
            return;
        }

        await Send.FluentResult(Result.Ok(), ct);
    }
}
