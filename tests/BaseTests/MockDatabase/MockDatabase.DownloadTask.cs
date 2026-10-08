namespace Reaparr.BaseTests;

public static partial class MockDatabase
{
    /// <summary>
    /// Rewrites seeded movie download-task child paths so integration tests use the current
    /// per-database sandbox instead of any generic/default fake-data paths.
    /// </summary>
    private static void ApplyIntegrationTestPaths(
        IEnumerable<DownloadTaskMovie> downloadTasks,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo
    )
    {
        // Unit tests do not need sandbox rewriting. They usually assert on entities or behavior only,
        // while integration tests boot the full AppHost and exercise real file-system flows.
        if (!appRuntimeInfo.IsIntegrationTestMode)
            return;

        foreach (var downloadTask in downloadTasks)
        foreach (var child in downloadTask.Children)
        {
            // Each integration test gets its own filesystem sandbox. The seeded download tasks must point
            // at that sandbox so download, move, and cleanup jobs all operate on the active test paths.
            child.DirectoryMeta.DownloadRootPath = pathProvider.DefaultDownloadsDestinationFolder;
            child.DirectoryMeta.DestinationRootPath = child.MediaType switch
            {
                // Keep destination roots aligned with the same media-specific defaults used by the test
                // path provider so seeded tasks match the runtime file-placement rules.
                PlexMediaType.Movie => pathProvider.DefaultMovieDestinationFolder,
                PlexMediaType.TvShow or PlexMediaType.Season or PlexMediaType.Episode =>
                    pathProvider.DefaultTvShowsDestinationFolder,
                PlexMediaType.MusicArtist or PlexMediaType.MusicAlbum or PlexMediaType.MusicTrack =>
                    pathProvider.DefaultMusicDestinationFolder,
                PlexMediaType.PhotoImage => pathProvider.DefaultPhotosDestinationFolder,
                PlexMediaType.OtherVideos => pathProvider.DefaultOtherDestinationFolder,
                PlexMediaType.Games => pathProvider.DefaultGamesDestinationFolder,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(child.MediaType),
                    child.MediaType,
                    $"Unsupported PlexMediaType '{child.MediaType}' while mapping seeded movie download task destination root path."
                ),
            };
        }
    }

    /// <summary>
    /// Rewrites seeded TV-show episode-file paths so integration tests resolve downloads and moves
    /// inside the current sandboxed TV destination tree.
    /// </summary>
    private static void ApplyIntegrationTestPaths(
        IEnumerable<DownloadTaskTvShow> downloadTasks,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo
    )
    {
        if (!appRuntimeInfo.IsIntegrationTestMode)
            return;

        foreach (var downloadTask in downloadTasks)
        foreach (var season in downloadTask.Children)
        foreach (var episode in season.Children)
        foreach (var child in episode.Children)
        {
            // TV download task seeds are nested root -> season -> episode -> file, so only the leaf file
            // tasks need their directory metadata rewritten for integration-test filesystem operations.
            child.DirectoryMeta.DownloadRootPath = pathProvider.DefaultDownloadsDestinationFolder;
            child.DirectoryMeta.DestinationRootPath = pathProvider.DefaultTvShowsDestinationFolder;
        }
    }

    private static void ApplyIntegrationTestPaths(
        IEnumerable<DownloadTaskMusicArtist> downloadTasks,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo
    )
    {
        if (!appRuntimeInfo.IsIntegrationTestMode)
            return;

        foreach (var downloadTask in downloadTasks)
        foreach (var album in downloadTask.Children)
        foreach (var track in album.Children)
        foreach (var child in track.Children)
        {
            child.DirectoryMeta.DownloadRootPath = pathProvider.DefaultDownloadsDestinationFolder;
            child.DirectoryMeta.DestinationRootPath = pathProvider.DefaultMusicDestinationFolder;
        }
    }

    private static void ApplyIntegrationTestPaths(
        IEnumerable<DownloadTaskPhotoAlbum> downloadTasks,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo
    )
    {
        if (!appRuntimeInfo.IsIntegrationTestMode)
            return;

        foreach (var downloadTask in downloadTasks)
        foreach (var image in downloadTask.Children)
        foreach (var child in image.Children)
        {
            child.DirectoryMeta.DownloadRootPath = pathProvider.DefaultDownloadsDestinationFolder;
            child.DirectoryMeta.DestinationRootPath = pathProvider.DefaultPhotosDestinationFolder;
        }
    }

    private static void ApplyIntegrationTestPaths(
        IEnumerable<DownloadTaskOtherVideo> downloadTasks,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo
    )
    {
        if (!appRuntimeInfo.IsIntegrationTestMode)
            return;

        foreach (var downloadTask in downloadTasks)
        foreach (var child in downloadTask.Children)
        {
            child.DirectoryMeta.DownloadRootPath = pathProvider.DefaultDownloadsDestinationFolder;
            child.DirectoryMeta.DestinationRootPath = pathProvider.DefaultOtherDestinationFolder;
        }
    }

    private static async Task<ReaparrDbContext> AddDownloadTaskMovies(
        this ReaparrDbContext context,
        Seed seed,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        var downloadTasks = FakeData.GetMovieDownloadTask(seed, options).Generate(config.MovieDownloadTasksCount);

        var plexLibrary = context.PlexLibraries.FirstOrDefault(x => x.Type == PlexMediaType.Movie);
        plexLibrary.ShouldNotBeNull(
            "No PlexLibrary available with type Movie, consider setting config.DisableForeignKeyCheck = true"
        );

        var plexServer = context.PlexServers.IncludeConnections().FirstOrDefault(x => x.Id == plexLibrary.PlexServerId);
        plexServer.ShouldNotBeNull();

        downloadTasks.SetRelationshipIds(plexLibrary.PlexServerId, plexLibrary.Id);

        // Normalize seeded file paths for integration tests after relationship IDs are assigned, so the
        // generated DirectoryMeta values line up with the current test database sandbox.
        ApplyIntegrationTestPaths(downloadTasks, pathProvider, appRuntimeInfo);

        context.DownloadTaskMovie.AddRange(downloadTasks);
        await context.SaveChangesAsync();

        _log.Here()
            .Debug(
                "Added {MovieDownloadTasksCount} Movie {NameOfDownloadTask}s to ReaparrDbContext: {DatabaseName}",
                config.MovieDownloadTasksCount,
                nameof(DownloadTaskMovie),
                context.DatabaseName
            );

        return context;
    }

    private static async Task<ReaparrDbContext> AddDownloadTaskTvShows(
        this ReaparrDbContext context,
        Seed seed,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        var downloadTasks = FakeData.GetDownloadTaskTvShow(seed, options).Generate(config.TvShowDownloadTasksCount);

        var plexLibrary = context.PlexLibraries.FirstOrDefault(x => x.Type == PlexMediaType.TvShow);
        plexLibrary.ShouldNotBeNull(
            "No PlexLibrary available with type TvShow, consider setting config.DisableForeignKeyCheck = true"
        );

        var plexServer = context.PlexServers.IncludeConnections().FirstOrDefault(x => x.Id == plexLibrary.PlexServerId);
        plexServer.ShouldNotBeNull();

        downloadTasks.SetRelationshipIds(plexLibrary.PlexServerId, plexLibrary.Id);

        // Normalize seeded nested episode-file paths for integration tests after relationship IDs are
        // assigned, so runtime jobs read/write within the current test sandbox.
        ApplyIntegrationTestPaths(downloadTasks, pathProvider, appRuntimeInfo);

        context.DownloadTaskTvShow.AddRange(downloadTasks);
        await context.SaveChangesAsync();

        _log.Here()
            .Debug(
                "Added {TvShowDownloadTasksCount} TvShow {NameOfDownloadTask}s to ReaparrDbContext: {DatabaseName}",
                config.TvShowDownloadTasksCount,
                nameof(DownloadTaskTvShow),
                context.DatabaseName
            );

        return context;
    }

    private static async Task<ReaparrDbContext> AddDownloadTaskMusicArtists(
        this ReaparrDbContext context,
        Seed seed,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        var downloadTasks = FakeData
            .GetDownloadTaskMusicArtist(seed, options)
            .Generate(config.MusicArtistDownloadTasksCount);

        var plexLibrary = context.PlexLibraries.FirstOrDefault(x => x.Type == PlexMediaType.MusicArtist);
        plexLibrary.ShouldNotBeNull(
            "No PlexLibrary available with type MusicArtist, consider setting config.DisableForeignKeyCheck = true"
        );

        var plexServer = context.PlexServers.IncludeConnections().FirstOrDefault(x => x.Id == plexLibrary.PlexServerId);
        plexServer.ShouldNotBeNull();

        downloadTasks.SetRelationshipIds(plexLibrary.PlexServerId, plexLibrary.Id);
        ApplyIntegrationTestPaths(downloadTasks, pathProvider, appRuntimeInfo);

        context.DownloadTaskMusicArtists.AddRange(downloadTasks);
        await context.SaveChangesAsync();

        _log.Here()
            .Debug(
                "Added {MusicArtistDownloadTasksCount} MusicArtist {NameOfDownloadTask}s to ReaparrDbContext: {DatabaseName}",
                config.MusicArtistDownloadTasksCount,
                nameof(DownloadTaskMusicArtist),
                context.DatabaseName
            );

        return context;
    }

    private static async Task<ReaparrDbContext> AddDownloadTaskPhotoAlbums(
        this ReaparrDbContext context,
        Seed seed,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        var downloadTasks = FakeData
            .GetDownloadTaskPhotoAlbum(seed, options)
            .Generate(config.PhotoAlbumDownloadTasksCount);

        var plexLibrary = context.PlexLibraries.FirstOrDefault(x => x.Type == PlexMediaType.PhotoAlbum);
        plexLibrary.ShouldNotBeNull(
            "No PlexLibrary available with type PhotoAlbum, consider setting config.DisableForeignKeyCheck = true"
        );

        var plexServer = context.PlexServers.IncludeConnections().FirstOrDefault(x => x.Id == plexLibrary.PlexServerId);
        plexServer.ShouldNotBeNull();

        downloadTasks.SetRelationshipIds(plexLibrary.PlexServerId, plexLibrary.Id);
        ApplyIntegrationTestPaths(downloadTasks, pathProvider, appRuntimeInfo);

        context.DownloadTaskPhotoAlbums.AddRange(downloadTasks);
        await context.SaveChangesAsync();

        _log.Here()
            .Debug(
                "Added {PhotoAlbumDownloadTasksCount} PhotoAlbum {NameOfDownloadTask}s to ReaparrDbContext: {DatabaseName}",
                config.PhotoAlbumDownloadTasksCount,
                nameof(DownloadTaskPhotoAlbum),
                context.DatabaseName
            );

        return context;
    }

    private static async Task<ReaparrDbContext> AddDownloadTaskOtherVideos(
        this ReaparrDbContext context,
        Seed seed,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        var downloadTasks = FakeData
            .GetDownloadTaskOtherVideo(seed, options)
            .Generate(config.OtherVideoDownloadTasksCount);

        var plexLibrary = context.PlexLibraries.FirstOrDefault(x => x.Type == PlexMediaType.OtherVideos);
        plexLibrary.ShouldNotBeNull(
            "No PlexLibrary available with type OtherVideos, consider setting config.DisableForeignKeyCheck = true"
        );

        var plexServer = context.PlexServers.IncludeConnections().FirstOrDefault(x => x.Id == plexLibrary.PlexServerId);
        plexServer.ShouldNotBeNull();

        downloadTasks.SetRelationshipIds(plexLibrary.PlexServerId, plexLibrary.Id);
        ApplyIntegrationTestPaths(downloadTasks, pathProvider, appRuntimeInfo);

        context.DownloadTaskOtherVideos.AddRange(downloadTasks);
        await context.SaveChangesAsync();

        _log.Here()
            .Debug(
                "Added {OtherVideoDownloadTasksCount} OtherVideo {NameOfDownloadTask}s to ReaparrDbContext: {DatabaseName}",
                config.OtherVideoDownloadTasksCount,
                nameof(DownloadTaskOtherVideo),
                context.DatabaseName
            );

        return context;
    }

}
