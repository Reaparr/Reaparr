using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reaparr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaOverviewSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "PlexGenres",
                type: "TEXT",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "\"Unknown\"");

            migrationBuilder.CreateTable(
                name: "MediaOverviewMovieSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TitleRank = table.Column<int>(type: "INTEGER", nullable: false),
                    YearRank = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAtRank = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtRank = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationRank = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaSizeRank = table.Column<int>(type: "INTEGER", nullable: false),
                    QualityRank = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexMovieId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaOverviewMovieSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaOverviewMovieSnapshots_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaOverviewMovieSnapshots_PlexMovie_PlexMovieId",
                        column: x => x.PlexMovieId,
                        principalTable: "PlexMovie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaOverviewTvShowSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TitleRank = table.Column<int>(type: "INTEGER", nullable: false),
                    YearRank = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAtRank = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtRank = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationRank = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaSizeRank = table.Column<int>(type: "INTEGER", nullable: false),
                    QualityRank = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexTvShowId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlexLibraryId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaOverviewTvShowSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaOverviewTvShowSnapshots_PlexLibraries_PlexLibraryId",
                        column: x => x.PlexLibraryId,
                        principalTable: "PlexLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaOverviewTvShowSnapshots_PlexTvShows_PlexTvShowId",
                        column: x => x.PlexTvShowId,
                        principalTable: "PlexTvShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMovieSnapshots_AddedAtRank",
                table: "MediaOverviewMovieSnapshots",
                column: "AddedAtRank");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMovieSnapshots_DurationRank",
                table: "MediaOverviewMovieSnapshots",
                column: "DurationRank");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMovieSnapshots_MediaSizeRank",
                table: "MediaOverviewMovieSnapshots",
                column: "MediaSizeRank");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMovieSnapshots_PlexLibraryId",
                table: "MediaOverviewMovieSnapshots",
                column: "PlexLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMovieSnapshots_PlexMovieId",
                table: "MediaOverviewMovieSnapshots",
                column: "PlexMovieId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMovieSnapshots_QualityRank",
                table: "MediaOverviewMovieSnapshots",
                column: "QualityRank");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMovieSnapshots_TitleRank",
                table: "MediaOverviewMovieSnapshots",
                column: "TitleRank");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMovieSnapshots_UpdatedAtRank",
                table: "MediaOverviewMovieSnapshots",
                column: "UpdatedAtRank");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewMovieSnapshots_YearRank",
                table: "MediaOverviewMovieSnapshots",
                column: "YearRank");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewTvShowSnapshots_AddedAtRank",
                table: "MediaOverviewTvShowSnapshots",
                column: "AddedAtRank");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewTvShowSnapshots_DurationRank",
                table: "MediaOverviewTvShowSnapshots",
                column: "DurationRank");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewTvShowSnapshots_MediaSizeRank",
                table: "MediaOverviewTvShowSnapshots",
                column: "MediaSizeRank");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewTvShowSnapshots_PlexLibraryId",
                table: "MediaOverviewTvShowSnapshots",
                column: "PlexLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewTvShowSnapshots_PlexTvShowId",
                table: "MediaOverviewTvShowSnapshots",
                column: "PlexTvShowId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewTvShowSnapshots_QualityRank",
                table: "MediaOverviewTvShowSnapshots",
                column: "QualityRank");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewTvShowSnapshots_TitleRank",
                table: "MediaOverviewTvShowSnapshots",
                column: "TitleRank");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewTvShowSnapshots_UpdatedAtRank",
                table: "MediaOverviewTvShowSnapshots",
                column: "UpdatedAtRank");

            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewTvShowSnapshots_YearRank",
                table: "MediaOverviewTvShowSnapshots",
                column: "YearRank");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaOverviewMovieSnapshots");

            migrationBuilder.DropTable(
                name: "MediaOverviewTvShowSnapshots");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "PlexGenres");
        }
    }
}
