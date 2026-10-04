namespace Reaparr.Domain;

public class DownloadTaskTrack : DownloadTaskParentBase
{
    public required ICollection<DownloadTaskTrackFile> Children { get; set; } = [];

    public required Guid ParentId { get; init; }

    public DownloadTaskAlbum? Parent { get; init; }

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.Track;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.Track;

    [NotMapped]
    public override bool IsDownloadable => false;

    [NotMapped]
    public override int Count => Children.Sum(x => x.Count) + 1;

    public override DownloadTaskKey ToParentKey() =>
        new()
        {
            Type = DownloadTaskType.Album,
            Id = ParentId,
            PlexServerId = PlexServerId,
            PlexLibraryId = PlexLibraryId,
        };
}
