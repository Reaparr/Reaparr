namespace Reaparr.Application;

public record DeleteRadarrIntegrationRequest
{
    [RouteParam]
    public Guid IntegrationId { get; init; }

    [QueryParam]
    public bool Force { get; init; }
}

public class DeleteRadarrIntegrationEndpoint : Endpoint<DeleteRadarrIntegrationRequest>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly IRadarrHttpClientFactory _radarrHttpClientFactory;
    private readonly ITelemetry _telemetry;

    public DeleteRadarrIntegrationEndpoint(
        IReaparrDbContext dbContext,
        IRadarrHttpClientFactory radarrHttpClientFactory,
        ITelemetry telemetry
    )
    {
        _dbContext = dbContext;
        _radarrHttpClientFactory = radarrHttpClientFactory;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Delete(ApiRoutes.IntegrationController + "/Radarr/{integrationId:guid}");
    }

    public override async Task HandleAsync(DeleteRadarrIntegrationRequest req, CancellationToken ct)
    {
        var integration = await _dbContext
            .RadarrIntegrations.AsTracking()
            .SingleOrDefaultAsync(x => x.Id == req.IntegrationId, ct);
        if (integration is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var clientResult = await _radarrHttpClientFactory.CreateAsync(integration.Id);
        if (clientResult.IsFailed)
        {
            _telemetry.CaptureErrors(clientResult, TelemetryActivity.ExternalDownloadRemoval);
            if (!req.Force)
            {
                _telemetry.IntegrationDeleted(integration, clientResult, localRemoved: false, forced: false);
                await Send.FluentResult(clientResult.ToResult(), ct);
                return;
            }

            _dbContext.RadarrIntegrations.Remove(integration);
            await _dbContext.SaveChangesAsync(ct);
            _telemetry.IntegrationDeleted(integration, clientResult, localRemoved: true, forced: true);
            await Send.NoContentAsync(ct);
            return;
        }

        using var client = clientResult.Value;
        var cleanupResult = await client.DeleteRadarrResourcesAsync(
            integration.ExternalIndexerId,
            integration.ExternalDownloadClientId,
            ct
        );
        if (cleanupResult.IsFailed)
        {
            _telemetry.CaptureErrors(cleanupResult, TelemetryActivity.ExternalDownloadRemoval);
            if (!req.Force)
            {
                _telemetry.IntegrationDeleted(integration, cleanupResult, localRemoved: false, forced: false);
                await Send.FluentResult(cleanupResult, ct);
                return;
            }
        }

        _dbContext.RadarrIntegrations.Remove(integration);
        await _dbContext.SaveChangesAsync(ct);
        _telemetry.IntegrationDeleted(integration, cleanupResult, localRemoved: true, forced: req.Force);
        await Send.NoContentAsync(ct);
    }
}
