namespace Reaparr.Data.Contracts;

public static partial class DownloadTaskExtensions
{
    public static IDownloadTaskProgress Calculate(this DownloadTaskGeneric downloadTask)
    {
        if (!downloadTask.Children.Any())
            return downloadTask;

        foreach (var child in downloadTask.Children)
            child.Calculate();

        downloadTask.DownloadSpeed = downloadTask.Children.Select(x => x.DownloadSpeed).Max();
        downloadTask.FileTransferSpeed = downloadTask.Children.Select(x => x.FileTransferSpeed).Max();

        downloadTask.FileDataTransferred = downloadTask.Children.Select(x => x.FileDataTransferred).Sum();
        downloadTask.DataReceived = downloadTask.Children.Select(x => x.DataReceived).Sum();
        downloadTask.DataTotal = downloadTask.Children.Select(x => x.DataTotal).Sum();
        downloadTask.Percentage = downloadTask.Children.Average(x => x.Percentage);
        downloadTask.DownloadStatus = DownloadTaskActions.Aggregate(
            downloadTask.Children.Select(x => x.DownloadStatus).ToList()
        );
        downloadTask.TimeRemaining = DownloadTaskPhaseExtensions.TimeRemaining(
            downloadTask.DownloadTaskPhase,
            downloadTask,
            downloadTask
        );

        return downloadTask;
    }

    public static IDownloadTaskProgress Calculate(this DownloadTaskMovie downloadTask)
    {
        if (!downloadTask.Children.Any())
        {
            downloadTask.Percentage = 0;
            downloadTask.TimeRemaining = 0;
            return downloadTask;
        }

        downloadTask.DownloadSpeed = downloadTask.Children.Select(x => x.DownloadSpeed).Max();
        downloadTask.FileTransferSpeed = downloadTask.Children.Select(x => x.FileTransferSpeed).Max();
        downloadTask.FileDataTransferred = downloadTask.Children.Select(x => x.FileDataTransferred).Sum();
        downloadTask.DataReceived = downloadTask.Children.Select(x => x.DataReceived).Sum();
        downloadTask.DataTotal = downloadTask.Children.Select(x => x.DataTotal).Sum();
        downloadTask.Percentage = downloadTask.Children.Average(x => x.Percentage);
        downloadTask.TimeRemaining = downloadTask.Children.Sum(x => x.TimeRemaining);
        downloadTask.DownloadStatus = DownloadTaskActions.Aggregate(
            downloadTask.Children.Select(x => x.DownloadStatus).ToList()
        );

        return downloadTask;
    }

    /// <summary>
    /// Calculate <see cref="IDownloadTaskProgress"/> properties such as DataReceived, DataTotal based on the nested children.
    /// </summary>
    public static IDownloadTaskProgress Calculate(this DownloadTaskTvShow downloadTask)
    {
        if (!downloadTask.Children.Any())
        {
            downloadTask.Percentage = 0;
            downloadTask.TimeRemaining = 0;
            return downloadTask;
        }

        foreach (var child in downloadTask.Children)
            child.Calculate();

        downloadTask.DownloadSpeed = downloadTask.Children.Select(x => x.DownloadSpeed).Max();
        downloadTask.FileTransferSpeed = downloadTask.Children.Select(x => x.FileTransferSpeed).Max();
        downloadTask.FileDataTransferred = downloadTask.Children.Select(x => x.FileDataTransferred).Sum();
        downloadTask.DataReceived = downloadTask.Children.Select(x => x.DataReceived).Sum();
        downloadTask.DataTotal = downloadTask.Children.Select(x => x.DataTotal).Sum();
        downloadTask.Percentage = downloadTask.Children.Average(x => x.Percentage);
        downloadTask.TimeRemaining = downloadTask.Children.Sum(x => x.TimeRemaining);
        downloadTask.DownloadStatus = DownloadTaskActions.Aggregate(
            downloadTask.Children.Select(x => x.DownloadStatus).ToList()
        );

        return downloadTask;
    }

