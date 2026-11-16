namespace Reaparr.Domain;

/// <summary>
/// Names backend operations for tracing and error context. The telemetry facade uses these values
/// to name activity spans and associate captured exceptions with an operation.
/// </summary>
public enum TelemetryActivity
{
    BackgroundJob,
    IntegrationSetup,
    ExternalDownloadSubmission,
    ExternalDownloadRemoval,
    Download,
    DirectDownload,
    DirectDownloadAttempt,
    DashDownload,
    DashDownloadAttempt,
    MoveDownloadJob,
    MoveDownloadFile,
    DownloadCleanup,
    LibrarySync,
    LibraryComparison,
    MovieComparison,
    TvShowComparison,
    MusicComparison,
    ComparisonStateApplication,
    LibraryUpdateCheck,
    MediaOverviewSnapshot,
    MediaOverviewRebuild,
    MetadataSync,
    PlexAccessRefresh,
    ServerInspection,
    ConnectionCheck,
    ScheduledConnectionCheck,
    ScheduledDownloadLimits,
    DatabaseOptimize,
    UpdateLauncher,
    UpdateCheck,
    MediaQuery,
    DownloadPreview,
}
