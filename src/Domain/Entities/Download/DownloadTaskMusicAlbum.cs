namespace Reaparr.Domain;

public class DownloadTaskMusicAlbum : DownloadTaskParentBase
{
    public required ICollection<DownloadTaskMusicTrack> Children { get; set; } = [];

    public required Guid ParentId { get; init; }

    public DownloadTaskMusicArtist? Parent { get; init; }

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.MusicAlbum;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.MusicAlbum;

    [NotMapped]
    public override bool IsDownloadable => false;

    [NotMapped]
    public override int Count => Children.Sum(x => x.Count) + 1;

    public override DownloadTaskKey ToParentKey() =>
        new()
        {
            Type = DownloadTaskType.MusicArtist,
            Id = ParentId,
            PlexServerId = PlexServerId,
            PlexLibraryId = PlexLibraryId,
        };
}
