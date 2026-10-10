namespace Reaparr.Domain;

public class DownloadTaskMusicTrackFile : DownloadTaskFileBase
{
    public required Guid ParentId { get; init; }

    public DownloadTaskMusicTrack? Parent { get; init; }

    public List<DownloadTaskTrackFileLog> Logs { get; init; } = [];

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.MusicTrack;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.MusicTrackData;

    [NotMapped]
    public override bool IsDownloadable => true;

    [NotMapped]
    public override int Count => 1;

    public override DownloadTaskKey ToParentKey() =>
        new()
        {
            Type = DownloadTaskType.MusicTrack,
            Id = ParentId,
            PlexServerId = PlexServerId,
            PlexLibraryId = PlexLibraryId,
        };
}
