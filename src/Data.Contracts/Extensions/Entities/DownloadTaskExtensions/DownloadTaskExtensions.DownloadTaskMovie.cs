namespace Reaparr.Data.Contracts;

public static partial class DownloadTaskExtensions
{
    /// <summary>
    /// This will set the relationship ids for the download tasks and it's children.
    /// </summary>
    /// TODO Change to a recursive function on DownloadTaskBase and not on individual descendants
    public static void SetRelationshipIds<T>(this ICollection<T> downloadTasks, int plexServerId, int plexLibraryId)
        where T : DownloadTaskBase
    {
        foreach (var task in downloadTasks)
        {
            task.PlexLibraryId = plexLibraryId;
            task.PlexServerId = plexServerId;
            switch (task)
            {
                case DownloadTaskMovie movie:
                    movie.Children.SetRelationshipIds(plexServerId, plexLibraryId);
                    break;
                case DownloadTaskTvShow show:
                    show.Children.SetRelationshipIds(plexServerId, plexLibraryId);
                    break;
                case DownloadTaskTvShowSeason season:
                    season.Children.SetRelationshipIds(plexServerId, plexLibraryId);
                    break;
                case DownloadTaskTvShowEpisode episode:
                    episode.Children.SetRelationshipIds(plexServerId, plexLibraryId);
                    break;
                case DownloadTaskMusicArtist artist:
                    artist.Children.SetRelationshipIds(plexServerId, plexLibraryId);
                    break;
                case DownloadTaskMusicAlbum album:
                    album.Children.SetRelationshipIds(plexServerId, plexLibraryId);
                    break;
                case DownloadTaskMusicTrack track:
                    track.Children.SetRelationshipIds(plexServerId, plexLibraryId);
                    break;
                case DownloadTaskPhotoAlbum photoAlbum:
                    photoAlbum.Children.SetRelationshipIds(plexServerId, plexLibraryId);
                    break;
                case DownloadTaskPhotoImage image:
                    image.Children.SetRelationshipIds(plexServerId, plexLibraryId);
                    break;
                case DownloadTaskOtherVideo video:
                    video.Children.SetRelationshipIds(plexServerId, plexLibraryId);
                    break;
            }
        }
    }

    /// <summary>
    /// This will set the relationship ids for the download tasks and it's children.
    /// </summary>
    public static void SetRelationshipIds(
        this ICollection<DownloadTaskTvShow> downloadTasks,
        int plexServerId,
        int plexLibraryId
    )
    {
        foreach (var downloadTaskTvShow in downloadTasks)
        {
            downloadTaskTvShow.PlexLibraryId = plexLibraryId;
            downloadTaskTvShow.PlexServerId = plexServerId;
            downloadTaskTvShow.Children.SetRelationshipIds(plexServerId, plexLibraryId);
        }
    }

    /// <summary>
    /// This will set the relationship ids for the download tasks and it's children.
    /// </summary>
    public static void SetRelationshipIds(
        this ICollection<DownloadTaskTvShowSeason> downloadTasks,
        int plexServerId,
        int plexLibraryId
    )
    {
        foreach (var downloadTaskTvShowSeason in downloadTasks)
        {
            downloadTaskTvShowSeason.PlexLibraryId = plexLibraryId;
            downloadTaskTvShowSeason.PlexServerId = plexServerId;
            downloadTaskTvShowSeason.Children.SetRelationshipIds(plexServerId, plexLibraryId);
        }
    }

    /// <summary>
    /// This will set the relationship ids for the download tasks and it's children.
    /// </summary>
    public static void SetRelationshipIds(
        this ICollection<DownloadTaskTvShowEpisode> downloadTasks,
        int plexServerId,
        int plexLibraryId
    )
    {
        foreach (var downloadTaskTvShowEpisode in downloadTasks)
        {
            downloadTaskTvShowEpisode.PlexLibraryId = plexLibraryId;
            downloadTaskTvShowEpisode.PlexServerId = plexServerId;
            foreach (var downloadTaskTvShowEpisodeFile in downloadTaskTvShowEpisode.Children)
            {
                downloadTaskTvShowEpisodeFile.PlexLibraryId = plexLibraryId;
                downloadTaskTvShowEpisodeFile.PlexServerId = plexServerId;
            }
        }
    }
}
