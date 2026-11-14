using System.Diagnostics;

namespace Reaparr.Application.Contracts;

public partial interface ITelemetry
{
    /// <summary>
    /// Records completion of a download preview.
    /// </summary>
    void DownloadPreviewFinished(
        DownloadPreviewRequest request,
        ResultBase result,
        IReadOnlyCollection<(int Id, string Title, long Size, int ChildCount, PlexMediaType MediaType)> report
    );

    /// <summary>
    /// Records processing of download creation.
    /// </summary>
    void DownloadCreationProcessed(CreateDownloadTasksRequest request, Result<DownloadTaskCreationReport> result);

    /// <summary>
    /// Records a download control action.
    /// </summary>
    void DownloadControlProcessed(
        TelemetryAction action,
        Guid downloadTaskId,
        ResultBase result,
        bool deleteFiles = false
    );

    /// <summary>
    /// Records processing of download deletion.
    /// </summary>
    void DownloadDeletionProcessed(
        IReadOnlyCollection<Guid> requestedTaskIds,
        IReadOnlyCollection<DownloadTaskKey> taskKeys,
        ResultBase result
    );

    /// <summary>
    /// Records clearing of completed downloads.
    /// </summary>
    void CompletedDownloadsCleared(
        IReadOnlyCollection<Guid> requestedTaskIds,
        IReadOnlyCollection<DownloadTaskKey> taskKeys,
        Result<int> result
    );

    /// <summary>
    /// Records clearing of completed downloads.
    /// </summary>
    void CompletedDownloadsCleared(int plexServerId, Result<int> result);

    /// <summary>
    /// Records clearing of download logs.
    /// </summary>
    void DownloadLogsCleared(DownloadTaskKey taskKey, Result<int> result);

    /// <summary>
    /// Records a download lifecycle change.
    /// </summary>
    void DownloadLifecycleChanged(DownloadTaskKey rootTaskKey, DownloadStatus status);

    /// <summary>
    /// Starts telemetry for a direct download.
    /// </summary>
    Activity? StartDirectDownload(DownloadTaskFileBase downloadTask, int segments);

    /// <summary>
    /// Starts telemetry for a direct download attempt.
    /// </summary>
    Activity? StartDirectDownloadAttempt(
        DownloadTaskFileBase downloadTask,
        int attempt,
        int configuredSegments,
        int segments,
        long effectiveSpeedLimitBytes
    );

    /// <summary>
    /// Starts telemetry for a DASH download.
    /// </summary>
    Activity? StartDashDownload(
        DownloadTaskFileBase downloadTask,
        VideoQuality outputQuality,
        string normalizedFileName
    );

    /// <summary>
    /// Starts telemetry for a DASH download attempt.
    /// </summary>
    Activity? StartDashDownloadAttempt(
        DownloadTaskFileBase downloadTask,
        string outputPath,
        long effectiveSpeedLimitBytes
    );

    /// <summary>
    /// Starts telemetry for moving a download file.
    /// </summary>
    Activity? StartMoveDownloadFile(
        DownloadTaskFileBase downloadTask,
        string sourcePath,
        string destinationPath,
        bool keepInDownloads,
        TelemetryRetentionReason retentionReason
    );

    /// <summary>
    /// Sets the source path for a move activity.
    /// </summary>
    void SetMoveDownloadFileSourcePath(Activity? activity, string sourcePath);

    /// <summary>
    /// Starts telemetry for download cleanup.
    /// </summary>
    Activity? StartDownloadCleanup(DownloadTaskFileBase downloadTask);
    /// <summary>
    /// Sets context on the current download activity.
    /// </summary>
    void SetDownloadJobContext(
        DownloadTaskFileBase downloadTask,
        PlexDownloadClientType clientType
    );
    /// <summary>
    /// Sets context on the current move activity.
    /// </summary>
    void SetMoveDownloadJobContext(DownloadTaskKey taskKey, int retryCount);



    /// <summary>
    /// Completes telemetry for a download job.
    /// </summary>
    void CompleteDownloadJob(Activity? activity, ResultBase result);

    /// <summary>
    /// Completes telemetry for scheduled download limits.
    /// </summary>
    void CompleteScheduledDownloadLimits(Activity? activity, ResultBase result);
}
