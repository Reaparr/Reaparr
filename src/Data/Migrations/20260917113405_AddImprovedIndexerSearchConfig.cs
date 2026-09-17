using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reaparr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImprovedIndexerSearchConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlexTvShowEpisodes_PlexLibraryId",
                table: "PlexTvShowEpisodes");

            migrationBuilder.AlterColumn<string>(
                name: "SearchTitle",
                table: "PlexTvShows",
                type: "TEXT",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "SearchTitle",
                table: "PlexMovie",
                type: "TEXT",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "TvShowCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 16)
                .OldAnnotation("Relational:ColumnOrder", 15);

            migrationBuilder.AlterColumn<int>(
                name: "SeasonCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 17)
                .OldAnnotation("Relational:ColumnOrder", 16);

            migrationBuilder.AlterColumn<bool>(
                name: "Outdated",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 23)
                .OldAnnotation("Relational:ColumnOrder", 21);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: true)
                .Annotation("Relational:ColumnOrder", 24)
                .OldAnnotation("Relational:ColumnOrder", 22);

            migrationBuilder.AlterColumn<int>(
                name: "GenresCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 21)
                .OldAnnotation("Relational:ColumnOrder", 19);

            migrationBuilder.AlterColumn<int>(
                name: "EpisodeCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 18)
                .OldAnnotation("Relational:ColumnOrder", 17);

            migrationBuilder.AlterColumn<int>(
                name: "CountriesCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 22)
                .OldAnnotation("Relational:ColumnOrder", 20);

            migrationBuilder.AlterColumn<int>(
                name: "ActorsCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 20)
                .OldAnnotation("Relational:ColumnOrder", 18);

            migrationBuilder.AddColumn<int>(
                name: "EpisodeMediaDataCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Relational:ColumnOrder", 19);

            migrationBuilder.AddColumn<int>(
                name: "MovieMediaDataCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Relational:ColumnOrder", 15);

            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShowSeason_TvShowId_SeasonNumber",
                table: "PlexTvShowSeason",
                columns: new[] { "TvShowId", "SeasonNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShows_PlexLibraryId_Guid_IMDB",
                table: "PlexTvShows",
                columns: new[] { "PlexLibraryId", "Guid_IMDB" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShows_PlexLibraryId_Guid_TMDB",
                table: "PlexTvShows",
                columns: new[] { "PlexLibraryId", "Guid_TMDB" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShows_PlexLibraryId_Guid_TVDB",
                table: "PlexTvShows",
                columns: new[] { "PlexLibraryId", "Guid_TVDB" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShows_PlexLibraryId_SearchTitle",
                table: "PlexTvShows",
                columns: new[] { "PlexLibraryId", "SearchTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShowEpisodes_PlexLibraryId_AddedAt_PlexServerId_PlexApiRatingKey",
                table: "PlexTvShowEpisodes",
                columns: new[] { "PlexLibraryId", "AddedAt", "PlexServerId", "PlexApiRatingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShowEpisodes_TvShowSeasonId_EpisodeNumber",
                table: "PlexTvShowEpisodes",
                columns: new[] { "TvShowSeasonId", "EpisodeNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMovie_PlexLibraryId_AddedAt_PlexServerId_PlexApiRatingKey",
                table: "PlexMovie",
                columns: new[] { "PlexLibraryId", "AddedAt", "PlexServerId", "PlexApiRatingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMovie_PlexLibraryId_Guid_IMDB",
                table: "PlexMovie",
                columns: new[] { "PlexLibraryId", "Guid_IMDB" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMovie_PlexLibraryId_Guid_TMDB",
                table: "PlexMovie",
                columns: new[] { "PlexLibraryId", "Guid_TMDB" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMovie_PlexLibraryId_SearchTitle",
                table: "PlexMovie",
                columns: new[] { "PlexLibraryId", "SearchTitle" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlexTvShowSeason_TvShowId_SeasonNumber",
                table: "PlexTvShowSeason");

            migrationBuilder.DropIndex(
                name: "IX_PlexTvShows_PlexLibraryId_Guid_IMDB",
                table: "PlexTvShows");

            migrationBuilder.DropIndex(
                name: "IX_PlexTvShows_PlexLibraryId_Guid_TMDB",
                table: "PlexTvShows");

            migrationBuilder.DropIndex(
                name: "IX_PlexTvShows_PlexLibraryId_Guid_TVDB",
                table: "PlexTvShows");

            migrationBuilder.DropIndex(
                name: "IX_PlexTvShows_PlexLibraryId_SearchTitle",
                table: "PlexTvShows");

            migrationBuilder.DropIndex(
                name: "IX_PlexTvShowEpisodes_PlexLibraryId_AddedAt_PlexServerId_PlexApiRatingKey",
                table: "PlexTvShowEpisodes");

            migrationBuilder.DropIndex(
                name: "IX_PlexTvShowEpisodes_TvShowSeasonId_EpisodeNumber",
                table: "PlexTvShowEpisodes");

            migrationBuilder.DropIndex(
                name: "IX_PlexMovie_PlexLibraryId_AddedAt_PlexServerId_PlexApiRatingKey",
                table: "PlexMovie");

            migrationBuilder.DropIndex(
                name: "IX_PlexMovie_PlexLibraryId_Guid_IMDB",
                table: "PlexMovie");

            migrationBuilder.DropIndex(
                name: "IX_PlexMovie_PlexLibraryId_Guid_TMDB",
                table: "PlexMovie");

            migrationBuilder.DropIndex(
                name: "IX_PlexMovie_PlexLibraryId_SearchTitle",
                table: "PlexMovie");

            migrationBuilder.DropColumn(
                name: "EpisodeMediaDataCount",
                table: "PlexLibraries");

            migrationBuilder.DropColumn(
                name: "MovieMediaDataCount",
                table: "PlexLibraries");

            migrationBuilder.AlterColumn<string>(
                name: "SearchTitle",
                table: "PlexTvShows",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "SearchTitle",
                table: "PlexMovie",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<int>(
                name: "TvShowCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 15)
                .OldAnnotation("Relational:ColumnOrder", 16);

            migrationBuilder.AlterColumn<int>(
                name: "SeasonCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 16)
                .OldAnnotation("Relational:ColumnOrder", 17);

            migrationBuilder.AlterColumn<bool>(
                name: "Outdated",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 21)
                .OldAnnotation("Relational:ColumnOrder", 23);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEnabled",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: true)
                .Annotation("Relational:ColumnOrder", 22)
                .OldAnnotation("Relational:ColumnOrder", 24);

            migrationBuilder.AlterColumn<int>(
                name: "GenresCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 19)
                .OldAnnotation("Relational:ColumnOrder", 21);

            migrationBuilder.AlterColumn<int>(
                name: "EpisodeCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 17)
                .OldAnnotation("Relational:ColumnOrder", 18);

            migrationBuilder.AlterColumn<int>(
                name: "CountriesCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 20)
                .OldAnnotation("Relational:ColumnOrder", 22);

            migrationBuilder.AlterColumn<int>(
                name: "ActorsCount",
                table: "PlexLibraries",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Relational:ColumnOrder", 18)
                .OldAnnotation("Relational:ColumnOrder", 20);

            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShowEpisodes_PlexLibraryId",
                table: "PlexTvShowEpisodes",
                column: "PlexLibraryId");
        }
    }
}
