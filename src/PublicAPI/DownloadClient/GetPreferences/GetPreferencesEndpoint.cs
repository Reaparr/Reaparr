namespace Reaparr.PublicAPI;

public class GetPreferencesEndpoint : EndpointWithoutRequest<object>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public GetPreferencesEndpoint(ILogger logger, IReaparrDbContext dbContext)
    {
        _log = logger.ForContext<GetPreferencesEndpoint>();
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Get(PublicApiRoutes.DownloadClient + "/app/preferences");
        Description(x => x.IsDownloadClient());
        AllowAnonymous();
        PreProcessor<DownloadClientAuthenticationPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext);

        var downloadFolder = await _dbContext.GetDownloadFolder(HttpContext.GetIntegrationIdentity());

        // Normally qBittorrent returns a big JSON with settings
        // You only need a minimal subset that Sonarr expects
        var prefs = new
        {
            // must exist, Sonarr checks these
            save_path = downloadFolder.DirectoryPath,
            temp_path_enabled = false,
            max_ratio_enabled = false,
            max_ratio = 0,
            max_seeding_time_enabled = false,
            max_seeding_time = 0,
            max_inactive_seeding_time_enabled = false,
            max_inactive_seeding_time = 0,
            max_ratio_act = 0,
            queueing_enabled = true,
            dht = true,
        };

        await Send.OkAsync(prefs, cancellation: ct);
    }
}
