using Xdg.Directories;

namespace Reaparr.Environment.UnitTests;

public class PathProviderUnitTests : BaseUnitTest<PathProvider>
{
    [Test]
    public void ShouldExposeExpectedDefaultFolderAndFileNames()
    {
        // Arrange
        var sut = Sut;

        // Act
        var defaultConfigFolderName = sut.DefaultConfigFolderName;
        var defaultReaparrFolderName = sut.DefaultReaparrFolderName;
        var defaultMovieFolderName = sut.DefaultMovieFolderName;
        var defaultDownloadsFolderName = sut.DefaultDownloadsFolderName;
        var defaultTvShowsFolderName = sut.DefaultTvShowsFolderName;
        var defaultMusicFolderName = sut.DefaultMusicFolderName;
        var defaultPhotosFolderName = sut.DefaultPhotosFolderName;
        var defaultOtherFolderName = sut.DefaultOtherFolderName;
        var defaultGamesFolderName = sut.DefaultGamesFolderName;
        var configFileName = sut.ConfigFileName;
        var databaseName = sut.DatabaseName;
        var databaseShmName = sut.DatabaseShmName;
        var databaseWalName = sut.DatabaseWalName;

        // Assert
        defaultConfigFolderName.ShouldBe("Config");
        defaultReaparrFolderName.ShouldBe("Reaparr");
        defaultMovieFolderName.ShouldBe("Movies");
        defaultDownloadsFolderName.ShouldBe("Downloads");
        defaultTvShowsFolderName.ShouldBe("TvShows");
        defaultMusicFolderName.ShouldBe("Music");
        defaultPhotosFolderName.ShouldBe("Photos");
        defaultOtherFolderName.ShouldBe("Other");
        defaultGamesFolderName.ShouldBe("Games");
        configFileName.ShouldBe("ReaparrSettings.json");
        databaseName.ShouldBe("ReaparrDB.db");
        databaseShmName.ShouldBe("ReaparrDB.db-shm");
        databaseWalName.ShouldBe("ReaparrDB.db-wal");
    }

    [Test]
    public void ShouldBuildExpectedConfigDerivedPaths_WhenConfigDirectoryIsOverridden()
    {
        // Arrange
        const string configDirectory = "/custom/config";
        SetAppRuntimeInfo(x => x.ConfigPath = configDirectory);
        var sut = Sut;

        // Act
        var configFileLocation = sut.ConfigFileLocation;
        var databaseBackupDirectory = sut.DatabaseBackupDirectory;
        var databasePath = sut.DatabasePath;
        var databaseShmPath = sut.Database_SHM_Path;
        var databaseWalPath = sut.Database_WAL_Path;
        var logsDirectory = sut.LogsDirectory;
        var databaseFiles = sut.DatabaseFiles;

        // Assert
        configFileLocation.ShouldBe(Path.Combine(configDirectory, sut.ConfigFileName));
        databaseBackupDirectory.ShouldBe(Path.Combine(configDirectory, "Database BackUp"));
        databasePath.ShouldBe(Path.Combine(configDirectory, sut.DatabaseName));
        databaseShmPath.ShouldBe(Path.Combine(configDirectory, sut.DatabaseShmName));
        databaseWalPath.ShouldBe(Path.Combine(configDirectory, sut.DatabaseWalName));
        logsDirectory.ShouldBe(Path.Combine(configDirectory, "Logs"));
        databaseFiles.ShouldBe([databasePath, databaseShmPath, databaseWalPath]);
    }

    [Test]
    public void ShouldReturnConfiguredDataDirectory_WhenDataPathOverrideExists()
    {
        // Arrange
        const string dataDirectory = "/custom/data";
        SetAppRuntimeInfo(x => x.DataPath = dataDirectory);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");
        var sut = Sut;

        // Act
        var result = sut.DataDirectory;

        // Assert
        result.ShouldBe(dataDirectory);
    }

