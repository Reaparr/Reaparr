namespace Reaparr.Domain;

public class DownloadTaskArtist : DownloadTaskParentBase
{
    public required ICollection<DownloadTaskAlbum> Children { get; set; } = [];

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.MusicArtist;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.Artist;

    [NotMapped]
    public override bool IsDownloadable => false;

    [NotMapped]
    public override int Count => Children.Sum(x => x.Count) + 1;

    public override DownloadTaskKey? ToParentKey() => null;
}
