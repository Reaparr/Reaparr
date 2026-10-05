using Xdg.Directories;

namespace Reaparr.Environment;

public class PathProvider : IPathProvider
{
    private readonly IAppBuildInfo _appBuildInfo;
    private readonly IAppRuntimeInfo _appRuntimeInfo;

    public PathProvider()
    {
        _appBuildInfo = new AppBuildInfo();
        _appRuntimeInfo = new AppRuntimeInfo();
    }

    public PathProvider(IAppBuildInfo appBuildInfo, IAppRuntimeInfo appRuntimeInfo)
    {
        _appBuildInfo = appBuildInfo;
        _appRuntimeInfo = appRuntimeInfo;
    }

    #region Properties

    #region DirectoryNames

    public string DefaultLogsFolderName => "Logs";

    /// <inheritdoc/>
    public string DefaultConfigFolderName => "Config";

    /// <inheritdoc/>
    public string DefaultReaparrFolderName => "Reaparr";

    /// <inheritdoc/>
    public string DefaultMovieFolderName => "Movies";

    /// <inheritdoc/>
    public string DefaultDownloadsFolderName => "Downloads";

    /// <inheritdoc/>
    public string DefaultTvShowsFolderName => "TvShows";

    /// <inheritdoc/>
    public string DefaultMusicFolderName => "Music";

    /// <inheritdoc/>
    public string DefaultPhotosFolderName => "Photos";

    /// <inheritdoc/>
    public string DefaultOtherFolderName => "Other";

    /// <inheritdoc/>
    public string DefaultGamesFolderName => "Games";

    #region FileNames

    /// <inheritdoc/>
    public string ConfigFileName => "ReaparrSettings.json";

    /// <inheritdoc/>
    public string DatabaseName => "ReaparrDB.db";

    /// <inheritdoc/>
    public string DatabaseShmName => $"{DatabaseName}-shm";

    /// <inheritdoc/>
    public string DatabaseWalName => $"{DatabaseName}-wal";

    #endregion

    /// <inheritdoc/>
    public string DefaultDownloadsDestinationFolder =>
        GetDestinationFolder(_appRuntimeInfo.DownloadsPath, DefaultDownloadsFolderName, useDataPath: false);

    /// <inheritdoc/>
    public string DefaultMovieDestinationFolder =>
        GetDestinationFolder(_appRuntimeInfo.MoviesPath, DefaultMovieFolderName);

    /// <inheritdoc/>
    public string DefaultTvShowsDestinationFolder =>
        GetDestinationFolder(_appRuntimeInfo.TvShowsPath, DefaultTvShowsFolderName);

    /// <inheritdoc/>
    public string DefaultMusicDestinationFolder =>
        GetDestinationFolder(_appRuntimeInfo.MusicPath, DefaultMusicFolderName);

    /// <inheritdoc/>
    public string DefaultPhotosDestinationFolder =>
        GetDestinationFolder(_appRuntimeInfo.PhotosPath, DefaultPhotosFolderName);

    /// <inheritdoc/>
    public string DefaultOtherDestinationFolder =>
        GetDestinationFolder(_appRuntimeInfo.OtherPath, DefaultOtherFolderName);

    /// <inheritdoc/>
    public string DefaultGamesDestinationFolder =>
        GetDestinationFolder(_appRuntimeInfo.GamesPath, DefaultGamesFolderName);

    #endregion

    /// <inheritdoc/>
    public string ConfigDirectory
    {
        get
        {
            var configPath = _appRuntimeInfo.ConfigPath;
            if (configPath != null)
                return configPath;

            if (_appBuildInfo.IsDockerMode)
                return Path.Combine("/", DefaultConfigFolderName);

            if (_appBuildInfo.IsDesktopMode)
                return Path.Combine(BaseDirectory.ConfigHome, DefaultReaparrFolderName);

            throw new PlatformNotSupportedException($"Platform: {_appBuildInfo.CurrentOS} is not supported");
        }
    }

    /// <inheritdoc/>
    public string ConfigFileLocation => Path.Combine(ConfigDirectory, ConfigFileName);

    /// <inheritdoc/>
    public string DatabaseBackupDirectory => Path.Combine(ConfigDirectory, "Database BackUp");

    /// <inheritdoc/>
    public string DatabasePath => Path.Combine(ConfigDirectory, DatabaseName);

    /// <inheritdoc/>
    // ReSharper disable once InconsistentNaming
    public string Database_SHM_Path => Path.Combine(ConfigDirectory, DatabaseShmName);

    /// <inheritdoc/>
    // ReSharper disable once InconsistentNaming
    public string Database_WAL_Path => Path.Combine(ConfigDirectory, DatabaseWalName);

    /// <inheritdoc/>
    public string LogsDirectory => Path.Combine(ConfigDirectory, DefaultLogsFolderName);

    /// <inheritdoc/>
    public List<string> DatabaseFiles => [DatabasePath, Database_SHM_Path, Database_WAL_Path];

    /// <inheritdoc/>
    public string DataDirectory
    {
        get
        {
            var dataPath = _appRuntimeInfo.DataPath;
            if (dataPath is not null)
                return dataPath;

            if (_appBuildInfo.IsDockerMode)
                return "/";

            if (_appBuildInfo.IsDesktopMode)
                return Path.Combine(UserDirectory.DownloadDir, DefaultReaparrFolderName);

            throw new PlatformNotSupportedException($"Platform: {_appBuildInfo.CurrentOS} is not supported");
        }
    }

    #endregion

    private string GetDestinationFolder(string? overridePath, string folderName, bool useDataPath = true)
    {
        if (overridePath is not null)
            return overridePath;

        if (useDataPath)
        {
            var dataPath = _appRuntimeInfo.DataPath;
            if (dataPath is not null)
                return Path.Combine(dataPath, folderName);
        }

        if (_appBuildInfo.IsDockerMode)
            return Path.Combine("/", folderName);

        if (_appBuildInfo.IsDesktopMode)
            return Path.Combine(DataDirectory, folderName);

        throw new PlatformNotSupportedException($"Platform: {_appBuildInfo.CurrentOS} is not supported");
    }
}
