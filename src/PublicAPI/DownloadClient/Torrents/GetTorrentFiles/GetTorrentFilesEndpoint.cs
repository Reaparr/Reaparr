using System.Text.Json.Serialization;

namespace Reaparr.PublicAPI;

public sealed record GetTorrentFilesRequest
{
    [QueryParam, BindFrom("hash")]
    public required string Hash { get; init; }
}

public sealed class GetTorrentFilesRequestValidator : Validator<GetTorrentFilesRequest>
{
    public GetTorrentFilesRequestValidator()
    {
        RuleFor(x => x.Hash).NotEmpty().WithMessage("Hash is required.");
    }
}

public sealed record QBittorrentTorrentFile
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public sealed class GetTorrentFilesEndpoint : Endpoint<GetTorrentFilesRequest, List<QBittorrentTorrentFile>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ILogger _log;

    public GetTorrentFilesEndpoint(ILogger logger, IReaparrDbContext dbContext)
    {
        _log = logger.ForContext<GetTorrentFilesEndpoint>();
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Get(PublicApiRoutes.DownloadClient + "/torrents/files");
        Description(x => x.IsDownloadClient());
        AllowAnonymous();
        PreProcessor<DownloadClientAuthenticationPreProcessor<GetTorrentFilesRequest>>();
    }

    public override async Task HandleAsync(GetTorrentFilesRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var integration = HttpContext.GetIntegrationIdentity();

        var episodeFilesTask = _dbContext
            .DownloadTaskTvShowEpisodeFile.WhereIntegrationOwnershipMatches(integration)
            .Where(x => x.HashId == req.Hash)
            .ToListAsync(ct);

        var movieFilesTask = _dbContext
            .DownloadTaskMovieFile.WhereIntegrationOwnershipMatches(integration)
            .Where(x => x.HashId == req.Hash)
            .ToListAsync(ct);

        await Task.WhenAll(episodeFilesTask, movieFilesTask);

        var episodeFiles = await episodeFilesTask;
        var movieFiles = await movieFilesTask;

        var files = episodeFiles
            .Cast<DownloadTaskFileBase>()
            .Concat(movieFiles)
            .Select(file => new QBittorrentTorrentFile { Name = ResolveTorrentFilePath(file) })
            .ToList();

        await Send.OkAsync(files, ct);
    }

    private static string ResolveTorrentFilePath(DownloadTaskFileBase file)
    {
        if (string.IsNullOrWhiteSpace(file.DirectoryMeta.DownloadRootPath))
            return file.FileName;

        var downloadDirectory = file.DownloadDirectory;
        if (string.IsNullOrWhiteSpace(downloadDirectory))
            return file.FileName;

        var relativeDirectory = Path.GetRelativePath(file.DirectoryMeta.DownloadRootPath, downloadDirectory);
        if (
            string.IsNullOrWhiteSpace(relativeDirectory)
            || relativeDirectory == "."
            || relativeDirectory.StartsWith("..")
        )
            return file.FileName;

        var relativePath = Path.Combine(relativeDirectory, file.FileName);
        return relativePath.Replace('\\', '/');
    }
}
