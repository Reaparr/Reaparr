namespace Reaparr.Domain;

public class DownloadTaskTrackFile : DownloadTaskFileBase
{
    public required Guid ParentId { get; init; }

    public DownloadTaskTrack? Parent { get; init; }

    public List<DownloadTaskTrackFileLog> Logs { get; init; } = [];

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.Track;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.TrackData;

    [NotMapped]
    public override bool IsDownloadable => true;

    [NotMapped]
    public override int Count => 1;

    public override DownloadTaskKey ToParentKey() =>
        new()
        {
            Type = DownloadTaskType.Track,
            Id = ParentId,
            PlexServerId = PlexServerId,
            PlexLibraryId = PlexLibraryId,
        };
}
