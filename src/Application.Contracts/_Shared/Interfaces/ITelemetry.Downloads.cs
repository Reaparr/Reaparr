using System.Diagnostics;

namespace Reaparr.Application.Contracts;

public partial interface ITelemetry
{
    /// <summary>Records completion of a download preview.</summary>
    /// <param name="request">The requested media selection and destination settings.</param>
    /// <param name="result">The outcome of generating the preview.</param>
    /// <param name="report">The preview entries returned on success.</param>
    void DownloadPreviewFinished(
        DownloadPreviewRequest request,
        ResultBase result,
        IReadOnlyCollection<(int Id, string Title, long Size, int ChildCount, PlexMediaType MediaType)> report
    );

    /// <summary>Records processing of download creation.</summary>
    /// <param name="request">The requested media selection.</param>
    /// <param name="result">The outcome and created-task counts.</param>
    void DownloadCreationProcessed(CreateDownloadTasksRequest request, Result<DownloadTaskCreationReport> result);

    /// <summary>Records a download control action.</summary>
    /// <param name="action">The control action that was requested.</param>
    /// <param name="downloadTaskId">The identifier of the affected download task.</param>
    /// <param name="result">The action outcome.</param>
    /// <param name="deleteFiles">Whether the action also deletes task files.</param>
    void DownloadControlProcessed(
        TelemetryAction action,
        Guid downloadTaskId,
        ResultBase result,
        bool deleteFiles = false
    );

    /// <summary>Records processing of download deletion.</summary>
    /// <param name="requestedTaskIds">The task identifiers requested for deletion.</param>
    /// <param name="taskKeys">The task keys selected for deletion.</param>
    /// <param name="result">The deletion outcome.</param>
    void DownloadDeletionProcessed(
        IReadOnlyCollection<Guid> requestedTaskIds,
        IReadOnlyCollection<DownloadTaskKey> taskKeys,
        ResultBase result
    );

    /// <summary>Records clearing of completed downloads.</summary>
    /// <param name="requestedTaskIds">The task identifiers requested for clearing.</param>
    /// <param name="taskKeys">The selected download task keys.</param>
    /// <param name="result">The outcome and number of cleared downloads.</param>
    void CompletedDownloadsCleared(
        IReadOnlyCollection<Guid> requestedTaskIds,
        IReadOnlyCollection<DownloadTaskKey> taskKeys,
        Result<int> result
    );

    /// <summary>Records clearing of completed downloads.</summary>
    /// <param name="plexServerId">The Plex server whose completed downloads were cleared.</param>
    /// <param name="result">The outcome and number of cleared downloads.</param>
    void CompletedDownloadsCleared(int plexServerId, Result<int> result);

    /// <summary>Records clearing of download logs.</summary>
    /// <param name="taskKey">The task whose logs were cleared.</param>
    /// <param name="result">The outcome and number of deleted log entries.</param>
    void DownloadLogsCleared(DownloadTaskKey taskKey, Result<int> result);

    /// <summary>Records a download lifecycle change.</summary>
    /// <param name="rootTaskKey">The root task whose lifecycle changed.</param>
    /// <param name="status">The new lifecycle status.</param>
    /// <param name="createdAt">The persisted creation time of the root task.</param>
    /// <param name="integrationIdentity">The integration that owns the root task, when attributed.</param>
    /// <param name="observedAt">The time the persisted lifecycle transition was observed.</param>
    void DownloadLifecycleChanged(
        DownloadTaskKey rootTaskKey,
        DownloadStatus status,
        DateTime createdAt,
        IntegrationIdentity? integrationIdentity,
        DateTime observedAt
    );

    /// <summary>Starts telemetry for a direct download.</summary>
    /// <param name="downloadTask">The task being downloaded.</param>
    /// <param name="segments">The number of download segments.</param>
    /// <returns>The created activity, or <see langword="null"/> when no activity is created.</returns>
    Activity? StartDirectDownload(DownloadTaskFileBase downloadTask, int segments);

