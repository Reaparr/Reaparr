namespace Reaparr.SignalR.Contracts;

public interface IDownloadHubService
{
    /// <summary>Sends a download progress update to the front-end.</summary>
    /// <param name="downloadTasks">The download tasks whose progress is sent.</param>
    /// <param name="cancellationToken">A token that cancels sending progress updates.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendDownloadProgressUpdateAsync(
        List<DownloadTaskGeneric> downloadTasks,
        CancellationToken cancellationToken = default
    );

    /// <summary>Sends a download patch update to the front-end.</summary>
    /// <param name="plexServerId">The Plex server associated with the patch.</param>
    /// <param name="sequence">The patch sequence number.</param>
    /// <param name="upserts">The download patches to add or update.</param>
    /// <param name="deletedIds">The identifiers of downloads to remove.</param>
    /// <param name="cancellationToken">A token that cancels sending the patch.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendDownloadPatchAsync(
        int plexServerId,
        long sequence,
        IReadOnlyCollection<DownloadPatchDTO> upserts,
        IReadOnlyCollection<Guid>? deletedIds = null,
        CancellationToken cancellationToken = default
    );
}
