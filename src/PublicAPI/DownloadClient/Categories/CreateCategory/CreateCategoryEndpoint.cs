namespace Reaparr.PublicAPI.CreateCategory;

public record CreateCategoryRequest
{
    [BindFrom("category")]
    public required string Category { get; init; }

    [BindFrom("savePath")]
    public string? SavePath { get; init; }
}

public class CreateCategoryEndpoint : Endpoint<CreateCategoryRequest>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public CreateCategoryEndpoint(ILogger logger, IReaparrDbContext dbContext)
    {
        _log = logger.ForContext<CreateCategoryEndpoint>();
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Post(PublicApiRoutes.DownloadClient + "/torrents/createCategory");
        Description(x => x.IsDownloadClient());
        AllowFormData(urlEncoded: true);
        AllowAnonymous();
        PreProcessor<DownloadClientAuthenticationPreProcessor<CreateCategoryRequest>>();
    }

    public override async Task HandleAsync(CreateCategoryRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var integration = await HttpContext.GetIntegrationSettings(
            _dbContext,
            HttpContext.GetIntegrationIdentity(),
            ct
        );
        if (!string.Equals(req.Category, integration.Category, StringComparison.Ordinal))
        {
            await Send.ErrorsAsync(403, cancellation: ct);
            return;
        }
        await Send.OkAsync(cancellation: ct);
    }
}
