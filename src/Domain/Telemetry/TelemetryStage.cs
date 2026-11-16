namespace Reaparr.Domain;

/// <summary>
/// Identifies the processing step reached by a backend operation. Added to telemetry outcomes
/// to locate a rejection, cancellation, or failure within its workflow.
/// </summary>
public enum TelemetryStage
{
    Download,
    DownloadWorker,
    DownloadMove,
    DatabaseOptimize,
    LibrarySync,
    LibraryComparison,
    MetadataSync,
    PlexAccessRefresh,
    ServerInspection,
    ConnectionCheck,
    MediaSupport,
    SourceValidation,
    TaskCreation,
    HashAssociation,
    Request,
    Lookup,
    Prerequisites,
    Delete,
    Cleanup,
    Launcher,
    Shutdown,
    ScheduledUpdateCheck,
    ScheduledDownloadLimits,
}
