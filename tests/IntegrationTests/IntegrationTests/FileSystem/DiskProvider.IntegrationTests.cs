using Reaparr.FileSystem;

namespace Reaparr.IntegrationTests;

public class DiskProviderIntegrationTests : BaseIntegrationTests
{
    public DiskProviderIntegrationTests() { }

    [Test]
    public async Task ShouldLeaveExcludedDirectoriesUntouched_WhenListingVisibleDirectories()
    {
        // Arrange
        using var container = await CreateContainer(new Seed(44830));
        var rootPath = Path.Combine(container.TestFileSystemRootPath, "directory-listing");
        Directory.CreateDirectory(rootPath);
        var excludedPath = Directory.CreateDirectory(Path.Combine(rootPath, "CaChE")).FullName;
        var zebraPath = Directory.CreateDirectory(Path.Combine(rootPath, "Zebra")).FullName;
        var cacheMediaPath = Directory.CreateDirectory(Path.Combine(rootPath, "CacheMedia")).FullName;
        var retainedFile = Path.Combine(excludedPath, "keep.txt");
        File.WriteAllText(retainedFile, "preserved");
        var modifiedUtc = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        Directory.SetLastWriteTimeUtc(excludedPath, modifiedUtc);
        Directory.SetLastWriteTimeUtc(zebraPath, modifiedUtc);
        Directory.SetLastWriteTimeUtc(cacheMediaPath, modifiedUtc);
        Directory.GetLastWriteTimeUtc(excludedPath).ShouldBe(modifiedUtc);
        var provider = new DiskProvider();

        // Act
        var result = provider.GetDirectories(rootPath);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe([
            new FileSystemModel
            {
                Name = "CacheMedia",
                Path = cacheMediaPath + Path.DirectorySeparatorChar,
                Type = FileSystemEntityType.Folder,
                Extension = string.Empty,
                Size = 0,
                LastModified = modifiedUtc,
                HasReadPermission = true,
                HasWritePermission = true,
            },
            new FileSystemModel
            {
                Name = "Zebra",
                Path = zebraPath + Path.DirectorySeparatorChar,
                Type = FileSystemEntityType.Folder,
                Extension = string.Empty,
                Size = 0,
                LastModified = modifiedUtc,
                HasReadPermission = true,
                HasWritePermission = true,
            },
        ]);
        Directory.GetLastWriteTimeUtc(excludedPath).ShouldBe(modifiedUtc);
        Directory.GetFileSystemEntries(excludedPath).ShouldBe([retainedFile]);
        File.ReadAllText(retainedFile).ShouldBe("preserved");
        Directory.GetDirectories(rootPath)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ShouldBe(["CaChE", "CacheMedia", "Zebra"]);
    }
}
