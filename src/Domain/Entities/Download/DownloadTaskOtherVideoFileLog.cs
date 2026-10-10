namespace Reaparr.Domain;

public class DownloadTaskOtherVideoFileLog : DownloadTaskLogBase
{
    public required Guid DownloadTaskFileId { get; init; }

    public DownloadTaskOtherVideoFile? DownloadTaskFile { get; init; }

    public required Guid DownloadTaskOtherVideoId { get; init; }

    public DownloadTaskOtherVideo? DownloadTaskOtherVideo { get; init; }
}
