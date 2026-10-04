namespace Reaparr.Domain;

public static class PlexMediaTypeExtensions
{
    public static int ToDefaultDestinationFolderId(this PlexMediaType type)
    {
        return type switch
        {
            PlexMediaType.Movie => 2,
            PlexMediaType.TvShow or PlexMediaType.Season or PlexMediaType.Episode => 3,
            PlexMediaType.Music or PlexMediaType.Album or PlexMediaType.Track => 4,
            PlexMediaType.Photos or PlexMediaType.PhotoAlbum => 5,
            PlexMediaType.OtherVideos => 6,
            PlexMediaType.Games => 7,
            _ => FolderTypeDefaults.DefaultDownloadFolderId, // Used for Downloads folder
        };
    }
}
