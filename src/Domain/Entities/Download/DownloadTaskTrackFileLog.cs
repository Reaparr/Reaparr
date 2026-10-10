namespace Reaparr.Domain;

public class DownloadTaskTrackFileLog : DownloadTaskLogBase
{
    public required Guid DownloadTaskFileId { get; init; }

    public DownloadTaskMusicTrackFile? DownloadTaskFile { get; init; }

    public required Guid DownloadTaskTrackId { get; init; }

    public DownloadTaskMusicTrack? DownloadTaskTrack { get; init; }

    public required Guid DownloadTaskAlbumId { get; init; }

    public DownloadTaskMusicAlbum? DownloadTaskAlbum { get; init; }

    public required Guid DownloadTaskArtistId { get; init; }

    public DownloadTaskMusicArtist? DownloadTaskArtist { get; init; }
}
