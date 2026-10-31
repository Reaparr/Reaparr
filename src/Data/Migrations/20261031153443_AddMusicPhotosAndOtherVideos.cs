using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reaparr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMusicPhotosAndOtherVideos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlexMovieGenres_PlexGenres_GenresId",
                table: "PlexMovieGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_PlexTvShowGenres_PlexGenres_GenresId",
                table: "PlexTvShowGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_PlexLibraryGenres_PlexGenres_PlexGenreId",
                table: "PlexLibraryGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_PlexLibraryGenres_PlexLibraries_PlexLibraryId",
                table: "PlexLibraryGenres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlexLibraryGenres",
                table: "PlexLibraryGenres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlexGenres",
                table: "PlexGenres");

            migrationBuilder.RenameTable(
                name: "PlexGenres",
                newName: "PlexGenresVideo");

            migrationBuilder.RenameTable(
                name: "PlexLibraryGenres",
                newName: "PlexLibraryGenresVideo");

            migrationBuilder.RenameIndex(
                name: "IX_PlexGenres_Key",
                table: "PlexGenresVideo",
                newName: "IX_PlexGenresVideo_Key");

            migrationBuilder.RenameIndex(
                name: "IX_PlexLibraryGenres_PlexLibraryId",
                table: "PlexLibraryGenresVideo",
                newName: "IX_PlexLibraryGenresVideo_PlexLibraryId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlexGenresVideo",
                table: "PlexGenresVideo",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlexLibraryGenresVideo",
                table: "PlexLibraryGenresVideo",
                columns: new[] { "PlexGenreId", "PlexLibraryId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PlexLibraryGenresVideo_PlexGenresVideo_PlexGenreId",
                table: "PlexLibraryGenresVideo",
                column: "PlexGenreId",
                principalTable: "PlexGenresVideo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlexLibraryGenresVideo_PlexLibraries_PlexLibraryId",
                table: "PlexLibraryGenresVideo",
                column: "PlexLibraryId",
                principalTable: "PlexLibraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropIndex(
                name: "IX_PlexTvShowSeason_TvShowId",
                table: "PlexTvShowSeason");

            migrationBuilder.DropIndex(
                name: "IX_PlexTvShowEpisodeData_PlexTvShowEpisodeId_Quality",
                table: "PlexTvShowEpisodeData");

            migrationBuilder.DropIndex(
                name: "IX_PlexTvShowEpisodeData_Quality",
                table: "PlexTvShowEpisodeData");

            migrationBuilder.DropIndex(
                name: "IX_PlexMovieData_PlexMovieId_Quality",
                table: "PlexMovieData");

            migrationBuilder.DropIndex(
                name: "IX_PlexMovieData_Quality",
                table: "PlexMovieData");

            migrationBuilder.DropColumn(
                name: "Quality",
                table: "PlexTvShowEpisodeData");

            migrationBuilder.DropColumn(
                name: "Quality",
                table: "PlexMovieData");

            migrationBuilder.RenameColumn(
                name: "Quality",
                table: "PlexTvShowSeasonMediaQualities",
                newName: "VideoQuality");

            migrationBuilder.RenameColumn(
                name: "Quality",
                table: "PlexTvShows",
                newName: "VideoQuality");

            migrationBuilder.RenameIndex(
                name: "IX_PlexTvShows_Quality",
                table: "PlexTvShows",
                newName: "IX_PlexTvShows_VideoQuality");

            migrationBuilder.RenameIndex(
                name: "IX_PlexTvShows_PlexLibraryId_Quality",
                table: "PlexTvShows",
                newName: "IX_PlexTvShows_PlexLibraryId_VideoQuality");

            migrationBuilder.RenameColumn(
                name: "Quality",
                table: "PlexTvShowMediaQualities",
                newName: "VideoQuality");

            migrationBuilder.RenameColumn(
                name: "VideoResolution",
                table: "PlexTvShowEpisodeData",
                newName: "VideoQuality");

            migrationBuilder.RenameColumn(
                name: "VideoResolution",
                table: "PlexMovieData",
                newName: "VideoQuality");

            migrationBuilder.RenameColumn(
                name: "Quality",
                table: "PlexMovie",
                newName: "VideoQuality");

            migrationBuilder.RenameIndex(
                name: "IX_PlexMovie_Quality",
                table: "PlexMovie",
                newName: "IX_PlexMovie_VideoQuality");

            migrationBuilder.RenameIndex(
                name: "IX_PlexMovie_PlexLibraryId_Quality",
                table: "PlexMovie",
                newName: "IX_PlexMovie_PlexLibraryId_VideoQuality");

            migrationBuilder.RenameColumn(
                name: "Quality",
                table: "DownloadTaskTvShowEpisodeFile",
                newName: "VideoQuality");

            migrationBuilder.RenameColumn(
                name: "Quality",
                table: "DownloadTaskMovieFile",
                newName: "VideoQuality");

            migrationBuilder.AlterColumn<int>(
                name: "VideoQuality",
                table: "PlexTvShowEpisodeData",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Relational:ColumnOrder", 4);

            migrationBuilder.AlterColumn<int>(
                name: "VideoQuality",
                table: "PlexMovieData",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Relational:ColumnOrder", 4);

            migrationBuilder.AddColumn<int>(
                name: "MusicAlbumCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MusicArtistCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MusicTrackCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OtherVideoCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PhotoAlbumCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PhotoClipCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PhotoImageCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "MediaCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                computedColumnSql: "CASE WHEN Type = 'Movie' THEN MovieCount WHEN Type = 'TvShow' THEN TvShowCount WHEN Type = 'MusicArtist' THEN MusicArtistCount WHEN Type = 'PhotoAlbum' THEN PhotoAlbumCount WHEN Type = 'OtherVideos' THEN OtherVideoCount ELSE -1 END",
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldComputedColumnSql: "CASE WHEN Type = 'Movie' THEN MovieCount WHEN Type = 'TvShow' THEN TvShowCount ELSE -1 END");

            migrationBuilder.CreateTable(
                name: "DownloadTaskMusicArtists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false, collation: "NATURALSORT"),
                    DownloadStatus = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FullTitle = table.Column<string>(type: "TEXT", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SonarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    RadarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadTaskMusicArtists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicArtists_IntegrationsRadarr_RadarrIntegrationId",
                        column: x => x.RadarrIntegrationId,
                        principalTable: "IntegrationsRadarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicArtists_IntegrationsSonarr_SonarrIntegrationId",
                        column: x => x.SonarrIntegrationId,
                        principalTable: "IntegrationsSonarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicArtists_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicArtists_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DownloadTaskOtherVideos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false, collation: "NATURALSORT"),
                    DownloadStatus = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FullTitle = table.Column<string>(type: "TEXT", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SonarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    RadarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadTaskOtherVideos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadTaskOtherVideos_IntegrationsRadarr_RadarrIntegrationId",
                        column: x => x.RadarrIntegrationId,
                        principalTable: "IntegrationsRadarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskOtherVideos_IntegrationsSonarr_SonarrIntegrationId",
                        column: x => x.SonarrIntegrationId,
                        principalTable: "IntegrationsSonarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskOtherVideos_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskOtherVideos_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DownloadTaskPhotoFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false, collation: "NATURALSORT"),
                    DownloadStatus = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FullTitle = table.Column<string>(type: "TEXT", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SonarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    RadarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    DownloadRootPath = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    DestinationRootPath = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadTaskPhotoFolders", x => x.Id);
                    table.CheckConstraint("CK_DownloadTaskPhotoFolders_RootShape", "(\"ParentId\" IS NULL AND \"RelativePath\" = '')\nOR\n(\"ParentId\" IS NOT NULL AND \"RelativePath\" <> '')");
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoFolders_DownloadTaskPhotoFolders_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DownloadTaskPhotoFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoFolders_IntegrationsRadarr_RadarrIntegrationId",
                        column: x => x.RadarrIntegrationId,
                        principalTable: "IntegrationsRadarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoFolders_IntegrationsSonarr_SonarrIntegrationId",
                        column: x => x.SonarrIntegrationId,
                        principalTable: "IntegrationsSonarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoFolders_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoFolders_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexGenresAudio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexGenresAudio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicArtists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    SortIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    SearchTitle = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaSize = table.Column<long>(type: "INTEGER", nullable: false),
                    PlexApiMetaDataKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ChildCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HasThumb = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasArt = table.Column<bool>(type: "INTEGER", nullable: false),
                    PlexApiIdentityId = table.Column<string>(type: "TEXT", nullable: true, collation: "BINARY"),
                    MusicBrainzArtistId = table.Column<string>(type: "TEXT", nullable: true, collation: "BINARY"),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicArtists", x => x.Id);
                    table.UniqueConstraint("AK_PlexMusicArtists_PlexLibraryId_Id", x => new { x.PlexLibraryId, x.Id });
                    table.ForeignKey(
                        name: "FK_PlexMusicArtists_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicArtists_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexOtherVideos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    SortIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    SearchTitle = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaSize = table.Column<long>(type: "INTEGER", nullable: false),
                    PlexApiMetaDataKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ChildCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OriginallyAvailableAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VideoQuality = table.Column<int>(type: "INTEGER", nullable: false),
                    HasThumb = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasArt = table.Column<bool>(type: "INTEGER", nullable: false),
                    Guid = table.Column<string>(type: "TEXT", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexOtherVideos", x => x.Id);
                    table.UniqueConstraint("AK_PlexOtherVideos_PlexLibraryId_Id", x => new { x.PlexLibraryId, x.Id });
                    table.ForeignKey(
                        name: "FK_PlexOtherVideos_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexOtherVideos_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexPhotoAlbums",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false, collation: "NATURALSORT"),
                    SearchTitle = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    HasThumb = table.Column<bool>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexPhotoAlbums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexPhotoAlbums_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexPhotoAlbums_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexPhotoFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<int>(type: "INTEGER", nullable: true),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    Title = table.Column<string>(type: "TEXT", nullable: false, collation: "NATURALSORT"),
                    RootIdentityKey = table.Column<string>(type: "TEXT", nullable: true, collation: "BINARY"),
                    SourceRootPath = table.Column<string>(type: "TEXT", nullable: true, collation: "BINARY"),
                    PlexApiLocationId = table.Column<long>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexPhotoFolders", x => x.Id);
                    table.UniqueConstraint("AK_PlexPhotoFolders_PlexLibraryId_Id", x => new { x.PlexLibraryId, x.Id });
                    table.CheckConstraint("CK_PlexPhotoFolders_RootShape", "(\n    \"ParentId\" IS NULL\n    AND \"RelativePath\" = ''\n    AND \"RootIdentityKey\" IS NOT NULL\n    AND \"RootIdentityKey\" <> ''\n    AND \"SourceRootPath\" IS NOT NULL\n    AND \"SourceRootPath\" <> ''\n    AND \"IsActive\" IS NOT NULL\n)\nOR\n(\n    \"ParentId\" IS NOT NULL\n    AND \"RelativePath\" <> ''\n    AND \"RootIdentityKey\" IS NULL\n    AND \"SourceRootPath\" IS NULL\n    AND \"PlexApiLocationId\" IS NULL\n    AND \"IsActive\" IS NULL\n)");
                    table.ForeignKey(
                        name: "FK_PlexPhotoFolders_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexPhotoFolders_PlexPhotoFolders_PlexLibraryId_ParentId",
                        columns: x => new { x.PlexLibraryId, x.ParentId },
                        principalTable: "PlexPhotoFolders",
                        principalColumns: new[] { "PlexLibraryId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexPhotoFolders_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StreamLanguages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LanguageCode = table.Column<string>(type: "TEXT", nullable: false),
                    LanguageTag = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamLanguages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DownloadTaskMusicAlbums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false, collation: "NATURALSORT"),
                    DownloadStatus = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FullTitle = table.Column<string>(type: "TEXT", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SonarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    RadarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadTaskMusicAlbums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicAlbums_DownloadTaskMusicArtists_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DownloadTaskMusicArtists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicAlbums_IntegrationsRadarr_RadarrIntegrationId",
                        column: x => x.RadarrIntegrationId,
                        principalTable: "IntegrationsRadarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicAlbums_IntegrationsSonarr_SonarrIntegrationId",
                        column: x => x.SonarrIntegrationId,
                        principalTable: "IntegrationsSonarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicAlbums_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicAlbums_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DownloadTaskOtherVideoFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false, collation: "NATURALSORT"),
                    DownloadStatus = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FullTitle = table.Column<string>(type: "TEXT", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SonarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PlexApiMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    RadarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PlexApiPartId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false, collation: "NATURALSORT"),
                    FileLocationUrl = table.Column<string>(type: "TEXT", nullable: false),
                    HashId = table.Column<string>(type: "TEXT", nullable: true),
                    VideoQuality = table.Column<int>(type: "INTEGER", nullable: false),
                    DirectoryMeta = table.Column<string>(type: "TEXT", nullable: false),
                    DataReceived = table.Column<long>(type: "INTEGER", nullable: false),
                    DataTotal = table.Column<long>(type: "INTEGER", nullable: false),
                    DownloadSpeed = table.Column<long>(type: "INTEGER", nullable: false),
                    DirectDownloadSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    DownloadClientType = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false, defaultValue: "Direct"),
                    FileTransferSpeed = table.Column<long>(type: "INTEGER", nullable: false),
                    FileDataTransferred = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrentFileTransferBytesOffset = table.Column<long>(type: "INTEGER", nullable: false),
                    Percentage = table.Column<decimal>(type: "TEXT", nullable: false),
                    TimeRemaining = table.Column<int>(type: "INTEGER", nullable: false),
                    DestinationFolderPathId = table.Column<int>(type: "INTEGER", nullable: true),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadTaskOtherVideoFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadTaskOtherVideoFiles_DownloadTaskOtherVideos_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DownloadTaskOtherVideos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskOtherVideoFiles_FolderPaths_DestinationFolderPathId",
                        column: x => x.DestinationFolderPathId,
                        principalTable: "FolderPaths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskOtherVideoFiles_IntegrationsRadarr_RadarrIntegrationId",
                        column: x => x.RadarrIntegrationId,
                        principalTable: "IntegrationsRadarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskOtherVideoFiles_IntegrationsSonarr_SonarrIntegrationId",
                        column: x => x.SonarrIntegrationId,
                        principalTable: "IntegrationsSonarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskOtherVideoFiles_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskOtherVideoFiles_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DownloadTaskPhotoImageFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false, collation: "NATURALSORT"),
                    DownloadStatus = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FullTitle = table.Column<string>(type: "TEXT", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SonarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PlexApiMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    RadarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PlexApiPartId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false, collation: "NATURALSORT"),
                    FileLocationUrl = table.Column<string>(type: "TEXT", nullable: false),
                    HashId = table.Column<string>(type: "TEXT", nullable: true),
                    VideoQuality = table.Column<int>(type: "INTEGER", nullable: false),
                    DirectoryMeta = table.Column<string>(type: "TEXT", nullable: false),
                    DataReceived = table.Column<long>(type: "INTEGER", nullable: false),
                    DataTotal = table.Column<long>(type: "INTEGER", nullable: false),
                    DownloadSpeed = table.Column<long>(type: "INTEGER", nullable: false),
                    DirectDownloadSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    DownloadClientType = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false, defaultValue: "Direct"),
                    FileTransferSpeed = table.Column<long>(type: "INTEGER", nullable: false),
                    FileDataTransferred = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrentFileTransferBytesOffset = table.Column<long>(type: "INTEGER", nullable: false),
                    Percentage = table.Column<decimal>(type: "TEXT", nullable: false),
                    TimeRemaining = table.Column<int>(type: "INTEGER", nullable: false),
                    DestinationFolderPathId = table.Column<int>(type: "INTEGER", nullable: true),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssetKind = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadTaskPhotoImageFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoImageFiles_DownloadTaskPhotoFolders_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DownloadTaskPhotoFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoImageFiles_IntegrationsRadarr_RadarrIntegrationId",
                        column: x => x.RadarrIntegrationId,
                        principalTable: "IntegrationsRadarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoImageFiles_IntegrationsSonarr_SonarrIntegrationId",
                        column: x => x.SonarrIntegrationId,
                        principalTable: "IntegrationsSonarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoImageFiles_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoImageFiles_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexLibraryGenresAudio",
                columns: table => new
                {
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexGenreId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexLibraryGenresAudio", x => new { x.PlexGenreId, x.PlexLibraryId });
                    table.ForeignKey(
                        name: "FK_PlexLibraryGenresAudio_PlexGenresAudio_PlexGenreId",
                        column: x => x.PlexGenreId,
                        principalTable: "PlexGenresAudio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexLibraryGenresAudio_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaOverviewMusicArtistSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlexArtistId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SearchTitle = table.Column<string>(type: "TEXT", nullable: false),
                    NavigationTitleLabel = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MediaSize = table.Column<long>(type: "INTEGER", nullable: false),
                    TitleRank = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAtRank = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtRank = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationRank = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaSizeRank = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaOverviewMusicArtistSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaOverviewMusicArtistSnapshots_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaOverviewMusicArtistSnapshots_PlexMusicArtists_PlexArtistId",
                        column: x => x.PlexArtistId,
                        principalTable: "PlexMusicArtists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicAlbums",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    SortIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    SearchTitle = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaSize = table.Column<long>(type: "INTEGER", nullable: false),
                    PlexApiMetaDataKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Rating = table.Column<double>(type: "REAL", nullable: false),
                    ChildCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OriginallyAvailableAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HasThumb = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasArt = table.Column<bool>(type: "INTEGER", nullable: false),
                    ParentKey = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexArtistId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiIdentityId = table.Column<string>(type: "TEXT", nullable: true, collation: "BINARY"),
                    MusicBrainzReleaseId = table.Column<string>(type: "TEXT", nullable: true, collation: "BINARY"),
                    RecordLabel = table.Column<string>(type: "TEXT", nullable: true),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicAlbums", x => x.Id);
                    table.UniqueConstraint("AK_PlexMusicAlbums_PlexLibraryId_Id", x => new { x.PlexLibraryId, x.Id });
                    table.ForeignKey(
                        name: "FK_PlexMusicAlbums_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicAlbums_PlexMusicArtists_PlexLibraryId_PlexArtistId",
                        columns: x => new { x.PlexLibraryId, x.PlexArtistId },
                        principalTable: "PlexMusicArtists",
                        principalColumns: new[] { "PlexLibraryId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicAlbums_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicArtistActors",
                columns: table => new
                {
                    PlexActorId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexMusicArtistId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicArtistActors", x => new { x.PlexActorId, x.PlexMusicArtistId });
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistActors_PlexActors_PlexActorId",
                        column: x => x.PlexActorId,
                        principalTable: "PlexActors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistActors_PlexMusicArtists_PlexMusicArtistId",
                        column: x => x.PlexMusicArtistId,
                        principalTable: "PlexMusicArtists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicArtistComparisons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RemotePlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnedPlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    RemotePlexMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnedPlexMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchType = table.Column<int>(type: "INTEGER", nullable: false),
                    ComparedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicArtistComparisons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistComparisons_PlexLibraries_OwnedPlexLibraryId",
                        column: x => x.OwnedPlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistComparisons_PlexLibraries_RemotePlexLibraryId",
                        column: x => x.RemotePlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistComparisons_PlexMusicArtists_OwnedPlexLibraryId_OwnedPlexMediaId",
                        columns: x => new { x.OwnedPlexLibraryId, x.OwnedPlexMediaId },
                        principalTable: "PlexMusicArtists",
                        principalColumns: new[] { "PlexLibraryId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistComparisons_PlexMusicArtists_RemotePlexLibraryId_RemotePlexMediaId",
                        columns: x => new { x.RemotePlexLibraryId, x.RemotePlexMediaId },
                        principalTable: "PlexMusicArtists",
                        principalColumns: new[] { "PlexLibraryId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicArtistCountries",
                columns: table => new
                {
                    CountryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexMusicArtistId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicArtistCountries", x => new { x.CountryId, x.PlexMusicArtistId });
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistCountries_PlexCountries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "PlexCountries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistCountries_PlexMusicArtists_PlexMusicArtistId",
                        column: x => x.PlexMusicArtistId,
                        principalTable: "PlexMusicArtists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicArtistGenres",
                columns: table => new
                {
                    GenresId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexMusicArtistId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicArtistGenres", x => new { x.GenresId, x.PlexMusicArtistId });
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistGenres_PlexGenresAudio_GenresId",
                        column: x => x.GenresId,
                        principalTable: "PlexGenresAudio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistGenres_PlexMusicArtists_PlexMusicArtistId",
                        column: x => x.PlexMusicArtistId,
                        principalTable: "PlexMusicArtists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicArtistMediaQualities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexMusicArtistId = table.Column<int>(type: "INTEGER", nullable: false),
                    AudioQuality = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicArtistMediaQualities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistMediaQualities_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistMediaQualities_PlexMusicArtists_PlexLibraryId_PlexMusicArtistId",
                        columns: x => new { x.PlexLibraryId, x.PlexMusicArtistId },
                        principalTable: "PlexMusicArtists",
                        principalColumns: new[] { "PlexLibraryId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaOverviewOtherVideoSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QualityRank = table.Column<int>(type: "INTEGER", nullable: false),
                    YearRank = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoQuality = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexOtherVideoId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SearchTitle = table.Column<string>(type: "TEXT", nullable: false),
                    NavigationTitleLabel = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MediaSize = table.Column<long>(type: "INTEGER", nullable: false),
                    TitleRank = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAtRank = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtRank = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationRank = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaSizeRank = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaOverviewOtherVideoSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaOverviewOtherVideoSnapshots_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaOverviewOtherVideoSnapshots_PlexOtherVideos_PlexOtherVideoId",
                        column: x => x.PlexOtherVideoId,
                        principalTable: "PlexOtherVideos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexOtherVideoActors",
                columns: table => new
                {
                    PlexActorId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexOtherVideoId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexOtherVideoActors", x => new { x.PlexActorId, x.PlexOtherVideoId });
                    table.ForeignKey(
                        name: "FK_PlexOtherVideoActors_PlexActors_PlexActorId",
                        column: x => x.PlexActorId,
                        principalTable: "PlexActors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexOtherVideoActors_PlexOtherVideos_PlexOtherVideoId",
                        column: x => x.PlexOtherVideoId,
                        principalTable: "PlexOtherVideos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexOtherVideoCountries",
                columns: table => new
                {
                    CountryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexOtherVideoId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexOtherVideoCountries", x => new { x.CountryId, x.PlexOtherVideoId });
                    table.ForeignKey(
                        name: "FK_PlexOtherVideoCountries_PlexCountries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "PlexCountries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexOtherVideoCountries_PlexOtherVideos_PlexOtherVideoId",
                        column: x => x.PlexOtherVideoId,
                        principalTable: "PlexOtherVideos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexOtherVideoData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiPartId = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalFilename = table.Column<string>(type: "TEXT", nullable: false),
                    Container = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoCodec = table.Column<string>(type: "TEXT", nullable: false),
                    AudioCodec = table.Column<string>(type: "TEXT", nullable: false),
                    PlexOtherVideoId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoQuality = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexOtherVideoData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexOtherVideoData_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexOtherVideoData_PlexOtherVideos_PlexLibraryId_PlexOtherVideoId",
                        columns: x => new { x.PlexLibraryId, x.PlexOtherVideoId },
                        principalTable: "PlexOtherVideos",
                        principalColumns: new[] { "PlexLibraryId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexOtherVideoData_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexOtherVideoGenres",
                columns: table => new
                {
                    GenresId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexOtherVideoId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexOtherVideoGenres", x => new { x.GenresId, x.PlexOtherVideoId });
                    table.ForeignKey(
                        name: "FK_PlexOtherVideoGenres_PlexGenresVideo_GenresId",
                        column: x => x.GenresId,
                        principalTable: "PlexGenresVideo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexOtherVideoGenres_PlexOtherVideos_PlexOtherVideoId",
                        column: x => x.PlexOtherVideoId,
                        principalTable: "PlexOtherVideos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaOverviewPhotoAlbumSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlexPhotoAlbumId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SearchTitle = table.Column<string>(type: "TEXT", nullable: false),
                    NavigationTitleLabel = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MediaSize = table.Column<long>(type: "INTEGER", nullable: false),
                    TitleRank = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAtRank = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtRank = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationRank = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaSizeRank = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaOverviewPhotoAlbumSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaOverviewPhotoAlbumSnapshots_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaOverviewPhotoAlbumSnapshots_PlexPhotoAlbums_PlexPhotoAlbumId",
                        column: x => x.PlexPhotoAlbumId,
                        principalTable: "PlexPhotoAlbums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexPhotoMediaData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiPartId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssetKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false, collation: "NATURALSORT"),
                    SearchTitle = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    HasThumb = table.Column<bool>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OriginallyAvailableAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SourcePath = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalFilename = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    Container = table.Column<string>(type: "TEXT", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    Duration = table.Column<int>(type: "INTEGER", nullable: true),
                    VideoCodec = table.Column<string>(type: "TEXT", nullable: true),
                    AudioCodec = table.Column<string>(type: "TEXT", nullable: true),
                    PlexPhotoFolderId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexPhotoMediaData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexPhotoMediaData_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexPhotoMediaData_PlexPhotoFolders_PlexPhotoFolderId",
                        column: x => x.PlexPhotoFolderId,
                        principalTable: "PlexPhotoFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlexPhotoMediaData_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexMovieDataStreams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StreamType = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaDataId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiStreamId = table.Column<int>(type: "INTEGER", nullable: false),
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsExternal = table.Column<bool>(type: "INTEGER", nullable: true),
                    SidecarFormat = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMovieDataStreams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexMovieDataStreams_PlexMovieData_MediaDataId",
                        column: x => x.MediaDataId,
                        principalTable: "PlexMovieData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMovieDataStreams_StreamLanguages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "StreamLanguages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlexTvShowEpisodeDataStreams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StreamType = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexTvShowEpisodeDataId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiStreamId = table.Column<int>(type: "INTEGER", nullable: false),
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsExternal = table.Column<bool>(type: "INTEGER", nullable: true),
                    SidecarFormat = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexTvShowEpisodeDataStreams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexTvShowEpisodeDataStreams_PlexTvShowEpisodeData_PlexTvShowEpisodeDataId",
                        column: x => x.PlexTvShowEpisodeDataId,
                        principalTable: "PlexTvShowEpisodeData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexTvShowEpisodeDataStreams_StreamLanguages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "StreamLanguages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DownloadTaskMusicTracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false, collation: "NATURALSORT"),
                    DownloadStatus = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FullTitle = table.Column<string>(type: "TEXT", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SonarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    RadarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadTaskMusicTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicTracks_DownloadTaskMusicAlbums_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DownloadTaskMusicAlbums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicTracks_IntegrationsRadarr_RadarrIntegrationId",
                        column: x => x.RadarrIntegrationId,
                        principalTable: "IntegrationsRadarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicTracks_IntegrationsSonarr_SonarrIntegrationId",
                        column: x => x.SonarrIntegrationId,
                        principalTable: "IntegrationsSonarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicTracks_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicTracks_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DownloadTaskOtherVideoFileLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false, defaultValue: "Unknown"),
                    LogLevel = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false, defaultValue: "None"),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DownloadTaskFileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DownloadTaskOtherVideoId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadTaskOtherVideoFileLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadTaskOtherVideoFileLogs_DownloadTaskOtherVideoFiles_DownloadTaskFileId",
                        column: x => x.DownloadTaskFileId,
                        principalTable: "DownloadTaskOtherVideoFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskOtherVideoFileLogs_DownloadTaskOtherVideos_DownloadTaskOtherVideoId",
                        column: x => x.DownloadTaskOtherVideoId,
                        principalTable: "DownloadTaskOtherVideos",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DownloadTaskPhotoImageFileLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false, defaultValue: "Unknown"),
                    LogLevel = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false, defaultValue: "None"),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DownloadTaskFileId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadTaskPhotoImageFileLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoImageFileLogs_DownloadTaskPhotoImageFiles_DownloadTaskFileId",
                        column: x => x.DownloadTaskFileId,
                        principalTable: "DownloadTaskPhotoImageFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicAlbumComparisons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RemotePlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnedPlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    RemotePlexMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnedPlexMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchType = table.Column<int>(type: "INTEGER", nullable: false),
                    ComparedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicAlbumComparisons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexMusicAlbumComparisons_PlexLibraries_OwnedPlexLibraryId",
                        column: x => x.OwnedPlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicAlbumComparisons_PlexLibraries_RemotePlexLibraryId",
                        column: x => x.RemotePlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicAlbumComparisons_PlexMusicAlbums_OwnedPlexLibraryId_OwnedPlexMediaId",
                        columns: x => new { x.OwnedPlexLibraryId, x.OwnedPlexMediaId },
                        principalTable: "PlexMusicAlbums",
                        principalColumns: new[] { "PlexLibraryId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicAlbumComparisons_PlexMusicAlbums_RemotePlexLibraryId_RemotePlexMediaId",
                        columns: x => new { x.RemotePlexLibraryId, x.RemotePlexMediaId },
                        principalTable: "PlexMusicAlbums",
                        principalColumns: new[] { "PlexLibraryId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicAlbumCountries",
                columns: table => new
                {
                    CountryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexMusicAlbumId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicAlbumCountries", x => new { x.CountryId, x.PlexMusicAlbumId });
                    table.ForeignKey(
                        name: "FK_PlexMusicAlbumCountries_PlexCountries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "PlexCountries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicAlbumCountries_PlexMusicAlbums_PlexMusicAlbumId",
                        column: x => x.PlexMusicAlbumId,
                        principalTable: "PlexMusicAlbums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicAlbumMediaQualities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexMusicAlbumId = table.Column<int>(type: "INTEGER", nullable: false),
                    AudioQuality = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicAlbumMediaQualities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexMusicAlbumMediaQualities_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicAlbumMediaQualities_PlexMusicAlbums_PlexLibraryId_PlexMusicAlbumId",
                        columns: x => new { x.PlexLibraryId, x.PlexMusicAlbumId },
                        principalTable: "PlexMusicAlbums",
                        principalColumns: new[] { "PlexLibraryId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicTracks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    SortIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    SearchTitle = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaSize = table.Column<long>(type: "INTEGER", nullable: false),
                    PlexApiMetaDataKey = table.Column<int>(type: "INTEGER", nullable: false),
                    ChildCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HasThumb = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasArt = table.Column<bool>(type: "INTEGER", nullable: false),
                    ParentKey = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexAlbumId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiIdentityId = table.Column<string>(type: "TEXT", nullable: true, collation: "BINARY"),
                    MusicBrainzReleaseTrackId = table.Column<string>(type: "TEXT", nullable: true, collation: "BINARY"),
                    DiscNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    TrackNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicTracks", x => x.Id);
                    table.UniqueConstraint("AK_PlexMusicTracks_PlexLibraryId_Id", x => new { x.PlexLibraryId, x.Id });
                    table.ForeignKey(
                        name: "FK_PlexMusicTracks_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicTracks_PlexMusicAlbums_PlexLibraryId_PlexAlbumId",
                        columns: x => new { x.PlexLibraryId, x.PlexAlbumId },
                        principalTable: "PlexMusicAlbums",
                        principalColumns: new[] { "PlexLibraryId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicTracks_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexOtherVideoDataStreams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StreamType = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaDataId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiStreamId = table.Column<int>(type: "INTEGER", nullable: false),
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsExternal = table.Column<bool>(type: "INTEGER", nullable: true),
                    SidecarFormat = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexOtherVideoDataStreams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexOtherVideoDataStreams_PlexOtherVideoData_MediaDataId",
                        column: x => x.MediaDataId,
                        principalTable: "PlexOtherVideoData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexOtherVideoDataStreams_StreamLanguages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "StreamLanguages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlexPhotoAlbumMemberships",
                columns: table => new
                {
                    PlexPhotoAlbumId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexPhotoMediaDataId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexPhotoAlbumMemberships", x => new { x.PlexPhotoAlbumId, x.PlexPhotoMediaDataId });
                    table.ForeignKey(
                        name: "FK_PlexPhotoAlbumMemberships_PlexPhotoAlbums_PlexPhotoAlbumId",
                        column: x => x.PlexPhotoAlbumId,
                        principalTable: "PlexPhotoAlbums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexPhotoAlbumMemberships_PlexPhotoMediaData_PlexPhotoMediaDataId",
                        column: x => x.PlexPhotoMediaDataId,
                        principalTable: "PlexPhotoMediaData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DownloadTaskMusicTrackFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false, collation: "NATURALSORT"),
                    DownloadStatus = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FullTitle = table.Column<string>(type: "TEXT", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    SonarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PlexApiMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    RadarrIntegrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PlexApiPartId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false, collation: "NATURALSORT"),
                    FileLocationUrl = table.Column<string>(type: "TEXT", nullable: false),
                    HashId = table.Column<string>(type: "TEXT", nullable: true),
                    VideoQuality = table.Column<int>(type: "INTEGER", nullable: false),
                    DirectoryMeta = table.Column<string>(type: "TEXT", nullable: false),
                    DataReceived = table.Column<long>(type: "INTEGER", nullable: false),
                    DataTotal = table.Column<long>(type: "INTEGER", nullable: false),
                    DownloadSpeed = table.Column<long>(type: "INTEGER", nullable: false),
                    DirectDownloadSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    DownloadClientType = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false, defaultValue: "Direct"),
                    FileTransferSpeed = table.Column<long>(type: "INTEGER", nullable: false),
                    FileDataTransferred = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrentFileTransferBytesOffset = table.Column<long>(type: "INTEGER", nullable: false),
                    Percentage = table.Column<decimal>(type: "TEXT", nullable: false),
                    TimeRemaining = table.Column<int>(type: "INTEGER", nullable: false),
                    DestinationFolderPathId = table.Column<int>(type: "INTEGER", nullable: true),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AudioQuality = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadTaskMusicTrackFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicTrackFiles_DownloadTaskMusicTracks_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DownloadTaskMusicTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicTrackFiles_IntegrationsRadarr_RadarrIntegrationId",
                        column: x => x.RadarrIntegrationId,
                        principalTable: "IntegrationsRadarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicTrackFiles_IntegrationsSonarr_SonarrIntegrationId",
                        column: x => x.SonarrIntegrationId,
                        principalTable: "IntegrationsSonarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicTrackFiles_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskMusicTrackFiles_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicTrackData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiPartId = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalFilename = table.Column<string>(type: "TEXT", nullable: false),
                    Container = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    AudioCodec = table.Column<string>(type: "TEXT", nullable: false),
                    PlexTrackId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    AudioBitrateKbps = table.Column<int>(type: "INTEGER", nullable: true),
                    AudioBitDepth = table.Column<int>(type: "INTEGER", nullable: true),
                    AudioSampleRateHz = table.Column<int>(type: "INTEGER", nullable: true),
                    AudioChannels = table.Column<int>(type: "INTEGER", nullable: true),
                    AudioChannelLayout = table.Column<string>(type: "TEXT", nullable: true),
                    AudioProfile = table.Column<string>(type: "TEXT", nullable: true),
                    AudioQuality = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicTrackData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexMusicTrackData_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicTrackData_PlexMusicTracks_PlexLibraryId_PlexTrackId",
                        columns: x => new { x.PlexLibraryId, x.PlexTrackId },
                        principalTable: "PlexMusicTracks",
                        principalColumns: new[] { "PlexLibraryId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicTrackData_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DownloadTaskTrackFileLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false, defaultValue: "Unknown"),
                    LogLevel = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false, defaultValue: "None"),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DownloadTaskFileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DownloadTaskTrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DownloadTaskAlbumId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DownloadTaskArtistId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadTaskTrackFileLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadTaskTrackFileLogs_DownloadTaskMusicAlbums_DownloadTaskAlbumId",
                        column: x => x.DownloadTaskAlbumId,
                        principalTable: "DownloadTaskMusicAlbums",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DownloadTaskTrackFileLogs_DownloadTaskMusicArtists_DownloadTaskArtistId",
                        column: x => x.DownloadTaskArtistId,
                        principalTable: "DownloadTaskMusicArtists",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DownloadTaskTrackFileLogs_DownloadTaskMusicTrackFiles_DownloadTaskFileId",
                        column: x => x.DownloadTaskFileId,
                        principalTable: "DownloadTaskMusicTrackFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskTrackFileLogs_DownloadTaskMusicTracks_DownloadTaskTrackId",
                        column: x => x.DownloadTaskTrackId,
                        principalTable: "DownloadTaskMusicTracks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicTrackComparisons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RemotePlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnedPlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    RemotePlexMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnedPlexMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    HitState = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    RemoteAudioQuality = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    OwnedAudioQuality = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    RemotePlexMediaDataId = table.Column<int>(type: "INTEGER", nullable: true),
                    OwnedPlexMediaDataId = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchType = table.Column<int>(type: "INTEGER", nullable: false),
                    ComparedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicTrackComparisons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexMusicTrackComparisons_PlexLibraries_OwnedPlexLibraryId",
                        column: x => x.OwnedPlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicTrackComparisons_PlexLibraries_RemotePlexLibraryId",
                        column: x => x.RemotePlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicTrackComparisons_PlexMusicTrackData_OwnedPlexMediaDataId",
                        column: x => x.OwnedPlexMediaDataId,
                        principalTable: "PlexMusicTrackData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlexMusicTrackComparisons_PlexMusicTrackData_RemotePlexMediaDataId",
                        column: x => x.RemotePlexMediaDataId,
                        principalTable: "PlexMusicTrackData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlexMusicTrackComparisons_PlexMusicTracks_OwnedPlexLibraryId_OwnedPlexMediaId",
                        columns: x => new { x.OwnedPlexLibraryId, x.OwnedPlexMediaId },
                        principalTable: "PlexMusicTracks",
                        principalColumns: new[] { "PlexLibraryId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicTrackComparisons_PlexMusicTracks_RemotePlexLibraryId_RemotePlexMediaId",
                        columns: x => new { x.RemotePlexLibraryId, x.RemotePlexMediaId },
                        principalTable: "PlexMusicTracks",
                        principalColumns: new[] { "PlexLibraryId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicTrackDataStreams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StreamType = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaDataId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiStreamId = table.Column<int>(type: "INTEGER", nullable: false),
                    LanguageId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsExternal = table.Column<bool>(type: "INTEGER", nullable: true),
                    SidecarFormat = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicTrackDataStreams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexMusicTrackDataStreams_PlexMusicTrackData_MediaDataId",
                        column: x => x.MediaDataId,
                        principalTable: "PlexMusicTrackData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicTrackDataStreams_StreamLanguages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "StreamLanguages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShowEpisodeData_PlexTvShowEpisodeId_VideoQuality",
                table: "PlexTvShowEpisodeData",
                columns: new[] { "PlexTvShowEpisodeId", "VideoQuality" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShowEpisodeData_VideoQuality",
                table: "PlexTvShowEpisodeData",
                column: "VideoQuality");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMovieData_PlexMovieId_VideoQuality",
                table: "PlexMovieData",
                columns: new[] { "PlexMovieId", "VideoQuality" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMovieData_VideoQuality",
                table: "PlexMovieData",
                column: "VideoQuality");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicAlbums_DownloadStatus",
                table: "DownloadTaskMusicAlbums",
                column: "DownloadStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicAlbums_ParentId",
                table: "DownloadTaskMusicAlbums",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicAlbums_PlexLibraryId",
                table: "DownloadTaskMusicAlbums",
                column: "PlexLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicAlbums_PlexLibraryId_PlexServerId_PlexApiRatingKey",
                table: "DownloadTaskMusicAlbums",
                columns: new[] { "PlexLibraryId", "PlexServerId", "PlexApiRatingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicAlbums_PlexServerId",
                table: "DownloadTaskMusicAlbums",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicAlbums_RadarrIntegrationId",
                table: "DownloadTaskMusicAlbums",
                column: "RadarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicAlbums_SonarrIntegrationId",
                table: "DownloadTaskMusicAlbums",
                column: "SonarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicArtists_DownloadStatus",
                table: "DownloadTaskMusicArtists",
                column: "DownloadStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicArtists_PlexLibraryId",
                table: "DownloadTaskMusicArtists",
                column: "PlexLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicArtists_PlexLibraryId_PlexServerId_PlexApiRatingKey",
                table: "DownloadTaskMusicArtists",
                columns: new[] { "PlexLibraryId", "PlexServerId", "PlexApiRatingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicArtists_PlexServerId",
                table: "DownloadTaskMusicArtists",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicArtists_RadarrIntegrationId",
                table: "DownloadTaskMusicArtists",
                column: "RadarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicArtists_SonarrIntegrationId",
                table: "DownloadTaskMusicArtists",
                column: "SonarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicTrackFiles_DownloadStatus",
                table: "DownloadTaskMusicTrackFiles",
                column: "DownloadStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicTrackFiles_ParentId",
                table: "DownloadTaskMusicTrackFiles",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicTrackFiles_PlexLibraryId",
                table: "DownloadTaskMusicTrackFiles",
                column: "PlexLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicTrackFiles_PlexLibraryId_PlexServerId_PlexApiRatingKey",
                table: "DownloadTaskMusicTrackFiles",
                columns: new[] { "PlexLibraryId", "PlexServerId", "PlexApiRatingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicTrackFiles_PlexServerId",
                table: "DownloadTaskMusicTrackFiles",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicTrackFiles_RadarrIntegrationId",
                table: "DownloadTaskMusicTrackFiles",
                column: "RadarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicTrackFiles_SonarrIntegrationId",
                table: "DownloadTaskMusicTrackFiles",
                column: "SonarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicTracks_DownloadStatus",
                table: "DownloadTaskMusicTracks",
                column: "DownloadStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicTracks_ParentId",
                table: "DownloadTaskMusicTracks",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicTracks_PlexLibraryId",
                table: "DownloadTaskMusicTracks",
                column: "PlexLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicTracks_PlexLibraryId_PlexServerId_PlexApiRatingKey",
                table: "DownloadTaskMusicTracks",
                columns: new[] { "PlexLibraryId", "PlexServerId", "PlexApiRatingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicTracks_PlexServerId",
                table: "DownloadTaskMusicTracks",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicTracks_RadarrIntegrationId",
                table: "DownloadTaskMusicTracks",
                column: "RadarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskMusicTracks_SonarrIntegrationId",
                table: "DownloadTaskMusicTracks",
                column: "SonarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideoFileLogs_DownloadTaskFileId",
                table: "DownloadTaskOtherVideoFileLogs",
                column: "DownloadTaskFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideoFileLogs_DownloadTaskOtherVideoId",
                table: "DownloadTaskOtherVideoFileLogs",
                column: "DownloadTaskOtherVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideoFiles_DestinationFolderPathId",
                table: "DownloadTaskOtherVideoFiles",
                column: "DestinationFolderPathId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideoFiles_DownloadStatus",
                table: "DownloadTaskOtherVideoFiles",
                column: "DownloadStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideoFiles_ParentId",
                table: "DownloadTaskOtherVideoFiles",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideoFiles_PlexLibraryId",
                table: "DownloadTaskOtherVideoFiles",
                column: "PlexLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideoFiles_PlexLibraryId_PlexServerId_PlexApiRatingKey",
                table: "DownloadTaskOtherVideoFiles",
                columns: new[] { "PlexLibraryId", "PlexServerId", "PlexApiRatingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideoFiles_PlexServerId",
                table: "DownloadTaskOtherVideoFiles",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideoFiles_RadarrIntegrationId",
                table: "DownloadTaskOtherVideoFiles",
                column: "RadarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideoFiles_SonarrIntegrationId",
                table: "DownloadTaskOtherVideoFiles",
                column: "SonarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideos_DownloadStatus",
                table: "DownloadTaskOtherVideos",
                column: "DownloadStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideos_PlexLibraryId",
                table: "DownloadTaskOtherVideos",
                column: "PlexLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideos_PlexLibraryId_PlexServerId_PlexApiRatingKey",
                table: "DownloadTaskOtherVideos",
                columns: new[] { "PlexLibraryId", "PlexServerId", "PlexApiRatingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideos_PlexServerId",
                table: "DownloadTaskOtherVideos",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideos_RadarrIntegrationId",
                table: "DownloadTaskOtherVideos",
                column: "RadarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskOtherVideos_SonarrIntegrationId",
                table: "DownloadTaskOtherVideos",
                column: "SonarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoFolders_DownloadStatus",
                table: "DownloadTaskPhotoFolders",
                column: "DownloadStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoFolders_ParentId",
                table: "DownloadTaskPhotoFolders",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoFolders_ParentId_RelativePath",
                table: "DownloadTaskPhotoFolders",
                columns: new[] { "ParentId", "RelativePath" },
                unique: true,
                filter: "\"ParentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoFolders_PlexLibraryId",
                table: "DownloadTaskPhotoFolders",
                column: "PlexLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoFolders_PlexLibraryId_PlexServerId_PlexApiRatingKey",
                table: "DownloadTaskPhotoFolders",
                columns: new[] { "PlexLibraryId", "PlexServerId", "PlexApiRatingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoFolders_PlexServerId",
                table: "DownloadTaskPhotoFolders",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoFolders_PlexServerId_PlexLibraryId_DownloadRootPath_DestinationRootPath",
                table: "DownloadTaskPhotoFolders",
                columns: new[] { "PlexServerId", "PlexLibraryId", "DownloadRootPath", "DestinationRootPath" },
                unique: true,
                filter: "\"ParentId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoFolders_RadarrIntegrationId",
                table: "DownloadTaskPhotoFolders",
                column: "RadarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoFolders_SonarrIntegrationId",
                table: "DownloadTaskPhotoFolders",
                column: "SonarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImageFileLogs_DownloadTaskFileId",
                table: "DownloadTaskPhotoImageFileLogs",
                column: "DownloadTaskFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImageFiles_DownloadStatus",
                table: "DownloadTaskPhotoImageFiles",
                column: "DownloadStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImageFiles_ParentId",
                table: "DownloadTaskPhotoImageFiles",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImageFiles_PlexLibraryId",
                table: "DownloadTaskPhotoImageFiles",
                column: "PlexLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImageFiles_PlexLibraryId_PlexServerId_PlexApiRatingKey",
                table: "DownloadTaskPhotoImageFiles",
                columns: new[] { "PlexLibraryId", "PlexServerId", "PlexApiRatingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImageFiles_PlexServerId",
                table: "DownloadTaskPhotoImageFiles",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImageFiles_RadarrIntegrationId",
                table: "DownloadTaskPhotoImageFiles",
                column: "RadarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImageFiles_SonarrIntegrationId",
                table: "DownloadTaskPhotoImageFiles",
                column: "SonarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskTrackFileLogs_DownloadTaskAlbumId",
                table: "DownloadTaskTrackFileLogs",
                column: "DownloadTaskAlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskTrackFileLogs_DownloadTaskArtistId",
                table: "DownloadTaskTrackFileLogs",
                column: "DownloadTaskArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskTrackFileLogs_DownloadTaskFileId",
                table: "DownloadTaskTrackFileLogs",
                column: "DownloadTaskFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskTrackFileLogs_DownloadTaskTrackId",
                table: "DownloadTaskTrackFileLogs",
                column: "DownloadTaskTrackId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMusicArtistSnapshots_PlexArtistId",
                table: "MediaOverviewMusicArtistSnapshots",
                column: "PlexArtistId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMusicArtistSnapshots_PlexLibraryId_AddedAtRank",
                table: "MediaOverviewMusicArtistSnapshots",
                columns: new[] { "PlexLibraryId", "AddedAtRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMusicArtistSnapshots_PlexLibraryId_DurationRank",
                table: "MediaOverviewMusicArtistSnapshots",
                columns: new[] { "PlexLibraryId", "DurationRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMusicArtistSnapshots_PlexLibraryId_MediaSizeRank",
                table: "MediaOverviewMusicArtistSnapshots",
                columns: new[] { "PlexLibraryId", "MediaSizeRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMusicArtistSnapshots_PlexLibraryId_TitleRank",
                table: "MediaOverviewMusicArtistSnapshots",
                columns: new[] { "PlexLibraryId", "TitleRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMusicArtistSnapshots_PlexLibraryId_UpdatedAtRank",
                table: "MediaOverviewMusicArtistSnapshots",
                columns: new[] { "PlexLibraryId", "UpdatedAtRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMusicArtistSnapshots_TitleRank_PlexLibraryId_PlexArtistId",
                table: "MediaOverviewMusicArtistSnapshots",
                columns: new[] { "TitleRank", "PlexLibraryId", "PlexArtistId" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewOtherVideoSnapshots_PlexLibraryId_AddedAtRank",
                table: "MediaOverviewOtherVideoSnapshots",
                columns: new[] { "PlexLibraryId", "AddedAtRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewOtherVideoSnapshots_PlexLibraryId_DurationRank",
                table: "MediaOverviewOtherVideoSnapshots",
                columns: new[] { "PlexLibraryId", "DurationRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewOtherVideoSnapshots_PlexLibraryId_MediaSizeRank",
                table: "MediaOverviewOtherVideoSnapshots",
                columns: new[] { "PlexLibraryId", "MediaSizeRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewOtherVideoSnapshots_PlexLibraryId_QualityRank",
                table: "MediaOverviewOtherVideoSnapshots",
                columns: new[] { "PlexLibraryId", "QualityRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewOtherVideoSnapshots_PlexLibraryId_TitleRank",
                table: "MediaOverviewOtherVideoSnapshots",
                columns: new[] { "PlexLibraryId", "TitleRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewOtherVideoSnapshots_PlexLibraryId_UpdatedAtRank",
                table: "MediaOverviewOtherVideoSnapshots",
                columns: new[] { "PlexLibraryId", "UpdatedAtRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewOtherVideoSnapshots_PlexLibraryId_YearRank",
                table: "MediaOverviewOtherVideoSnapshots",
                columns: new[] { "PlexLibraryId", "YearRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewOtherVideoSnapshots_PlexOtherVideoId",
                table: "MediaOverviewOtherVideoSnapshots",
                column: "PlexOtherVideoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewOtherVideoSnapshots_TitleRank_PlexLibraryId_PlexOtherVideoId",
                table: "MediaOverviewOtherVideoSnapshots",
                columns: new[] { "TitleRank", "PlexLibraryId", "PlexOtherVideoId" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewPhotoAlbumSnapshots_PlexLibraryId_AddedAtRank",
                table: "MediaOverviewPhotoAlbumSnapshots",
                columns: new[] { "PlexLibraryId", "AddedAtRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewPhotoAlbumSnapshots_PlexLibraryId_DurationRank",
                table: "MediaOverviewPhotoAlbumSnapshots",
                columns: new[] { "PlexLibraryId", "DurationRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewPhotoAlbumSnapshots_PlexLibraryId_MediaSizeRank",
                table: "MediaOverviewPhotoAlbumSnapshots",
                columns: new[] { "PlexLibraryId", "MediaSizeRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewPhotoAlbumSnapshots_PlexLibraryId_TitleRank",
                table: "MediaOverviewPhotoAlbumSnapshots",
                columns: new[] { "PlexLibraryId", "TitleRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewPhotoAlbumSnapshots_PlexLibraryId_UpdatedAtRank",
                table: "MediaOverviewPhotoAlbumSnapshots",
                columns: new[] { "PlexLibraryId", "UpdatedAtRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewPhotoAlbumSnapshots_PlexPhotoAlbumId",
                table: "MediaOverviewPhotoAlbumSnapshots",
                column: "PlexPhotoAlbumId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewPhotoAlbumSnapshots_TitleRank_PlexLibraryId_PlexPhotoAlbumId",
                table: "MediaOverviewPhotoAlbumSnapshots",
                columns: new[] { "TitleRank", "PlexLibraryId", "PlexPhotoAlbumId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexGenresAudio_Key",
                table: "PlexGenresAudio",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexLibraryGenresAudio_PlexLibraryId",
                table: "PlexLibraryGenresAudio",
                column: "PlexLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMovieDataStreams_LanguageId_StreamType_MediaDataId",
                table: "PlexMovieDataStreams",
                columns: new[] { "LanguageId", "StreamType", "MediaDataId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMovieDataStreams_MediaDataId_PlexApiStreamId",
                table: "PlexMovieDataStreams",
                columns: new[] { "MediaDataId", "PlexApiStreamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbumComparisons_OwnedPlexLibraryId_OwnedPlexMediaId",
                table: "PlexMusicAlbumComparisons",
                columns: new[] { "OwnedPlexLibraryId", "OwnedPlexMediaId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbumComparisons_RemotePlexLibraryId_OwnedPlexLibraryId_RemotePlexMediaId_OwnedPlexMediaId",
                table: "PlexMusicAlbumComparisons",
                columns: new[] { "RemotePlexLibraryId", "OwnedPlexLibraryId", "RemotePlexMediaId", "OwnedPlexMediaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbumComparisons_RemotePlexLibraryId_RemotePlexMediaId",
                table: "PlexMusicAlbumComparisons",
                columns: new[] { "RemotePlexLibraryId", "RemotePlexMediaId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbumCountries_PlexMusicAlbumId",
                table: "PlexMusicAlbumCountries",
                column: "PlexMusicAlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbumMediaQualities_PlexLibraryId_PlexMusicAlbumId_AudioQuality",
                table: "PlexMusicAlbumMediaQualities",
                columns: new[] { "PlexLibraryId", "PlexMusicAlbumId", "AudioQuality" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbums_PlexApiRatingKey_PlexServerId",
                table: "PlexMusicAlbums",
                columns: new[] { "PlexApiRatingKey", "PlexServerId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbums_PlexLibraryId_PlexApiIdentityId",
                table: "PlexMusicAlbums",
                columns: new[] { "PlexLibraryId", "PlexApiIdentityId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbums_PlexLibraryId_PlexApiRatingKey",
                table: "PlexMusicAlbums",
                columns: new[] { "PlexLibraryId", "PlexApiRatingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbums_PlexLibraryId_PlexArtistId_SortIndex_Id",
                table: "PlexMusicAlbums",
                columns: new[] { "PlexLibraryId", "PlexArtistId", "SortIndex", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbums_PlexLibraryId_SearchTitle",
                table: "PlexMusicAlbums",
                columns: new[] { "PlexLibraryId", "SearchTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbums_PlexLibraryId_SortIndex",
                table: "PlexMusicAlbums",
                columns: new[] { "PlexLibraryId", "SortIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbums_PlexServerId",
                table: "PlexMusicAlbums",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtistActors_PlexMusicArtistId",
                table: "PlexMusicArtistActors",
                column: "PlexMusicArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtistComparisons_OwnedPlexLibraryId_OwnedPlexMediaId",
                table: "PlexMusicArtistComparisons",
                columns: new[] { "OwnedPlexLibraryId", "OwnedPlexMediaId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtistComparisons_RemotePlexLibraryId_OwnedPlexLibraryId_RemotePlexMediaId_OwnedPlexMediaId",
                table: "PlexMusicArtistComparisons",
                columns: new[] { "RemotePlexLibraryId", "OwnedPlexLibraryId", "RemotePlexMediaId", "OwnedPlexMediaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtistComparisons_RemotePlexLibraryId_RemotePlexMediaId",
                table: "PlexMusicArtistComparisons",
                columns: new[] { "RemotePlexLibraryId", "RemotePlexMediaId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtistCountries_PlexMusicArtistId",
                table: "PlexMusicArtistCountries",
                column: "PlexMusicArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtistGenres_PlexMusicArtistId",
                table: "PlexMusicArtistGenres",
                column: "PlexMusicArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtistMediaQualities_PlexLibraryId_PlexMusicArtistId_AudioQuality",
                table: "PlexMusicArtistMediaQualities",
                columns: new[] { "PlexLibraryId", "PlexMusicArtistId", "AudioQuality" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtists_PlexApiRatingKey_PlexServerId",
                table: "PlexMusicArtists",
                columns: new[] { "PlexApiRatingKey", "PlexServerId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtists_PlexLibraryId_MusicBrainzArtistId",
                table: "PlexMusicArtists",
                columns: new[] { "PlexLibraryId", "MusicBrainzArtistId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtists_PlexLibraryId_PlexApiIdentityId",
                table: "PlexMusicArtists",
                columns: new[] { "PlexLibraryId", "PlexApiIdentityId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtists_PlexLibraryId_PlexApiRatingKey",
                table: "PlexMusicArtists",
                columns: new[] { "PlexLibraryId", "PlexApiRatingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtists_PlexLibraryId_SearchTitle",
                table: "PlexMusicArtists",
                columns: new[] { "PlexLibraryId", "SearchTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtists_PlexLibraryId_SortIndex",
                table: "PlexMusicArtists",
                columns: new[] { "PlexLibraryId", "SortIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtists_PlexServerId",
                table: "PlexMusicArtists",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackComparisons_OwnedPlexLibraryId_OwnedPlexMediaId",
                table: "PlexMusicTrackComparisons",
                columns: new[] { "OwnedPlexLibraryId", "OwnedPlexMediaId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackComparisons_OwnedPlexMediaDataId",
                table: "PlexMusicTrackComparisons",
                column: "OwnedPlexMediaDataId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackComparisons_RemotePlexLibraryId_OwnedPlexLibraryId_RemotePlexMediaId_OwnedPlexMediaId",
                table: "PlexMusicTrackComparisons",
                columns: new[] { "RemotePlexLibraryId", "OwnedPlexLibraryId", "RemotePlexMediaId", "OwnedPlexMediaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackComparisons_RemotePlexLibraryId_RemotePlexMediaId",
                table: "PlexMusicTrackComparisons",
                columns: new[] { "RemotePlexLibraryId", "RemotePlexMediaId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackComparisons_RemotePlexMediaDataId",
                table: "PlexMusicTrackComparisons",
                column: "RemotePlexMediaDataId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackData_PlexLibraryId_PlexApiPartId",
                table: "PlexMusicTrackData",
                columns: new[] { "PlexLibraryId", "PlexApiPartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackData_PlexLibraryId_PlexTrackId",
                table: "PlexMusicTrackData",
                columns: new[] { "PlexLibraryId", "PlexTrackId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackData_PlexServerId",
                table: "PlexMusicTrackData",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackData_PlexTrackId_PlexApiMediaId_PartIndex",
                table: "PlexMusicTrackData",
                columns: new[] { "PlexTrackId", "PlexApiMediaId", "PartIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackDataStreams_LanguageId_StreamType_MediaDataId",
                table: "PlexMusicTrackDataStreams",
                columns: new[] { "LanguageId", "StreamType", "MediaDataId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackDataStreams_MediaDataId_PlexApiStreamId",
                table: "PlexMusicTrackDataStreams",
                columns: new[] { "MediaDataId", "PlexApiStreamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTracks_PlexApiRatingKey_PlexServerId",
                table: "PlexMusicTracks",
                columns: new[] { "PlexApiRatingKey", "PlexServerId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTracks_PlexLibraryId_PlexAlbumId_DiscNumber_TrackNumber_Id",
                table: "PlexMusicTracks",
                columns: new[] { "PlexLibraryId", "PlexAlbumId", "DiscNumber", "TrackNumber", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTracks_PlexLibraryId_PlexApiIdentityId",
                table: "PlexMusicTracks",
                columns: new[] { "PlexLibraryId", "PlexApiIdentityId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTracks_PlexLibraryId_PlexApiRatingKey",
                table: "PlexMusicTracks",
                columns: new[] { "PlexLibraryId", "PlexApiRatingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTracks_PlexLibraryId_SearchTitle",
                table: "PlexMusicTracks",
                columns: new[] { "PlexLibraryId", "SearchTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTracks_PlexLibraryId_SortIndex",
                table: "PlexMusicTracks",
                columns: new[] { "PlexLibraryId", "SortIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTracks_PlexServerId",
                table: "PlexMusicTracks",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideoActors_PlexOtherVideoId",
                table: "PlexOtherVideoActors",
                column: "PlexOtherVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideoCountries_PlexOtherVideoId",
                table: "PlexOtherVideoCountries",
                column: "PlexOtherVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideoData_PlexLibraryId_PlexApiPartId",
                table: "PlexOtherVideoData",
                columns: new[] { "PlexLibraryId", "PlexApiPartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideoData_PlexLibraryId_PlexOtherVideoId",
                table: "PlexOtherVideoData",
                columns: new[] { "PlexLibraryId", "PlexOtherVideoId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideoData_PlexOtherVideoId_PlexApiMediaId_PartIndex",
                table: "PlexOtherVideoData",
                columns: new[] { "PlexOtherVideoId", "PlexApiMediaId", "PartIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideoData_PlexServerId",
                table: "PlexOtherVideoData",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideoDataStreams_LanguageId_StreamType_MediaDataId",
                table: "PlexOtherVideoDataStreams",
                columns: new[] { "LanguageId", "StreamType", "MediaDataId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideoDataStreams_MediaDataId_PlexApiStreamId",
                table: "PlexOtherVideoDataStreams",
                columns: new[] { "MediaDataId", "PlexApiStreamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideoGenres_PlexOtherVideoId",
                table: "PlexOtherVideoGenres",
                column: "PlexOtherVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideos_PlexApiRatingKey_PlexServerId",
                table: "PlexOtherVideos",
                columns: new[] { "PlexApiRatingKey", "PlexServerId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideos_PlexLibraryId_PlexApiRatingKey",
                table: "PlexOtherVideos",
                columns: new[] { "PlexLibraryId", "PlexApiRatingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideos_PlexLibraryId_SearchTitle",
                table: "PlexOtherVideos",
                columns: new[] { "PlexLibraryId", "SearchTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideos_PlexLibraryId_SortIndex",
                table: "PlexOtherVideos",
                columns: new[] { "PlexLibraryId", "SortIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideos_PlexServerId",
                table: "PlexOtherVideos",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoAlbumMemberships_PlexPhotoMediaDataId_PlexPhotoAlbumId",
                table: "PlexPhotoAlbumMemberships",
                columns: new[] { "PlexPhotoMediaDataId", "PlexPhotoAlbumId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoAlbums_PlexApiRatingKey_PlexServerId",
                table: "PlexPhotoAlbums",
                columns: new[] { "PlexApiRatingKey", "PlexServerId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoAlbums_PlexLibraryId_PlexApiRatingKey",
                table: "PlexPhotoAlbums",
                columns: new[] { "PlexLibraryId", "PlexApiRatingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoAlbums_PlexLibraryId_SearchTitle",
                table: "PlexPhotoAlbums",
                columns: new[] { "PlexLibraryId", "SearchTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoAlbums_PlexLibraryId_Title_Id",
                table: "PlexPhotoAlbums",
                columns: new[] { "PlexLibraryId", "Title", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoAlbums_PlexServerId",
                table: "PlexPhotoAlbums",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoFolders_ParentId_RelativePath",
                table: "PlexPhotoFolders",
                columns: new[] { "ParentId", "RelativePath" },
                unique: true,
                filter: "\"ParentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoFolders_PlexLibraryId_ParentId_Title_Id",
                table: "PlexPhotoFolders",
                columns: new[] { "PlexLibraryId", "ParentId", "Title", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoFolders_PlexLibraryId_RootIdentityKey",
                table: "PlexPhotoFolders",
                columns: new[] { "PlexLibraryId", "RootIdentityKey" },
                unique: true,
                filter: "\"ParentId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoFolders_PlexServerId",
                table: "PlexPhotoFolders",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoMediaData_PlexApiRatingKey_PlexServerId",
                table: "PlexPhotoMediaData",
                columns: new[] { "PlexApiRatingKey", "PlexServerId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoMediaData_PlexLibraryId_PlexApiPartId",
                table: "PlexPhotoMediaData",
                columns: new[] { "PlexLibraryId", "PlexApiPartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoMediaData_PlexLibraryId_PlexApiRatingKey",
                table: "PlexPhotoMediaData",
                columns: new[] { "PlexLibraryId", "PlexApiRatingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoMediaData_PlexLibraryId_PlexPhotoFolderId_Title_Id",
                table: "PlexPhotoMediaData",
                columns: new[] { "PlexLibraryId", "PlexPhotoFolderId", "Title", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoMediaData_PlexLibraryId_SearchTitle",
                table: "PlexPhotoMediaData",
                columns: new[] { "PlexLibraryId", "SearchTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoMediaData_PlexLibraryId_Title_Id",
                table: "PlexPhotoMediaData",
                columns: new[] { "PlexLibraryId", "Title", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoMediaData_PlexPhotoFolderId",
                table: "PlexPhotoMediaData",
                column: "PlexPhotoFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoMediaData_PlexServerId",
                table: "PlexPhotoMediaData",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShowEpisodeDataStreams_LanguageId_StreamType_PlexTvShowEpisodeDataId",
                table: "PlexTvShowEpisodeDataStreams",
                columns: new[] { "LanguageId", "StreamType", "PlexTvShowEpisodeDataId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShowEpisodeDataStreams_PlexTvShowEpisodeDataId_PlexApiStreamId",
                table: "PlexTvShowEpisodeDataStreams",
                columns: new[] { "PlexTvShowEpisodeDataId", "PlexApiStreamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StreamLanguages_LanguageCode_LanguageTag",
                table: "StreamLanguages",
                columns: new[] { "LanguageCode", "LanguageTag" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PlexMovieGenres_PlexGenresVideo_GenresId",
                table: "PlexMovieGenres",
                column: "GenresId",
                principalTable: "PlexGenresVideo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlexTvShowGenres_PlexGenresVideo_GenresId",
                table: "PlexTvShowGenres",
                column: "GenresId",
                principalTable: "PlexGenresVideo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql(
                $"""
                UPDATE "FolderPaths" SET "{nameof(FolderPath.MediaType)}" = 'MusicArtist' WHERE "{nameof(FolderPath.MediaType)}" = 'Music';
                UPDATE "FolderPaths" SET "{nameof(FolderPath.MediaType)}" = 'PhotoAlbum' WHERE "{nameof(FolderPath.MediaType)}" = 'Photos';
                UPDATE "PlexLibraries" SET "{nameof(PlexLibrary.Type)}" = 'MusicArtist' WHERE "{nameof(PlexLibrary.Type)}" = 'Music';
                UPDATE "PlexLibraries" SET "{nameof(PlexLibrary.Type)}" = 'PhotoAlbum' WHERE "{nameof(PlexLibrary.Type)}" = 'Photos';
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlexMovieGenres_PlexGenresVideo_GenresId",
                table: "PlexMovieGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_PlexTvShowGenres_PlexGenresVideo_GenresId",
                table: "PlexTvShowGenres");

            migrationBuilder.DropTable(
                name: "DownloadTaskOtherVideoFileLogs");

            migrationBuilder.DropTable(
                name: "DownloadTaskPhotoImageFileLogs");

            migrationBuilder.DropTable(
                name: "DownloadTaskTrackFileLogs");

            migrationBuilder.DropTable(
                name: "MediaOverviewMusicArtistSnapshots");

            migrationBuilder.DropTable(
                name: "MediaOverviewOtherVideoSnapshots");

            migrationBuilder.DropTable(
                name: "MediaOverviewPhotoAlbumSnapshots");

            migrationBuilder.DropTable(
                name: "PlexLibraryGenresAudio");

            migrationBuilder.DropTable(
                name: "PlexMovieDataStreams");

            migrationBuilder.DropTable(
                name: "PlexMusicAlbumComparisons");

            migrationBuilder.DropTable(
                name: "PlexMusicAlbumCountries");

            migrationBuilder.DropTable(
                name: "PlexMusicAlbumMediaQualities");

            migrationBuilder.DropTable(
                name: "PlexMusicArtistActors");

            migrationBuilder.DropTable(
                name: "PlexMusicArtistComparisons");

            migrationBuilder.DropTable(
                name: "PlexMusicArtistCountries");

            migrationBuilder.DropTable(
                name: "PlexMusicArtistGenres");

            migrationBuilder.DropTable(
                name: "PlexMusicArtistMediaQualities");

            migrationBuilder.DropTable(
                name: "PlexMusicTrackComparisons");

            migrationBuilder.DropTable(
                name: "PlexMusicTrackDataStreams");

            migrationBuilder.DropTable(
                name: "PlexOtherVideoActors");

            migrationBuilder.DropTable(
                name: "PlexOtherVideoCountries");

            migrationBuilder.DropTable(
                name: "PlexOtherVideoDataStreams");

            migrationBuilder.DropTable(
                name: "PlexOtherVideoGenres");

            migrationBuilder.DropTable(
                name: "PlexPhotoAlbumMemberships");

            migrationBuilder.DropTable(
                name: "PlexTvShowEpisodeDataStreams");

            migrationBuilder.DropTable(
                name: "DownloadTaskOtherVideoFiles");

            migrationBuilder.DropTable(
                name: "DownloadTaskPhotoImageFiles");

            migrationBuilder.DropTable(
                name: "DownloadTaskMusicTrackFiles");

            migrationBuilder.DropTable(
                name: "PlexGenresAudio");

            migrationBuilder.DropTable(
                name: "PlexMusicTrackData");

            migrationBuilder.DropTable(
                name: "PlexOtherVideoData");

            migrationBuilder.DropTable(
                name: "PlexPhotoAlbums");

            migrationBuilder.DropTable(
                name: "PlexPhotoMediaData");

            migrationBuilder.DropTable(
                name: "StreamLanguages");

            migrationBuilder.DropTable(
                name: "DownloadTaskOtherVideos");

            migrationBuilder.DropTable(
                name: "DownloadTaskPhotoFolders");

            migrationBuilder.DropTable(
                name: "DownloadTaskMusicTracks");

            migrationBuilder.DropTable(
                name: "PlexMusicTracks");

            migrationBuilder.DropTable(
                name: "PlexOtherVideos");

            migrationBuilder.DropTable(
                name: "PlexPhotoFolders");

            migrationBuilder.DropTable(
                name: "DownloadTaskMusicAlbums");

            migrationBuilder.DropTable(
                name: "PlexMusicAlbums");

            migrationBuilder.DropTable(
                name: "DownloadTaskMusicArtists");

            migrationBuilder.DropTable(
                name: "PlexMusicArtists");

            migrationBuilder.DropIndex(
                name: "IX_PlexTvShowEpisodeData_PlexTvShowEpisodeId_VideoQuality",
                table: "PlexTvShowEpisodeData");

            migrationBuilder.DropIndex(
                name: "IX_PlexTvShowEpisodeData_VideoQuality",
                table: "PlexTvShowEpisodeData");

            migrationBuilder.DropIndex(
                name: "IX_PlexMovieData_PlexMovieId_VideoQuality",
                table: "PlexMovieData");

            migrationBuilder.DropIndex(
                name: "IX_PlexMovieData_VideoQuality",
                table: "PlexMovieData");

            migrationBuilder.DropColumn(
                name: "MusicAlbumCount",
                table: "PlexLibraries");

            migrationBuilder.DropColumn(
                name: "MusicArtistCount",
                table: "PlexLibraries");

            migrationBuilder.DropColumn(
                name: "MusicTrackCount",
                table: "PlexLibraries");

            migrationBuilder.DropColumn(
                name: "OtherVideoCount",
                table: "PlexLibraries");

            migrationBuilder.DropColumn(
                name: "PhotoAlbumCount",
                table: "PlexLibraries");

            migrationBuilder.DropColumn(
                name: "PhotoClipCount",
                table: "PlexLibraries");

            migrationBuilder.DropColumn(
                name: "PhotoImageCount",
                table: "PlexLibraries");

            migrationBuilder.RenameColumn(
                name: "VideoQuality",
                table: "PlexTvShowSeasonMediaQualities",
                newName: "Quality");

            migrationBuilder.RenameColumn(
                name: "VideoQuality",
                table: "PlexTvShows",
                newName: "Quality");

            migrationBuilder.RenameIndex(
                name: "IX_PlexTvShows_VideoQuality",
                table: "PlexTvShows",
                newName: "IX_PlexTvShows_Quality");

            migrationBuilder.RenameIndex(
                name: "IX_PlexTvShows_PlexLibraryId_VideoQuality",
                table: "PlexTvShows",
                newName: "IX_PlexTvShows_PlexLibraryId_Quality");

            migrationBuilder.RenameColumn(
                name: "VideoQuality",
                table: "PlexTvShowMediaQualities",
                newName: "Quality");

            migrationBuilder.RenameColumn(
                name: "VideoQuality",
                table: "PlexTvShowEpisodeData",
                newName: "VideoResolution");

            migrationBuilder.RenameColumn(
                name: "VideoQuality",
                table: "PlexMovieData",
                newName: "VideoResolution");

            migrationBuilder.RenameColumn(
                name: "VideoQuality",
                table: "PlexMovie",
                newName: "Quality");

            migrationBuilder.RenameIndex(
                name: "IX_PlexMovie_VideoQuality",
                table: "PlexMovie",
                newName: "IX_PlexMovie_Quality");

            migrationBuilder.RenameIndex(
                name: "IX_PlexMovie_PlexLibraryId_VideoQuality",
                table: "PlexMovie",
                newName: "IX_PlexMovie_PlexLibraryId_Quality");

            migrationBuilder.RenameColumn(
                name: "VideoQuality",
                table: "DownloadTaskTvShowEpisodeFile",
                newName: "Quality");

            migrationBuilder.RenameColumn(
                name: "VideoQuality",
                table: "DownloadTaskMovieFile",
                newName: "Quality");

            migrationBuilder.AlterColumn<int>(
                name: "VideoResolution",
                table: "PlexTvShowEpisodeData",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 4);

            migrationBuilder.AddColumn<int>(
                name: "Quality",
                table: "PlexTvShowEpisodeData",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "VideoResolution",
                table: "PlexMovieData",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 4);

            migrationBuilder.AddColumn<int>(
                name: "Quality",
                table: "PlexMovieData",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "MediaCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                computedColumnSql: "CASE WHEN Type = 'Movie' THEN MovieCount WHEN Type = 'TvShow' THEN TvShowCount ELSE -1 END",
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldComputedColumnSql: "CASE WHEN Type = 'Movie' THEN MovieCount WHEN Type = 'TvShow' THEN TvShowCount WHEN Type = 'MusicArtist' THEN MusicArtistCount WHEN Type = 'PhotoAlbum' THEN PhotoAlbumCount WHEN Type = 'OtherVideos' THEN OtherVideoCount ELSE -1 END");

            migrationBuilder.DropForeignKey(
                name: "FK_PlexLibraryGenresVideo_PlexGenresVideo_PlexGenreId",
                table: "PlexLibraryGenresVideo");

            migrationBuilder.DropForeignKey(
                name: "FK_PlexLibraryGenresVideo_PlexLibraries_PlexLibraryId",
                table: "PlexLibraryGenresVideo");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlexLibraryGenresVideo",
                table: "PlexLibraryGenresVideo");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlexGenresVideo",
                table: "PlexGenresVideo");

            migrationBuilder.RenameTable(
                name: "PlexLibraryGenresVideo",
                newName: "PlexLibraryGenres");

            migrationBuilder.RenameTable(
                name: "PlexGenresVideo",
                newName: "PlexGenres");

            migrationBuilder.RenameIndex(
                name: "IX_PlexGenresVideo_Key",
                table: "PlexGenres",
                newName: "IX_PlexGenres_Key");

            migrationBuilder.RenameIndex(
                name: "IX_PlexLibraryGenresVideo_PlexLibraryId",
                table: "PlexLibraryGenres",
                newName: "IX_PlexLibraryGenres_PlexLibraryId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlexGenres",
                table: "PlexGenres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlexLibraryGenres",
                table: "PlexLibraryGenres",
                columns: new[] { "PlexGenreId", "PlexLibraryId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PlexLibraryGenres_PlexGenres_PlexGenreId",
                table: "PlexLibraryGenres",
                column: "PlexGenreId",
                principalTable: "PlexGenres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlexLibraryGenres_PlexLibraries_PlexLibraryId",
                table: "PlexLibraryGenres",
                column: "PlexLibraryId",
                principalTable: "PlexLibraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShowSeason_TvShowId",
                table: "PlexTvShowSeason",
                column: "TvShowId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShowEpisodeData_PlexTvShowEpisodeId_Quality",
                table: "PlexTvShowEpisodeData",
                columns: new[] { "PlexTvShowEpisodeId", "Quality" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShowEpisodeData_Quality",
                table: "PlexTvShowEpisodeData",
                column: "Quality");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMovieData_PlexMovieId_Quality",
                table: "PlexMovieData",
                columns: new[] { "PlexMovieId", "Quality" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMovieData_Quality",
                table: "PlexMovieData",
                column: "Quality");

            migrationBuilder.AddForeignKey(
                name: "FK_PlexMovieGenres_PlexGenres_GenresId",
                table: "PlexMovieGenres",
                column: "GenresId",
                principalTable: "PlexGenres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlexTvShowGenres_PlexGenres_GenresId",
                table: "PlexTvShowGenres",
                column: "GenresId",
                principalTable: "PlexGenres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // The preceding schema stored resolution in both Quality and VideoResolution.
            // Restore that duplicated value instead of leaving the recreated Quality at zero.
            migrationBuilder.Sql(
                $"""
                UPDATE "FolderPaths" SET "{nameof(FolderPath.MediaType)}" = 'Music' WHERE "{nameof(FolderPath.MediaType)}" = 'MusicArtist';
                UPDATE "FolderPaths" SET "{nameof(FolderPath.MediaType)}" = 'Photos' WHERE "{nameof(FolderPath.MediaType)}" = 'PhotoAlbum';
                UPDATE "PlexLibraries" SET "{nameof(PlexLibrary.Type)}" = 'Music' WHERE "{nameof(PlexLibrary.Type)}" = 'MusicArtist';
                UPDATE "PlexLibraries" SET "{nameof(PlexLibrary.Type)}" = 'Photos' WHERE "{nameof(PlexLibrary.Type)}" = 'PhotoAlbum';
                UPDATE "PlexMovieData" SET "Quality" = "VideoResolution";
                UPDATE "PlexTvShowEpisodeData" SET "Quality" = "VideoResolution";
                """
            );
        }
    }
}