    [Test]
    public void ShouldReturnConfiguredConfigDirectory_WhenConfigPathOverrideExists()
    {
        // Arrange
        const string configDirectory = "/custom/config";
        SetAppRuntimeInfo(x => x.ConfigPath = configDirectory);
        SetAppBuildInfo(x => x.RuntimeMode = string.Empty);
        var sut = Sut;

        // Act
        var result = sut.ConfigDirectory;

        // Assert
        result.ShouldBe(configDirectory);
    }

    [Test]
    public void ShouldReturnConfiguredDownloadsPath_WhenDownloadsOverrideExists()
    {
        // Arrange
        const string downloadsDirectory = "/custom/downloads";
        SetAppRuntimeInfo(x =>
        {
            x.DataPath = "/custom/data";
            x.DownloadsPath = downloadsDirectory;
        });
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");
        var sut = Sut;

        // Act
        var result = sut.DefaultDownloadsDestinationFolder;

        // Assert
        result.ShouldBe(downloadsDirectory);
    }

    [Test]
    public void ShouldReturnDownloadsFolderInsideDataDirectory_WhenDownloadsOverrideIsMissing()
    {
        // Arrange
        const string dataDirectory = "/custom/data";
        SetAppRuntimeInfo(x => x.DataPath = dataDirectory);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");
        var sut = Sut;

        // Act
        var result = sut.DefaultDownloadsDestinationFolder;

        // Assert
        result.ShouldBe(Path.Combine(dataDirectory, sut.DefaultDownloadsFolderName));
    }

    [Test]
    [Arguments(EnvKeys.ReaparrMoviesPath, "/custom/media/movies", PlexMediaType.Movie)]
    [Arguments(EnvKeys.ReaparrTvShowsPath, "/custom/media/tvshows", PlexMediaType.TvShow)]
    [Arguments(EnvKeys.ReaparrMusicPath, "/custom/media/music", PlexMediaType.MusicArtist)]
    [Arguments(EnvKeys.ReaparrPhotosPath, "/custom/media/photos", PlexMediaType.PhotoImage)]
    [Arguments(EnvKeys.ReaparrOtherPath, "/custom/media/other", PlexMediaType.OtherVideos)]
    [Arguments(EnvKeys.ReaparrGamesPath, "/custom/media/games", PlexMediaType.Games)]
    public void ShouldReturnDedicatedMediaPath_WhenPerTypeOverrideExists(
        string environmentKey,
        string dedicatedPath,
        PlexMediaType mediaType
    )
    {
        // Arrange
        SetAppRuntimeInfo(x =>
        {
            x.DataPath = "/custom/data";
            switch (environmentKey)
            {
                case EnvKeys.ReaparrMoviesPath:
                    x.MoviesPath = dedicatedPath;
                    break;
                case EnvKeys.ReaparrTvShowsPath:
                    x.TvShowsPath = dedicatedPath;
                    break;
                case EnvKeys.ReaparrMusicPath:
                    x.MusicPath = dedicatedPath;
                    break;
                case EnvKeys.ReaparrPhotosPath:
                    x.PhotosPath = dedicatedPath;
                    break;
                case EnvKeys.ReaparrOtherPath:
                    x.OtherPath = dedicatedPath;
                    break;
                case EnvKeys.ReaparrGamesPath:
                    x.GamesPath = dedicatedPath;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(environmentKey), environmentKey, null);
            }
        });
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");
        var sut = Sut;

        // Act
        var result = GetMediaDestinationFolder(sut, mediaType);