    /// <summary>Starts telemetry for a direct download attempt.</summary>
    /// <param name="downloadTask">The task being downloaded.</param>
    /// <param name="attempt">The attempt number.</param>
    /// <param name="configuredSegments">The configured segment count.</param>
    /// <param name="segments">The segment count used by this attempt.</param>
    /// <param name="effectiveSpeedLimitBytes">The effective speed limit in bytes per second.</param>
    /// <returns>The created activity, or <see langword="null"/> when no activity is created.</returns>
    Activity? StartDirectDownloadAttempt(
        DownloadTaskFileBase downloadTask,
        int attempt,
        int configuredSegments,
        int segments,
        long effectiveSpeedLimitBytes
    );

    /// <summary>Starts telemetry for a DASH download.</summary>
    /// <param name="downloadTask">The task being downloaded.</param>
    /// <param name="outputQuality">The requested output video quality.</param>
    /// <param name="normalizedFileName">The normalized output file name.</param>
    /// <returns>The created activity, or <see langword="null"/> when no activity is created.</returns>
    Activity? StartDashDownload(
        DownloadTaskFileBase downloadTask,
        VideoQuality outputQuality,
        string normalizedFileName
    );

    /// <summary>Starts telemetry for a DASH download attempt.</summary>
    /// <param name="downloadTask">The task being downloaded.</param>
    /// <param name="outputPath">The output path for this attempt.</param>
    /// <param name="effectiveSpeedLimitBytes">The effective speed limit in bytes per second.</param>
    /// <returns>The created activity, or <see langword="null"/> when no activity is created.</returns>
    Activity? StartDashDownloadAttempt(
        DownloadTaskFileBase downloadTask,
        string outputPath,
        long effectiveSpeedLimitBytes
    );

    /// <summary>Starts telemetry for moving a download file.</summary>
    /// <param name="downloadTask">The task whose file is being moved.</param>
    /// <param name="sourcePath">The source file path.</param>
    /// <param name="destinationPath">The destination file path.</param>
    /// <param name="keepInDownloads">Whether the file should remain in the downloads directory.</param>
    /// <param name="retentionReason">The reason for retaining or removing the download.</param>
    /// <returns>The created activity, or <see langword="null"/> when no activity is created.</returns>
    Activity? StartMoveDownloadFile(
        DownloadTaskFileBase downloadTask,
        string sourcePath,
        string destinationPath,
        bool keepInDownloads,
        TelemetryRetentionReason retentionReason
    );

    /// <summary>Sets the source path for a move activity.</summary>
    /// <param name="activity">The move activity to update, if present.</param>
    /// <param name="sourcePath">The source file path.</param>
    void SetMoveDownloadFileSourcePath(Activity? activity, string sourcePath);

    /// <summary>Starts telemetry for download cleanup.</summary>
    /// <param name="downloadTask">The task whose files are being cleaned up.</param>
    /// <returns>The created activity, or <see langword="null"/> when no activity is created.</returns>
    Activity? StartDownloadCleanup(DownloadTaskFileBase downloadTask);

    /// <summary>Sets context on the current download activity.</summary>
    /// <param name="downloadTask">The task providing the download context.</param>
    /// <param name="clientType">The client used for the download.</param>
    void SetDownloadJobContext(DownloadTaskFileBase downloadTask, PlexDownloadClientType clientType);

    /// <summary>Sets context on the current move activity.</summary>
    /// <param name="taskKey">The task providing the move context.</param>
    /// <param name="retryCount">The number of retries performed.</param>
    void SetMoveDownloadJobContext(DownloadTaskKey taskKey, int retryCount);

    /// <summary>Completes telemetry for a download job.</summary>
    /// <param name="activity">The download activity to complete, if present.</param>
    /// <param name="result">The download job outcome.</param>
    void CompleteDownloadJob(Activity? activity, ResultBase result);

    /// <summary>Completes telemetry for scheduled download limits.</summary>
    /// <param name="activity">The activity to complete, if present.</param>
    /// <param name="result">The scheduled limits operation outcome.</param>
    void CompleteScheduledDownloadLimits(Activity? activity, ResultBase result);
}
