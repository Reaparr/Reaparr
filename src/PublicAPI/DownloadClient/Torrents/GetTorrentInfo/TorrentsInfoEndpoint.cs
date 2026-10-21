using System.Text.Json.Serialization;

namespace Reaparr.PublicAPI;

public record TorrentsInfoEndpointRequest
{
    [QueryParam, BindFrom("category")]
    public string? Category { get; init; }

    [QueryParam, BindFrom("hashes")]
    public string? Hashes { get; init; }
}

public sealed class TorrentsInfoEndpointRequestValidator : Validator<TorrentsInfoEndpointRequest>
{
    public TorrentsInfoEndpointRequestValidator()
    {
        RuleFor(x => x.Hashes)
            .Must(hashes =>
                string.IsNullOrWhiteSpace(hashes)
                || string.Equals(hashes, "all", StringComparison.OrdinalIgnoreCase)
                || hashes.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length > 0
            )
            .WithMessage("Hashes must be 'all' or a pipe-delimited list of hashes.");
    }
}

public record QBittorrentTorrentInfo
{
    /// <summary>
    /// SHA-1 hash string of the torrent (40 hex characters). Unique identifier.
    /// </summary>
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Torrent name (usually the root folder or main file).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Total size of the torrent in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// Download progress (0.0 = 0%, 1.0 = 100%).
    /// </summary>
    [JsonPropertyName("progress")]
    public decimal Progress { get; set; }

    /// <summary>
    /// Current download speed in bytes per second.
    /// </summary>
    [JsonPropertyName("dlspeed")]
    public long DlSpeed { get; set; }

    /// <summary>
    /// Estimated time of arrival (time left) in seconds. -1 if unknown.
    /// </summary>
    [JsonPropertyName("eta")]
    public long Eta { get; set; }

    /// <summary>
    /// Current torrent state (e.g. "downloading", "pausedDL", "queuedDL", "stalledDL").
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path on disk where torrent data is being saved.
    /// </summary>
    [JsonPropertyName("save_path")]
    public string SavePath { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path to the torrent content on disk.
    /// </summary>
    [JsonPropertyName("content_path")]
    public string ContentPath { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("ratio")]
    public float Ratio { get; set; }

    [JsonPropertyName("ratio_limit")]
    public float RatioLimit { get; set; } = -2;

    [JsonPropertyName("seeding_time")]
    public long? SeedingTime { get; set; }

    [JsonPropertyName("seeding_time_limit")]
    public long SeedingTimeLimit { get; set; } = -2;

    [JsonPropertyName("inactive_seeding_time_limit")]
    public long InactiveSeedingTimeLimit { get; set; } = -2;

    [JsonPropertyName("last_activity")]
    public long LastActivity { get; set; }
}

public sealed class TorrentsInfoEndpoint : Endpoint<TorrentsInfoEndpointRequest, List<QBittorrentTorrentInfo>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly ILogger _log;

    public TorrentsInfoEndpoint(ILogger logger, IReaparrDbContextFactory dbContextFactory)
    {
        _log = logger.ForContext<TorrentsInfoEndpoint>();
        _dbContextFactory = dbContextFactory;
    }

    public override void Configure()
    {
        Get(PublicApiRoutes.DownloadClient + "/torrents/info");
        Description(x => x.IsDownloadClient());
        AllowAnonymous();
        PreProcessor<DownloadClientAuthenticationPreProcessor<TorrentsInfoEndpointRequest>>();
    }

    public override async Task HandleAsync(TorrentsInfoEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext);

        var hashesFilter = ParseHashes(req.Hashes);
        var categoryFilter = NormalizeCategory(req.Category);
        var integration = HttpContext.GetIntegrationIdentity();

        // Restrict requested hashes before materializing download tasks and their related rows.
        using var dbContext = await _dbContextFactory.CreateAsync();

        var nonOwnedServerIds = dbContext.PlexServers.WhereIsNotOwned().Select(x => x.Id);

        var episodeFilesTask = dbContext
            .DownloadTaskTvShowEpisodeFile.WhereIntegrationIsOrUnowned(integration)
            .Where(x => x.HashId != null && nonOwnedServerIds.Contains(x.PlexServerId))
            .Where(x => hashesFilter == null || hashesFilter.Contains(x.HashId!.ToLower()))
            .Include(x => x.Parent)
            .Include(x => x.SonarrIntegration)
            .Include(x => x.RadarrIntegration)
            .ToListAsync(ct);

        var movieFilesTask = dbContext
            .DownloadTaskMovieFile.WhereIntegrationIsOrUnowned(integration)
            .Where(x => x.HashId != null && nonOwnedServerIds.Contains(x.PlexServerId))
            .Where(x => hashesFilter == null || hashesFilter.Contains(x.HashId!.ToLower()))
            .Include(x => x.Parent)
            .Include(x => x.RadarrIntegration)
            .Include(x => x.SonarrIntegration)
            .ToListAsync(ct);

        await Task.WhenAll(episodeFilesTask, movieFilesTask);

