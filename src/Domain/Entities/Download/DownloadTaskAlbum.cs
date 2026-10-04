namespace Reaparr.Domain;

public class DownloadTaskAlbum : DownloadTaskParentBase
{
    public required ICollection<DownloadTaskTrack> Children { get; set; } = [];

    public required Guid ParentId { get; init; }

    public DownloadTaskArtist? Parent { get; init; }

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.MusicAlbum;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.Album;

    [NotMapped]
    public override bool IsDownloadable => false;

    [NotMapped]
    public override int Count => Children.Sum(x => x.Count) + 1;

    public override DownloadTaskKey ToParentKey() =>
        new()
        {
            Type = DownloadTaskType.Artist,
            Id = ParentId,
            PlexServerId = PlexServerId,
            PlexLibraryId = PlexLibraryId,
        };
}
