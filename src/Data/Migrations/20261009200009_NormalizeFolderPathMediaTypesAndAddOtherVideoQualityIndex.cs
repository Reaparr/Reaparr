using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reaparr.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeFolderPathMediaTypesAndAddOtherVideoQualityIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MediaOverviewOtherVideoSnapshots_PlexLibraryId_QualityRank",
                table: "MediaOverviewOtherVideoSnapshots",
                columns: new[] { "PlexLibraryId", "QualityRank" });
            migrationBuilder.Sql("UPDATE FolderPaths SET MediaType = 'MusicArtist' WHERE MediaType = 'Music';");
            migrationBuilder.Sql("UPDATE FolderPaths SET MediaType = 'PhotoAlbum' WHERE MediaType = 'Photos';");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaOverviewOtherVideoSnapshots_PlexLibraryId_QualityRank",
                table: "MediaOverviewOtherVideoSnapshots");
            migrationBuilder.Sql("UPDATE FolderPaths SET MediaType = 'Music' WHERE MediaType = 'MusicArtist';");
            migrationBuilder.Sql("UPDATE FolderPaths SET MediaType = 'Photos' WHERE MediaType = 'PhotoAlbum';");
        }
    }
}
