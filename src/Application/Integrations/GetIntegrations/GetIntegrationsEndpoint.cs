namespace Reaparr.Application;

public record IntegrationSummary
{
    public required Guid Id { get; init; }
    public required IntegrationType Type { get; init; }
    public required string Name { get; init; }
    public required string BaseUrl { get; init; }
    public required string Category { get; init; }
    public required int DownloadFolderId { get; init; }
    public int? ExternalDownloadClientId { get; init; }
    public int? ExternalIndexerId { get; init; }
    public required IntegrationProvisioningState ProvisioningState { get; init; }
    public TestConnectionStatus LastConnectionTestStatus { get; init; }
    public int? LastConnectionTestHttpStatusCode { get; init; }
    public string? LastConnectionTestErrorMessage { get; init; }
    public DateTime? LastConnectionTestedAt { get; init; }
}

public class GetIntegrationsEndpoint : EndpointWithoutRequest<List<IntegrationSummary>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public GetIntegrationsEndpoint(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<GetIntegrationsEndpoint>();
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Get(ApiRoutes.IntegrationController);
        Description(x =>
            x.Produces<ResultDTO<List<IntegrationSummary>>>()
                .Produces<BaseResultDTO>(StatusCodes.Status500InternalServerError)
        );
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext);
        var sonarr = await _dbContext
            .SonarrIntegrations.Select(x => new IntegrationSummary
            {
                Id = x.Id,
                Type = IntegrationType.Sonarr,
                Name = x.DisplayName,
                BaseUrl = x.BaseUrl,
                Category = x.Category,
                DownloadFolderId = x.DownloadFolderId,
                ExternalDownloadClientId = x.ExternalDownloadClientId,
                ExternalIndexerId = x.ExternalIndexerId,
                ProvisioningState = x.ProvisioningState,
                LastConnectionTestStatus = x.LastConnectionTestStatus,
                LastConnectionTestHttpStatusCode = x.LastConnectionTestHttpStatusCode,
                LastConnectionTestErrorMessage = x.LastConnectionTestErrorMessage,
                LastConnectionTestedAt = x.LastConnectionTestedAt,
            })
            .ToListAsync(ct);
        var radarr = await _dbContext
            .RadarrIntegrations.Select(x => new IntegrationSummary
            {
                Id = x.Id,
                Type = IntegrationType.Radarr,
                Name = x.DisplayName,
                BaseUrl = x.BaseUrl,
                Category = x.Category,
                DownloadFolderId = x.DownloadFolderId,
                ExternalDownloadClientId = x.ExternalDownloadClientId,
                ExternalIndexerId = x.ExternalIndexerId,
                ProvisioningState = x.ProvisioningState,
                LastConnectionTestStatus = x.LastConnectionTestStatus,
                LastConnectionTestHttpStatusCode = x.LastConnectionTestHttpStatusCode,
                LastConnectionTestErrorMessage = x.LastConnectionTestErrorMessage,
                LastConnectionTestedAt = x.LastConnectionTestedAt,
            })
            .ToListAsync(ct);

        HttpContext.Response.Headers.CacheControl = "no-store";
        var integrations = sonarr.Concat(radarr).OrderBy(x => x.Type).ThenBy(x => x.Name).ToList();
        await Send.FluentResult(Result.Ok(integrations), ct);
    }
}