        var episodeInfos = episodeFilesTask
            .Result.Where(x => MatchesCategory(x, categoryFilter))
            .Select(MapToTorrentInfo)
            .ToList();
        var movieInfos = movieFilesTask
            .Result.Where(x => MatchesCategory(x, categoryFilter))
            .Select(MapToTorrentInfo)
            .ToList();

        var torrents = new List<QBittorrentTorrentInfo>(episodeInfos.Count + movieInfos.Count);
        torrents.AddRange(episodeInfos);
        torrents.AddRange(movieInfos);

        await Send.OkAsync(torrents, ct);
    }

    private QBittorrentTorrentInfo MapToTorrentInfo(DownloadTaskFileBase file)
    {
        // Save path: prefer active download directory, else destination directory
        var savePath = !string.IsNullOrWhiteSpace(file.DownloadDirectory)
            ? file.DownloadDirectory
            : (file.DestinationDirectory);

        var category = ResolveCategory(file);

        // Signal to Radarr/Sonarr that the seed limit has been reached so CanBeRemoved becomes true.
        // Radarr only sets CanBeRemoved when HasReachedSeedLimit() is true. With ratio_limit=-2 and
        // no global ratio/time limits configured, HasReachedSeedLimit() always returns false and
        // RemoveItem (DELETE) is never called, leaving the file stranded in the downloads folder.
        // Setting ratio_limit=0 with ratio=0 satisfies the (ratio_limit - ratio <= 0.001) check.
        var isReadyForRemoval =
            file.DownloadStatus
            is DownloadStatus.Completed
                or DownloadStatus.MoveFinished
                or DownloadStatus.DownloadFinished;

        return new QBittorrentTorrentInfo
        {
            Hash = file.HashId!,
            Name = file.FileName,
            Size = file.DataTotal,
            Progress = Math.Clamp(file.Percentage / 100m, 0, 1),
            DlSpeed = file.Speed,
            Eta = file.TimeRemaining,
            State = MapStatusToQbittorrentState(file.DownloadStatus),
            SavePath = savePath,
            ContentPath = Path.Combine(savePath, file.FileName),
            Category = category,
            Label = category,
            Ratio = 0,
            RatioLimit = isReadyForRemoval ? 0 : -2,
            SeedingTime = null,
            SeedingTimeLimit = -2,
            InactiveSeedingTimeLimit = -2,
            LastActivity = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
    }

    private static string ResolveCategory(DownloadTaskFileBase file)
    {
        return file.SonarrIntegration?.Category
            ?? file.RadarrIntegration?.Category
            ?? (
                file.MediaType == PlexMediaType.Movie ? IntegrationDefinitions.RADARR_DEFAULT_CATEGORY
                : file.MediaType == PlexMediaType.Episode ? IntegrationDefinitions.SONARR_DEFAULT_CATEGORY
                : string.Empty
            );
    }

    private static HashSet<string>? ParseHashes(string? hashes)
    {
        if (string.IsNullOrWhiteSpace(hashes))
            return null;

        if (string.Equals(hashes, "all", StringComparison.OrdinalIgnoreCase))
            return null;

        var parsed = hashes
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return parsed.Count == 0 ? null : parsed;
    }

    private static string? NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return null;

        if (string.Equals(category, "all", StringComparison.OrdinalIgnoreCase))
            return null;

        return category;
    }

    private bool MatchesCategory(DownloadTaskFileBase file, string? categoryFilter)
    {
        if (categoryFilter is null)
            return true;

        var category = ResolveCategory(file);
        return string.Equals(category, categoryFilter, StringComparison.OrdinalIgnoreCase);
    }

    private string MapStatusToQbittorrentState(DownloadStatus status)
    {
        switch (status)
        {
            case DownloadStatus.Downloading:
                return "downloading";
            case DownloadStatus.Queued:
            case DownloadStatus.Restarting:
                return "queuedDL";
            case DownloadStatus.Stopped:
            case DownloadStatus.Paused:
            case DownloadStatus.AutoPaused:
                return "pausedDL";
            case DownloadStatus.Completed:
            case DownloadStatus.MoveFinished:
            case DownloadStatus.DownloadFinished:
            case DownloadStatus.MovePaused:
            case DownloadStatus.AutoMovePaused:
                return "pausedUP";
            case DownloadStatus.Deleted:
            case DownloadStatus.Error:
            case DownloadStatus.MoveError:
            case DownloadStatus.ServerUnreachable:
            case DownloadStatus.AuthError:
            case DownloadStatus.StorageError:
            case DownloadStatus.SourceUnavailable:
            case DownloadStatus.DownloadClientError:
            case DownloadStatus.IntegrityError:
                return "error";
            case DownloadStatus.Moving:
                return "moving";
            case DownloadStatus.Unknown:
            default:
                _log.Here()
                    .Warning(
                        "Unknown DownloadStatus {DownloadStatus} encountered when mapping to qBittorrent state",
                        status
                    );
                return "stalledDL";
        }
    }
}
