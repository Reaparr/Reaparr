using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reaparr.Data.Migrations
{
    /// <inheritdoc />
    public partial class TEMP : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "DownloadTaskPhotoAlbums",
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
                    table.PrimaryKey("PK_DownloadTaskPhotoAlbums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoAlbums_IntegrationsRadarr_RadarrIntegrationId",
                        column: x => x.RadarrIntegrationId,
                        principalTable: "IntegrationsRadarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoAlbums_IntegrationsSonarr_SonarrIntegrationId",
                        column: x => x.SonarrIntegrationId,
                        principalTable: "IntegrationsSonarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoAlbums_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoAlbums_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexArtists",
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
                    Studio = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ContentRating = table.Column<string>(type: "TEXT", nullable: false),
                    Rating = table.Column<double>(type: "REAL", nullable: false),
                    ChildCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OriginallyAvailableAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Quality = table.Column<int>(type: "INTEGER", nullable: false),
                    HasThumb = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasArt = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasTheme = table.Column<bool>(type: "INTEGER", nullable: false),
                    FullTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Guid = table.Column<string>(type: "TEXT", nullable: false),
                    Guid_IMDB = table.Column<string>(type: "TEXT", nullable: true),
                    Guid_TMDB = table.Column<int>(type: "INTEGER", nullable: true),
                    Guid_TVDB = table.Column<int>(type: "INTEGER", nullable: true),
                    MusicBrainzArtistId = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE"),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexArtists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexArtists_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexArtists_PlexServers_PlexServerId",
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
                    Studio = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ContentRating = table.Column<string>(type: "TEXT", nullable: false),
                    Rating = table.Column<double>(type: "REAL", nullable: false),
                    ChildCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OriginallyAvailableAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Quality = table.Column<int>(type: "INTEGER", nullable: false),
                    HasThumb = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasArt = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasTheme = table.Column<bool>(type: "INTEGER", nullable: false),
                    FullTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Guid = table.Column<string>(type: "TEXT", nullable: false),
                    Guid_IMDB = table.Column<string>(type: "TEXT", nullable: true),
                    Guid_TMDB = table.Column<int>(type: "INTEGER", nullable: true),
                    Guid_TVDB = table.Column<int>(type: "INTEGER", nullable: true),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexOtherVideos", x => x.Id);
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
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    SortIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    SearchTitle = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaSize = table.Column<long>(type: "INTEGER", nullable: false),
                    PlexApiMetaDataKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Studio = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ContentRating = table.Column<string>(type: "TEXT", nullable: false),
                    Rating = table.Column<double>(type: "REAL", nullable: false),
                    ChildCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OriginallyAvailableAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Quality = table.Column<int>(type: "INTEGER", nullable: false),
                    HasThumb = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasArt = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasTheme = table.Column<bool>(type: "INTEGER", nullable: false),
                    FullTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Guid = table.Column<string>(type: "TEXT", nullable: false),
                    Guid_IMDB = table.Column<string>(type: "TEXT", nullable: true),
                    Guid_TMDB = table.Column<int>(type: "INTEGER", nullable: true),
                    Guid_TVDB = table.Column<int>(type: "INTEGER", nullable: true),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false)
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
                    Quality = table.Column<int>(type: "INTEGER", nullable: false),
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
                name: "DownloadTaskPhotoImages",
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
                    table.PrimaryKey("PK_DownloadTaskPhotoImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoImages_DownloadTaskPhotoAlbums_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DownloadTaskPhotoAlbums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoImages_IntegrationsRadarr_RadarrIntegrationId",
                        column: x => x.RadarrIntegrationId,
                        principalTable: "IntegrationsRadarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoImages_IntegrationsSonarr_SonarrIntegrationId",
                        column: x => x.SonarrIntegrationId,
                        principalTable: "IntegrationsSonarr",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoImages_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoImages_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
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
                    TitleRank = table.Column<int>(type: "INTEGER", nullable: false),
                    YearRank = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAtRank = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtRank = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationRank = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaSizeRank = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaOverviewMusicArtistSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaOverviewMusicArtistSnapshots_PlexArtists_PlexArtistId",
                        column: x => x.PlexArtistId,
                        principalTable: "PlexArtists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaOverviewMusicArtistSnapshots_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexAlbums",
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
                    Studio = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ContentRating = table.Column<string>(type: "TEXT", nullable: false),
                    Rating = table.Column<double>(type: "REAL", nullable: false),
                    ChildCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OriginallyAvailableAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Quality = table.Column<int>(type: "INTEGER", nullable: false),
                    HasThumb = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasArt = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasTheme = table.Column<bool>(type: "INTEGER", nullable: false),
                    FullTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Guid = table.Column<string>(type: "TEXT", nullable: false),
                    Guid_IMDB = table.Column<string>(type: "TEXT", nullable: true),
                    Guid_TMDB = table.Column<int>(type: "INTEGER", nullable: true),
                    Guid_TVDB = table.Column<int>(type: "INTEGER", nullable: true),
                    ParentKey = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexArtistId = table.Column<int>(type: "INTEGER", nullable: false),
                    MusicBrainzReleaseId = table.Column<string>(type: "TEXT", nullable: true),
                    MusicBrainzReleaseGroupId = table.Column<string>(type: "TEXT", nullable: true),
                    ReleaseDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RecordLabel = table.Column<string>(type: "TEXT", nullable: true),
                    Country = table.Column<string>(type: "TEXT", nullable: true),
                    DiscCount = table.Column<int>(type: "INTEGER", nullable: true),
                    TrackCount = table.Column<int>(type: "INTEGER", nullable: true),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexAlbums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexAlbums_PlexArtists_PlexArtistId",
                        column: x => x.PlexArtistId,
                        principalTable: "PlexArtists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexAlbums_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexAlbums_PlexServers_PlexServerId",
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
                    PlexMusicArtistId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false)
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
                        name: "FK_PlexMusicArtistActors_PlexArtists_PlexMusicArtistId",
                        column: x => x.PlexMusicArtistId,
                        principalTable: "PlexArtists",
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
                        name: "FK_PlexMusicArtistComparisons_PlexArtists_OwnedPlexMediaId",
                        column: x => x.OwnedPlexMediaId,
                        principalTable: "PlexArtists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistComparisons_PlexArtists_RemotePlexMediaId",
                        column: x => x.RemotePlexMediaId,
                        principalTable: "PlexArtists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicArtistCountries",
                columns: table => new
                {
                    CountryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexMusicArtistId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicArtistCountries", x => new { x.CountryId, x.PlexMusicArtistId });
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistCountries_PlexArtists_PlexMusicArtistId",
                        column: x => x.PlexMusicArtistId,
                        principalTable: "PlexArtists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistCountries_PlexCountries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "PlexCountries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexMusicArtistGenres",
                columns: table => new
                {
                    GenresId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexMusicArtistId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexMusicArtistGenres", x => new { x.GenresId, x.PlexMusicArtistId });
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistGenres_PlexArtists_PlexMusicArtistId",
                        column: x => x.PlexMusicArtistId,
                        principalTable: "PlexArtists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicArtistGenres_PlexGenres_GenresId",
                        column: x => x.GenresId,
                        principalTable: "PlexGenres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaOverviewOtherVideoSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QualityRank = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexOtherVideoId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    TitleRank = table.Column<int>(type: "INTEGER", nullable: false),
                    YearRank = table.Column<int>(type: "INTEGER", nullable: false),
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
                    PlexOtherVideoId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false)
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
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
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
                    VideoResolution = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalFilename = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedFilename = table.Column<string>(type: "TEXT", nullable: true),
                    Container = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoCodec = table.Column<string>(type: "TEXT", nullable: false),
                    AudioCodec = table.Column<string>(type: "TEXT", nullable: false),
                    NeedsGeneratedName = table.Column<bool>(type: "INTEGER", nullable: false),
                    GeneratedNameSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PlexOtherVideoId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalFilePath = table.Column<string>(type: "TEXT", nullable: true),
                    SourceRelativePath = table.Column<string>(type: "TEXT", nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    VideoProfile = table.Column<string>(type: "TEXT", nullable: true),
                    VideoFrameRate = table.Column<decimal>(type: "TEXT", nullable: true),
                    Quality = table.Column<int>(type: "INTEGER", nullable: false),
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
                        name: "FK_PlexOtherVideoData_PlexOtherVideos_PlexOtherVideoId",
                        column: x => x.PlexOtherVideoId,
                        principalTable: "PlexOtherVideos",
                        principalColumn: "Id",
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
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexOtherVideoId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexOtherVideoGenres", x => new { x.GenresId, x.PlexOtherVideoId });
                    table.ForeignKey(
                        name: "FK_PlexOtherVideoGenres_PlexGenres_GenresId",
                        column: x => x.GenresId,
                        principalTable: "PlexGenres",
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
                    TitleRank = table.Column<int>(type: "INTEGER", nullable: false),
                    YearRank = table.Column<int>(type: "INTEGER", nullable: false),
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
                name: "PlexPhotoImages",
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
                    Studio = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ContentRating = table.Column<string>(type: "TEXT", nullable: false),
                    Rating = table.Column<double>(type: "REAL", nullable: false),
                    ChildCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OriginallyAvailableAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Quality = table.Column<int>(type: "INTEGER", nullable: false),
                    HasThumb = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasArt = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasTheme = table.Column<bool>(type: "INTEGER", nullable: false),
                    FullTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Guid = table.Column<string>(type: "TEXT", nullable: false),
                    Guid_IMDB = table.Column<string>(type: "TEXT", nullable: true),
                    Guid_TMDB = table.Column<int>(type: "INTEGER", nullable: true),
                    Guid_TVDB = table.Column<int>(type: "INTEGER", nullable: true),
                    ParentKey = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexPhotoAlbumId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexPhotoImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexPhotoImages_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexPhotoImages_PlexPhotoAlbums_PlexPhotoAlbumId",
                        column: x => x.PlexPhotoAlbumId,
                        principalTable: "PlexPhotoAlbums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexPhotoImages_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    Quality = table.Column<int>(type: "INTEGER", nullable: false),
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
                    table.PrimaryKey("PK_DownloadTaskPhotoImageFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoImageFiles_DownloadTaskPhotoImages_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DownloadTaskPhotoImages",
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
                        name: "FK_PlexMusicAlbumComparisons_PlexAlbums_OwnedPlexMediaId",
                        column: x => x.OwnedPlexMediaId,
                        principalTable: "PlexAlbums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicAlbumComparisons_PlexAlbums_RemotePlexMediaId",
                        column: x => x.RemotePlexMediaId,
                        principalTable: "PlexAlbums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                });

            migrationBuilder.CreateTable(
                name: "PlexTracks",
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
                    Studio = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ContentRating = table.Column<string>(type: "TEXT", nullable: false),
                    Rating = table.Column<double>(type: "REAL", nullable: false),
                    ChildCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OriginallyAvailableAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Quality = table.Column<int>(type: "INTEGER", nullable: false),
                    HasThumb = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasArt = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasTheme = table.Column<bool>(type: "INTEGER", nullable: false),
                    FullTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Guid = table.Column<string>(type: "TEXT", nullable: false),
                    Guid_IMDB = table.Column<string>(type: "TEXT", nullable: true),
                    Guid_TMDB = table.Column<int>(type: "INTEGER", nullable: true),
                    Guid_TVDB = table.Column<int>(type: "INTEGER", nullable: true),
                    ParentKey = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexAlbumId = table.Column<int>(type: "INTEGER", nullable: false),
                    MusicBrainzRecordingId = table.Column<string>(type: "TEXT", nullable: true),
                    MusicBrainzReleaseTrackId = table.Column<string>(type: "TEXT", nullable: true),
                    DiscNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    TrackNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexTracks_PlexAlbums_PlexAlbumId",
                        column: x => x.PlexAlbumId,
                        principalTable: "PlexAlbums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexTracks_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexTracks_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexPhotoData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiPartId = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoResolution = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalFilename = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedFilename = table.Column<string>(type: "TEXT", nullable: true),
                    Container = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoCodec = table.Column<string>(type: "TEXT", nullable: false),
                    AudioCodec = table.Column<string>(type: "TEXT", nullable: false),
                    NeedsGeneratedName = table.Column<bool>(type: "INTEGER", nullable: false),
                    GeneratedNameSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PlexPhotoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    Quality = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexPhotoData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexPhotoData_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexPhotoData_PlexPhotoImages_PlexPhotoId",
                        column: x => x.PlexPhotoId,
                        principalTable: "PlexPhotoImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexPhotoData_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
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
                    Quality = table.Column<int>(type: "INTEGER", nullable: false),
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
                name: "DownloadTaskPhotoImageFileLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false, defaultValue: "Unknown"),
                    LogLevel = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false, defaultValue: "None"),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DownloadTaskFileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DownloadTaskPhotoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DownloadTaskPhotoAlbumId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadTaskPhotoImageFileLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoImageFileLogs_DownloadTaskPhotoAlbums_DownloadTaskPhotoAlbumId",
                        column: x => x.DownloadTaskPhotoAlbumId,
                        principalTable: "DownloadTaskPhotoAlbums",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoImageFileLogs_DownloadTaskPhotoImageFiles_DownloadTaskFileId",
                        column: x => x.DownloadTaskFileId,
                        principalTable: "DownloadTaskPhotoImageFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DownloadTaskPhotoImageFileLogs_DownloadTaskPhotoImages_DownloadTaskPhotoId",
                        column: x => x.DownloadTaskPhotoId,
                        principalTable: "DownloadTaskPhotoImages",
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
                        name: "FK_PlexMusicTrackComparisons_PlexTracks_OwnedPlexMediaId",
                        column: x => x.OwnedPlexMediaId,
                        principalTable: "PlexTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexMusicTrackComparisons_PlexTracks_RemotePlexMediaId",
                        column: x => x.RemotePlexMediaId,
                        principalTable: "PlexTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlexTrackData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlexApiRatingKey = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexApiPartId = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoResolution = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalFilename = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedFilename = table.Column<string>(type: "TEXT", nullable: true),
                    Container = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoCodec = table.Column<string>(type: "TEXT", nullable: false),
                    AudioCodec = table.Column<string>(type: "TEXT", nullable: false),
                    NeedsGeneratedName = table.Column<bool>(type: "INTEGER", nullable: false),
                    GeneratedNameSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PlexTrackId = table.Column<int>(type: "INTEGER", nullable: false),
                    PartIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalFilePath = table.Column<string>(type: "TEXT", nullable: true),
                    SourceRelativePath = table.Column<string>(type: "TEXT", nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    VideoProfile = table.Column<string>(type: "TEXT", nullable: true),
                    VideoFrameRate = table.Column<decimal>(type: "TEXT", nullable: true),
                    Quality = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexServerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlexTrackData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlexTrackData_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexTrackData_PlexServers_PlexServerId",
                        column: x => x.PlexServerId,
                        principalTable: "PlexServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlexTrackData_PlexTracks_PlexTrackId",
                        column: x => x.PlexTrackId,
                        principalTable: "PlexTracks",
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
                name: "IX_DownloadTaskPhotoAlbums_DownloadStatus",
                table: "DownloadTaskPhotoAlbums",
                column: "DownloadStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoAlbums_PlexLibraryId",
                table: "DownloadTaskPhotoAlbums",
                column: "PlexLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoAlbums_PlexLibraryId_PlexServerId_PlexApiRatingKey",
                table: "DownloadTaskPhotoAlbums",
                columns: new[] { "PlexLibraryId", "PlexServerId", "PlexApiRatingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoAlbums_PlexServerId",
                table: "DownloadTaskPhotoAlbums",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoAlbums_RadarrIntegrationId",
                table: "DownloadTaskPhotoAlbums",
                column: "RadarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoAlbums_SonarrIntegrationId",
                table: "DownloadTaskPhotoAlbums",
                column: "SonarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImageFileLogs_DownloadTaskFileId",
                table: "DownloadTaskPhotoImageFileLogs",
                column: "DownloadTaskFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImageFileLogs_DownloadTaskPhotoAlbumId",
                table: "DownloadTaskPhotoImageFileLogs",
                column: "DownloadTaskPhotoAlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImageFileLogs_DownloadTaskPhotoId",
                table: "DownloadTaskPhotoImageFileLogs",
                column: "DownloadTaskPhotoId");

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
                name: "IX_DownloadTaskPhotoImages_DownloadStatus",
                table: "DownloadTaskPhotoImages",
                column: "DownloadStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImages_ParentId",
                table: "DownloadTaskPhotoImages",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImages_PlexLibraryId",
                table: "DownloadTaskPhotoImages",
                column: "PlexLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImages_PlexLibraryId_PlexServerId_PlexApiRatingKey",
                table: "DownloadTaskPhotoImages",
                columns: new[] { "PlexLibraryId", "PlexServerId", "PlexApiRatingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImages_PlexServerId",
                table: "DownloadTaskPhotoImages",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImages_RadarrIntegrationId",
                table: "DownloadTaskPhotoImages",
                column: "RadarrIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadTaskPhotoImages_SonarrIntegrationId",
                table: "DownloadTaskPhotoImages",
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
                name: "IX_MediaOverviewMusicArtistSnapshots_PlexLibraryId_YearRank",
                table: "MediaOverviewMusicArtistSnapshots",
                columns: new[] { "PlexLibraryId", "YearRank" });

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
                name: "IX_MediaOverviewPhotoAlbumSnapshots_PlexLibraryId_YearRank",
                table: "MediaOverviewPhotoAlbumSnapshots",
                columns: new[] { "PlexLibraryId", "YearRank" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewPhotoAlbumSnapshots_PlexPhotoAlbumId",
                table: "MediaOverviewPhotoAlbumSnapshots",
                column: "PlexPhotoAlbumId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexAlbums_PlexArtistId",
                table: "PlexAlbums",
                column: "PlexArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexAlbums_PlexLibraryId_PlexApiRatingKey",
                table: "PlexAlbums",
                columns: new[] { "PlexLibraryId", "PlexApiRatingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexAlbums_PlexLibraryId_SearchTitle",
                table: "PlexAlbums",
                columns: new[] { "PlexLibraryId", "SearchTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexAlbums_PlexLibraryId_SortIndex",
                table: "PlexAlbums",
                columns: new[] { "PlexLibraryId", "SortIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexAlbums_PlexServerId",
                table: "PlexAlbums",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexArtists_PlexLibraryId_MusicBrainzArtistId",
                table: "PlexArtists",
                columns: new[] { "PlexLibraryId", "MusicBrainzArtistId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexArtists_PlexLibraryId_PlexApiRatingKey",
                table: "PlexArtists",
                columns: new[] { "PlexLibraryId", "PlexApiRatingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexArtists_PlexLibraryId_SearchTitle",
                table: "PlexArtists",
                columns: new[] { "PlexLibraryId", "SearchTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexArtists_PlexLibraryId_SortIndex",
                table: "PlexArtists",
                columns: new[] { "PlexLibraryId", "SortIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexArtists_PlexServerId",
                table: "PlexArtists",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbumComparisons_OwnedPlexLibraryId_OwnedPlexMediaId",
                table: "PlexMusicAlbumComparisons",
                columns: new[] { "OwnedPlexLibraryId", "OwnedPlexMediaId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbumComparisons_OwnedPlexMediaId",
                table: "PlexMusicAlbumComparisons",
                column: "OwnedPlexMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbumComparisons_RemotePlexLibraryId_OwnedPlexLibraryId",
                table: "PlexMusicAlbumComparisons",
                columns: new[] { "RemotePlexLibraryId", "OwnedPlexLibraryId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbumComparisons_RemotePlexLibraryId_OwnedPlexLibraryId_RemotePlexMediaId_OwnedPlexMediaId",
                table: "PlexMusicAlbumComparisons",
                columns: new[] { "RemotePlexLibraryId", "OwnedPlexLibraryId", "RemotePlexMediaId", "OwnedPlexMediaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicAlbumComparisons_RemotePlexMediaId",
                table: "PlexMusicAlbumComparisons",
                column: "RemotePlexMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtistActors_PlexMusicArtistId",
                table: "PlexMusicArtistActors",
                column: "PlexMusicArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtistComparisons_OwnedPlexLibraryId_OwnedPlexMediaId",
                table: "PlexMusicArtistComparisons",
                columns: new[] { "OwnedPlexLibraryId", "OwnedPlexMediaId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtistComparisons_OwnedPlexMediaId",
                table: "PlexMusicArtistComparisons",
                column: "OwnedPlexMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtistComparisons_RemotePlexLibraryId_OwnedPlexLibraryId",
                table: "PlexMusicArtistComparisons",
                columns: new[] { "RemotePlexLibraryId", "OwnedPlexLibraryId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtistComparisons_RemotePlexLibraryId_OwnedPlexLibraryId_RemotePlexMediaId_OwnedPlexMediaId",
                table: "PlexMusicArtistComparisons",
                columns: new[] { "RemotePlexLibraryId", "OwnedPlexLibraryId", "RemotePlexMediaId", "OwnedPlexMediaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtistComparisons_RemotePlexMediaId",
                table: "PlexMusicArtistComparisons",
                column: "RemotePlexMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtistCountries_PlexMusicArtistId",
                table: "PlexMusicArtistCountries",
                column: "PlexMusicArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicArtistGenres_PlexMusicArtistId",
                table: "PlexMusicArtistGenres",
                column: "PlexMusicArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackComparisons_OwnedPlexLibraryId_OwnedPlexMediaId",
                table: "PlexMusicTrackComparisons",
                columns: new[] { "OwnedPlexLibraryId", "OwnedPlexMediaId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackComparisons_OwnedPlexMediaId",
                table: "PlexMusicTrackComparisons",
                column: "OwnedPlexMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackComparisons_RemotePlexLibraryId_OwnedPlexLibraryId",
                table: "PlexMusicTrackComparisons",
                columns: new[] { "RemotePlexLibraryId", "OwnedPlexLibraryId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackComparisons_RemotePlexLibraryId_OwnedPlexLibraryId_RemotePlexMediaId_OwnedPlexMediaId",
                table: "PlexMusicTrackComparisons",
                columns: new[] { "RemotePlexLibraryId", "OwnedPlexLibraryId", "RemotePlexMediaId", "OwnedPlexMediaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexMusicTrackComparisons_RemotePlexMediaId",
                table: "PlexMusicTrackComparisons",
                column: "RemotePlexMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideoActors_PlexOtherVideoId",
                table: "PlexOtherVideoActors",
                column: "PlexOtherVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideoCountries_PlexOtherVideoId",
                table: "PlexOtherVideoCountries",
                column: "PlexOtherVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideoData_PlexApiRatingKey",
                table: "PlexOtherVideoData",
                column: "PlexApiRatingKey");

            migrationBuilder.CreateIndex(
                name: "IX_PlexOtherVideoData_PlexLibraryId_PlexApiPartId",
                table: "PlexOtherVideoData",
                columns: new[] { "PlexLibraryId", "PlexApiPartId" },
                unique: true);

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
                name: "IX_PlexOtherVideoGenres_PlexOtherVideoId",
                table: "PlexOtherVideoGenres",
                column: "PlexOtherVideoId");

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
                name: "IX_PlexPhotoAlbums_PlexLibraryId_PlexApiRatingKey",
                table: "PlexPhotoAlbums",
                columns: new[] { "PlexLibraryId", "PlexApiRatingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoAlbums_PlexLibraryId_SearchTitle",
                table: "PlexPhotoAlbums",
                columns: new[] { "PlexLibraryId", "SearchTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoAlbums_PlexLibraryId_SortIndex",
                table: "PlexPhotoAlbums",
                columns: new[] { "PlexLibraryId", "SortIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoAlbums_PlexServerId",
                table: "PlexPhotoAlbums",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoData_PlexApiRatingKey",
                table: "PlexPhotoData",
                column: "PlexApiRatingKey");

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoData_PlexLibraryId_PlexApiPartId",
                table: "PlexPhotoData",
                columns: new[] { "PlexLibraryId", "PlexApiPartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoData_PlexPhotoId",
                table: "PlexPhotoData",
                column: "PlexPhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoData_PlexServerId",
                table: "PlexPhotoData",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoImages_PlexLibraryId_PlexApiRatingKey",
                table: "PlexPhotoImages",
                columns: new[] { "PlexLibraryId", "PlexApiRatingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoImages_PlexPhotoAlbumId_SortIndex",
                table: "PlexPhotoImages",
                columns: new[] { "PlexPhotoAlbumId", "SortIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexPhotoImages_PlexServerId",
                table: "PlexPhotoImages",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexTrackData_PlexApiRatingKey",
                table: "PlexTrackData",
                column: "PlexApiRatingKey");

            migrationBuilder.CreateIndex(
                name: "IX_PlexTrackData_PlexLibraryId_PlexApiPartId",
                table: "PlexTrackData",
                columns: new[] { "PlexLibraryId", "PlexApiPartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexTrackData_PlexServerId",
                table: "PlexTrackData",
                column: "PlexServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexTrackData_PlexTrackId_PlexApiMediaId_PartIndex",
                table: "PlexTrackData",
                columns: new[] { "PlexTrackId", "PlexApiMediaId", "PartIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexTracks_PlexAlbumId",
                table: "PlexTracks",
                column: "PlexAlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_PlexTracks_PlexLibraryId_PlexApiRatingKey",
                table: "PlexTracks",
                columns: new[] { "PlexLibraryId", "PlexApiRatingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlexTracks_PlexLibraryId_SearchTitle",
                table: "PlexTracks",
                columns: new[] { "PlexLibraryId", "SearchTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexTracks_PlexLibraryId_SortIndex",
                table: "PlexTracks",
                columns: new[] { "PlexLibraryId", "SortIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexTracks_PlexServerId",
                table: "PlexTracks",
                column: "PlexServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "PlexMusicAlbumComparisons");

            migrationBuilder.DropTable(
                name: "PlexMusicArtistActors");

            migrationBuilder.DropTable(
                name: "PlexMusicArtistComparisons");

            migrationBuilder.DropTable(
                name: "PlexMusicArtistCountries");

            migrationBuilder.DropTable(
                name: "PlexMusicArtistGenres");

            migrationBuilder.DropTable(
                name: "PlexMusicTrackComparisons");

            migrationBuilder.DropTable(
                name: "PlexOtherVideoActors");

            migrationBuilder.DropTable(
                name: "PlexOtherVideoCountries");

            migrationBuilder.DropTable(
                name: "PlexOtherVideoData");

            migrationBuilder.DropTable(
                name: "PlexOtherVideoGenres");

            migrationBuilder.DropTable(
                name: "PlexPhotoData");

            migrationBuilder.DropTable(
                name: "PlexTrackData");

            migrationBuilder.DropTable(
                name: "DownloadTaskOtherVideoFiles");

            migrationBuilder.DropTable(
                name: "DownloadTaskPhotoImageFiles");

            migrationBuilder.DropTable(
                name: "DownloadTaskMusicTrackFiles");

            migrationBuilder.DropTable(
                name: "PlexOtherVideos");

            migrationBuilder.DropTable(
                name: "PlexPhotoImages");

            migrationBuilder.DropTable(
                name: "PlexTracks");

            migrationBuilder.DropTable(
                name: "DownloadTaskOtherVideos");

            migrationBuilder.DropTable(
                name: "DownloadTaskPhotoImages");

            migrationBuilder.DropTable(
                name: "DownloadTaskMusicTracks");

            migrationBuilder.DropTable(
                name: "PlexPhotoAlbums");

            migrationBuilder.DropTable(
                name: "PlexAlbums");

            migrationBuilder.DropTable(
                name: "DownloadTaskPhotoAlbums");

            migrationBuilder.DropTable(
                name: "DownloadTaskMusicAlbums");

            migrationBuilder.DropTable(
                name: "PlexArtists");

            migrationBuilder.DropTable(
                name: "DownloadTaskMusicArtists");

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

            migrationBuilder.AlterColumn<int>(
                name: "MediaCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                computedColumnSql: "CASE WHEN Type = 'Movie' THEN MovieCount WHEN Type = 'TvShow' THEN TvShowCount ELSE -1 END",
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldComputedColumnSql: "CASE WHEN Type = 'Movie' THEN MovieCount WHEN Type = 'TvShow' THEN TvShowCount WHEN Type = 'MusicArtist' THEN MusicArtistCount WHEN Type = 'PhotoAlbum' THEN PhotoAlbumCount WHEN Type = 'OtherVideos' THEN OtherVideoCount ELSE -1 END");
        }
    }
}
