using Reaparr.Environment;

namespace Reaparr.Domain;

public static class DownloadTaskDirectoryExtensions
{
    public static string GetDownloadCategoryDirectory(this DownloadTaskDirectory directory, DownloadTaskType type)
    {
        ArgumentNullException.ThrowIfNull(directory);

        if (string.IsNullOrEmpty(directory.DownloadRootPath))
            return string.Empty;

        return Path.Combine(directory.DownloadRootPath, GetCategoryFolderName(type));
    }

    public static string GetDownloadDirectory(this DownloadTaskDirectory directory, DownloadTaskType type)
    {
        var categoryDirectory = directory.GetDownloadCategoryDirectory(type);
        return string.IsNullOrEmpty(categoryDirectory)
            ? string.Empty
            : Path.Combine(categoryDirectory, GetRelativeDirectory(directory, type));
    }

    public static string GetDestinationDirectory(this DownloadTaskDirectory directory, DownloadTaskType type)
    {
        ArgumentNullException.ThrowIfNull(directory);

        if (string.IsNullOrEmpty(directory.DestinationRootPath))
            return string.Empty;

        return Path.Combine(directory.DestinationRootPath, GetRelativeDirectory(directory, type));
    }

    private static string GetCategoryFolderName(DownloadTaskType type) =>
        type switch
        {
            DownloadTaskType.Movie or DownloadTaskType.MovieData or DownloadTaskType.MoviePart =>
                IPathProvider.DefaultMovieFolderName,
            DownloadTaskType.TvShow
            or DownloadTaskType.Season
            or DownloadTaskType.Episode
            or DownloadTaskType.EpisodeData
            or DownloadTaskType.EpisodePart => IPathProvider.DefaultTvShowsFolderName,
            DownloadTaskType.MusicArtist
            or DownloadTaskType.MusicAlbum
            or DownloadTaskType.MusicTrack
            or DownloadTaskType.MusicTrackData
            or DownloadTaskType.MusicTrackPart => IPathProvider.DefaultMusicFolderName,
            DownloadTaskType.PhotoAlbum
            or DownloadTaskType.PhotoImage
            or DownloadTaskType.PhotoData
            or DownloadTaskType.PhotoPart => IPathProvider.DefaultPhotosFolderName,
            DownloadTaskType.OtherVideo
            or DownloadTaskType.OtherVideoData
            or DownloadTaskType.OtherVideoPart => IPathProvider.DefaultOtherFolderName,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported download task type."),
        };

    private static string GetRelativeDirectory(DownloadTaskDirectory directory, DownloadTaskType type) =>
        type switch
        {
            DownloadTaskType.Movie or DownloadTaskType.MovieData or DownloadTaskType.MoviePart => directory.MovieFolder,
            DownloadTaskType.TvShow => directory.TvShowFolder,
            DownloadTaskType.Season
            or DownloadTaskType.Episode
            or DownloadTaskType.EpisodeData
            or DownloadTaskType.EpisodePart => Path.Combine(directory.TvShowFolder, directory.SeasonFolder),
            DownloadTaskType.MusicArtist => directory.MusicArtistFolder,
            DownloadTaskType.MusicAlbum
            or DownloadTaskType.MusicTrack
            or DownloadTaskType.MusicTrackData
            or DownloadTaskType.MusicTrackPart => Path.Combine(
                directory.MusicArtistFolder,
                directory.MusicAlbumFolder
            ),
            DownloadTaskType.PhotoAlbum
            or DownloadTaskType.PhotoImage
            or DownloadTaskType.PhotoData
            or DownloadTaskType.PhotoPart => directory.PhotoAlbumFolder,
            DownloadTaskType.OtherVideo
            or DownloadTaskType.OtherVideoData
            or DownloadTaskType.OtherVideoPart => directory.OtherVideoFolder,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported download task type."),
        };

    public static string GetDownloadDirectory(this DownloadTaskFileBase task) =>
        task.DirectoryMeta.GetDownloadDirectory(task.DownloadTaskType);

    public static string GetDestinationDirectory(this DownloadTaskFileBase task) =>
        task.DirectoryMeta.GetDestinationDirectory(task.DownloadTaskType);

    public static string GetDownloadFilePath(this DownloadTaskFileBase task) =>
        Path.Combine(task.GetDownloadDirectory(), task.FileName.AddReaparrTempSuffixToFileName());

    public static string GetDestinationFilePath(this DownloadTaskFileBase task) =>
        Path.Join(task.GetDestinationDirectory(), task.FileName);

}