        // Assert
        result.ShouldBe(dedicatedPath);
    }

    [Test]
    [Arguments(PlexMediaType.Movie, "Movies")]
    [Arguments(PlexMediaType.TvShow, "TvShows")]
    [Arguments(PlexMediaType.MusicArtist, "Music")]
    [Arguments(PlexMediaType.PhotoImage, "Photos")]
    [Arguments(PlexMediaType.OtherVideos, "Other")]
    [Arguments(PlexMediaType.Games, "Games")]
    public void ShouldReturnMediaFolderInsideDataDirectory_WhenPerTypeOverrideIsMissing(
        PlexMediaType mediaType,
        string expectedFolderName
    )
    {
        // Arrange
        const string dataDirectory = "/custom/data";
        SetAppRuntimeInfo(x => x.DataPath = dataDirectory);
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");
        var sut = Sut;

        // Act
        var result = GetMediaDestinationFolder(sut, mediaType);

        // Assert
        result.ShouldBe(Path.Combine(dataDirectory, expectedFolderName));
    }

    [Test]
    public void ShouldReturnDockerFallbackPaths_WhenNoOverridesExist()
    {
        // Arrange
        SetAppBuildInfo(x => x.RuntimeMode = "docker");
        var sut = Sut;

        // Act
        var configDirectory = sut.ConfigDirectory;
        var dataDirectory = sut.DataDirectory;
        var downloadsDirectory = sut.DefaultDownloadsDestinationFolder;
        var movieDirectory = sut.DefaultMovieDestinationFolder;
        var tvShowsDirectory = sut.DefaultTvShowsDestinationFolder;
        var musicDirectory = sut.DefaultMusicDestinationFolder;
        var photosDirectory = sut.DefaultPhotosDestinationFolder;
        var otherDirectory = sut.DefaultOtherDestinationFolder;
        var gamesDirectory = sut.DefaultGamesDestinationFolder;

        // Assert
        configDirectory.ShouldBe(Path.Combine("/", sut.DefaultConfigFolderName));
        dataDirectory.ShouldBe("/");
        downloadsDirectory.ShouldBe(Path.Combine("/", sut.DefaultDownloadsFolderName));
        movieDirectory.ShouldBe(Path.Combine("/", sut.DefaultMovieFolderName));
        tvShowsDirectory.ShouldBe(Path.Combine("/", sut.DefaultTvShowsFolderName));
        musicDirectory.ShouldBe(Path.Combine("/", sut.DefaultMusicFolderName));
        photosDirectory.ShouldBe(Path.Combine("/", sut.DefaultPhotosFolderName));
        otherDirectory.ShouldBe(Path.Combine("/", sut.DefaultOtherFolderName));
        gamesDirectory.ShouldBe(Path.Combine("/", sut.DefaultGamesFolderName));
    }

    [Test]
    public void ShouldReturnDesktopFallbackPaths_WhenNoOverridesExist()
    {
        // Arrange
        SetAppBuildInfo(x => x.RuntimeMode = "desktop");
        var sut = Sut;
        var expectedConfigDirectory = Path.Combine(BaseDirectory.ConfigHome, sut.DefaultReaparrFolderName);
        var expectedDataDirectory = Path.Combine(UserDirectory.DownloadDir, sut.DefaultReaparrFolderName);

        // Act
        var configDirectory = sut.ConfigDirectory;
        var dataDirectory = sut.DataDirectory;
        var downloadsDirectory = sut.DefaultDownloadsDestinationFolder;
        var movieDirectory = sut.DefaultMovieDestinationFolder;

        // Assert
        configDirectory.ShouldBe(expectedConfigDirectory);
        dataDirectory.ShouldBe(expectedDataDirectory);
        downloadsDirectory.ShouldBe(Path.Combine(expectedDataDirectory, sut.DefaultDownloadsFolderName));
        movieDirectory.ShouldBe(Path.Combine(expectedDataDirectory, sut.DefaultMovieFolderName));
    }

    [Test]
    public void ShouldUseConfigAndDataOverrides_EvenWhenRuntimeModeIsUnsupported()
    {
        // Arrange
        const string configDirectory = "/custom/config";
        const string dataDirectory = "/custom/data";
        SetAppRuntimeInfo(x =>
        {
            x.ConfigPath = configDirectory;
            x.DataPath = dataDirectory;
        });
        SetAppBuildInfo(x =>
        {
            x.RuntimeMode = string.Empty;
            x.CurrentOS = OperatingSystemPlatform.Unknown;
        });
        var sut = Sut;

        // Act
        var resolvedConfigDirectory = sut.ConfigDirectory;
        var resolvedDataDirectory = sut.DataDirectory;
        var resolvedMovieDirectory = sut.DefaultMovieDestinationFolder;

        // Assert
        resolvedConfigDirectory.ShouldBe(configDirectory);
        resolvedDataDirectory.ShouldBe(dataDirectory);
        resolvedMovieDirectory.ShouldBe(Path.Combine(dataDirectory, sut.DefaultMovieFolderName));
    }

    [Test]
    [Arguments("/custom/destination", null)]
    [Arguments("", null)]
    [Arguments("/custom/destination", "/custom/data")]
    [Arguments("", "/custom/data")]
    public void ShouldUseDestinationOverrides_WhenRuntimeModeIsUnsupported(string destination, string? dataPath)
    {
        // Arrange
        SetAppRuntimeInfo(x =>
        {
            x.DataPath = dataPath;
            x.DownloadsPath = destination;
            x.MoviesPath = destination;
            x.TvShowsPath = destination;
            x.MusicPath = destination;
            x.PhotosPath = destination;
            x.OtherPath = destination;
            x.GamesPath = destination;
        });
        SetAppBuildInfo(x =>
        {
            x.RuntimeMode = string.Empty;
            x.CurrentOS = OperatingSystemPlatform.Unknown;
        });
        var sut = Sut;

        // Act
        string[] destinations =
        [
            sut.DefaultDownloadsDestinationFolder,
            sut.DefaultMovieDestinationFolder,
            sut.DefaultTvShowsDestinationFolder,
            sut.DefaultMusicDestinationFolder,
            sut.DefaultPhotosDestinationFolder,
            sut.DefaultOtherDestinationFolder,
            sut.DefaultGamesDestinationFolder,
        ];

        // Assert
        destinations.ShouldBe([destination, destination, destination, destination, destination, destination, destination]);
    }

    [Test]
    [Arguments("/custom/data")]
    [Arguments("")]
    public void ShouldUseDataPathOnlyForMediaDestinations_WhenDockerDataPathOverrideExists(string dataDirectory)
    {
        // Arrange
        SetAppRuntimeInfo(x => x.DataPath = dataDirectory);
        SetAppBuildInfo(x => x.RuntimeMode = "docker");
        var sut = Sut;

        // Act
        var downloadsDirectory = sut.DefaultDownloadsDestinationFolder;
        var movieDirectory = sut.DefaultMovieDestinationFolder;
        var tvShowsDirectory = sut.DefaultTvShowsDestinationFolder;
        var musicDirectory = sut.DefaultMusicDestinationFolder;
        var photosDirectory = sut.DefaultPhotosDestinationFolder;
        var otherDirectory = sut.DefaultOtherDestinationFolder;
        var gamesDirectory = sut.DefaultGamesDestinationFolder;

        // Assert
        downloadsDirectory.ShouldBe(Path.Combine("/", "Downloads"));
        movieDirectory.ShouldBe(Path.Combine(dataDirectory, "Movies"));
        tvShowsDirectory.ShouldBe(Path.Combine(dataDirectory, "TvShows"));
        musicDirectory.ShouldBe(Path.Combine(dataDirectory, "Music"));
        photosDirectory.ShouldBe(Path.Combine(dataDirectory, "Photos"));
        otherDirectory.ShouldBe(Path.Combine(dataDirectory, "Other"));
        gamesDirectory.ShouldBe(Path.Combine(dataDirectory, "Games"));
    }

    private static string GetMediaDestinationFolder(PathProvider sut, PlexMediaType mediaType) =>
        mediaType switch
        {
            PlexMediaType.Movie => sut.DefaultMovieDestinationFolder,
            PlexMediaType.TvShow => sut.DefaultTvShowsDestinationFolder,
            PlexMediaType.MusicArtist => sut.DefaultMusicDestinationFolder,
            PlexMediaType.PhotoImage => sut.DefaultPhotosDestinationFolder,
            PlexMediaType.OtherVideos => sut.DefaultOtherDestinationFolder,
            PlexMediaType.Games => sut.DefaultGamesDestinationFolder,
            _ => throw new ArgumentOutOfRangeException(nameof(mediaType), mediaType, null),
        };
}
