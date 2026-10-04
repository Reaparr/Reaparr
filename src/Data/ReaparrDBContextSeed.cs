namespace Reaparr.Data;

// ReSharper disable once InconsistentNaming
public static class ReaparrDBContextSeed
{
    private static readonly ILogger _log = Log.ForContext(typeof(ReaparrDBContextSeed));

    private static List<FolderPath> GetDefaultFolderPaths(IPathProvider pathProvider) =>
        [
            new()
            {
                Id = FolderTypeDefaults.DefaultDownloadFolderId,
                DisplayName = "Download Path",
                DirectoryPath = pathProvider.DefaultDownloadsDestinationFolder,
                FolderType = FolderType.DownloadFolder,
                MediaType = PlexMediaType.None,
            },
            new()
            {
                Id = PlexMediaType.Movie.ToDefaultDestinationFolderId(),
                DisplayName = "Movie Destination Path",
                DirectoryPath = pathProvider.DefaultMovieDestinationFolder,
                FolderType = FolderType.MovieFolder,
                MediaType = PlexMediaType.Movie,
            },
            new()
            {
                Id = PlexMediaType.TvShow.ToDefaultDestinationFolderId(),
                DisplayName = "Tv Show Destination Path",
                DirectoryPath = pathProvider.DefaultTvShowsDestinationFolder,
                FolderType = FolderType.TvShowFolder,
                MediaType = PlexMediaType.TvShow,
            },
            new()
            {
                Id = PlexMediaType.MusicArtist.ToDefaultDestinationFolderId(),
                DisplayName = "Music Destination Path",
                DirectoryPath = pathProvider.DefaultMusicDestinationFolder,
                FolderType = FolderType.MusicFolder,
                MediaType = PlexMediaType.MusicArtist,
            },
            new()
            {
                Id = PlexMediaType.PhotoAlbum.ToDefaultDestinationFolderId(),
                DisplayName = "Photos Destination Path",
                DirectoryPath = pathProvider.DefaultPhotosDestinationFolder,
                FolderType = FolderType.PhotosFolder,
                MediaType = PlexMediaType.PhotoAlbum,
            },
            new()
            {
                Id = PlexMediaType.OtherVideos.ToDefaultDestinationFolderId(),
                DisplayName = "Other Videos Destination Path",
                DirectoryPath = pathProvider.DefaultOtherDestinationFolder,
                FolderType = FolderType.OtherVideosFolder,
                MediaType = PlexMediaType.OtherVideos,
            },
            new()
            {
                Id = PlexMediaType.Games.ToDefaultDestinationFolderId(),
                DisplayName = "Games Videos Destination Path",
                DirectoryPath = pathProvider.DefaultGamesDestinationFolder,
                FolderType = FolderType.GamesVideosFolder,
                MediaType = PlexMediaType.Games,
            },
            new()
            {
                Id = 8,
                DisplayName = "Reserved #1 Destination Path",
                DirectoryPath = pathProvider.DataDirectory,
                FolderType = FolderType.None,
                MediaType = PlexMediaType.None,
            },
            new()
            {
                Id = 9,
                DisplayName = "Reserved #2 Destination Path",
                DirectoryPath = pathProvider.DataDirectory,
                FolderType = FolderType.None,
                MediaType = PlexMediaType.None,
            },
            new()
            {
                Id = 10,
                DisplayName = "Reserved #3 Destination Path",
                DirectoryPath = pathProvider.DataDirectory,
                FolderType = FolderType.None,
                MediaType = PlexMediaType.None,
            },
        ];

    public static Action<DbContext, bool> Seed(IPathProvider pathProvider) =>
        (context, _) =>
        {
            if (context is not ReaparrDbContext db)
                return;

            var existingFolderPathIds = db.FolderPaths.Select(x => x.Id).ToHashSet();

            foreach (var path in GetDefaultFolderPaths(pathProvider).Where(x => !existingFolderPathIds.Contains(x.Id)))
            {
                _log.Here()
                    .Debug(
                        "Seeding default folder path with id {Id} and directory {DirectoryPath}",
                        path.Id,
                        path.DirectoryPath
                    );
                db.FolderPaths.Add(path);
            }

            db.SaveChanges();
        };

    public static Func<DbContext, bool, CancellationToken, Task> SeedAsync(IPathProvider pathProvider) =>
        async (context, _, cancellationToken) =>
        {
            if (context is not ReaparrDbContext db)
                return;

            var existingFolderPathIds = await db.FolderPaths.Select(x => x.Id).ToHashSetAsync(cancellationToken);

            foreach (var path in GetDefaultFolderPaths(pathProvider).Where(x => !existingFolderPathIds.Contains(x.Id)))
            {
                _log.Here()
                    .Debug(
                        "Seeding default folder path with id {Id} and directory {DirectoryPath}",
                        path.Id,
                        path.DirectoryPath
                    );
                await db.FolderPaths.AddAsync(path, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
        };
}
