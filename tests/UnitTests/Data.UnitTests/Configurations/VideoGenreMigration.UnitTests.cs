using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Reaparr.Data.UnitTests;

public class VideoGenreMigrationUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldPreserveGenreAndLibraryLink_WhenUpgradingPreviousSchema()
    {
        // Arrange
        await SetupDatabase(626557, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 1;
            config.MovieCount = 1;
        });
        await using var context = (ReaparrDbContext)IDbContext;
        var libraryId = await context.PlexLibraries.Where(x => x.Type == PlexMediaType.Movie).Select(x => x.Id).SingleAsync();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20260917113405_AddImprovedIndexerSearchConfig", CancellationToken);
        const int genreId = 1000000042;
        const string genreName = "Migration sentinel genre";
        const string genreKey = "3eb501d482b8480a9b0e672c079d126f";
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PlexGenres (Id, Name, Key) VALUES ({genreId}, {genreName}, {genreKey})", CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PlexLibraryGenres (PlexGenreId, PlexLibraryId) VALUES ({genreId}, {libraryId})", CancellationToken);
        var oldGenreNames = await context.Database.SqlQuery<string>(
            $"SELECT Name AS Value FROM PlexGenres WHERE Id = {genreId}").ToListAsync(CancellationToken);
        oldGenreNames.ShouldBe([genreName]);
        var oldLibraryIds = await context.Database.SqlQuery<int>(
            $"SELECT PlexLibraryId AS Value FROM PlexLibraryGenres WHERE PlexGenreId = {genreId}").ToListAsync(CancellationToken);
        oldLibraryIds.ShouldBe([libraryId]);

        // Act
        await migrator.MigrateAsync(cancellationToken: CancellationToken);

        // Assert
        context.Database.HasPendingModelChanges().ShouldBeFalse();
        var genres = await context.PlexGenresVideo.Where(x => x.Id == genreId)
            .Select(x => new { x.Id, x.Name, x.Key, x.Type }).ToListAsync(CancellationToken);
        genres.ShouldBe([new { Id = genreId, Name = genreName, Key = genreKey, Type = PlexGenreType.Unknown }]);
        var links = await context.PlexLibraryGenresVideo.Where(x => x.PlexGenreId == genreId)
            .Select(x => new { x.PlexGenreId, x.PlexLibraryId }).ToListAsync(CancellationToken);
        links.ShouldBe([new { PlexGenreId = genreId, PlexLibraryId = libraryId }]);
    }
}
