using System.Data.Common;
using Autofac;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Reaparr.Environment;
using Reaparr.FileSystem.Contracts;

namespace Reaparr.Data.UnitTests;

public class ReaparrDbContextMigrationUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldPreserveVideoGenreGraphAndConvertFolderPaths_WhenUpgradingAndRollingBack()
    {
        // Arrange
        const int movieGenreId = 5401;
        const int tvGenreId = 5402;
        const int unreferencedGenreId = 5403;
        const int postUpgradeGenreId = 5404;
        const int musicFolderId = 5501;
        const int photosFolderId = 5502;
        const int controlFolderId = 5503;
        const int movieParentQuality = (int)VideoQuality.FullHD;
        const int tvParentQuality = (int)VideoQuality.HD;
        const int movieLegacyQuality = (int)VideoQuality.QHD;
        const int tvLegacyQuality = (int)VideoQuality.UHD_4K;
        const int movieVideoResolution = (int)VideoQuality.QHD;
        const int tvVideoResolution = (int)VideoQuality.UHD_4K;
        const int movieDownloadFileQuality = (int)VideoQuality.FullHD;
        const int tvDownloadFileQuality = (int)VideoQuality.HD;
        const int musicCount = 7;
        const int photoCount = 11;

        const string movieGenreName = "Legacy Movie Genre";
        const string tvGenreName = "Legacy TV Genre";
        const string unreferencedGenreName = "Legacy Unreferenced Genre";
        const string postUpgradeGenreName = "Added After Upgrade";
        const string movieGenreKey = "legacy-movie-genre";
        const string tvGenreKey = "legacy-tv-genre";
        const string unreferencedGenreKey = "legacy-unreferenced-genre";
        const string postUpgradeGenreKey = "added-after-upgrade";
        const string dramaType = "\"Drama\"";
        const string comedyType = "\"Comedy\"";
        const string unknownType = "\"Unknown\"";
        const string westernType = "\"Western\"";
        const string folderType = "DownloadFolder";
        const string musicMediaType = "Music";
        const string photosMediaType = "Photos";
        const string controlMediaType = "Movie";
        const string musicPath = "/migration/music";
        const string photosPath = "/migration/photos";
        const string controlPath = "/migration/control";

        await SetupDatabase(
            59305,
            config =>
            {
                config.PlexMovieLibraryCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.PlexMusicLibraryCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.MovieCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
                config.MovieDownloadTasksCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
            }
        );

        using var dbContext = IDbContext;
        var database = ((DbContext)dbContext).Database;
        var migrations = database.GetMigrations().ToList();
        var precedingMigration = migrations.Single(x =>
            x.EndsWith("_AddImprovedIndexerSearchConfig", StringComparison.Ordinal)
        );
        var targetMigration = migrations.Single(x =>
            x.EndsWith("_AddMusicPhotosAndOtherVideos", StringComparison.Ordinal)
        );
        var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync(precedingMigration, CancellationToken);
        await database.OpenConnectionAsync(CancellationToken);
        var connection = database.GetDbConnection();

        var movieLibraryId = await ReadScalarIntAsync(
            connection,
            "SELECT \"Id\" FROM \"PlexLibraries\" WHERE \"Type\" = 'Movie' ORDER BY \"Id\" LIMIT 1;",
            CancellationToken
        );
        var tvLibraryId = await ReadScalarIntAsync(
            connection,
            "SELECT \"Id\" FROM \"PlexLibraries\" WHERE \"Type\" = 'TvShow' ORDER BY \"Id\" LIMIT 1;",
            CancellationToken
        );
        var movieId = await ReadScalarIntAsync(
            connection,
            "SELECT \"Id\" FROM \"PlexMovie\" ORDER BY \"Id\" LIMIT 1;",
            CancellationToken
        );
        var tvShowId = await ReadScalarIntAsync(
            connection,
            "SELECT \"Id\" FROM \"PlexTvShows\" ORDER BY \"Id\" LIMIT 1;",
            CancellationToken
        );
        var musicLibraryId = await ReadScalarIntAsync(
            connection,
            "SELECT \"Id\" FROM \"PlexLibraries\" WHERE \"Type\" = 'Music' ORDER BY \"Id\" LIMIT 1;",
            CancellationToken
        );
        var photosLibraryId = await ReadScalarIntAsync(
            connection,
            "SELECT \"Id\" FROM \"PlexLibraries\" WHERE \"Type\" = 'Photos' ORDER BY \"Id\" LIMIT 1;",
            CancellationToken
        );
        var tvShowEpisodeId = await ReadScalarIntAsync(
            connection,
            $"SELECT \"Id\" FROM \"PlexTvShowEpisodes\" WHERE \"TvShowId\" = {tvShowId} ORDER BY \"Id\" LIMIT 1;",
            CancellationToken
        );
        movieLibraryId.ShouldBeGreaterThan(0);
        tvLibraryId.ShouldBeGreaterThan(0);
        musicLibraryId.ShouldBeGreaterThan(0);
        photosLibraryId.ShouldBeGreaterThan(0);
        movieId.ShouldBeGreaterThan(0);
        tvShowId.ShouldBeGreaterThan(0);
        tvShowEpisodeId.ShouldBeGreaterThan(0);
        movieLibraryId.ShouldNotBe(tvLibraryId);

        // Arrange: replace seeded legacy rows with a deterministic graph in the preceding schema.
        await database.ExecuteSqlInterpolatedAsync(
            $"""
            DELETE FROM "PlexMovieGenres";
            DELETE FROM "PlexTvShowGenres";
            DELETE FROM "PlexLibraryGenres";
            DELETE FROM "PlexGenres";
            DELETE FROM "FolderPaths";
            INSERT INTO "FolderPaths" ("Id", "DisplayName", "FolderType", "MediaType", "DirectoryPath") VALUES
                ({musicFolderId}, {"Legacy Music"}, {folderType}, {musicMediaType}, {musicPath}),
                ({photosFolderId}, {"Legacy Photos"}, {folderType}, {photosMediaType}, {photosPath}),
                ({controlFolderId}, {"Legacy Movie"}, {folderType}, {controlMediaType}, {controlPath});
            INSERT INTO "PlexGenres" ("Id", "Name", "Key", "Type") VALUES
                ({movieGenreId}, {movieGenreName}, {movieGenreKey}, {dramaType}),
                ({tvGenreId}, {tvGenreName}, {tvGenreKey}, {comedyType}),
                ({unreferencedGenreId}, {unreferencedGenreName}, {unreferencedGenreKey}, {unknownType});
            INSERT INTO "PlexLibraryGenres" ("PlexLibraryId", "PlexGenreId") VALUES
                ({movieLibraryId}, {movieGenreId}),
                ({tvLibraryId}, {tvGenreId});
            INSERT INTO "PlexMovieGenres" ("GenresId", "PlexLibraryId", "PlexMovieId") VALUES
                ({movieGenreId}, {movieLibraryId}, {movieId});
            INSERT INTO "PlexTvShowGenres" ("GenresId", "PlexLibraryId", "PlexTvShowId") VALUES
                ({tvGenreId}, {tvLibraryId}, {tvShowId});
            """,
            CancellationToken
        );

        await database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE "PlexMovie" SET "Quality" = {movieParentQuality} WHERE "Id" = {movieId};
            UPDATE "PlexTvShows" SET "Quality" = {tvParentQuality} WHERE "Id" = {tvShowId};
            UPDATE "PlexMovieData"
            SET "Quality" = {movieLegacyQuality}, "VideoResolution" = {movieVideoResolution}
            WHERE "PlexMovieId" = {movieId};
            UPDATE "PlexTvShowEpisodeData"
            SET "Quality" = {tvLegacyQuality}, "VideoResolution" = {tvVideoResolution}
            WHERE "PlexTvShowEpisodeId" = {tvShowEpisodeId};
            UPDATE "DownloadTaskMovieFile" SET "Quality" = {movieDownloadFileQuality};
            UPDATE "DownloadTaskTvShowEpisodeFile" SET "Quality" = {tvDownloadFileQuality};
            """,
            CancellationToken
        );

        (await ReadGenreRowsAsync(connection, "PlexGenres", CancellationToken)).ShouldBe(
            [
                (movieGenreId, movieGenreName, movieGenreKey, dramaType),
                (tvGenreId, tvGenreName, tvGenreKey, comedyType),
                (unreferencedGenreId, unreferencedGenreName, unreferencedGenreKey, unknownType),
            ]
        );
        (await ReadFolderRowsAsync(connection, "FolderPaths", CancellationToken)).ShouldBe(
            [
                (musicFolderId, "Legacy Music", folderType, musicMediaType, musicPath),
                (photosFolderId, "Legacy Photos", folderType, photosMediaType, photosPath),
                (controlFolderId, "Legacy Movie", folderType, controlMediaType, controlPath),
            ]
        );
        (await ReadLibraryGenreRowsAsync(connection, "PlexLibraryGenres", CancellationToken)).ShouldBe(
            [(movieLibraryId, movieGenreId), (tvLibraryId, tvGenreId)]
        );
        (await ReadMediaGenreRowsAsync(connection, "PlexMovieGenres", "PlexMovieId", CancellationToken)).ShouldBe(
            [(movieGenreId, movieLibraryId, movieId)]
        );
        (await ReadMediaGenreRowsAsync(connection, "PlexTvShowGenres", "PlexTvShowId", CancellationToken)).ShouldBe(
            [(tvGenreId, tvLibraryId, tvShowId)]
        );

        (await ReadScalarStringAsync(
            connection,
            $"SELECT \"Type\" FROM \"PlexLibraries\" WHERE \"Id\" = {musicLibraryId};",
            CancellationToken
        )).ShouldBe("Music");
        (await ReadScalarStringAsync(
            connection,
            $"SELECT \"Type\" FROM \"PlexLibraries\" WHERE \"Id\" = {photosLibraryId};",
            CancellationToken
        )).ShouldBe("Photos");
        (
            await ReadQualityRowsAsync(connection, "PlexMovie", "Quality", CancellationToken)
        ).ShouldBe([(movieId, movieParentQuality)]);
        (
            await ReadQualityRowsAsync(connection, "PlexTvShows", "Quality", CancellationToken)
        ).ShouldBe([(tvShowId, tvParentQuality)]);

        var movieMediaRowsBefore = await ReadMediaQualityRowsAsync(
            connection,
            "PlexMovieData",
            "PlexMovieId",
            "VideoResolution",
            CancellationToken
        );
        movieMediaRowsBefore.ShouldHaveSingleItem();
        movieMediaRowsBefore[0].ParentId.ShouldBe(movieId);
        movieMediaRowsBefore[0].Quality.ShouldBe(movieVideoResolution);
        (
            await ReadMediaQualityRowsAsync(connection, "PlexMovieData", "PlexMovieId", "Quality", CancellationToken)
        ).ShouldBe([(movieMediaRowsBefore[0].Id, movieId, movieLegacyQuality)]);

        var tvMediaRowsBefore = await ReadMediaQualityRowsAsync(
            connection,
            "PlexTvShowEpisodeData",
            "PlexTvShowEpisodeId",
            "VideoResolution",
            CancellationToken
        );
        tvMediaRowsBefore.ShouldHaveSingleItem();
        tvMediaRowsBefore[0].ParentId.ShouldBe(tvShowEpisodeId);
        tvMediaRowsBefore[0].Quality.ShouldBe(tvVideoResolution);
        (
            await ReadMediaQualityRowsAsync(
                connection,
                "PlexTvShowEpisodeData",
                "PlexTvShowEpisodeId",
                "Quality",
                CancellationToken
            )
        ).ShouldBe([(tvMediaRowsBefore[0].Id, tvShowEpisodeId, tvLegacyQuality)]);

        var movieDownloadFileRowsBefore = await ReadDownloadFileQualityRowsAsync(
            connection,
            "DownloadTaskMovieFile",
            CancellationToken
        );
        movieDownloadFileRowsBefore.ShouldHaveSingleItem();
        movieDownloadFileRowsBefore[0].Quality.ShouldBe(movieDownloadFileQuality);
        var tvDownloadFileRowsBefore = await ReadDownloadFileQualityRowsAsync(
            connection,
            "DownloadTaskTvShowEpisodeFile",
            CancellationToken
        );
        tvDownloadFileRowsBefore.ShouldHaveSingleItem();
        tvDownloadFileRowsBefore[0].Quality.ShouldBe(tvDownloadFileQuality);
        var precedingSchema = await ReadSchemaSnapshotAsync(connection, [], CancellationToken);

        // Act
        await migrator.MigrateAsync(targetMigration, CancellationToken);

        // Assert
        (await ReadScalarIntAsync(
            connection,
            "SELECT COUNT(*) FROM \"sqlite_master\" WHERE \"type\" = 'table' AND \"name\" = 'PlexGenresVideo';",
            CancellationToken
        )).ShouldBe(1);
        (await ReadScalarIntAsync(
            connection,
            "SELECT COUNT(*) FROM \"sqlite_master\" WHERE \"type\" = 'table' AND \"name\" = 'PlexGenres';",
            CancellationToken
        )).ShouldBe(0);

        await database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE "PlexLibraries" SET "MusicArtistCount" = {musicCount} WHERE "Id" = {musicLibraryId};
            UPDATE "PlexLibraries" SET "PhotoAlbumCount" = {photoCount} WHERE "Id" = {photosLibraryId};
            """,
            CancellationToken
        );

        using (var upgradedContext = IDbContext)
        {
            (
                await upgradedContext
                    .PlexGenresVideo.OrderBy(x => x.Id)
                    .Select(x => new
                    {
                        x.Id,
                        x.Name,
                        x.Key,
                        x.Type,
                    })
                    .ToListAsync(CancellationToken)
            ).ShouldBe(
                [
                    new
                    {
                        Id = movieGenreId,
                        Name = movieGenreName,
                        Key = movieGenreKey,
                        Type = PlexGenreType.Drama,
                    },
                    new
                    {
                        Id = tvGenreId,
                        Name = tvGenreName,
                        Key = tvGenreKey,
                        Type = PlexGenreType.Comedy,
                    },
                    new
                    {
                        Id = unreferencedGenreId,
                        Name = unreferencedGenreName,
                        Key = unreferencedGenreKey,
                        Type = PlexGenreType.Unknown,
                    },
                ]
            );
            (
                await upgradedContext
                    .PlexLibraryGenresVideo.OrderBy(x => x.PlexGenreId).ThenBy(x => x.PlexLibraryId)
                    .Select(x => new
                    {
                        x.PlexLibraryId,
                        x.PlexGenreId,
                    })
                    .ToListAsync(CancellationToken)
            ).ShouldBe(
                [
                    new { PlexLibraryId = movieLibraryId, PlexGenreId = movieGenreId },
                    new { PlexLibraryId = tvLibraryId, PlexGenreId = tvGenreId },
                ]
            );
            (
                await upgradedContext
                    .PlexMovieGenres.OrderBy(x => x.GenresId)
                    .Select(x => new
                    {
                        x.GenresId,
                        x.PlexLibraryId,
                        x.PlexMovieId,
                    })
                    .ToListAsync(CancellationToken)
            ).ShouldBe(
                [new { GenresId = movieGenreId, PlexLibraryId = movieLibraryId, PlexMovieId = movieId }]
            );
            (
                await upgradedContext
                    .PlexTvShowGenres.OrderBy(x => x.GenresId)
                    .Select(x => new
                    {
                        x.GenresId,
                        x.PlexLibraryId,
                        x.PlexTvShowId,
                    })
                    .ToListAsync(CancellationToken)
            ).ShouldBe(
                [new { GenresId = tvGenreId, PlexLibraryId = tvLibraryId, PlexTvShowId = tvShowId }]
            );
            (
                await upgradedContext
                    .PlexLibraries.Where(x => x.Id == musicLibraryId || x.Id == photosLibraryId)
                    .OrderBy(x => x.Id)
                    .Select(x => new
                    {
                        x.Id,
                        x.Type,
                        x.MusicArtistCount,
                        x.PhotoAlbumCount,
                        x.MediaCount,
                    })
                    .ToListAsync(CancellationToken)
            ).ShouldBe(
                [
                    new
                    {
                        Id = musicLibraryId,
                        Type = PlexMediaType.MusicArtist,
                        MusicArtistCount = musicCount,
                        PhotoAlbumCount = 0,
                        MediaCount = musicCount,
                    },
                    new
                    {
                        Id = photosLibraryId,
                        Type = PlexMediaType.PhotoAlbum,
                        MusicArtistCount = 0,
                        PhotoAlbumCount = photoCount,
                        MediaCount = photoCount,
                    },
                ]
            );
            (
                await upgradedContext
                    .PlexMovies.Where(x => x.Id == movieId)
                    .Select(x => new { x.Id, x.VideoQuality })
                    .ToListAsync(CancellationToken)
            ).ShouldBe([new { Id = movieId, VideoQuality = (VideoQuality)movieParentQuality }]);
            (
                await upgradedContext
                    .PlexTvShows.Where(x => x.Id == tvShowId)
                    .Select(x => new { x.Id, x.VideoQuality })
                    .ToListAsync(CancellationToken)
            ).ShouldBe([new { Id = tvShowId, VideoQuality = (VideoQuality)tvParentQuality }]);
            (
                await upgradedContext
                    .PlexMovieData.Where(x => x.PlexMovieId == movieId)
                    .Select(x => new { x.Id, x.PlexMovieId, x.VideoQuality })
                    .ToListAsync(CancellationToken)
            ).ShouldBe(
                [
                    new
                    {
                        Id = movieMediaRowsBefore[0].Id,
                        PlexMovieId = movieId,
                        VideoQuality = (VideoQuality)movieVideoResolution,
                    },
                ]
            );
            (
                await upgradedContext
                    .PlexTvShowEpisodeData.Where(x => x.PlexTvShowEpisodeId == tvShowEpisodeId)
                    .Select(x => new { x.Id, x.PlexTvShowEpisodeId, x.VideoQuality })
                    .ToListAsync(CancellationToken)
            ).ShouldBe(
                [
                    new
                    {
                        Id = tvMediaRowsBefore[0].Id,
                        PlexTvShowEpisodeId = tvShowEpisodeId,
                        VideoQuality = (VideoQuality)tvVideoResolution,
                    },
                ]
            );
            (
                await upgradedContext
                    .DownloadTaskMovieFile.Select(x => new { x.Id, x.VideoQuality }).ToListAsync(CancellationToken)
            ).ShouldBe(
                [new { Id = movieDownloadFileRowsBefore[0].Id, VideoQuality = (VideoQuality)movieDownloadFileQuality }]
            );
            (
                await upgradedContext
                    .DownloadTaskTvShowEpisodeFile.Select(x => new { x.Id, x.VideoQuality })
                    .ToListAsync(CancellationToken)
            ).ShouldBe(
                [new { Id = tvDownloadFileRowsBefore[0].Id, VideoQuality = (VideoQuality)tvDownloadFileQuality }]
            );
            (
                await upgradedContext
                    .FolderPaths.Where(x => x.Id == musicFolderId || x.Id == photosFolderId || x.Id == controlFolderId)
                    .OrderBy(x => x.Id)
                    .Select(x => new
                    {
                        x.Id,
                        x.DisplayName,
                        x.FolderType,
                        x.MediaType,
                        x.DirectoryPath,
                    })
                    .ToListAsync(CancellationToken)
            ).ShouldBe(
                [
                    new
                    {
                        Id = musicFolderId,
                        DisplayName = "Legacy Music",
                        FolderType = FolderType.DownloadFolder,
                        MediaType = PlexMediaType.MusicArtist,
                        DirectoryPath = musicPath,
                    },
                    new
                    {
                        Id = photosFolderId,
                        DisplayName = "Legacy Photos",
                        FolderType = FolderType.DownloadFolder,
                        MediaType = PlexMediaType.PhotoAlbum,
                        DirectoryPath = photosPath,
                    },
                    new
                    {
                        Id = controlFolderId,
                        DisplayName = "Legacy Movie",
                        FolderType = FolderType.DownloadFolder,
                        MediaType = PlexMediaType.Movie,
                        DirectoryPath = controlPath,
                    },
                ]
            );

            // Normal current-model reads must resolve both legacy media joins after the upgrade.
            (
                await upgradedContext
                    .PlexMovies.Where(x => x.Id == movieId)
                    .SelectMany(x => x.Genres)
                    .Select(x => new { x.Id, x.Name })
                    .SingleAsync(CancellationToken)
            ).ShouldBe(new { Id = movieGenreId, Name = movieGenreName });
            (
                await upgradedContext
                    .PlexTvShows.Where(x => x.Id == tvShowId)
                    .SelectMany(x => x.Genres)
                    .Select(x => new { x.Id, x.Name })
                    .SingleAsync(CancellationToken)
            ).ShouldBe(new { Id = tvGenreId, Name = tvGenreName });

            upgradedContext.PlexGenresVideo.Add(
                new PlexGenreVideo
                {
                    Id = postUpgradeGenreId,
                    Name = postUpgradeGenreName,
                    Key = postUpgradeGenreKey,
                    Type = PlexGenreType.Western,
                }
            );
            upgradedContext.PlexLibraryGenresVideo.Add(
                new PlexLibraryGenresVideo(movieLibraryId, postUpgradeGenreId)
            );
            await upgradedContext.SaveChangesAsync(CancellationToken);
        }

        (
            await ReadGenreRowsAsync(connection, "PlexGenresVideo", CancellationToken)
        ).ShouldBe(
            [
                (movieGenreId, movieGenreName, movieGenreKey, dramaType),
                (tvGenreId, tvGenreName, tvGenreKey, comedyType),
                (unreferencedGenreId, unreferencedGenreName, unreferencedGenreKey, unknownType),
                (postUpgradeGenreId, postUpgradeGenreName, postUpgradeGenreKey, westernType),
            ]
        );
        (
            await ReadLibraryGenreRowsAsync(connection, "PlexLibraryGenresVideo", CancellationToken)
        ).ShouldBe(
            [
                (movieLibraryId, movieGenreId),
                (tvLibraryId, tvGenreId),
                (movieLibraryId, postUpgradeGenreId),
            ]
        );
        (await ReadForeignKeyViolationCountAsync(connection, CancellationToken)).ShouldBe(0);
        (await ReadScalarIntAsync(connection, "PRAGMA foreign_keys;", CancellationToken)).ShouldBe(1);

        using (var uniqueContext = IDbContext)
        {
            uniqueContext.PlexGenresVideo.Add(
                new PlexGenreVideo
                {
                    Id = postUpgradeGenreId + 1,
                    Name = "Duplicate Genre Key",
                    Key = movieGenreKey,
                    Type = PlexGenreType.Western,
                }
            );
            await Should.ThrowAsync<DbUpdateException>(() => uniqueContext.SaveChangesAsync(CancellationToken));
        }

        var ensureCreatedSchema = await CreateEnsureCreatedSchemaAsync(CancellationToken);
        var migratedSchema = await ReadSchemaSnapshotAsync(
            connection,
            ensureCreatedSchema.ComputedColumns,
            CancellationToken
        );
        // The shared database also contains Identity and pre-existing legacy tables.
        // Compare the full current model, and prove unrelated tables remain unchanged.
        var modelTables = ensureCreatedSchema.Schema.Tables.ToHashSet(StringComparer.Ordinal);
        var unrelatedTables = precedingSchema.Tables
            .Except(modelTables)
            .Except(["PlexGenres", "PlexLibraryGenres"])
            .ToHashSet(StringComparer.Ordinal);
        migratedSchema.Tables.Where(modelTables.Contains).ShouldBe(ensureCreatedSchema.Schema.Tables);
        migratedSchema.Tables.Where(x => !modelTables.Contains(x))
            .ShouldBe(unrelatedTables.Order(StringComparer.Ordinal));
        // An earlier migration left DEFAULT 0 on ForceMediaRefresh; the current
        // model has no default. Preserve and explicitly bound that existing drift.
        var legacyDefault = precedingSchema.Columns.Single(x =>
            x.Table == "BackgroundJobLibrarySyncJobQueues" && x.Name == "ForceMediaRefresh");
        legacyDefault.DefaultValue.ShouldBe("0");
        migratedSchema.Columns.Single(x => x.Table == legacyDefault.Table && x.Name == legacyDefault.Name)
            .ShouldBe(legacyDefault);
        var expectedColumns = ensureCreatedSchema.Schema.Columns.Select(x =>
        {
            if (x.Table == legacyDefault.Table && x.Name == legacyDefault.Name)
                x.DefaultValue = legacyDefault.DefaultValue;
            return x;
        }).ToList();
        migratedSchema.Columns.Where(x => modelTables.Contains(x.Table))
            .Except(expectedColumns).ShouldBeEmpty("Unexpected migrated column definitions");
        expectedColumns.Except(migratedSchema.Columns)
            .ShouldBeEmpty("Missing current-model column definitions");
        migratedSchema.Indexes.Where(x => modelTables.Contains(x.Table))
            .Except(ensureCreatedSchema.Schema.Indexes).ShouldBeEmpty("Unexpected migrated index definitions");
        ensureCreatedSchema.Schema.Indexes.Except(migratedSchema.Indexes)
            .ShouldBeEmpty("Missing current-model index definitions");
        migratedSchema.ForeignKeys.Where(x => modelTables.Contains(x.Table))
            .Except(ensureCreatedSchema.Schema.ForeignKeys).ShouldBeEmpty("Unexpected migrated foreign keys");
        ensureCreatedSchema.Schema.ForeignKeys.Except(migratedSchema.ForeignKeys)
            .ShouldBeEmpty("Missing current-model foreign keys");
        migratedSchema.Columns.Where(x => unrelatedTables.Contains(x.Table))
            .ShouldBe(precedingSchema.Columns.Where(x => unrelatedTables.Contains(x.Table)));
        migratedSchema.Indexes.Where(x => unrelatedTables.Contains(x.Table))
            .ShouldBe(precedingSchema.Indexes.Where(x => unrelatedTables.Contains(x.Table)));
        migratedSchema.ForeignKeys.Where(x => unrelatedTables.Contains(x.Table))
            .ShouldBe(precedingSchema.ForeignKeys.Where(x => unrelatedTables.Contains(x.Table)));
        migratedSchema.ComputedColumns.ShouldBe(ensureCreatedSchema.Schema.ComputedColumns);

        // Act: rollback the same database to the preceding migration.
        await migrator.MigrateAsync(precedingMigration, CancellationToken);

        // Assert
        (await ReadScalarIntAsync(
            connection,
            "SELECT COUNT(*) FROM \"sqlite_master\" WHERE \"type\" = 'table' AND \"name\" = 'PlexGenres';",
            CancellationToken
        )).ShouldBe(1);
        (await ReadScalarIntAsync(
            connection,
            "SELECT COUNT(*) FROM \"sqlite_master\" WHERE \"type\" = 'table' AND \"name\" = 'PlexGenresVideo';",
            CancellationToken
        )).ShouldBe(0);
        (await ReadGenreRowsAsync(connection, "PlexGenres", CancellationToken)).ShouldBe(
            [
                (movieGenreId, movieGenreName, movieGenreKey, dramaType),
                (tvGenreId, tvGenreName, tvGenreKey, comedyType),
                (unreferencedGenreId, unreferencedGenreName, unreferencedGenreKey, unknownType),
                (postUpgradeGenreId, postUpgradeGenreName, postUpgradeGenreKey, westernType),
            ]
        );
        (await ReadLibraryGenreRowsAsync(connection, "PlexLibraryGenres", CancellationToken)).ShouldBe(
            [
                (movieLibraryId, movieGenreId),
                (tvLibraryId, tvGenreId),
                (movieLibraryId, postUpgradeGenreId),
            ]
        );
        (await ReadMediaGenreRowsAsync(connection, "PlexMovieGenres", "PlexMovieId", CancellationToken)).ShouldBe(
            [(movieGenreId, movieLibraryId, movieId)]
        );
        (await ReadMediaGenreRowsAsync(connection, "PlexTvShowGenres", "PlexTvShowId", CancellationToken)).ShouldBe(
            [(tvGenreId, tvLibraryId, tvShowId)]
        );
        (await ReadScalarStringAsync(
            connection,
            $"SELECT \"Type\" FROM \"PlexLibraries\" WHERE \"Id\" = {musicLibraryId};",
            CancellationToken
        )).ShouldBe("Music");
        (await ReadScalarStringAsync(
            connection,
            $"SELECT \"Type\" FROM \"PlexLibraries\" WHERE \"Id\" = {photosLibraryId};",
            CancellationToken
        )).ShouldBe("Photos");
        (
            await ReadQualityRowsAsync(connection, "PlexMovie", "Quality", CancellationToken)
        ).ShouldBe([(movieId, movieParentQuality)]);
        (
            await ReadQualityRowsAsync(connection, "PlexTvShows", "Quality", CancellationToken)
        ).ShouldBe([(tvShowId, tvParentQuality)]);
        (
            await ReadMediaQualityRowsAsync(
                connection,
                "PlexMovieData",
                "PlexMovieId",
                "Quality",
                CancellationToken
            )
        ).ShouldBe([(movieMediaRowsBefore[0].Id, movieId, movieVideoResolution)]);
        (
            await ReadMediaQualityRowsAsync(
                connection,
                "PlexTvShowEpisodeData",
                "PlexTvShowEpisodeId",
                "Quality",
                CancellationToken
            )
        ).ShouldBe([(tvMediaRowsBefore[0].Id, tvShowEpisodeId, tvVideoResolution)]);
        (
            await ReadMediaQualityRowsAsync(
                connection,
                "PlexMovieData",
                "PlexMovieId",
                "VideoResolution",
                CancellationToken
            )
        ).ShouldBe([(movieMediaRowsBefore[0].Id, movieId, movieVideoResolution)]);
        (
            await ReadMediaQualityRowsAsync(
                connection,
                "PlexTvShowEpisodeData",
                "PlexTvShowEpisodeId",
                "VideoResolution",
                CancellationToken
            )
        ).ShouldBe([(tvMediaRowsBefore[0].Id, tvShowEpisodeId, tvVideoResolution)]);
        (
            await ReadDownloadFileQualityRowsAsync(connection, "DownloadTaskMovieFile", CancellationToken)
        ).ShouldBe([(movieDownloadFileRowsBefore[0].Id, movieDownloadFileQuality)]);
        (
            await ReadDownloadFileQualityRowsAsync(connection, "DownloadTaskTvShowEpisodeFile", CancellationToken)
        ).ShouldBe([(tvDownloadFileRowsBefore[0].Id, tvDownloadFileQuality)]);
        (await ReadFolderRowsAsync(connection, "FolderPaths", CancellationToken)).ShouldBe(
            [
                (musicFolderId, "Legacy Music", folderType, musicMediaType, musicPath),
                (photosFolderId, "Legacy Photos", folderType, photosMediaType, photosPath),
                (controlFolderId, "Legacy Movie", folderType, controlMediaType, controlPath),
            ]
        );
        (await ReadForeignKeyViolationCountAsync(connection, CancellationToken)).ShouldBe(0);
        (await ReadScalarIntAsync(connection, "PRAGMA foreign_keys;", CancellationToken)).ShouldBe(1);
    }

    [Test]
    public async Task ShouldMigrateFreshSqliteDatabaseToLatestAndReadWriteCurrentModel()
    {
        // Arrange
        var databaseName = MockDatabase.GetMemoryDatabaseName();
        await using var keepAlive = new SqliteConnection(
            DbContextConnections.GetConnectionString(databaseName, SqliteOpenMode.Memory)
        );
        await keepAlive.OpenAsync(CancellationToken);
        DbContextConnections.InitializeDatabase(keepAlive);

        var options = new DbContextOptionsBuilder<ReaparrDbContext>();
        options.ConfigureSqlite(databaseName, SqliteOpenMode.Memory);
        new SqliteDbContextOptionsBuilder(options).MigrationsAssembly(
            typeof(ReaparrDbContext).Assembly.FullName
        );
        await using var dbContext = new ReaparrDbContext(
            options.Options,
            Mock.Container.Resolve<ILogger>(),
            Mock.Container.Resolve<IPathProvider>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            databaseName
        );
        var database = dbContext.Database;
        var targetMigration = database
            .GetMigrations()
            .Single(x => x.EndsWith("_AddMusicPhotosAndOtherVideos", StringComparison.Ordinal));

        // Act
        await database.MigrateAsync(CancellationToken);

        // Assert
        database.GetAppliedMigrations().ShouldContain(targetMigration);
        dbContext.PlexGenresVideo.Add(
            new PlexGenreVideo
            {
                Id = 5901,
                Name = "Fresh Database Genre",
                Key = "fresh-database-genre",
                Type = PlexGenreType.Documentary,
            }
        );
        dbContext.FolderPaths.Add(
            new FolderPath(FolderType.DownloadFolder, "Fresh Database Folder", "/fresh/database")
            {
                Id = 5902,
                MediaType = PlexMediaType.MusicArtist,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.ClearChangeTracker();

        (
            await dbContext
                .PlexGenresVideo.Where(x => x.Id == 5901)
                .Select(x => new { x.Id, x.Name, x.Key, x.Type })
                .SingleAsync(CancellationToken)
        ).ShouldBe(
            new
            {
                Id = 5901,
                Name = "Fresh Database Genre",
                Key = "fresh-database-genre",
                Type = PlexGenreType.Documentary,
            }
        );
        (
            await dbContext
                .FolderPaths.Where(x => x.Id == 5902)
                .Select(x => new { x.Id, x.DisplayName, x.FolderType, x.MediaType, x.DirectoryPath })
                .SingleAsync(CancellationToken)
        ).ShouldBe(
            new
            {
                Id = 5902,
                DisplayName = "Fresh Database Folder",
                FolderType = FolderType.DownloadFolder,
                MediaType = PlexMediaType.MusicArtist,
                DirectoryPath = "/fresh/database",
            }
        );
        await database.OpenConnectionAsync(CancellationToken);
        (await ReadForeignKeyViolationCountAsync(database.GetDbConnection(), CancellationToken)).ShouldBe(0);
        (await ReadScalarIntAsync(database.GetDbConnection(), "PRAGMA foreign_keys;", CancellationToken)).ShouldBe(1);
    }

    private static async Task<int> ReadScalarIntAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<string> ReadScalarStringAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? string.Empty;
    }

    private static async Task<List<(int Id, int Quality)>> ReadQualityRowsAsync(
        DbConnection connection,
        string tableName,
        string qualityColumn,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT \"Id\", \"{qualityColumn}\" FROM \"{tableName}\" ORDER BY \"Id\";";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(int Id, int Quality)>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add((reader.GetInt32(0), reader.GetInt32(1)));
        return rows;
    }

    private static async Task<List<(int Id, int ParentId, int Quality)>> ReadMediaQualityRowsAsync(
        DbConnection connection,
        string tableName,
        string parentColumn,
        string qualityColumn,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT \"Id\", \"{parentColumn}\", \"{qualityColumn}\" FROM \"{tableName}\" ORDER BY \"Id\";";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(int Id, int ParentId, int Quality)>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add((reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)));
        return rows;
    }

    private static async Task<List<(Guid Id, int Quality)>> ReadDownloadFileQualityRowsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"Id\", \"Quality\" FROM \"{tableName}\" ORDER BY \"Id\";";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(Guid Id, int Quality)>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add((Guid.Parse(reader.GetString(0)), reader.GetInt32(1)));
        return rows;
    }

    private sealed record SchemaSnapshot(
        List<string> Tables,
        List<(string Table, string Name, string Type, int NotNull, string? DefaultValue, int PrimaryKey, int Hidden)> Columns,
        List<(string Table, string Name, int Unique, string Columns, string Filter)> Indexes,
        List<
            (
                string Table,
                int Sequence,
                string PrincipalTable,
                string From,
                string To,
                string OnUpdate,
                string OnDelete,
                string Match
            )
        > ForeignKeys,
        List<(string Table, string Column, string Expression)> ComputedColumns
    );

    private sealed record EnsureCreatedSchemaResult(
        SchemaSnapshot Schema,
        List<(string Table, string Column, string Expression)> ComputedColumns
    );

    private async Task<EnsureCreatedSchemaResult> CreateEnsureCreatedSchemaAsync(
        CancellationToken cancellationToken
    )
    {
        var databaseName = MockDatabase.GetMemoryDatabaseName();
        await using var keepAlive = new SqliteConnection(
            DbContextConnections.GetConnectionString(databaseName, SqliteOpenMode.Memory)
        );
        await keepAlive.OpenAsync(cancellationToken);
        DbContextConnections.InitializeDatabase(keepAlive);

        var options = new DbContextOptionsBuilder<ReaparrDbContext>();
        options.ConfigureSqlite(databaseName, SqliteOpenMode.Memory);
        new SqliteDbContextOptionsBuilder(options).MigrationsAssembly(typeof(ReaparrDbContext).Assembly.FullName);
        await using var freshContext = new ReaparrDbContext(
            options.Options,
            Mock.Container.Resolve<ILogger>(),
            Mock.Container.Resolve<IPathProvider>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            databaseName
        );

        await freshContext.Database.EnsureCreatedAsync(cancellationToken);
        await freshContext.Database.OpenConnectionAsync(cancellationToken);
        var computedColumns = ReadComputedColumnDefinitions(freshContext);
        var schema = await ReadSchemaSnapshotAsync(
            freshContext.Database.GetDbConnection(),
            computedColumns,
            cancellationToken
        );
        return new EnsureCreatedSchemaResult(schema, computedColumns);
    }

    private static List<(string Table, string Column, string Expression)> ReadComputedColumnDefinitions(
        DbContext context
    )
    {
        var computedColumns = new List<(string Table, string Column, string Expression)>();
        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrEmpty(tableName))
                continue;

            var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            foreach (var property in entityType.GetProperties())
            {
                var computedSql = property.GetComputedColumnSql(storeObject);
                var columnName = property.GetColumnName(storeObject);
                if (string.IsNullOrEmpty(computedSql) || string.IsNullOrEmpty(columnName))
                    continue;

                var normalizedSql = NormalizeSql(computedSql);
                if (
                    computedColumns.Any(
                        x => x.Table == tableName && x.Column == columnName && x.Expression == normalizedSql
                    )
                )
                    continue;

                computedColumns.Add((tableName, columnName, normalizedSql));
            }
        }

        computedColumns.Sort(
            (left, right) =>
            {
                var tableComparison = string.Compare(left.Table, right.Table, StringComparison.Ordinal);
                return tableComparison != 0
                    ? tableComparison
                    : string.Compare(left.Column, right.Column, StringComparison.Ordinal);
            }
        );
        return computedColumns;
    }

    private static async Task<SchemaSnapshot> ReadSchemaSnapshotAsync(
        DbConnection connection,
        List<(string Table, string Column, string Expression)> computedColumnDefinitions,
        CancellationToken cancellationToken
    )
    {
        var tables = new List<string>();
        await using (var tableCommand = connection.CreateCommand())
        {
            tableCommand.CommandText =
                """
                SELECT "name"
                FROM "sqlite_master"
                WHERE "type" = 'table'
                  AND "name" NOT LIKE 'sqlite_%'
                  AND "name" NOT IN ('__EFMigrationsHistory', '__EFMigrationsLock')
                ORDER BY "name";
                """;
            await using var tableReader = await tableCommand.ExecuteReaderAsync(cancellationToken);
            while (await tableReader.ReadAsync(cancellationToken))
                tables.Add(tableReader.GetString(0));
        }

        var columns = new List<
            (string Table, string Name, string Type, int NotNull, string? DefaultValue, int PrimaryKey, int Hidden)
        >();
        var indexes = new List<(string Table, string Name, int Unique, string Columns, string Filter)>();
        var foreignKeys = new List<
            (
                string Table,
                int Sequence,
                string PrincipalTable,
                string From,
                string To,
                string OnUpdate,
                string OnDelete,
                string Match
            )
        >();

        foreach (var table in tables)
        {
            await using (var columnCommand = connection.CreateCommand())
            {
                columnCommand.CommandText = $"PRAGMA table_xinfo({QuoteIdentifier(table)});";
                await using var columnReader = await columnCommand.ExecuteReaderAsync(cancellationToken);
                while (await columnReader.ReadAsync(cancellationToken))
                {
                    columns.Add(
                        (
                            table,
                            columnReader.GetString(1),
                            columnReader.GetString(2),
                            columnReader.GetInt32(3),
                            columnReader.IsDBNull(4) ? null : columnReader.GetValue(4).ToString(),
                            columnReader.GetInt32(5),
                            columnReader.GetInt32(6)
                        )
                    );
                }
            }

            var tableIndexes = new List<(string Name, int Unique)>();
            await using (var indexCommand = connection.CreateCommand())
            {
                indexCommand.CommandText = $"PRAGMA index_list({QuoteIdentifier(table)});";
                await using var indexReader = await indexCommand.ExecuteReaderAsync(cancellationToken);
                while (await indexReader.ReadAsync(cancellationToken))
                {
                    if (indexReader.GetString(3) == "c")
                        tableIndexes.Add((indexReader.GetString(1), indexReader.GetInt32(2)));
                }
            }

            foreach (var (indexName, unique) in tableIndexes)
            {
                var indexColumns = new List<string>();
                await using (var indexInfoCommand = connection.CreateCommand())
                {
                    indexInfoCommand.CommandText = $"PRAGMA index_xinfo({QuoteIdentifier(indexName)});";
                    await using var indexInfoReader = await indexInfoCommand.ExecuteReaderAsync(cancellationToken);
                    while (await indexInfoReader.ReadAsync(cancellationToken))
                    {
                        if (indexInfoReader.GetInt32(5) == 1)
                            indexColumns.Add($"{indexInfoReader.GetString(2)}:{indexInfoReader.GetInt32(3)}:{indexInfoReader.GetString(4)}");
                    }
                }
                await using var indexSqlCommand = connection.CreateCommand();
                indexSqlCommand.CommandText = "SELECT \"sql\" FROM \"sqlite_master\" WHERE \"type\" = 'index' AND \"name\" = $name;";
                var indexNameParameter = indexSqlCommand.CreateParameter();
                indexNameParameter.ParameterName = "$name";
                indexNameParameter.Value = indexName;
                indexSqlCommand.Parameters.Add(indexNameParameter);
                var indexSql = Convert.ToString(await indexSqlCommand.ExecuteScalarAsync(cancellationToken))!;
                var filterStart = indexSql.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase);
                var filter = filterStart < 0 ? string.Empty : NormalizeSql(indexSql[(filterStart + 7)..]);
                indexes.Add((table, indexName, unique, string.Join("|", indexColumns), filter));
            }

            await using (var foreignKeyCommand = connection.CreateCommand())
            {
                foreignKeyCommand.CommandText = $"PRAGMA foreign_key_list({QuoteIdentifier(table)});";
                await using var foreignKeyReader = await foreignKeyCommand.ExecuteReaderAsync(cancellationToken);
                while (await foreignKeyReader.ReadAsync(cancellationToken))
                {
                    foreignKeys.Add(
                        (
                            table,
                            foreignKeyReader.GetInt32(1),
                            foreignKeyReader.GetString(2),
                            foreignKeyReader.GetString(3),
                            foreignKeyReader.GetString(4),
                            foreignKeyReader.GetString(5),
                            foreignKeyReader.GetString(6),
                            foreignKeyReader.GetString(7)
                        )
                    );
                }
            }
        }

        var computedColumns = new List<(string Table, string Column, string Expression)>();
        foreach (var definition in computedColumnDefinitions)
        {
            var tableSql = NormalizeSql(await ReadTableSqlAsync(connection, definition.Table, cancellationToken));
            computedColumns.Add(
                (
                    definition.Table,
                    definition.Column,
                    tableSql.Contains(definition.Expression, StringComparison.Ordinal) ? definition.Expression : string.Empty
                )
            );
        }

        indexes.Sort(
            (left, right) =>
            {
                var tableComparison = string.Compare(left.Table, right.Table, StringComparison.Ordinal);
                return tableComparison != 0
                    ? tableComparison
                    : string.Compare(left.Name, right.Name, StringComparison.Ordinal);
            }
        );
        foreignKeys = foreignKeys
            .OrderBy(x => x.Table, StringComparer.Ordinal)
            .ThenBy(x => x.PrincipalTable, StringComparer.Ordinal)
            .ThenBy(x => x.From, StringComparer.Ordinal)
            .ThenBy(x => x.To, StringComparer.Ordinal)
            .ThenBy(x => x.Sequence)
            .ToList();
        columns.Sort(
            (left, right) =>
            {
                var tableComparison = string.Compare(left.Table, right.Table, StringComparison.Ordinal);
                return tableComparison != 0
                    ? tableComparison
                    : string.Compare(left.Name, right.Name, StringComparison.Ordinal);
            }
        );

        return new SchemaSnapshot(tables, columns, indexes, foreignKeys, computedColumns);
    }

    private static async Task<string> ReadTableSqlAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"sql\" FROM \"sqlite_master\" WHERE \"type\" = 'table' AND \"name\" = $name;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? string.Empty;
    }

    private static string QuoteIdentifier(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string NormalizeSql(string value) =>
        string.Concat(value.Where(x => !char.IsWhiteSpace(x))).Replace("\"", string.Empty, StringComparison.Ordinal);

    private static async Task<List<(int Id, string Name, string Key, string Type)>> ReadGenreRowsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT \"Id\", \"Name\", \"Key\", \"Type\" FROM \"{tableName}\" ORDER BY \"Id\";";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(int Id, string Name, string Key, string Type)>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        return rows;
    }

    private static async Task<List<(int Id, string DisplayName, string FolderType, string MediaType, string DirectoryPath)>> ReadFolderRowsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT \"Id\", \"DisplayName\", \"FolderType\", \"MediaType\", \"DirectoryPath\" FROM \"{tableName}\" WHERE \"Id\" IN (5501, 5502, 5503) ORDER BY \"Id\";";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(int Id, string DisplayName, string FolderType, string MediaType, string DirectoryPath)>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(
                (
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4)
                )
            );
        return rows;
    }

    private static async Task<List<(int LibraryId, int GenreId)>> ReadLibraryGenreRowsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT \"PlexLibraryId\", \"PlexGenreId\" FROM \"{tableName}\" ORDER BY \"PlexGenreId\", \"PlexLibraryId\";";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(int LibraryId, int GenreId)>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add((reader.GetInt32(0), reader.GetInt32(1)));
        return rows;
    }

    private static async Task<List<(int GenreId, int LibraryId, int MediaId)>> ReadMediaGenreRowsAsync(
        DbConnection connection,
        string tableName,
        string mediaColumn,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT \"GenresId\", \"PlexLibraryId\", \"{mediaColumn}\" FROM \"{tableName}\" ORDER BY \"GenresId\", \"PlexLibraryId\", \"{mediaColumn}\";";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(int GenreId, int LibraryId, int MediaId)>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add((reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)));
        return rows;
    }

    private static async Task<int> ReadForeignKeyViolationCountAsync(
        DbConnection connection,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var violations = 0;
        while (await reader.ReadAsync(cancellationToken))
            violations++;
        return violations;
    }
}
