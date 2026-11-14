using System.Diagnostics;

namespace Reaparr.Application;

public sealed partial class Telemetry
{
    /// <inheritdoc/>
    public void MediaQueryFinished(
        MediaQueryTelemetryDTO request,
        string? filter,
        Result<PlexMediaStatisticsDTO> result
    )
    {
        CaptureErrors(result, TelemetryActivity.MediaQuery);

        var searchTerms = GetTelemetrySearchTerms(request.Search);
        if (searchTerms.Length == 0)
            return;

        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.MediaType] = request.MediaType.ToString(),
            [TelemetryProperty.Libraries.Search] = string.Join(' ', searchTerms),
            [TelemetryProperty.Page] = request.Page.GetValueOrDefault(1),
            [TelemetryProperty.PageSize] = request.PageSize.GetValueOrDefault(MediaQueryFilter.MaximumPageSize),
            [TelemetryProperty.Outcome] = Outcome(result),
        };
        if (request.PlexLibraryId.HasValue)
            properties[TelemetryProperty.LibraryId] = request.PlexLibraryId.Value;
        if (filter is not null)
            properties[TelemetryProperty.Libraries.Filter] = filter;
        if (request.Sort is not null)
            properties[TelemetryProperty.Libraries.Sort] = request.Sort;

        if (result.IsSuccess)
        {
            properties[TelemetryProperty.TotalCount] = result.Value.TotalCount;
            properties[TelemetryProperty.Libraries.MediaCount] = result.Value.MediaCount;
            properties[TelemetryProperty.Libraries.TotalMediaSize] = result.Value.TotalMediaSize;
        }

        Capture(TelemetryEvent.MediaQueryFinished, properties);
    }

    /// <inheritdoc/>
    public void PhotoSearchFinished(
        PhotoSearchTelemetryDTO query,
        IReadOnlyList<string> searchTerms,
        Result<PhotoAssetPageDTO> result
    )
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.Query] = string.Join(' ', searchTerms),
            [TelemetryProperty.Page] = query.Page,
            [TelemetryProperty.PageSize] = query.PageSize,
            [TelemetryProperty.Libraries.Sort] = query.Sort.ToString(),
            [TelemetryProperty.Libraries.Direction] = query.Direction.ToString(),
            [TelemetryProperty.Outcome] = Outcome(result),
        };

        if (result.IsSuccess)
        {
            properties[TelemetryProperty.ReturnedItemCount] = result.Value.Items.Count;
            properties[TelemetryProperty.TotalCount] = result.Value.TotalCount;
            properties[TelemetryProperty.TotalBytes] = result.Value.TotalBytes;
        }

        Capture(TelemetryEvent.PhotoSearchFinished, properties);
    }

    /// <inheritdoc/>
    public void LibraryRefreshRequested(
        LibraryRefreshTelemetryDTO request,
        ResultBase result,
        LibrarySyncJobStatus? queueStatus = null
    )
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.LibraryId] = request.PlexLibraryId,
            [TelemetryProperty.Libraries.ForceLibrarySync] = request.ForceLibrarySync,
            [TelemetryProperty.Libraries.ForceMediaRefresh] = request.ForceMediaRefresh,
            [TelemetryProperty.Outcome] = Outcome(result),
        };
        if (queueStatus.HasValue)
            properties[TelemetryProperty.Libraries.QueueStatus] = queueStatus.Value.ToString();

        Capture(TelemetryEvent.LibraryRefreshRequested, properties);
    }

    /// <inheritdoc/>
    public void ServerMediaSyncRequested(ServerMediaSyncTelemetryDTO request, ResultBase result)
    {
        Capture(
            TelemetryEvent.ServerMediaSyncRequested,
            new Dictionary<string, object>
            {
                [TelemetryProperty.ServerId] = request.PlexServerId,
                [TelemetryProperty.Libraries.ForceLibrarySync] = request.ForceLibrarySync,
                [TelemetryProperty.Libraries.ForceMediaRefresh] = request.ForceMediaRefresh,
                [TelemetryProperty.Outcome] = Outcome(result),
            }
        );
    }

    /// <inheritdoc/>
    public void LibrarySyncQueued(
        (int PlexLibraryId, int PlexServerId, DateTime CreatedAt, bool ForceMediaRefresh) admission,
        PlexMediaType mediaType,
        bool forceLibrarySync,
        TelemetryTrigger trigger
    )
    {
        Capture(
            TelemetryEvent.LibrarySyncQueued,
            new Dictionary<string, object>
            {
                [TelemetryProperty.ServerId] = admission.PlexServerId,
                [TelemetryProperty.LibraryId] = admission.PlexLibraryId,
                [TelemetryProperty.MediaType] = mediaType.ToString(),
                [TelemetryProperty.CreatedAt] = admission.CreatedAt,
                [TelemetryProperty.Libraries.ForceLibrarySync] = forceLibrarySync,
                [TelemetryProperty.Libraries.ForceMediaRefresh] = admission.ForceMediaRefresh,
                [TelemetryProperty.Trigger] = trigger.ToString(),
            }
        );
    }

    /// <inheritdoc/>
    public void LibrarySyncFinished(
        LibrarySyncJobQueue queue,
        PlexMediaType? mediaType,
        bool forceMediaRefresh,
        LibrarySyncJobStatus status,
        bool offline
    )
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.ServerId] = queue.PlexServerId,
            [TelemetryProperty.LibraryId] = queue.PlexLibraryId,
            [TelemetryProperty.CreatedAt] = queue.CreatedAt,
            [TelemetryProperty.Libraries.ForceMediaRefresh] = forceMediaRefresh,
            [TelemetryProperty.Outcome] = status switch
            {
                LibrarySyncJobStatus.Completed => TelemetryOutcome.Succeeded.ToString(),
                LibrarySyncJobStatus.Cancelled => TelemetryOutcome.Cancelled.ToString(),
                _ => TelemetryOutcome.Failed.ToString(),
            },
        };
        if (mediaType.HasValue)
            properties[TelemetryProperty.MediaType] = mediaType.Value.ToString();
        if (offline)
            properties[TelemetryProperty.Libraries.Offline] = true;

        Capture(TelemetryEvent.LibrarySyncFinished, properties);
    }

    /// <inheritdoc/>
    public void LibrarySyncCancellationProcessed(
        LibrarySyncJobQueue queue,
        ResultBase result,
        bool interrupted,
        LibrarySyncJobStatus? finalStatus = null,
        bool noOp = false
    )
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.LibraryId] = queue.PlexLibraryId,
            [TelemetryProperty.ServerId] = queue.PlexServerId,
            [TelemetryProperty.Libraries.Interrupted] = interrupted,
            [TelemetryProperty.Outcome] = Outcome(result, noOp),
            [TelemetryProperty.CreatedAt] = queue.CreatedAt,
        };
        if (finalStatus.HasValue)
            properties[TelemetryProperty.Status] = finalStatus.Value.ToString();

        Capture(TelemetryEvent.LibrarySyncCancellationProcessed, properties);
    }

    /// <inheritdoc/>
    public void LibrarySyncCancellationProcessed(int libraryId, ResultBase result, bool interrupted, bool noOp = false)
    {
        Capture(
            TelemetryEvent.LibrarySyncCancellationProcessed,
            new Dictionary<string, object>
            {
                [TelemetryProperty.LibraryId] = libraryId,
                [TelemetryProperty.ServerId] = 0,
                [TelemetryProperty.Libraries.Interrupted] = interrupted,
                [TelemetryProperty.Outcome] = Outcome(result, noOp),
            }
        );
    }

    /// <inheritdoc/>
    public void LibraryComparisonFinished(
        int ownedLibraryId,
        int remoteLibraryId,
        PlexMediaType mediaType,
        ResultBase result
    )
    {
        Capture(
            TelemetryEvent.LibraryComparisonFinished,
            new Dictionary<string, object>
            {
                [TelemetryProperty.Libraries.OwnedLibraryId] = ownedLibraryId,
                [TelemetryProperty.Libraries.RemoteLibraryId] = remoteLibraryId,
                [TelemetryProperty.MediaType] = mediaType.ToString(),
                [TelemetryProperty.Libraries.Pair] = $"{ownedLibraryId}:{remoteLibraryId}",
                [TelemetryProperty.Outcome] = Outcome(result),
            }
        );
    }

    /// <inheritdoc/>
    public void LibraryAccessChanged(
        int accountId,
        Guid refreshRunId,
        IReadOnlyCollection<PlexLibraryAccessRapport> reports
    )
    {
        var access = new List<Dictionary<string, object>>();
        var granted = 0;

        foreach (var report in reports)
        {
            foreach (var row in report.Data)
            {
                if (row.State is not (PlexAccessState.Granted or PlexAccessState.Revoked))
                    continue;

                if (row.State == PlexAccessState.Granted)
                    granted++;

                access.Add(
                    new Dictionary<string, object>
                    {
                        [TelemetryProperty.ServerId] = report.PlexServerId,
                        [TelemetryProperty.Libraries.ServerName] = report.PlexServerName,
                        [TelemetryProperty.LibraryId] = row.PlexLibraryId,
                        [TelemetryProperty.Libraries.LibraryName] = row.PlexLibraryName,
                        [TelemetryProperty.Libraries.State] = row.State.ToString(),
                    }
                );
            }
        }

        if (access.Count == 0)
            return;

        Capture(
            TelemetryEvent.LibraryAccessChanged,
            new Dictionary<string, object>
            {
                [TelemetryProperty.RefreshRunId] = refreshRunId,
                [TelemetryProperty.AccountId] = accountId,
                [TelemetryProperty.Libraries.Access] = access,
                [TelemetryProperty.Libraries.Granted] = granted,
                [TelemetryProperty.Libraries.Revoked] = access.Count - granted,
                [TelemetryProperty.Outcome] = TelemetryOutcome.Succeeded.ToString(),
            }
        );
    }

    /// <inheritdoc/>
    public void MetadataSyncFinished(int serverId, int processedCount, ResultBase result)
    {
        if (result.IsSuccess && processedCount == 0)
            return;

        Capture(
            TelemetryEvent.MetadataSyncFinished,
            new Dictionary<string, object>
            {
                [TelemetryProperty.ServerId] = serverId,
                [TelemetryProperty.Libraries.ProcessedCount] = processedCount,
                [TelemetryProperty.Outcome] = Outcome(result),
            }
        );
    }

    /// <inheritdoc/>
    public void PlexLibraryChanged(
        PlexLibrary library,
        TelemetryAction action,
        bool isEnabled,
        int? purgedMediaCount = null
    )
    {
        var properties = new Dictionary<string, object>
        {
            [TelemetryProperty.Action] = action.ToString(),
            [TelemetryProperty.ServerId] = library.PlexServerId,
            [TelemetryProperty.LibraryId] = library.Id,
            [TelemetryProperty.MediaType] = library.Type.ToString(),
            [TelemetryProperty.Libraries.IsEnabled] = isEnabled,
            [TelemetryProperty.Outcome] = TelemetryOutcome.Succeeded.ToString(),
        };
        if (purgedMediaCount.HasValue)
            properties[TelemetryProperty.Libraries.PurgedMediaCount] = purgedMediaCount.Value;

        Capture(TelemetryEvent.PlexLibraryChanged, properties);
    }

    /// <inheritdoc/>
    public void PlexLibraryChanged(int serverId, int libraryId, PlexMediaType mediaType, FolderPath folder)
    {
        Capture(
            TelemetryEvent.PlexLibraryChanged,
            new Dictionary<string, object>
            {
                [TelemetryProperty.Action] = TelemetryAction.DefaultDestination.ToString(),
                [TelemetryProperty.ServerId] = serverId,
                [TelemetryProperty.LibraryId] = libraryId,
                [TelemetryProperty.MediaType] = mediaType.ToString(),
                [TelemetryProperty.Libraries.DefaultDestinationId] = folder.Id,
                [TelemetryProperty.Libraries.DefaultDestination] = new Dictionary<string, object>
                {
                    [TelemetryProperty.FolderId] = folder.Id,
                    [TelemetryProperty.Libraries.FolderType] = folder.FolderType.ToString(),
                    [TelemetryProperty.MediaType] = folder.MediaType.ToString(),
                    [TelemetryProperty.DisplayName] = folder.DisplayName,
                    [TelemetryProperty.DirectoryPath] = folder.DirectoryPath,
                },
                [TelemetryProperty.Outcome] = TelemetryOutcome.Succeeded.ToString(),
            }
        );
    }

    /// <inheritdoc/>
    public Activity? StartMovieComparison(int ownedLibraryId, int remoteLibraryId) =>
        StartActivityCore(TelemetryActivity.MovieComparison, tags: CreateComparisonTags(ownedLibraryId, remoteLibraryId));

    /// <inheritdoc/>
    public Activity? StartTvShowComparison(int ownedLibraryId, int remoteLibraryId) =>
        StartActivityCore(TelemetryActivity.TvShowComparison, tags: CreateComparisonTags(ownedLibraryId, remoteLibraryId));

    /// <inheritdoc/>
    public Activity? StartMusicComparison(int ownedLibraryId, int remoteLibraryId) =>
        StartActivityCore(TelemetryActivity.MusicComparison, tags: CreateComparisonTags(ownedLibraryId, remoteLibraryId));

    /// <inheritdoc/>
    public Activity? StartComparisonStateApplication(PlexMediaType mediaType, int? libraryId, int itemCount) =>
        StartActivityCore(
            TelemetryActivity.ComparisonStateApplication,
            tags:
            [
                new(TelemetryProperty.MediaType, mediaType.ToString()),
                new(TelemetryProperty.LibraryId, libraryId),
                new(TelemetryProperty.Libraries.ItemCount, itemCount),
            ]
        );

    private static string[] GetTelemetrySearchTerms(string? search) =>
        string.IsNullOrWhiteSpace(search)
            ? []
            : search
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(term => term.Length >= 2)
                .Distinct()
                .ToArray();

    private static KeyValuePair<string, object?>[] CreateComparisonTags(int ownedLibraryId, int remoteLibraryId) =>
    [
        new(TelemetryProperty.Libraries.OwnedLibraryId, ownedLibraryId),
        new(TelemetryProperty.Libraries.RemoteLibraryId, remoteLibraryId),
        new(TelemetryProperty.Libraries.Pair, $"{ownedLibraryId}:{remoteLibraryId}"),
    ];
}
