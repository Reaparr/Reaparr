using Reaparr.Application.Contracts;

namespace Reaparr.PublicAPI;

public sealed class DeleteTorrentRequest
{
    [FromBody]
    public List<string>? Hashes { get; init; }

    [FormField, BindFrom("hashes")]
    public string? HashesRaw { get; init; }

    [FormField, BindFrom("deleteFiles")]
    public bool? DeleteFiles { get; init; }
}

public sealed class DeleteTorrentRequestValidator : Validator<DeleteTorrentRequest>
{
    public DeleteTorrentRequestValidator()
    {
        RuleForEach(x => x.Hashes)
            .Must(hash => !string.IsNullOrWhiteSpace(hash))
            .When(x => x.Hashes is { Count: > 0 })
            .WithMessage("Hashes must not be empty.");

        RuleFor(x => x.HashesRaw)
            .Must(raw =>
                string.IsNullOrWhiteSpace(raw)
                || string.Equals(raw.Trim(), "all", StringComparison.OrdinalIgnoreCase)
                || raw.Split(['|', ',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length > 0
            )
            .WithMessage("Hashes must be 'all' or a delimited list of hashes.");
    }
}

public sealed class DeleteTorrentEndpoint : Endpoint<DeleteTorrentRequest>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public DeleteTorrentEndpoint(ILogger log, IReaparrDbContext dbContext, ICommandExecutor commandExecutor)
    {
        _log = log.ForContext<DeleteTorrentEndpoint>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Post(PublicApiRoutes.DownloadClient + "/torrents/delete");
        Description(x => x.IsDownloadClient());
        AllowFormData(urlEncoded: true);
        AllowAnonymous();
        PreProcessor<DownloadClientAuthenticationPreProcessor<DeleteTorrentRequest>>();
    }

    public override async Task HandleAsync(DeleteTorrentRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var integration = HttpContext.GetIntegrationIdentity();

        var normalizedHashes = ParseHashes(req);

        if (normalizedHashes is { Count: 0 })
        {
            await Send.StringAsync("Ok.", cancellation: ct);
            return;
        }

        var (activeKeys, nonActiveKeys, allKeys) = await QueryKeysByStatus(normalizedHashes, integration, ct);

        if (allKeys.Count == 0)
        {
            await Send.StringAsync("Ok.", cancellation: ct);
            return;
        }

        var deleteFiles = req.DeleteFiles ?? true;
        var keysToDelete = new List<DownloadTaskKey>();

        foreach (var key in activeKeys)
        {
            var stopResult = await _commandExecutor.Send(new StopDownloadTaskCommand(key.Id, deleteFiles), ct);
            if (stopResult.IsFailed)
            {
                _log.Here().Warning("Failed to stop download task {DownloadTaskKey}: {Errors}", key, stopResult.Errors);
                continue;
            }

            keysToDelete.Add(key);
        }

        // Active tasks are handled via StopDownloadTaskCommand above.
        // For non-active tasks, explicitly delete source files when requested.
        if (deleteFiles && nonActiveKeys.Count > 0)
        {
            var deleteFilesResult = await _commandExecutor.Send(new DeleteDownloadTaskFilesCommand(nonActiveKeys), ct);
            if (deleteFilesResult.IsFailed)
            {
                _log.Here()
                    .Warning(
                        "Failed to delete download files for non-active tasks: {Errors}",
                        deleteFilesResult.Errors
                    );
            }
            else
            {
                keysToDelete.AddRange(nonActiveKeys);
            }
        }
        else
        {
            keysToDelete.AddRange(nonActiveKeys);
        }

        if (keysToDelete.Count == 0)
        {
            _log.Here()
                .Warning(
                    "Skipping download task deletion because prerequisite operations failed for all matched tasks ({TaskCount})",
                    allKeys.Count
                );

            await Send.StringAsync("Ok.", cancellation: ct);
            return;
        }

        var deleteResult = await _commandExecutor.Send(new DeleteDownloadTasksByKeyCommand(keysToDelete), ct);
        if (deleteResult.IsFailed)
        {
            _log.Here().Warning("Failed to delete download tasks: {Errors}", deleteResult.Errors);
        }
        else
        {
            var rootKeys = await GetRootKeysAsync(keysToDelete, integration, ct);
            if (rootKeys.Count > 0)
            {
                var clearResult = await _commandExecutor.Send(
                    new ClearCompletedDownloadTasksByDownloadTaskKeyCommand(rootKeys),
                    ct
                );
                if (clearResult.IsFailed)
                    _log.Here().Warning("Failed to clear completed download tasks: {Errors}", clearResult.Errors);
            }
        }

        await Send.StringAsync("Ok.", cancellation: ct);
    }

    /// <summary>
    /// Parses the request into a normalized lower-case hash list.
    /// Returns <c>null</c> when "all" is requested (caller passes <c>null</c> to query all tasks).
    /// Returns an empty list when no parseable hashes are present (caller should bail early).
    /// </summary>
    private static List<string>? ParseHashes(DeleteTorrentRequest req)
    {
        if (string.Equals(req.HashesRaw?.Trim(), "all", StringComparison.OrdinalIgnoreCase))
            return null;

        var hashes = new List<string>();
        if (req.Hashes?.Count > 0)
            hashes.AddRange(req.Hashes);

        if (!string.IsNullOrWhiteSpace(req.HashesRaw))
            hashes.AddRange(
                req.HashesRaw.Split(['|', ',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            );

        return hashes
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Resolves the root-level task key (Movie or TvShow) for each matched leaf file key.
    /// Movie file parents are a direct FK; episode file parents are resolved per leaf via
    /// </summary>
    private async Task<List<DownloadTaskKey>> GetRootKeysAsync(
        IReadOnlyCollection<DownloadTaskKey> leafKeys,
        IntegrationIdentity? integration,
        CancellationToken ct
    )
    {
        var movieFileIds = leafKeys.Where(k => k.Type is DownloadTaskType.MovieData).Select(k => k.Id).ToList();

        var episodeFileIds = leafKeys.Where(k => k.Type is DownloadTaskType.EpisodeData).Select(k => k.Id).ToList();

        var rootKeys = new List<DownloadTaskKey>();

        if (movieFileIds.Count > 0)
        {
            var movieRoots = await _dbContext
                .DownloadTaskMovieFile.Where(x => movieFileIds.Contains(x.Id))
                .Select(x => new DownloadTaskKey
                {
                    Id = x.ParentId,
                    Type = DownloadTaskType.Movie,
                    PlexServerId = x.PlexServerId,
                    PlexLibraryId = x.PlexLibraryId,
                })
                .ToListAsync(ct);

            rootKeys.AddRange(movieRoots.Distinct());
        }

        if (episodeFileIds.Count > 0)
        {
            var episodeLeafKeys = leafKeys.Where(k => k.Type is DownloadTaskType.EpisodeData);
            foreach (var episodeLeafKey in episodeLeafKeys)
            {
                var rootKey = await _dbContext.GetRootDownloadTaskKeyAsync(episodeLeafKey, integration, ct);
                if (rootKey is not null && rootKey.Type == DownloadTaskType.TvShow)
                {
                    rootKeys.Add(rootKey);
                }
            }
        }

        return rootKeys;
    }

    /// <summary>
    /// Queries the two file-task tables and returns all matched keys split by active/non-active status.
    /// Pass <c>null</c> for <paramref name="normalizedHashes"/> to match all tasks with a HashId.
    /// Returns all matched keys regardless of status — callers stop active ones first and then delete all.
    /// </summary>
    private async Task<(
        List<DownloadTaskKey> Active,
        List<DownloadTaskKey> NonActive,
        List<DownloadTaskKey> All
    )> QueryKeysByStatus(List<string>? normalizedHashes, IntegrationIdentity integration, CancellationToken ct)
    {
        static bool IsActive(DownloadStatus status) =>
            status
                is DownloadStatus.Downloading
                    or DownloadStatus.Queued
                    or DownloadStatus.Moving
                    or DownloadStatus.AutoMovePaused
                    or DownloadStatus.MovePaused
                    or DownloadStatus.AutoPaused
                    or DownloadStatus.Restarting;

        var movieTask = _dbContext
            .DownloadTaskMovieFile.WhereIntegrationOwnershipMatches(integration)
            .Where(x => x.HashId != null && (normalizedHashes == null || normalizedHashes.Contains(x.HashId.ToLower())))
            .Select(x => new
            {
                Key = new DownloadTaskKey
                {
                    Id = x.Id,
                    Type = x.DownloadTaskType,
                    PlexServerId = x.PlexServerId,
                    PlexLibraryId = x.PlexLibraryId,
                },
                x.DownloadStatus,
            })
            .ToListAsync(ct);

        var episodeTask = _dbContext
            .DownloadTaskTvShowEpisodeFile.WhereIntegrationOwnershipMatches(integration)
            .Where(x => x.HashId != null && (normalizedHashes == null || normalizedHashes.Contains(x.HashId.ToLower())))
            .Select(x => new
            {
                Key = new DownloadTaskKey
                {
                    Id = x.Id,
                    Type = x.DownloadTaskType,
                    PlexServerId = x.PlexServerId,
                    PlexLibraryId = x.PlexLibraryId,
                },
                x.DownloadStatus,
            })
            .ToListAsync(ct);

        await Task.WhenAll(movieTask, episodeTask);

        var all = movieTask.Result.Concat(episodeTask.Result).ToList();
        return (
            all.Where(x => IsActive(x.DownloadStatus)).Select(x => x.Key).ToList(),
            all.Where(x => !IsActive(x.DownloadStatus)).Select(x => x.Key).ToList(),
            all.Select(x => x.Key).ToList()
        );
    }
}
