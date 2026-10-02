namespace Reaparr.Domain;

public class DownloadTaskPhotoFile : DownloadTaskFileBase
{
    public required Guid ParentId { get; init; }

    public DownloadTaskPhoto? Parent { get; init; }

    public List<DownloadTaskPhotoFileLog> Logs { get; init; } = [];

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.Photos;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.PhotoData;

    [NotMapped]
    public override bool IsDownloadable => true;

    [NotMapped]
    public override int Count => 1;

    public override DownloadTaskKey ToParentKey() =>
        new()
        {
            Type = DownloadTaskType.Photo,
            Id = ParentId,
            PlexServerId = PlexServerId,
            PlexLibraryId = PlexLibraryId,
        };
}
