using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reaparr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTorznabFeedOrderingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PlexTvShowEpisodes_AddedAt_PlexServerId_PlexApiRatingKey",
                table: "PlexTvShowEpisodes",
                columns: new[] { "AddedAt", "PlexServerId", "PlexApiRatingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_PlexMovie_AddedAt_PlexServerId_PlexApiRatingKey",
                table: "PlexMovie",
                columns: new[] { "AddedAt", "PlexServerId", "PlexApiRatingKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlexTvShowEpisodes_AddedAt_PlexServerId_PlexApiRatingKey",
                table: "PlexTvShowEpisodes");

            migrationBuilder.DropIndex(
                name: "IX_PlexMovie_AddedAt_PlexServerId_PlexApiRatingKey",
                table: "PlexMovie");
        }
    }
}
