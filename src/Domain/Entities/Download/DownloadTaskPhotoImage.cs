namespace Reaparr.Domain;

public class DownloadTaskPhotoImage : DownloadTaskParentBase
{
    public required Guid ParentId { get; init; }

    public DownloadTaskPhotoAlbum? Parent { get; init; }

    public required ICollection<DownloadTaskPhotoImageFile> Children { get; set; } = [];

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.PhotoImage;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.PhotoImage;

    [NotMapped]
    public override bool IsDownloadable => false;

    [NotMapped]
    public override int Count => Children.Sum(x => x.Count) + 1;

    public override DownloadTaskKey? ToParentKey() => null;
}
