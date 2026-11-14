namespace Reaparr.Application;

public record DeleteSonarrIntegrationRequest
{
    [RouteParam]
    public Guid IntegrationId { get; init; }

    [QueryParam]
    public bool Force { get; init; }
}

public class DeleteSonarrIntegrationEndpoint : Endpoint<DeleteSonarrIntegrationRequest>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ISonarrHttpClientFactory _sonarrHttpClientFactory;
    private readonly ITelemetry _telemetry;

    public DeleteSonarrIntegrationEndpoint(
        IReaparrDbContext dbContext,
        ISonarrHttpClientFactory sonarrHttpClientFactory,
        ITelemetry telemetry
    )
    {
        _dbContext = dbContext;
        _sonarrHttpClientFactory = sonarrHttpClientFactory;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Delete(ApiRoutes.IntegrationController + "/Sonarr/{integrationId:guid}");
    }

    public override async Task HandleAsync(DeleteSonarrIntegrationRequest req, CancellationToken ct)
    {
        var integration = await _dbContext
            .SonarrIntegrations.AsTracking()
            .SingleOrDefaultAsync(x => x.Id == req.IntegrationId, ct);
        if (integration is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var clientResult = await _sonarrHttpClientFactory.CreateAsync(integration.Id);
        if (clientResult.IsFailed)
        {
            _telemetry.CaptureErrors(clientResult, TelemetryActivity.ExternalDownloadRemoval);
            if (!req.Force)
            {
                _telemetry.IntegrationDeleted(integration, clientResult, localRemoved: false, forced: false);
                await Send.FluentResult(clientResult.ToResult(), ct);
                return;
            }

            _dbContext.SonarrIntegrations.Remove(integration);
            await _dbContext.SaveChangesAsync(ct);
            _telemetry.IntegrationDeleted(integration, clientResult, localRemoved: true, forced: true);
            await Send.NoContentAsync(ct);
            return;
        }

        using var client = clientResult.Value;
        var cleanupResult = await client.DeleteSonarrResourcesAsync(
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

        _dbContext.SonarrIntegrations.Remove(integration);
        await _dbContext.SaveChangesAsync(ct);
        _telemetry.IntegrationDeleted(integration, cleanupResult, localRemoved: true, forced: req.Force);
        await Send.NoContentAsync(ct);
    }
}
