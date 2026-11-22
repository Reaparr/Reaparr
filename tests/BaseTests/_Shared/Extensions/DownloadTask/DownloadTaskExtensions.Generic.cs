namespace Reaparr.BaseTests;

public static partial class DownloadTaskExtensions
{
    public static DownloadTaskGeneric SetDownloadStatus(
        this DownloadTaskGeneric downloadTask,
        DownloadStatus downloadStatus
    )
    {
        downloadTask.DownloadStatus = downloadStatus;
        if (downloadTask.Children.Any())
            downloadTask.Children = downloadTask.Children.SetDownloadStatus(downloadStatus).ToList();

        return downloadTask;
    }

    public static ICollection<DownloadTaskGeneric> SetDownloadStatus(
        this ICollection<DownloadTaskGeneric> downloadTasks,
        DownloadStatus downloadStatus
    )
    {
        foreach (var downloadTask in downloadTasks)
        {
            downloadTask.DownloadStatus = downloadStatus;
            if (downloadTask.Children.Any())
                downloadTask.Children = downloadTask.Children.SetDownloadStatus(downloadStatus).ToList();
        }

        return downloadTasks;
    }

    /// <summary>
    /// The percentage is set based on the download status as to ensure <see cref="DownloadTaskPhase"/> is set correctly.
    /// </summary>
    /// <param name="downloadTask"></param>
    /// <param name="downloadStatus"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static void SetPercentageBasedOnStatus(
        this DownloadTaskFileBase downloadTask,
        DownloadStatus downloadStatus
    )
    {
        switch (downloadStatus)
        {
            case DownloadStatus.Queued:
            case DownloadStatus.Stopped:
                downloadTask.DataReceived = 0;
                downloadTask.FileDataTransferred = 0;
                break;
            case DownloadStatus.Downloading:
            case DownloadStatus.Paused:
            case DownloadStatus.AutoPaused:
            case DownloadStatus.Error:
            case DownloadStatus.ServerUnreachable:
                downloadTask.DataReceived = 1;
                downloadTask.FileDataTransferred = 0;
                break;
            case DownloadStatus.DownloadFinished:
            case DownloadStatus.Moving:
            case DownloadStatus.MovePaused:
            case DownloadStatus.AutoMovePaused:
            case DownloadStatus.MoveError:
                downloadTask.DataReceived = downloadTask.DataTotal;
                downloadTask.FileDataTransferred = 0;
                break;
            case DownloadStatus.MoveFinished:
            case DownloadStatus.Completed:
                downloadTask.DataReceived = downloadTask.DataTotal;
                downloadTask.FileDataTransferred = downloadTask.DataTotal;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(downloadStatus), downloadStatus, null);
        }
    }
}
