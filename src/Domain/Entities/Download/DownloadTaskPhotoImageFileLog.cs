namespace Reaparr.Domain;

public class DownloadTaskPhotoImageFileLog : DownloadTaskLogBase
{
    public required Guid DownloadTaskFileId { get; init; }

    public DownloadTaskPhotoImageFile? DownloadTaskFile { get; init; }

    public required Guid DownloadTaskPhotoId { get; init; }

    public DownloadTaskPhotoImage? DownloadTaskPhoto { get; init; }
}
