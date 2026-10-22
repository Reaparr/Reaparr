using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Reaparr.Data.UnitTests;

public class SyncedMediaMetadataMigrationUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldPreserveAncestorValues_WhenUpgradingShowWithoutEpisodes()
    {
        // Arrange
        await SetupDatabase(59322, config =>
        {
            config.PlexServerCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 0;
        });
        using var dbContext = IDbContext;
        var database = ((DbContext)dbContext).Database;
        var migrator = database.GetService<IMigrator>();
        var show = await dbContext.PlexTvShows.Select(x => new { x.Id, x.PlexLibraryId })
            .SingleAsync(CancellationToken);
        var seasonId = await dbContext.PlexTvShowSeason.Select(x => x.Id).SingleAsync(CancellationToken);
        (await dbContext.PlexTvShowEpisodes.ToListAsync(CancellationToken)).ShouldBeEmpty();
        await dbContext.PlexTvShows.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.VideoQuality, VideoQuality.UHD_4K)
                .SetProperty(x => x.MediaSize, 100L).SetProperty(x => x.Duration, 60)
                .SetProperty(x => x.ChildCount, 4).SetProperty(x => x.GrandChildCount, 5), CancellationToken);
        await dbContext.PlexTvShowSeason.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.MediaSize, 100L).SetProperty(x => x.Duration, 60)
                .SetProperty(x => x.ChildCount, 5), CancellationToken);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.MediaSize, 100L), CancellationToken);
        dbContext.PlexTvShowMediaQualities.Add(new PlexTvShowMediaQuality
        {
            PlexTvShowId = show.Id, PlexLibraryId = show.PlexLibraryId, VideoQuality = VideoQuality.UHD_4K,
        });
        dbContext.PlexTvShowSeasonMediaQualities.Add(new PlexTvShowSeasonMediaQuality
        {
            PlexTvShowSeasonId = seasonId, PlexLibraryId = show.PlexLibraryId, VideoQuality = VideoQuality.UHD_4K,
        });
        await dbContext.SaveChangesAsync(CancellationToken);
        (await dbContext.PlexTvShows.Select(x => x.VideoQuality).SingleAsync(CancellationToken))
            .ShouldBe(VideoQuality.UHD_4K);
        (await dbContext.PlexTvShowMediaQualities.Select(x => x.PlexTvShowId)
            .ToListAsync(CancellationToken)).ShouldBe([show.Id]);
        (await dbContext.PlexTvShowSeasonMediaQualities.Select(x => x.PlexTvShowSeasonId)
            .ToListAsync(CancellationToken)).ShouldBe([seasonId]);
        await migrator.MigrateAsync("20260917113405_AddImprovedIndexerSearchConfig", CancellationToken);
        (await database.SqlQueryRaw<int>("SELECT Quality AS Value FROM PlexTvShows")
            .SingleAsync(CancellationToken)).ShouldBe((int)VideoQuality.UHD_4K);

        // Act
        await migrator.MigrateAsync(cancellationToken: CancellationToken);
        dbContext.ClearChangeTracker();

        // Assert
        (await dbContext.PlexTvShows
            .Select(x => new { x.Id, x.VideoQuality, x.MediaSize, x.Duration, x.ChildCount, x.GrandChildCount })
            .SingleAsync(CancellationToken)).ShouldBe(new
            {
                show.Id, VideoQuality = VideoQuality.UHD_4K, MediaSize = 100L,
                Duration = 60, ChildCount = 4, GrandChildCount = 5,
            });
        (await dbContext.PlexTvShowSeason
            .Select(x => new { x.Id, x.MediaSize, x.Duration, x.ChildCount })
            .SingleAsync(CancellationToken)).ShouldBe(new
            {
                Id = seasonId, MediaSize = 100L, Duration = 60, ChildCount = 5,
            });
        (await dbContext.PlexTvShowMediaQualities.Select(x => x.PlexTvShowId)
            .ToListAsync(CancellationToken)).ShouldBe([show.Id]);
        (await dbContext.PlexTvShowSeasonMediaQualities.Select(x => x.PlexTvShowSeasonId)
            .ToListAsync(CancellationToken)).ShouldBe([seasonId]);
        (await dbContext.PlexLibraries.Select(x => x.MediaSize).SingleAsync(CancellationToken)).ShouldBe(100L);
    }

    [Test]
    public async Task ShouldCreateCanonicalFileSchema_WhenMigratingFreshDatabase()
    {
        // Arrange
        await SetupDatabase(59321);
        using var dbContext = IDbContext;
        var database = ((DbContext)dbContext).Database;
        await database.OpenConnectionAsync(CancellationToken);
        await using var command = database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM (
                SELECT name FROM pragma_table_info('PlexMovieData') WHERE name = 'VideoResolution'
                UNION ALL SELECT name FROM pragma_table_info('PlexTvShowEpisodeData') WHERE name = 'VideoResolution'
                UNION ALL SELECT name FROM pragma_table_info('PlexPhotoMediaData')
                    WHERE name IN ('VideoResolution', 'PlexApiMetaDataKey')
                UNION ALL SELECT name FROM pragma_table_info('PlexPhotoAlbums')
                    WHERE name = 'PlexApiMetaDataKey'
                UNION ALL SELECT name FROM pragma_table_info('PlexMusicArtists')
                    WHERE name IN ('Year', 'ContentRating', 'Rating', 'HasTheme', 'OriginallyAvailableAt', 'FullTitle')
                UNION ALL SELECT name FROM pragma_table_info('PlexMusicAlbums')
                    WHERE name IN ('ContentRating', 'HasTheme', 'DiscCount', 'TrackCount', 'ReleaseDate', 'FullTitle')
                UNION ALL SELECT name FROM pragma_table_info('PlexMusicTracks')
                    WHERE name IN ('Year', 'Summary', 'ContentRating', 'Rating', 'HasTheme', 'OriginallyAvailableAt', 'FullTitle')
                UNION ALL SELECT name FROM pragma_table_info('PlexOtherVideos')
                    WHERE name IN ('Studio', 'ContentRating', 'Rating', 'HasTheme', 'Guid_IMDB', 'Guid_TMDB', 'Guid_TVDB', 'FullTitle')
                UNION ALL SELECT name FROM pragma_table_info('PlexOtherVideoData')
                    WHERE name IN ('VideoResolution', 'OriginalFilePath', 'SourceRelativePath',
                        'GeneratedFilename', 'GeneratedNameSyncedAt', 'NeedsGeneratedName',
                        'Width', 'Height', 'VideoProfile', 'VideoFrameRate')
                UNION ALL SELECT name FROM pragma_table_info('PlexMusicTrackData')
                    WHERE name IN ('OriginalFilePath', 'SourceRelativePath',
                        'GeneratedFilename', 'GeneratedNameSyncedAt', 'NeedsGeneratedName')
                UNION ALL SELECT name FROM sqlite_schema WHERE type = 'index' AND name IN (
                    'IX_PlexTvShowSeason_TvShowId',
                    'IX_PlexMusicArtistComparisons_RemotePlexLibraryId_OwnedPlexLibraryId',
                    'IX_PlexMusicAlbumComparisons_RemotePlexLibraryId_OwnedPlexLibraryId',
                    'IX_PlexMusicTrackComparisons_RemotePlexLibraryId_OwnedPlexLibraryId'
                )
            );
            """;

        // Act
        var obsoleteSchemaObjects = await command.ExecuteScalarAsync(CancellationToken);

        // Assert
        Convert.ToInt64(obsoleteSchemaObjects).ShouldBe(0);
        (await database.GetAppliedMigrationsAsync(CancellationToken)).Last()
            .ShouldBe("20261021234328_AddMusicPhotosAndOtherVideos");
    }

    [Test]
    public async Task ShouldCreateSingleFilePhotoSchema_WhenMigratingFreshDatabase()
    {
        // Arrange
        await SetupDatabase(59323);
        using var dbContext = IDbContext;
        var database = ((DbContext)dbContext).Database;
        await database.OpenConnectionAsync(CancellationToken);
        await using var command = database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT name FROM sqlite_schema
            WHERE type = 'table' AND (name LIKE 'PlexPhoto%' OR name LIKE 'DownloadTaskPhoto%')
            ORDER BY name;
            """;

        // Act
        var tables = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(CancellationToken))
        {
            while (await reader.ReadAsync(CancellationToken))
                tables.Add(reader.GetString(0));
        }
        command.CommandText = """
            SELECT 'catalog-folder:' || on_delete
                FROM pragma_foreign_key_list('PlexPhotoMediaData') WHERE "from" = 'PlexPhotoFolderId'
            UNION ALL SELECT 'membership-album:' || on_delete
                FROM pragma_foreign_key_list('PlexPhotoAlbumMemberships') WHERE "from" = 'PlexPhotoAlbumId'
            UNION ALL SELECT 'membership-file:' || on_delete
                FROM pragma_foreign_key_list('PlexPhotoAlbumMemberships') WHERE "from" = 'PlexPhotoMediaDataId'
            UNION ALL SELECT 'queue-folder:' || on_delete
                FROM pragma_foreign_key_list('DownloadTaskPhotoImageFiles') WHERE "from" = 'ParentId'
            UNION ALL SELECT 'queue-log:' || on_delete
                FROM pragma_foreign_key_list('DownloadTaskPhotoImageFileLogs') WHERE "from" = 'DownloadTaskFileId'
            ORDER BY 1;
            """;
        var deletionRules = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(CancellationToken))
        {
            while (await reader.ReadAsync(CancellationToken))
                deletionRules.Add(reader.GetString(0));
        }

        // Assert
        tables.ShouldBe([
            "DownloadTaskPhotoFolders",
            "DownloadTaskPhotoImageFileLogs",
            "DownloadTaskPhotoImageFiles",
            "PlexPhotoAlbumMemberships",
            "PlexPhotoAlbums",
            "PlexPhotoFolders",
            "PlexPhotoMediaData",
        ]);
        deletionRules.ShouldBe([
            "catalog-folder:SET NULL",
            "membership-album:CASCADE",
            "membership-file:CASCADE",
            "queue-folder:CASCADE",
            "queue-log:CASCADE",
        ]);
    }

    [Test]
    public async Task ShouldPreserveVideoGenresAndMemberships_WhenUpgradingReleasedSchema()
    {
        // Arrange
        await SetupDatabase(59324, config =>
        {
            config.PlexMovieLibraryCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.MovieCount = 1;
            config.TvShowCount = 1;
        });
        using var dbContext = IDbContext;
        var database = ((DbContext)dbContext).Database;
        var movie = await dbContext.PlexMovies.Select(x => new { x.Id, x.PlexLibraryId }).SingleAsync(CancellationToken);
        var show = await dbContext.PlexTvShows.Select(x => new { x.Id, x.PlexLibraryId }).SingleAsync(CancellationToken);
        var genre = new PlexGenreVideo { Name = "Migration preservation", Key = "migration-preservation" };
        dbContext.PlexGenresVideo.Add(genre);
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.PlexMovieGenres.Add(new PlexMovieGenres
        {
            GenresId = genre.Id, PlexMovieId = movie.Id, PlexLibraryId = movie.PlexLibraryId,
        });
        dbContext.PlexTvShowGenres.Add(new PlexTvShowGenres
        {
            GenresId = genre.Id, PlexTvShowId = show.Id, PlexLibraryId = show.PlexLibraryId,
        });
        dbContext.PlexLibraryGenresVideo.AddRange(
            new PlexLibraryGenresVideo { PlexGenreId = genre.Id, PlexLibraryId = movie.PlexLibraryId },
            new PlexLibraryGenresVideo { PlexGenreId = genre.Id, PlexLibraryId = show.PlexLibraryId });
        await dbContext.SaveChangesAsync(CancellationToken);
        var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260917113405_AddImprovedIndexerSearchConfig", CancellationToken);
        const string retainedValuesQuery = """
            SELECT json_array('genre', Id, Name, "Key", Type) AS Value FROM PlexGenres
            UNION ALL SELECT json_array('library', PlexGenreId, PlexLibraryId) FROM PlexLibraryGenres
            UNION ALL SELECT json_array('movie', GenresId, PlexMovieId, PlexLibraryId) FROM PlexMovieGenres
            UNION ALL SELECT json_array('show', GenresId, PlexTvShowId, PlexLibraryId) FROM PlexTvShowGenres
            ORDER BY Value
            """;
        var before = await database.SqlQueryRaw<string>(retainedValuesQuery).ToListAsync(CancellationToken);
        (await dbContext.PlexGenresVideo.Where(x => x.Id == genre.Id).Select(x => x.Name)
            .SingleAsync(CancellationToken)).ShouldBe(genre.Name);
        (await dbContext.PlexLibraryGenresVideo.Where(x => x.PlexGenreId == genre.Id)
            .Select(x => x.PlexLibraryId).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(new[] { movie.PlexLibraryId, show.PlexLibraryId }.OrderBy(x => x));

        // Act
        await migrator.MigrateAsync(cancellationToken: CancellationToken);

        // Assert
        (await database.SqlQueryRaw<string>(retainedValuesQuery).ToListAsync(CancellationToken)).ShouldBe(before);
        (await database.GetPendingMigrationsAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldPreserveVideoFiles_WhenUpgradingReleasedSchema()
    {
        // Arrange
        await SetupDatabase(59325, config =>
        {
            config.PlexMovieLibraryCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.MovieCount = 1;
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 1;
        });
        using var dbContext = IDbContext;
        var database = ((DbContext)dbContext).Database;
        var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260917113405_AddImprovedIndexerSearchConfig", CancellationToken);
        const string retainedFilesQuery = """
            SELECT json_array('movie', Id, PlexMovieId, PlexLibraryId, PlexServerId,
                OriginalFilename, PlexApiPartId, Size, Duration, VideoResolution) AS Value FROM PlexMovieData
            UNION ALL
            SELECT json_array('episode', Id, PlexTvShowEpisodeId, PlexLibraryId, PlexServerId,
                OriginalFilename, PlexApiPartId, Size, Duration, VideoResolution) FROM PlexTvShowEpisodeData
            ORDER BY Value
            """;
        var before = await database.SqlQueryRaw<string>(retainedFilesQuery).ToListAsync(CancellationToken);
        before.Count.ShouldBe(2);

        // Act
        await migrator.MigrateAsync(cancellationToken: CancellationToken);

        // Assert
        var after = await database.SqlQueryRaw<string>(retainedFilesQuery.Replace("VideoResolution", "VideoQuality"))
            .ToListAsync(CancellationToken);
        after.ShouldBe(before);
        (await database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_foreign_key_check")
            .SingleAsync(CancellationToken)).ShouldBe(0);
    }
}
