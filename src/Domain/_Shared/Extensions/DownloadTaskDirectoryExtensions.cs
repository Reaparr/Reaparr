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
            DownloadTaskType.Movie or DownloadTaskType.MovieData => IPathProvider.DefaultMovieFolderName,
            DownloadTaskType.TvShow
            or DownloadTaskType.Season
            or DownloadTaskType.Episode
            or DownloadTaskType.EpisodeData => IPathProvider.DefaultTvShowsFolderName,
            DownloadTaskType.MusicArtist
            or DownloadTaskType.MusicAlbum
            or DownloadTaskType.MusicTrack
            or DownloadTaskType.MusicTrackData => IPathProvider.DefaultMusicFolderName,
            DownloadTaskType.PhotoFolder or DownloadTaskType.PhotoImage => IPathProvider.DefaultPhotosFolderName,
            DownloadTaskType.OtherVideo or DownloadTaskType.OtherVideoData => IPathProvider.DefaultOtherFolderName,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported download task type."),
        };

    private static string GetRelativeDirectory(DownloadTaskDirectory directory, DownloadTaskType type) =>
        type switch
        {
            DownloadTaskType.Movie or DownloadTaskType.MovieData => directory.MovieFolder,
            DownloadTaskType.TvShow => directory.TvShowFolder,
            DownloadTaskType.Season or DownloadTaskType.Episode or DownloadTaskType.EpisodeData => Path.Combine(
                directory.TvShowFolder,
                directory.SeasonFolder
            ),
            DownloadTaskType.MusicArtist => directory.MusicArtistFolder,
            DownloadTaskType.MusicAlbum or DownloadTaskType.MusicTrack or DownloadTaskType.MusicTrackData =>
                Path.Combine(directory.MusicArtistFolder, directory.MusicAlbumFolder),
            DownloadTaskType.PhotoFolder or DownloadTaskType.PhotoImage => directory.PhotoFolder,
            DownloadTaskType.OtherVideo or DownloadTaskType.OtherVideoData => directory.OtherVideoFolder,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported download task type."),
        };

    public static string GetDownloadDirectory(this DownloadTaskFileBase task) =>
        task.DirectoryMeta.GetDownloadDirectory(task.DownloadTaskType);

    public static string GetDestinationDirectory(this DownloadTaskFileBase task) =>
        task.DirectoryMeta.GetDestinationDirectory(task.DownloadTaskType);

    public static string GetDownloadFilePath(this DownloadTaskFileBase task) =>
        Path.Combine(task.GetDownloadDirectory(), task.FileName.AddReaparrTempSuffixToFileName());

    public static string GetDestinationFilePath(this DownloadTaskFileBase task) =>
        task.DirectoryMeta.GetDestinationFilePath(task.DownloadTaskType, task.FileName);

    public static string GetDestinationFilePath(
        this DownloadTaskDirectory directory,
        DownloadTaskType type,
        string fileName
    ) => Path.Join(directory.GetDestinationDirectory(type), fileName);
}
