namespace Reaparr.Application.Contracts;

public interface IDownloadTaskScheduler
{
    Task<Result> StartDownloadTaskJob(DownloadTaskKey downloadTaskKey, CancellationToken cancellationToken);

    Task<Result> StopDownloadTaskJob(
        DownloadTaskKey downloadTaskKey,
        CancellationToken cancellationToken = default,
        bool waitForCompletion = true
    );

    Task<bool> IsDownloading(DownloadTaskKey downloadTaskKey, CancellationToken cancellationToken = default);

    Task<bool> IsServerDownloading(int plexServerId);


    Task<List<DownloadTaskKey>> GetCurrentlyDownloadingKeysByServer(int plexServerId);
}
