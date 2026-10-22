using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Autofac;

namespace Reaparr.Build.UnitTests;

public class FileSystemTasksUnitTests : BaseUnitTest
{
    [Test]
    [Arguments("/repo/source", "/repo/source/nested-target")]
    [Arguments("/repo/source/./", "/repo/source/../source/nested-target/")]
    [Arguments("/", "/repo/source/nested-target")]
    public void ShouldThrow_WhenCopyTargetIsNestedInsideSource(string source, string target)
    {
        // Arrange
        SetupFileSystem(system =>
        {
            system.AddDirectory(target);
            system.AddFile("/repo/source/source-file.txt", new MockFileData("source"));
        });

        var fileSystemTasks = CreateFileSystemTasks();
        var fileSystem = Mock.Container.Resolve<IFileSystem>();

        // Act
        var action = () => fileSystemTasks.CopyDirectory(
            new BuildDirectoryPath(fileSystem, source), new BuildDirectoryPath(fileSystem, target));

        // Assert
        var exception = action.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("Refusing to copy");
        exception.Message.ShouldContain("nested target");
    }

    [Test]
    [Arguments("/repo", false)]
    [Arguments("/repo", true)]
    [Arguments("/repo/Releases", false)]
    [Arguments("/repo/Releases", true)]
    public void ShouldRetainFiles_WhenArtifactCleanupTargetIsUnsafe(string artifactDirectory, bool preservePublish)
    {
        // Arrange
        const string root = "/repo";
        var marker = Path.Combine(artifactDirectory, "keep.txt");
        SetupFileSystem(system => system.AddFile(marker, new MockFileData("keep")));
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var fileSystemTasks = CreateFileSystemTasks();

        // Act
        var action = () => fileSystemTasks.ClearArtifactDirectory(
            new BuildDirectoryPath(fileSystem, root),
            new BuildDirectoryPath(fileSystem, artifactDirectory),
            preservePublish ? new BuildDirectoryPath(fileSystem, Path.Combine(artifactDirectory, "publish")) : null);

        // Assert
        action.ShouldThrow<InvalidOperationException>();
        fileSystem.File.ReadAllText(marker).ShouldBe("keep");
        fileSystem.Directory.EnumerateFiles(artifactDirectory, "*", SearchOption.AllDirectories).ShouldBe([marker]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void ShouldRetainOnlyPublishTree_WhenClearingDefaultArtifacts(bool realFileSystem)
    {
        // Arrange
        IFileSystem fileSystem = realFileSystem ? new System.IO.Abstractions.FileSystem() : new MockFileSystem();
        var root = fileSystem.Path.Combine(
            fileSystem.Path.GetTempPath(), nameof(FileSystemTasksUnitTests), Guid.NewGuid().ToString("N"));
        var paths = new BuildPaths(root, fileSystem);
        var artifacts = paths.ArtifactDirectory("linux-x64");
        var publish = paths.PublishDirectory("linux-x64");
        var binary = fileSystem.Path.Combine(publish, "Reaparr.AppHost");
        var index = fileSystem.Path.Combine(publish, "wwwroot", "index.html");
        var control = fileSystem.Path.Combine(paths.ArtifactDirectory("win-x64"), "keep.bin");
        var files = new Dictionary<string, byte[]>
        {
            [binary] = [0, 1, 2, 255],
            [index] = "frontend"u8.ToArray(),
            [fileSystem.Path.Combine(artifacts, "old.AppImage")] = [9],
            [fileSystem.Path.Combine(artifacts, "old-output", "stale.bin")] = [8],
            [fileSystem.Path.Combine(artifacts, "publish-backup", "stale.bin")] = [7],
            [control] = [6, 5],
        };
        foreach (var (file, bytes) in files)
        {
            fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(file)!);
            fileSystem.File.WriteAllBytes(file, bytes);
        }
        fileSystem.File.Exists(fileSystem.Path.Combine(artifacts, "old.AppImage")).ShouldBeTrue();
        fileSystem.File.ReadAllBytes(binary).ShouldBe([0, 1, 2, 255]);
        var fileSystemTasks = new FileSystemTasks(fileSystem);

        // Act
        fileSystemTasks.ClearArtifactDirectory(
            new BuildDirectoryPath(fileSystem, root),
            new BuildDirectoryPath(fileSystem, artifacts),
            new BuildDirectoryPath(fileSystem, publish));

        // Assert
        fileSystem.Directory.EnumerateFiles(artifacts, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ShouldBe(new[] { binary, index }.Order(StringComparer.Ordinal));
        fileSystem.Directory.EnumerateDirectories(artifacts).ShouldBe([publish]);
        fileSystem.File.ReadAllBytes(binary).ShouldBe([0, 1, 2, 255]);
        fileSystem.File.ReadAllBytes(index).ShouldBe("frontend"u8.ToArray());
        fileSystem.File.ReadAllBytes(control).ShouldBe([6, 5]);
    }

    [Test]
    public void ShouldRetainFiles_WhenPreservedDirectoryIsNotAnArtifactChild()
    {
        // Arrange
        const string root = "/repo";
        const string artifacts = "/repo/.artifacts/linux-x64";
        const string publish = "/repo/protected/publish";
        SetupFileSystem(system =>
        {
            system.AddFile(artifacts + "/old.bin", new MockFileData("old"));
            system.AddFile(publish + "/Reaparr.AppHost", new MockFileData("binary"));
        });
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var fileSystemTasks = CreateFileSystemTasks();

        // Act
        var action = () => fileSystemTasks.ClearArtifactDirectory(
            new BuildDirectoryPath(fileSystem, root),
            new BuildDirectoryPath(fileSystem, artifacts),
            new BuildDirectoryPath(fileSystem, publish));

        // Assert
        action.ShouldThrow<InvalidOperationException>();
        fileSystem.File.ReadAllText(artifacts + "/old.bin").ShouldBe("old");
        fileSystem.File.ReadAllText(publish + "/Reaparr.AppHost").ShouldBe("binary");
        fileSystem.Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ShouldBe([artifacts + "/old.bin", publish + "/Reaparr.AppHost"]);
    }

    [Test]
    public void ShouldClearDirectory_WhenArtifactDirectoryIsUnderDefaultArtifactsRoot()
    {
        // Arrange
        const string root = "/repo";
        const string artifactDirectory = "/repo/.artifacts/linux-x64";
        SetupFileSystem(system =>
        {
            system.AddDirectory(artifactDirectory);
            system.AddFile("/repo/.artifacts/linux-x64/old-file.txt", new MockFileData("old"));
            system.AddDirectory("/repo/.artifacts/linux-x64/old-directory");
        });

        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var fileSystemTasks = CreateFileSystemTasks();

        // Act
        fileSystemTasks.ClearArtifactDirectory(
            new BuildDirectoryPath(fileSystem, root), new BuildDirectoryPath(fileSystem, artifactDirectory));

        // Assert
        fileSystem.Directory.Exists(artifactDirectory).ShouldBeTrue();
        fileSystem.Directory.EnumerateFileSystemEntries(artifactDirectory).ShouldBeEmpty();
    }

    [Test]
    public void ShouldCopyNestedTreeWithoutChangingSource_WhenTargetIsAnAdjacentNormalizedDirectory()
    {
        // Arrange
        SetupFileSystem(system =>
        {
            system.AddFile("/repo/source/app.bin", new MockFileData(new byte[] { 0, 1, 255 }));
            system.AddFile("/repo/source/nested/config.json", new MockFileData("config"));
        });
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var source = new BuildDirectoryPath(fileSystem, "/repo/source/./");
        var target = new BuildDirectoryPath(fileSystem, "/repo/source/../source-other/");
        var fileSystemTasks = CreateFileSystemTasks();

        // Act
        fileSystemTasks.CopyDirectory(source, target);

        // Assert
        fileSystem.Directory.EnumerateFiles(target.Value, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ShouldBe(["/repo/source-other/app.bin", "/repo/source-other/nested/config.json"]);
        fileSystem.File.ReadAllBytes("/repo/source-other/app.bin").ShouldBe([0, 1, 255]);
        fileSystem.File.ReadAllText("/repo/source-other/nested/config.json").ShouldBe("config");
        fileSystem.Directory.EnumerateFiles(source.Value, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ShouldBe(["/repo/source/app.bin", "/repo/source/nested/config.json"]);
        fileSystem.File.ReadAllBytes("/repo/source/app.bin").ShouldBe([0, 1, 255]);
        fileSystem.File.ReadAllText("/repo/source/nested/config.json").ShouldBe("config");
    }

    private FileSystemTasks CreateFileSystemTasks() => new(Mock.Container.Resolve<IFileSystem>());
}
