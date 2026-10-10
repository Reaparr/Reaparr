namespace Reaparr.Domain;

public class DownloadTaskOtherVideo : DownloadTaskParentBase
{
    public required ICollection<DownloadTaskOtherVideoFile> Children { get; set; } = [];

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.OtherVideos;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.OtherVideo;

    [NotMapped]
    public override bool IsDownloadable => false;

    [NotMapped]
    public override int Count => Children.Sum(x => x.Count) + 1;

    public override DownloadTaskKey? ToParentKey() => null;
}
