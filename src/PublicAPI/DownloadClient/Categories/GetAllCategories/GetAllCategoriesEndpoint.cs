namespace Reaparr.PublicAPI;

public class GetAllCategoriesEndpoint : EndpointWithoutRequest<object>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public GetAllCategoriesEndpoint(ILogger logger, IReaparrDbContext dbContext)
    {
        _log = logger.ForContext<GetAllCategoriesEndpoint>();
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Get(PublicApiRoutes.DownloadClient + "/torrents/categories");
        Description(x => x.IsDownloadClient());
        AllowAnonymous();
        PreProcessor<DownloadClientAuthenticationPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext);

        var identity = HttpContext.GetIntegrationIdentity();
        var integration = await HttpContext.GetIntegrationSettings(_dbContext, identity, ct);
        var downloadFolder = await _dbContext.GetDownloadFolder(identity);

        var categories = new Dictionary<string, object>
        {
            [integration.Category] = new { name = integration.Category, savePath = downloadFolder.DirectoryPath },
        };

        await Send.OkAsync(categories, cancellation: ct);
    }
}
