namespace Reaparr.Domain;

public class DownloadTaskPhotoImageFile : DownloadTaskFileBase
{
    public required Guid ParentId { get; init; }

    public DownloadTaskPhotoImage? Parent { get; init; }

    public List<DownloadTaskPhotoImageFileLog> Logs { get; init; } = [];

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.PhotoImage;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.PhotoData;

    [NotMapped]
    public override bool IsDownloadable => true;

    [NotMapped]
    public override int Count => 1;

    public override DownloadTaskKey ToParentKey() =>
        new()
        {
            Type = DownloadTaskType.PhotoImage,
            Id = ParentId,
            PlexServerId = PlexServerId,
            PlexLibraryId = PlexLibraryId,
        };
}
