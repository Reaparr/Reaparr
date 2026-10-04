namespace Reaparr.Domain;

public class DownloadTaskMusicArtist : DownloadTaskParentBase
{
    public required ICollection<DownloadTaskMusicAlbum> Children { get; set; } = [];

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.MusicArtist;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.MusicArtist;

    [NotMapped]
    public override bool IsDownloadable => false;

    [NotMapped]
    public override int Count => Children.Sum(x => x.Count) + 1;

    public override DownloadTaskKey? ToParentKey() => null;
}
