namespace Reaparr.BaseTests;

public class MockPathProvider : IPathProvider
{
    private readonly string _sandboxFolder;
    private readonly IPathProvider _pathProvider;

    public MockPathProvider(
        string memoryDbName,
        IAppBuildInfo? appBuildInfo = null,
        IAppRuntimeInfo? appRuntimeInfo = null
    )
    {
        appBuildInfo ??= new MockAppBuildInfo();
        appRuntimeInfo ??= new MockAppRuntimeInfo();

        _pathProvider = new PathProvider(appBuildInfo, appRuntimeInfo);
        _sandboxFolder = Path.GetFullPath(IntegrationTestFileSystemSandbox.GetSandboxFolder(memoryDbName));
    }

    /// <inheritdoc/>
    public string ConfigFileName => _pathProvider.ConfigFileName;

    /// <inheritdoc/>
    public string DatabaseName => _pathProvider.DatabaseName;

    /// <inheritdoc/>
    public string DatabaseShmName => _pathProvider.DatabaseShmName;

    /// <inheritdoc/>
    public string DatabaseWalName => _pathProvider.DatabaseWalName;

    /// <inheritdoc/>
    public string DefaultDownloadsDestinationFolder =>
        Path.Combine(_sandboxFolder, IPathProvider.DefaultDownloadsFolderName);

    /// <inheritdoc/>
    public string DefaultMovieDestinationFolder => Path.Combine(_sandboxFolder, IPathProvider.DefaultMovieFolderName);

    /// <inheritdoc/>
    public string DefaultTvShowsDestinationFolder =>
        Path.Combine(_sandboxFolder, IPathProvider.DefaultTvShowsFolderName);

    /// <inheritdoc/>
    public string DefaultMusicDestinationFolder => Path.Combine(_sandboxFolder, IPathProvider.DefaultMusicFolderName);

    /// <inheritdoc/>
    public string DefaultPhotosDestinationFolder =>
        Path.Combine(_sandboxFolder, IPathProvider.DefaultPhotosFolderName);

    /// <inheritdoc/>
    public string DefaultOtherDestinationFolder => Path.Combine(_sandboxFolder, IPathProvider.DefaultOtherFolderName);

    /// <inheritdoc/>
    public string DefaultGamesDestinationFolder => Path.Combine(_sandboxFolder, IPathProvider.DefaultGamesFolderName);

    /// <inheritdoc/>
    public string ConfigDirectory => Path.Combine(_sandboxFolder, IPathProvider.DefaultConfigFolderName);

    /// <inheritdoc/>
    public string ConfigFileLocation => Path.Combine(ConfigDirectory, ConfigFileName);

    /// <inheritdoc/>
    public string DatabaseBackupDirectory => Path.Combine(ConfigDirectory, "Database BackUp");

    /// <inheritdoc/>
    public string DatabasePath => Path.Combine(ConfigDirectory, DatabaseName);

    /// <inheritdoc/>
    public string Database_SHM_Path => Path.Combine(ConfigDirectory, DatabaseShmName);

    /// <inheritdoc/>
    public string Database_WAL_Path => Path.Combine(ConfigDirectory, DatabaseWalName);

    /// <inheritdoc/>
    public string LogsDirectory => Path.Combine(ConfigDirectory, IPathProvider.DefaultLogsFolderName);

    /// <inheritdoc/>
    public List<string> DatabaseFiles => [DatabasePath, Database_SHM_Path, Database_WAL_Path];

    /// <inheritdoc/>
    public string DataDirectory => _sandboxFolder;
}
