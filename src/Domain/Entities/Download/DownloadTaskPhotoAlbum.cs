namespace Reaparr.Domain;

public class DownloadTaskPhotoAlbum : DownloadTaskParentBase
{
    public required ICollection<DownloadTaskPhotoImage> Children { get; set; } = [];

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.PhotoAlbum;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.PhotoAlbum;

    [NotMapped]
    public override bool IsDownloadable => false;

    [NotMapped]
    public override int Count => Children.Sum(x => x.Count) + 1;

    public override DownloadTaskKey? ToParentKey() => null;
}
