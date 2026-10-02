namespace Reaparr.Domain;

public class DownloadTaskPhotoFileLog : DownloadTaskLogBase
{
    public required Guid DownloadTaskFileId { get; init; }

    public DownloadTaskPhotoFile? DownloadTaskFile { get; init; }

    public required Guid DownloadTaskPhotoId { get; init; }

    public DownloadTaskPhoto? DownloadTaskPhoto { get; init; }
}