    /// <summary>
    /// Calculate <see cref="IDownloadTaskProgress"/> properties such as DataReceived, DataTotal based on the nested children.
    /// </summary>
    public static IDownloadTaskProgress Calculate(this DownloadTaskTvShowSeason downloadTask)
    {
        if (!downloadTask.Children.Any())
        {
            downloadTask.Percentage = 0;
            downloadTask.TimeRemaining = 0;
            return downloadTask;
        }

        foreach (var child in downloadTask.Children)
            child.Calculate();

        downloadTask.DownloadSpeed = downloadTask.Children.Select(x => x.DownloadSpeed).Max();
        downloadTask.FileTransferSpeed = downloadTask.Children.Select(x => x.FileTransferSpeed).Max();
        downloadTask.FileDataTransferred = downloadTask.Children.Select(x => x.FileDataTransferred).Sum();
        downloadTask.DataReceived = downloadTask.Children.Select(x => x.DataReceived).Sum();
        downloadTask.DataTotal = downloadTask.Children.Select(x => x.DataTotal).Sum();
        downloadTask.Percentage = downloadTask.Children.Average(x => x.Percentage);
        downloadTask.TimeRemaining = downloadTask.Children.Sum(x => x.TimeRemaining);
        downloadTask.DownloadStatus = DownloadTaskActions.Aggregate(
            downloadTask.Children.Select(x => x.DownloadStatus).ToList()
        );

        return downloadTask;
    }

    /// <summary>
    /// Calculate <see cref="IDownloadTaskProgress"/> properties such as DataReceived, DataTotal based on the nested children.
    /// </summary>
    public static IDownloadTaskProgress Calculate(this DownloadTaskTvShowEpisode downloadTask)
    {
        if (!downloadTask.Children.Any())
        {
            downloadTask.Percentage = 0;
            downloadTask.TimeRemaining = 0;
            return downloadTask;
        }

        downloadTask.DownloadSpeed = downloadTask.Children.Select(x => x.DownloadSpeed).Max();
        downloadTask.FileTransferSpeed = downloadTask.Children.Select(x => x.FileTransferSpeed).Max();
        downloadTask.FileDataTransferred = downloadTask.Children.Select(x => x.FileDataTransferred).Sum();
        downloadTask.DataReceived = downloadTask.Children.Select(x => x.DataReceived).Sum();
        downloadTask.DataTotal = downloadTask.Children.Select(x => x.DataTotal).Sum();
        downloadTask.Percentage = downloadTask.Children.Average(x => x.Percentage);
        downloadTask.TimeRemaining = downloadTask.Children.Sum(x => x.TimeRemaining);
        downloadTask.DownloadStatus = DownloadTaskActions.Aggregate(
            downloadTask.Children.Select(x => x.DownloadStatus).ToList()
        );

        return downloadTask;
    }

    public static IDownloadTaskProgress Calculate(this DownloadTaskOtherVideo downloadTask)
    {
        if (!downloadTask.Children.Any())
        {
            downloadTask.Percentage = 0;
            downloadTask.TimeRemaining = 0;
            return downloadTask;
        }

        downloadTask.DownloadSpeed = downloadTask.Children.Select(x => x.DownloadSpeed).Max();
        downloadTask.FileTransferSpeed = downloadTask.Children.Select(x => x.FileTransferSpeed).Max();
        downloadTask.FileDataTransferred = downloadTask.Children.Select(x => x.FileDataTransferred).Sum();
        downloadTask.DataReceived = downloadTask.Children.Select(x => x.DataReceived).Sum();
        downloadTask.DataTotal = downloadTask.Children.Select(x => x.DataTotal).Sum();
        downloadTask.Percentage = downloadTask.Children.Average(x => x.Percentage);
        downloadTask.TimeRemaining = downloadTask.Children.Sum(x => x.TimeRemaining);
        downloadTask.DownloadStatus = DownloadTaskActions.Aggregate(
            downloadTask.Children.Select(x => x.DownloadStatus).ToList()
        );

        return downloadTask;
    }
}
