namespace Reaparr.Environment;

public interface IPathProvider
{
    /// <summary>
    /// Gets the full path to the main Reaparr settings file.
    /// </summary>
    string ConfigFileLocation { get; }

    /// <summary>
    /// Gets the directory used to store database backup files.
    /// </summary>
    string DatabaseBackupDirectory { get; }

    /// <summary>
    /// Gets the full path to the SQLite database file.
    /// </summary>
    string DatabasePath { get; }

    /// <summary>
    /// Gets the directory used to store application log files.
    /// </summary>
    string LogsDirectory { get; }

    List<string> DatabaseFiles { get; }

    /// <summary>
    /// Gets the default downloads destination path based on the configured data root or the current platform fallback.
    /// </summary>
    string DefaultDownloadsDestinationFolder { get; }

    /// <summary>
    /// Gets the default movies destination path based on the configured data root or the current platform fallback.
    /// </summary>
    string DefaultMovieDestinationFolder { get; }

    /// <summary>
    /// Gets the default TV shows destination path based on the configured data root or the current platform fallback.
    /// </summary>
    string DefaultTvShowsDestinationFolder { get; }

    /// <summary>
    /// Gets the default music destination path based on the configured data root or the current platform fallback.
    /// </summary>
    string DefaultMusicDestinationFolder { get; }

    /// <summary>
    /// Gets the default photos destination path based on the configured data root or the current platform fallback.
    /// </summary>
    string DefaultPhotosDestinationFolder { get; }

    /// <summary>
    /// Gets the default uncategorized media destination path based on the configured data root or the current platform fallback.
    /// </summary>
    string DefaultOtherDestinationFolder { get; }

    /// <summary>
    /// Gets the default games destination path based on the configured data root or the current platform fallback.
    /// </summary>
    string DefaultGamesDestinationFolder { get; }

    /// <summary>
    /// Gets the directory that stores Reaparr's own application state, such as settings, the SQLite database, backups, and logs.
    /// This is distinct from <see cref="DataDirectory"/>, which is the root location for user media content like Movies, TV shows, and Downloads.
    /// </summary>
    string ConfigDirectory { get; }

    /// <summary>
    /// Gets the root directory for user media data, where default destination folders such as Movies, TV shows, Music, and Downloads are created.
    /// This is distinct from <see cref="ConfigDirectory"/>, which stores Reaparr's internal application files rather than media content.
    /// </summary>
    string DataDirectory { get; }

    /// <summary>
    /// Gets the full path to the SQLite shared-memory sidecar file.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    string Database_SHM_Path { get; }

    /// <summary>
    /// Gets the full path to the SQLite write-ahead log sidecar file.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    string Database_WAL_Path { get; }

    /// <summary>
    /// Gets the default folder name used for application logs.
    /// </summary>
    public static string DefaultLogsFolderName => "Logs";

    /// <summary>
    /// Gets the sub-folder in some cases such as in Desktop mode.
    /// </summary>
    public static string DefaultReaparrFolderName => "Reaparr";

    /// <summary>
    /// Gets the default folder name used for movie libraries under the root media directory.
    /// </summary>
    public static string DefaultMovieFolderName => "Movies";

    /// <summary>
    /// Gets the default folder name used for downloaded files under the root media directory.
    /// </summary>
    public static string DefaultDownloadsFolderName => "Downloads";

    /// <summary>
    /// Gets the default folder name used for TV show libraries under the root media directory.
    /// </summary>
    public static string DefaultTvShowsFolderName => "TvShows";

    /// <summary>
    /// Gets the default folder name used for music libraries under the root media directory.
    /// </summary>
    public static string DefaultMusicFolderName => "Music";

    /// <summary>
    /// Gets the default folder name used for photo libraries under the root media directory.
    /// </summary>
    public static string DefaultPhotosFolderName => "Photos";

    /// <summary>
    /// Gets the default folder name used for uncategorized media under the root media directory.
    /// </summary>
    public static string DefaultOtherFolderName => "OtherVideos";

    /// <summary>
    /// Gets the default folder name used for game libraries under the root media directory.
    /// </summary>
    public static string DefaultGamesFolderName => "Games";

    /// <summary>
    /// Gets the default folder name used for the Config folder.
    /// </summary>
    public static string DefaultConfigFolderName => "Config";

    /// <summary>
    /// Gets the file name used for the main Reaparr settings file.
    /// </summary>
    string ConfigFileName { get; }

    /// <summary>
    /// Gets the file name used for the SQLite database.
    /// </summary>
    string DatabaseName { get; }

    /// <summary>
    /// Gets the file name used for the SQLite shared-memory sidecar file.
    /// </summary>
    string DatabaseShmName { get; }

    /// <summary>
    /// Gets the file name used for the SQLite write-ahead log sidecar file.
    /// </summary>
    string DatabaseWalName { get; }
}
