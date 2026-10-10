namespace Reaparr.Domain;

public class DownloadTaskMusicTrack : DownloadTaskParentBase
{
    public required ICollection<DownloadTaskMusicTrackFile> Children { get; init; } = [];

    public required Guid ParentId { get; init; }

    public DownloadTaskMusicAlbum? Parent { get; init; }

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.MusicTrack;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.MusicTrack;

    [NotMapped]
    public override bool IsDownloadable => false;

    [NotMapped]
    public override int Count => Children.Sum(x => x.Count) + 1;

    public override DownloadTaskKey ToParentKey() =>
        new()
        {
            Type = DownloadTaskType.MusicAlbum,
            Id = ParentId,
            PlexServerId = PlexServerId,
            PlexLibraryId = PlexLibraryId,
        };
}
