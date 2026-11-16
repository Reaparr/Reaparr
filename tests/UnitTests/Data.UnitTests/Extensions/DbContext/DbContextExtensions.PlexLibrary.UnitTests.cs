namespace Reaparr.Data.UnitTests;

public class DbContextExtensionsPlexLibraryUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldReturnLibraryName_WhenLibraryExists()
    {
        // Arrange
        await SetupDatabase(12300, cfg =>
        {
            cfg.PlexServerCount = 1;
            cfg.PlexMovieLibraryCount = 1;
        });
        var library = IDbContext.PlexLibraries.First();

        // Act
        var name = await IDbContext.GetPlexLibraryNameById(library.Id);

        // Assert
        name.ShouldBe(library.Title);
    }

    [Test]
    public async Task ShouldReturnFallbackLibraryName_WhenLibraryDoesNotExist()
    {
        // Arrange
        await SetupDatabase(12301);

        // Act
        var name = await IDbContext.GetPlexLibraryNameById(9999);

        // Assert
        name.ShouldBe("Library Name Not Found");
    }
}
