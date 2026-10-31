namespace Reaparr.Domain.UnitTests;

public class PhotoSourcePathExtensionsUnitTests
{
    [Test]
    public void ShouldMatchPosixRootComponentsCaseSensitively_WhenResolvingFiles()
    {
        // Arrange
        var root = CreateRoot(1, "/photos/Family");

        // Act
        var matched = "/photos/Family/2026/photo.jpg".TryResolvePhotoSourcePath([root], out var match);
        var differentCase = "/photos/family/2026/photo.jpg".TryResolvePhotoSourcePath([root], out _);

        // Assert
        matched.ShouldBeTrue();
        (match.Root.Id, match.RelativePath, match.CanonicalRelativePath).ShouldBe(
            (1, "2026/photo.jpg", "2026/photo.jpg")
        );
        differentCase.ShouldBeFalse();
    }

    [Test]
    [Arguments(@"c:\Photos", @"C:\PHOTOS\Family\photo.jpg", "C:/PHOTOS")]
    [Arguments(@"\\server\share\Photos", @"//SERVER/SHARE/photos\Family\photo.jpg", "//SERVER/SHARE/PHOTOS")]
    public void ShouldResolveWindowsSourcesIndependentOfHostOs_WhenCaseAndSeparatorsDiffer(
        string rootPath,
        string sourcePath,
        string canonicalRoot
    )
    {
        // Arrange
        var root = CreateRoot(7, rootPath);

        // Act
        var matched = sourcePath.TryResolvePhotoSourcePath([root], out var match);
        var fileName = sourcePath.GetSourcePathLeafName();

        // Assert
        root.SourceRootPath.ShouldBe(canonicalRoot);
        matched.ShouldBeTrue();
        (match.Root.Id, match.RelativePath, match.CanonicalRelativePath).ShouldBe(
            (7, "Family/photo.jpg", "FAMILY/PHOTO.JPG")
        );
        fileName.ShouldBe("photo.jpg");
    }

    [Test]
    [Arguments("/", "/photo.jpg", "path:posix:/", "photo.jpg")]
    [Arguments(@"C:\", @"c:\photo.jpg", "path:windows:C:/", "PHOTO.JPG")]
    [Arguments(@"C:\\", "C:/photo.jpg", "path:windows:C:/", "PHOTO.JPG")]
    public void ShouldPreserveFilesystemRootIdentity_WhenTheConfiguredRootHasNoDirectorySegments(
        string rootPath,
        string sourcePath,
        string expectedIdentity,
        string canonicalRelativePath
    )
    {
        // Arrange
        var root = CreateRoot(8, rootPath);

        // Act
        var valid = rootPath.TryCreatePhotoRootIdentity(out var identity, out _, out var error);
        var matched = sourcePath.TryResolvePhotoSourcePath([root], out var match);

        // Assert
        valid.ShouldBeTrue(error);
        identity.ShouldBe(expectedIdentity);
        matched.ShouldBeTrue();
        (match.Root.Id, match.RelativePath, match.CanonicalRelativePath).ShouldBe(
            (8, "photo.jpg", canonicalRelativePath)
        );
    }

    [Test]
    public void ShouldUseTheLongestContainingRoot_WhenLessSpecificRootsAreAmbiguous()
    {
        // Arrange
        var roots = new[] { CreateRoot(1, "/photos"), CreateRoot(2, "/photos"), CreateRoot(3, "/photos/family") };

        // Act
        var matched = "/photos/family/one.jpg".TryResolvePhotoSourcePath(roots, out var match);

        // Assert
        matched.ShouldBeTrue();
        (match.Root.Id, match.RelativePath, match.CanonicalRelativePath).ShouldBe((3, "one.jpg", "one.jpg"));
    }

    [Test]
    [Arguments("/photos", "/photos/one.jpg")]
    [Arguments(@"C:\photos", @"c:\PHOTOS\one.jpg")]
    public void ShouldRejectAmbiguousMappings_WhenDistinctRootsHaveEqualSpecificity(string rootPath, string sourcePath)
    {
        // Arrange
        var roots = new[] { CreateRoot(1, rootPath), CreateRoot(2, rootPath) };

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            sourcePath.TryResolvePhotoSourcePath(roots, out _)
        );

        // Assert
        exception.Message.ShouldContain("equally specific");
    }

    [Test]
    [Arguments("/photos-other/one.jpg")]
    [Arguments("/photos")]
    [Arguments("/Photos/one.jpg")]
    [Arguments("/photos/../secret.jpg")]
    [Arguments(@"C:\photos\one.jpg")]
    public void ShouldLeaveFilesUnmapped_WhenTheyAreNotInsideTheConfiguredRoot(string sourcePath)
    {
        // Arrange
        var root = CreateRoot(1, "/photos");

        // Act
        var matched = sourcePath.TryResolvePhotoSourcePath([root], out _);

        // Assert
        matched.ShouldBeFalse();
    }

    [Test]
    public void ShouldNotTreatUncSourcesAsPosixFiles_WhenThePosixRootIsSlash()
    {
        // Arrange
        var root = CreateRoot(1, "/");

        // Act
        var matched = @"\\server\share\photo.jpg".TryResolvePhotoSourcePath([root], out _);

        // Assert
        matched.ShouldBeFalse();
    }

    [Test]
    public void ShouldPreserveBackslashesInPosixFileNames_WhenResolvingOnAnyHost()
    {
        // Arrange
        var root = CreateRoot(1, "/photos");
        const string sourcePath = "/photos/back\\slash.jpg";

        // Act
        var matched = sourcePath.TryResolvePhotoSourcePath([root], out var match);
        var fileName = sourcePath.GetSourcePathLeafName();

        // Assert
        matched.ShouldBeTrue();
        (match.RelativePath, match.CanonicalRelativePath, fileName).ShouldBe(
            ("back\\slash.jpg", "back\\slash.jpg", "back\\slash.jpg")
        );
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("photos")]
    [Arguments(@"C:photos")]
    [Arguments(@"\\server")]
    [Arguments("/photos/../other")]
    [Arguments("/photos/\\0other")]
    public void ShouldRejectInvalidRootConfiguration_WhenPathsAreNotAbsoluteAndTraversalFree(string? path)
    {
        // Arrange
        // Keep control characters escaped in report names, not in the actual Plex Location input.
        var sourcePath = path?.Replace("\\0", "\0", StringComparison.Ordinal);

        // Act
        var valid = sourcePath.TryCreatePhotoRootIdentity(out var identity, out var canonical, out var error);

        // Assert
        valid.ShouldBeFalse();
        (identity, canonical).ShouldBe((string.Empty, string.Empty));
        error.ShouldContain("absolute");
    }

    private static PlexPhotoFolder CreateRoot(int id, string path)
    {
        if (!path.TryCreatePhotoRootIdentity(out _, out var canonicalPath, out var error))
            throw new ArgumentException(error, nameof(path));
        return new PlexPhotoFolder
        {
            Id = id,
            PlexServerId = 1,
            PlexLibraryId = 2,
            Title = $"Root {id}",
            RelativePath = string.Empty,
            RootIdentityKey = $"id:{id}",
            SourceRootPath = canonicalPath,
            PlexApiLocationId = id,
            IsActive = true,
        };
    }
}
