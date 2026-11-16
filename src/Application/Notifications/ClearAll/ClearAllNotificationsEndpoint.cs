namespace Reaparr.Application;

/// <summary>
/// Deletes/Clears all <see cref="Notification">Notifications</see>.
/// </summary>
/// <returns>Returns the number of <see cref="Notification">Notifications</see> that have been deleted.</returns>
public class ClearAllNotificationsEndpoint : EndpointWithoutRequest<CountResponseDTO>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ITelemetry _telemetry;

    public ClearAllNotificationsEndpoint(ILogger log, IReaparrDbContext dbContext, ITelemetry telemetry)
    {
        _log = log.ForContext<ClearAllNotificationsEndpoint>();
        _dbContext = dbContext;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Delete(ApiRoutes.NotificationController + "/clear");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<CountResponseDTO>))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext);
        // Empty the table
        var deletedNotificationsCount = await _dbContext.Notifications.ExecuteDeleteAsync(ct);

        _telemetry.NotificationsCleared(deletedNotificationsCount);

        await Send.FluentResult(Result.Ok(new CountResponseDTO(deletedNotificationsCount)), x => x, ct);
    }
}
