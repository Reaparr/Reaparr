namespace Reaparr.Domain;

public class DownloadTaskOtherVideoFile : DownloadTaskFileBase
{
    public FolderPath? DestinationFolderPath { get; init; }

    public required Guid ParentId { get; init; }

    public DownloadTaskOtherVideo? Parent { get; init; }

    public List<DownloadTaskOtherVideoFileLog> Logs { get; init; } = [];

    [NotMapped]
    public override PlexMediaType MediaType => PlexMediaType.OtherVideos;

    [NotMapped]
    public override DownloadTaskType DownloadTaskType => DownloadTaskType.OtherVideoData;

    [NotMapped]
    public override bool IsDownloadable => true;

    [NotMapped]
    public override int Count => 1;

    public override DownloadTaskKey ToParentKey() =>
        new()
        {
            Type = DownloadTaskType.OtherVideo,
            Id = ParentId,
            PlexServerId = PlexServerId,
            PlexLibraryId = PlexLibraryId,
        };
}
