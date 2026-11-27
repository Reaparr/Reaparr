namespace Reaparr.Application.Contracts;

public interface IDownloadQueue : ISetup, IBusy
{
    Result Setup(CancellationToken cancellationToken);

    /// <summary>Waits for the worker to exit after its application-stopping token is cancelled.</summary>
    Task WaitForShutdownAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Check the DownloadQueue for downloadTasks which can be started.
    /// </summary>
    Task<Result> CheckDownloadQueue(List<int> plexServerIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the DownloadQueue for every Plex server. Used at boot to kick the queue for
    /// servers that were already online and therefore do not emit an online-status transition.
    /// </summary>
    Task<Result> CheckDownloadQueueForAllServers() => CheckDownloadQueueForAllServers(CancellationToken.None);

    Task<Result> CheckDownloadQueueForAllServers(CancellationToken cancellationToken);
}
