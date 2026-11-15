namespace Reaparr.Domain.UnitTests;

public class FolderPathUnitTests : BaseUnitTest
{
    [Test]
    public void ShouldMatchConfiguration_WhenIdentityAndNavigationInstancesDiffer()
    {
        // Arrange
        var folder = new FolderPath(FolderType.MovieFolder, "Movies", "/movies")
        {
            Id = 1,
            MediaType = PlexMediaType.Movie,
            PlexLibraries = new List<PlexLibrary>(),
        };
        var other = new FolderPath(FolderType.MovieFolder, "Movies", "/movies")
        {
            Id = 2,
            MediaType = PlexMediaType.Movie,
            PlexLibraries = new List<PlexLibrary>(),
        };

        // Act
        var equal = folder.Equals(other);

        // Assert
        equal.ShouldBeTrue();
        folder.HasSameRole(other).ShouldBeTrue();
        new HashSet<FolderPath> { folder }
            .Contains(other)
            .ShouldBeTrue();
    }

    [Test]
    [Arguments(nameof(FolderPath.DisplayName), true)]
    [Arguments(nameof(FolderPath.DirectoryPath), true)]
    [Arguments(nameof(FolderPath.FolderType), false)]
    [Arguments(nameof(FolderPath.MediaType), false)]
    public void ShouldDetectConfigurationChanges_WithoutConfusingThemWithRoleChanges(
        string changedProperty,
        bool sameRole
    )
    {
        // Arrange
        var folder = new FolderPath(FolderType.MovieFolder, "Movies", "/movies")
        {
            Id = 1,
            MediaType = PlexMediaType.Movie,
        };
        var other = new FolderPath
        {
            Id = folder.Id,
            DisplayName = changedProperty == nameof(FolderPath.DisplayName) ? "Renamed movies" : folder.DisplayName,
            DirectoryPath =
                changedProperty == nameof(FolderPath.DirectoryPath) ? "/other-movies" : folder.DirectoryPath,
            FolderType =
                changedProperty == nameof(FolderPath.FolderType) ? FolderType.DownloadFolder : folder.FolderType,
            MediaType = changedProperty == nameof(FolderPath.MediaType) ? PlexMediaType.TvShow : folder.MediaType,
        };

        // Act
        var equal = folder.Equals(other);
        var roleMatches = folder.HasSameRole(other);

        // Assert
        equal.ShouldBeFalse();
        roleMatches.ShouldBe(sameRole);
    }
}
