namespace Reaparr.Domain;

public static class DownloadTaskTypeExtensions
{
    public static bool IsDataOrPart(this DownloadTaskType downloadTaskType) =>
        downloadTaskType
            is DownloadTaskType.EpisodeData
                or DownloadTaskType.EpisodePart
                or DownloadTaskType.PhotoData
                or DownloadTaskType.PhotoPart
                or DownloadTaskType.MusicTrackData
                or DownloadTaskType.MusicTrackPart
                or DownloadTaskType.OtherVideoData
                or DownloadTaskType.OtherVideoPart;

    public static bool IsParent(this DownloadTaskType downloadTaskType) =>
        downloadTaskType
            is DownloadTaskType.TvShow
                or DownloadTaskType.Season
                or DownloadTaskType.MusicArtist
                or DownloadTaskType.MusicAlbum
                or DownloadTaskType.PhotoAlbum;
}
