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
            migrationBuilder.DropForeignKey(
                name: "FK_PlexLibraryGenres_PlexGenres_PlexGenreId",
                table: "PlexLibraryGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_PlexLibraryGenres_PlexLibraries_PlexLibraryId",
                table: "PlexLibraryGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_PlexMovieGenres_PlexGenres_GenresId",
                table: "PlexMovieGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_PlexOtherVideoGenres_PlexGenres_GenresId",
                table: "PlexOtherVideoGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_PlexTvShowGenres_PlexGenres_GenresId",
                table: "PlexTvShowGenres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlexLibraryGenres",
                table: "PlexLibraryGenres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlexGenres",
                table: "PlexGenres");

            migrationBuilder.RenameTable(
                name: "PlexLibraryGenres",
                newName: "PlexLibraryGenresVideo");

            migrationBuilder.RenameTable(
                name: "PlexGenres",
                newName: "PlexGenresVideo");

            migrationBuilder.RenameIndex(
                name: "IX_PlexLibraryGenres_PlexLibraryId",
                table: "PlexLibraryGenresVideo",
                newName: "IX_PlexLibraryGenresVideo_PlexLibraryId");

            migrationBuilder.RenameIndex(
                name: "IX_PlexGenres_Key",
                table: "PlexGenresVideo",
                newName: "IX_PlexGenresVideo_Key");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlexLibraryGenresVideo",
                table: "PlexLibraryGenresVideo",
                columns: new[] { "PlexGenreId", "PlexLibraryId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlexGenresVideo",
                table: "PlexGenresVideo",
                column: "Id");

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

            migrationBuilder.AddForeignKey(
                name: "FK_PlexMovieGenres_PlexGenresVideo_GenresId",
                table: "PlexMovieGenres",
                column: "GenresId",
                principalTable: "PlexGenresVideo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlexOtherVideoGenres_PlexGenresVideo_GenresId",
                table: "PlexOtherVideoGenres",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlexLibraryGenresVideo_PlexGenresVideo_PlexGenreId",
                table: "PlexLibraryGenresVideo");

            migrationBuilder.DropForeignKey(
                name: "FK_PlexLibraryGenresVideo_PlexLibraries_PlexLibraryId",
                table: "PlexLibraryGenresVideo");

            migrationBuilder.DropForeignKey(
                name: "FK_PlexMovieGenres_PlexGenresVideo_GenresId",
                table: "PlexMovieGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_PlexOtherVideoGenres_PlexGenresVideo_GenresId",
                table: "PlexOtherVideoGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_PlexTvShowGenres_PlexGenresVideo_GenresId",
                table: "PlexTvShowGenres");

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
                name: "IX_PlexLibraryGenresVideo_PlexLibraryId",
                table: "PlexLibraryGenres",
                newName: "IX_PlexLibraryGenres_PlexLibraryId");

            migrationBuilder.RenameIndex(
                name: "IX_PlexGenresVideo_Key",
                table: "PlexGenres",
                newName: "IX_PlexGenres_Key");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlexLibraryGenres",
                table: "PlexLibraryGenres",
                columns: new[] { "PlexGenreId", "PlexLibraryId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlexGenres",
                table: "PlexGenres",
                column: "Id");

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

            migrationBuilder.AddForeignKey(
                name: "FK_PlexMovieGenres_PlexGenres_GenresId",
                table: "PlexMovieGenres",
                column: "GenresId",
                principalTable: "PlexGenres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlexOtherVideoGenres_PlexGenres_GenresId",
                table: "PlexOtherVideoGenres",
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
        }
    }
}
