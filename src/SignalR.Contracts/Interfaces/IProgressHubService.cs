namespace Reaparr.SignalR.Contracts;

public interface IProgressHubService
{
    /// <summary>Sends a library progress update to the front-end.</summary>
    /// <param name="progress">The library synchronization progress to send.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendLibraryProgressUpdateAsync(LibrarySyncProgressDTO progress);

    /// <summary>Sends a server connection check status progress update to the front-end.</summary>
    /// <param name="progress">The server connection check status to send.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendServerConnectionCheckStatusProgressAsync(ServerConnectionCheckStatusProgress progress);

    /// <summary>Sends an app download progress update to the front-end.</summary>
    /// <param name="progress">The application update download progress to send.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendAppUpdateDownloadProgressAsync(AppUpdateDownloadProgressDTO progress);

    /// <summary>Sends a background job status update to the front-end.</summary>
    /// <param name="jobStatusUpdate">The background job status update to send.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendJobStatusUpdateAsync<T>(JobStatusUpdate<T> jobStatusUpdate)
        where T : class;

    /// <summary>Sends an integration setup progress update to the front-end.</summary>
    /// <param name="progress">The integration setup progress to send.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendIntegrationSetupProgressAsync(IntegrationSetupProgressDTO progress);
}
