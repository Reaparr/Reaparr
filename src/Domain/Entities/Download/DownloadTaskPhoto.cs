namespace Reaparr.Domain;

public class DownloadTaskPhoto : DownloadTaskParentBase
{
    public required PhotoAssetKind Kind { get; init; }

    public required ICollection<DownloadTaskPhotoFile> Children { get; set; } = [];

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.PhotoImage;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.Photo;

    [NotMapped]
    public override bool IsDownloadable => false;

    [NotMapped]
    public override int Count => Children.Sum(x => x.Count) + 1;

    public override DownloadTaskKey? ToParentKey() => null;
}
