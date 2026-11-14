using System.Diagnostics;

namespace Reaparr.Application.Contracts;

public partial interface ITelemetry
{
    /// <summary>
    /// Records completion of a media query.
    /// </summary>
    void MediaQueryFinished(
        MediaQueryTelemetryDTO request,
        string? filter,
        Result<PlexMediaStatisticsDTO> result
    );

    /// <summary>
    /// Records completion of a photo search.
    /// </summary>
    void PhotoSearchFinished(
        PhotoSearchTelemetryDTO query,
        IReadOnlyList<string> searchTerms,
        Result<PhotoAssetPageDTO> result
    );

    /// <summary>
    /// Records a requested library refresh.
    /// </summary>
    void LibraryRefreshRequested(
        LibraryRefreshTelemetryDTO request,
        ResultBase result,
        LibrarySyncJobStatus? queueStatus = null
    );

    /// <summary>
    /// Records a requested server media sync.
    /// </summary>
    void ServerMediaSyncRequested(ServerMediaSyncTelemetryDTO request, ResultBase result);

    /// <summary>
    /// Records a queued library synchronization.
    /// </summary>
    void LibrarySyncQueued(
        (int PlexLibraryId, int PlexServerId, DateTime CreatedAt, bool ForceMediaRefresh) admission,
        PlexMediaType mediaType,
        bool forceLibrarySync,
        TelemetryTrigger trigger
    );

    /// <summary>
    /// Records completion of a library synchronization.
    /// </summary>
    void LibrarySyncFinished(
        LibrarySyncJobQueue queue,
        PlexMediaType? mediaType,
        bool forceMediaRefresh,
        LibrarySyncJobStatus status,
        bool offline
    );

    /// <summary>
    /// Records processing of a library synchronization cancellation.
    /// </summary>
    void LibrarySyncCancellationProcessed(
        LibrarySyncJobQueue queue,
        ResultBase result,
        bool interrupted,
        LibrarySyncJobStatus? finalStatus = null,
        bool noOp = false
    );

    /// <summary>
    /// Records processing of a library synchronization cancellation.
    /// </summary>
    void LibrarySyncCancellationProcessed(int libraryId, ResultBase result, bool interrupted, bool noOp = false);

    /// <summary>
    /// Records completion of a library comparison.
    /// </summary>
    void LibraryComparisonFinished(int ownedLibraryId, int remoteLibraryId, PlexMediaType mediaType, ResultBase result);

    /// <summary>
    /// Records a library access change.
    /// </summary>
    void LibraryAccessChanged(
        int accountId,
        Guid refreshRunId,
        IReadOnlyCollection<PlexLibraryAccessRapport> reports
    );

    /// <summary>
    /// Records completion of metadata synchronization.
    /// </summary>
    void MetadataSyncFinished(int serverId, int processedCount, ResultBase result);

    /// <summary>
    /// Records a Plex library change.
    /// </summary>
    void PlexLibraryChanged(
        PlexLibrary library,
        TelemetryAction action,
        bool isEnabled,
        int? purgedMediaCount = null
    );

    /// <summary>
    /// Records a Plex library change.
    /// </summary>
    void PlexLibraryChanged(int serverId, int libraryId, PlexMediaType mediaType, FolderPath folder);

    /// <summary>
    /// Starts telemetry for a movie library comparison.
    /// </summary>
    Activity? StartMovieComparison(int ownedLibraryId, int remoteLibraryId);

    /// <summary>
    /// Starts telemetry for a TV show library comparison.
    /// </summary>
    Activity? StartTvShowComparison(int ownedLibraryId, int remoteLibraryId);

    /// <summary>
    /// Starts telemetry for a music library comparison.
    /// </summary>
    Activity? StartMusicComparison(int ownedLibraryId, int remoteLibraryId);

    /// <summary>
    /// Starts telemetry for applying comparison state.
    /// </summary>
    Activity? StartComparisonStateApplication(PlexMediaType mediaType, int? libraryId, int itemCount);
}
