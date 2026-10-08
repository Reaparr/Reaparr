namespace Reaparr.Domain;

public static class PlexMediaTypeExtensions
{
    public static bool IsRootType(this PlexMediaType downloadTaskType) =>
        downloadTaskType
            is PlexMediaType.Movie
                or PlexMediaType.TvShow
                or PlexMediaType.MusicArtist
                or PlexMediaType.PhotoAlbum
                or PlexMediaType.OtherVideos;

    public static int ToDefaultDestinationFolderId(this PlexMediaType type)
    {
        return type switch
        {
            PlexMediaType.Movie => 2,
            PlexMediaType.TvShow or PlexMediaType.Season or PlexMediaType.Episode => 3,
            PlexMediaType.MusicArtist or PlexMediaType.MusicAlbum or PlexMediaType.MusicTrack => 4,
            PlexMediaType.PhotoImage or PlexMediaType.PhotoAlbum => 5,
            PlexMediaType.OtherVideos => 6,
            PlexMediaType.Games => 7,
            _ => FolderTypeDefaults.DefaultDownloadFolderId, // Used for Downloads folder
        };
    }
}
