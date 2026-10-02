namespace Reaparr.Domain;

public class DownloadTaskTrackFileLog : DownloadTaskLogBase
{
    public required Guid DownloadTaskFileId { get; init; }

    public DownloadTaskTrackFile? DownloadTaskFile { get; init; }

    public required Guid DownloadTaskTrackId { get; init; }

    public DownloadTaskTrack? DownloadTaskTrack { get; init; }

    public required Guid DownloadTaskAlbumId { get; init; }

    public DownloadTaskAlbum? DownloadTaskAlbum { get; init; }

    public required Guid DownloadTaskArtistId { get; init; }

    public DownloadTaskArtist? DownloadTaskArtist { get; init; }
}
