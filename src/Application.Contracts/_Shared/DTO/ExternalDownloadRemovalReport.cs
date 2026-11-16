namespace Reaparr.Application.Contracts;

/// <summary>
/// Captures task-key matching, prerequisite, deletion, and cleanup counts for one external download-removal request.
/// </summary>
/// <remarks>
/// Task counts refer to movie and TV episode file-task keys; hash and command-failure counts have separate units described below.
/// <see cref="Result"/> is populated only when task deletion or root cleanup returns a failed or cancelled result.
/// </remarks>
public struct ExternalDownloadRemovalReport
{
    /// <summary>Initializes a report with the request scope and file-deletion option.</summary>
    /// <param name="requestedHashCount">Number of distinct normalized hash values supplied by the caller; zero when all tasks are requested.</param>
    /// <param name="allRequested">Whether the request selected all tasks instead of an explicit hash list.</param>
    /// <param name="deleteFiles">Whether removal should also delete source files.</param>
    public ExternalDownloadRemovalReport(int requestedHashCount, bool allRequested, bool deleteFiles)
    {
        RequestedHashCount = requestedHashCount;
        AllRequested = allRequested;
        DeleteFiles = deleteFiles;
        Stage = TelemetryStage.Lookup;
    }

    /// <summary>
    /// Failed or cancelled result from task deletion or completed-root cleanup; null when no deletion or cleanup failure result was assigned, including lookup or prerequisite-only reports and successful task deletion.
    /// </summary>
    public ResultBase? Result { get; set; }

    /// <summary>
    /// Workflow stage at which the report is emitted; initialized to <see cref="TelemetryStage.Lookup"/> and later advanced for prerequisite, deletion, or cleanup processing.
    /// </summary>
    public TelemetryStage Stage { get; set; }

    /// <summary>Number of distinct normalized hash values supplied by the caller; zero when all tasks are requested.</summary>
    public int RequestedHashCount { get; set; }

    /// <summary>Whether the request selected all tasks instead of an explicit hash list.</summary>
    public bool AllRequested { get; set; }

    /// <summary>Number of matching movie and TV episode file-task keys found for the request.</summary>
    public int MatchedCount { get; set; }

    /// <summary>Number of matching file-task keys classified as active and sent to the stop command.</summary>
    public int ActiveCount { get; set; }

    /// <summary>Number of matching file-task keys classified as non-active.</summary>
    public int NonActiveCount { get; set; }

    /// <summary>Number of keys that passed prerequisite handling and were submitted to task deletion.</summary>
    public int EligibleCount { get; set; }

    /// <summary>Number of eligible keys recorded after the task-deletion command succeeds.</summary>
    public int DeletedCount { get; set; }

    /// <summary>Whether source files are requested for deletion, including files belonging to active and non-active tasks.</summary>
    public bool DeleteFiles { get; set; }

    /// <summary>Number of active file-task keys whose stop command failed without cancellation.</summary>
    public int StopFailureCount { get; set; }

    /// <summary>Number of active file-task keys whose stop command was cancelled.</summary>
    public int StopCancellationCount { get; set; }

    /// <summary>Number of non-active file-deletion batch commands that failed; the current flow performs at most one.</summary>
    public int FileFailureCount { get; set; }

    /// <summary>Number of non-active file-deletion batch commands that were cancelled; the current flow performs at most one.</summary>
    public int FileCancellationCount { get; set; }

    /// <summary>Number of completed-root cleanup commands that failed or were cancelled; the current flow performs at most one.</summary>
    public int CleanupFailureCount { get; set; }
}
