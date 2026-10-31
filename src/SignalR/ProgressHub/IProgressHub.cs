namespace Reaparr.SignalR;

/// <summary>
///  The ProgressHub class is a SignalR hub that sends progress updates to the front-end.
/// </summary>
public interface IProgressHub
{
    /// <summary>
    ///  Sends a background job status update to the front-end.
    /// </summary>
    /// <param name="jobStatusUpdate"></param>
    /// <param name="cancellationToken"> The <see cref="CancellationToken"/> to use.</param>
    Task JobStatusUpdate(JobStatusUpdateDTO jobStatusUpdate, CancellationToken cancellationToken = default);

    /// <summary>
    ///  Sends a server connection check status progress update to the front-end.
    /// </summary>
    /// <param name="serverConnectionCheckStatusProgress"></param>
    /// <param name="cancellationToken"> The <see cref="CancellationToken"/> to use.</param>
    Task ServerConnectionCheckStatusProgress(
        ServerConnectionCheckStatusProgressDTO serverConnectionCheckStatusProgress,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///  Sends a library progress update to the front-end.
    /// </summary>
    /// <param name="libraryProgress"></param>
    /// <param name="cancellationToken"> The <see cref="CancellationToken"/> to use.</param>
    Task LibraryProgress(LibrarySyncProgressDTO libraryProgress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a Reaparr update download progress update to the front-end.
    /// </summary>
    /// <param name="appUpdateDownloadProgress"></param>
    /// <param name="cancellationToken"> The <see cref="CancellationToken"/> to use.</param>
    Task AppUpdateDownloadProgress(
        AppUpdateDownloadProgressDTO appUpdateDownloadProgress,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Sends an integration setup progress update to the front-end.
    /// </summary>
    /// <param name="progress"></param>
    /// <param name="cancellationToken"> The <see cref="CancellationToken"/> to use.</param>
    Task IntegrationSetupProgress(IntegrationSetupProgressDTO progress, CancellationToken cancellationToken = default);
}
