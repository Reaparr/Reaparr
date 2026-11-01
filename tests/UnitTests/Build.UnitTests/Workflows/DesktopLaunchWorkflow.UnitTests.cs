using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Autofac;

namespace Reaparr.Build.UnitTests;

internal class DesktopLaunchWorkflowUnitTests : BaseUnitTest<DesktopLaunchBuildCommandHandler>
{
    [Test]
    public async Task ShouldLaunchOnlyTopLevelAppImage_WhenLinuxPackagedLaunchIsRequested()
    {
        // Arrange
        const string rootDirectory = "/repo";
        const string artifactDirectorySetting = "release-output";
        const string artifactName = "Reaparr-1.1.0.AppImage";
        const string nestedArtifactName = "Reaparr-nested.AppImage";
        var writeTime = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

        SetupFileSystem(system =>
        {
            var artifactDirectory = system.Path.GetFullPath(artifactDirectorySetting, rootDirectory);
            var nestedArtifactDirectory = system.Path.Combine(artifactDirectory, "nested");
            system.AddDirectory(artifactDirectory);
            system.AddDirectory(nestedArtifactDirectory);
            system.AddFile(
                system.Path.Combine(artifactDirectory, artifactName),
                new MockFileData("app image") { LastWriteTime = writeTime }
            );
            system.AddFile(
                system.Path.Combine(nestedArtifactDirectory, nestedArtifactName),
                new MockFileData("nested") { LastWriteTime = writeTime.AddDays(1) }
            );
            system.AddFile(system.Path.Combine(artifactDirectory, "not-an-app-image.zip"), new MockFileData("other"));
        });

        SetupDependencies(builder => builder.Register(_ => rootDirectory).As<string>().SingleInstance());
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var artifactDirectory = fileSystem.Path.GetFullPath(artifactDirectorySetting, rootDirectory);
        var appImage = fileSystem.Path.Combine(artifactDirectory, artifactName);

        var settings = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            LaunchMode = "packaged",
            ArtifactDirectory = artifactDirectorySetting,
        };

        Mock.Mock<IDesktopCommandRunner>()
            .Setup(x =>
                x.ExecuteCommandAsync(
                    "chmod",
                    It.Is<IReadOnlyList<string>>(arguments =>
                        arguments.Count == 2 && arguments[0] == "+x" && arguments[1] == appImage
                    ),
                    null
                )
            )
            .ReturnsAsync(0)
            .Verifiable(Times.Once());

        Mock.Mock<IDesktopCommandRunner>()
            .Setup(x =>
                x.ExecuteCommandAsync(
                    appImage,
                    It.Is<IReadOnlyList<string>>(arguments => arguments.Count == 0),
                    null
                )
            )
            .ReturnsAsync(17)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new DesktopLaunchBuildCommand(settings), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(17);
        Mock.Mock<IDesktopCommandRunner>().Verify();
    }

    [Test]
    public async Task ShouldFailWithoutCommandsOrFileChanges_WhenMultipleTopLevelAppImagesExist()
    {
        // Arrange
        const string rootDirectory = "/repo";
        const string artifactDirectorySetting = "release-output";
        var older = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        SetupFileSystem(system =>
        {
            var directory = system.Path.GetFullPath(artifactDirectorySetting, rootDirectory);
            system.AddFile(system.Path.Combine(directory, "Reaparr-1.0.AppImage"), new MockFileData("old") { LastWriteTime = older });
            system.AddFile(system.Path.Combine(directory, "Reaparr-2.0.AppImage"), new MockFileData("new") { LastWriteTime = older.AddDays(1) });
            system.AddFile(system.Path.Combine(directory, "nested", "Reaparr.AppImage"), new MockFileData("nested"));
            system.AddFile(system.Path.Combine(directory, "Reaparr.zip"), new MockFileData("archive"));
        });
        SetupDependencies(builder => builder.Register(_ => rootDirectory).As<string>().SingleInstance());
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var directory = fileSystem.Path.GetFullPath(artifactDirectorySetting, rootDirectory);
        fileSystem.Directory.EnumerateFiles(directory, "*.AppImage", SearchOption.TopDirectoryOnly)
            .Select(fileSystem.Path.GetFileName).Order(StringComparer.Ordinal)
            .ShouldBe(["Reaparr-1.0.AppImage", "Reaparr-2.0.AppImage"]);
        var before = fileSystem.Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => (path, fileSystem.File.ReadAllText(path), fileSystem.File.GetLastWriteTimeUtc(path), fileSystem.File.GetAttributes(path)))
            .ToArray();
        var settings = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            LaunchMode = "packaged",
            ArtifactDirectory = artifactDirectorySetting,
        };
        Mock.Mock<IDesktopCommandRunner>()
            .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Verifiable(Times.Never());

        // Act
        var result = await Sut.ExecuteAsync(new DesktopLaunchBuildCommand(settings), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Message.ShouldContain("Multiple packaged Linux artifacts");
        result.Errors[0].Message.ShouldContain("exactly one top-level AppImage");
        fileSystem.Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => (path, fileSystem.File.ReadAllText(path), fileSystem.File.GetLastWriteTimeUtc(path), fileSystem.File.GetAttributes(path)))
            .ShouldBe(before);
        Mock.Mock<IDesktopCommandRunner>().Verify();
    }

    [Test]
    public async Task ShouldFail_WhenLinuxPackagedArtifactIsMissing()
    {
        // Arrange
        const string rootDirectory = "/repo";
        const string runtimeIdentifier = "linux-x64";
        SetupFileSystem(system =>
        {
            var artifactDirectory = system.Path.Combine(rootDirectory, ".artifacts", runtimeIdentifier);
            var nestedArtifactDirectory = system.Path.Combine(artifactDirectory, "nested");
            system.AddDirectory(artifactDirectory);
            system.AddDirectory(nestedArtifactDirectory);
            system.AddFile(
                system.Path.Combine(nestedArtifactDirectory, "Reaparr.AppImage"),
                new MockFileData("nested")
            );
            system.AddFile(system.Path.Combine(artifactDirectory, "Reaparr.zip"), new MockFileData("not an AppImage"));
        });

        var settings = new DesktopCommandSettings { RuntimeIdentifier = runtimeIdentifier, LaunchMode = "packaged" };

        SetupDependencies(builder => builder.Register(_ => rootDirectory).As<string>().SingleInstance());
        Mock.Mock<IDesktopCommandRunner>()
            .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Verifiable(Times.Never());

        // Act
        var result = await Sut.ExecuteAsync(new DesktopLaunchBuildCommand(settings), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Message.ShouldContain("No packaged Linux artifact");
        Mock.Mock<IDesktopCommandRunner>().Verify();
    }

    [Test]
    public async Task ShouldFail_WhenLinuxPackagedArtifactDirectoryIsMissing()
    {
        // Arrange
        SetupFileSystem();

        var settings = new DesktopCommandSettings { RuntimeIdentifier = "linux-x64", LaunchMode = "packaged" };

        SetupDependencies(builder => builder.Register(_ => "/repo").As<string>().SingleInstance());
        Mock.Mock<IDesktopCommandRunner>()
            .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Verifiable(Times.Never());

        // Act
        var result = await Sut.ExecuteAsync(new DesktopLaunchBuildCommand(settings), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Message.ShouldContain("artifact directory");
        Mock.Mock<IDesktopCommandRunner>().Verify();
    }

    [Test]
    public async Task ShouldFailAndNotLaunch_WhenChmodFailsForLinuxPackagedArtifact()
    {
        // Arrange
        const string rootDirectory = "/repo";
        const string runtimeIdentifier = "linux-x64";
        SetupFileSystem(system =>
        {
            var artifactDirectory = system.Path.Combine(rootDirectory, ".artifacts", runtimeIdentifier);
            var appImage = system.Path.Combine(artifactDirectory, "Reaparr.AppImage");
            system.AddDirectory(artifactDirectory);
            system.AddFile(appImage, new MockFileData("app image"));
        });

        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var artifactDirectory = fileSystem.Path.Combine(rootDirectory, ".artifacts", runtimeIdentifier);
        var appImage = fileSystem.Path.Combine(artifactDirectory, "Reaparr.AppImage");

        var settings = new DesktopCommandSettings { RuntimeIdentifier = runtimeIdentifier, LaunchMode = "packaged" };

        SetupDependencies(builder => builder.Register(_ => rootDirectory).As<string>().SingleInstance());

        Mock.Mock<IDesktopCommandRunner>()
            .Setup(x =>
                x.ExecuteCommandAsync(
                    "chmod",
                    It.Is<IReadOnlyList<string>>(arguments =>
                        arguments.Count == 2 && arguments[0] == "+x" && arguments[1] == appImage
                    ),
                    null
                )
            )
            .ReturnsAsync(9)
            .Verifiable(Times.Once());

        Mock.Mock<IDesktopCommandRunner>()
            .Setup(x =>
                x.ExecuteCommandAsync(appImage, It.Is<IReadOnlyList<string>>(arguments => arguments.Count == 0), null)
            )
            .ReturnsAsync(0)
            .Verifiable(Times.Never());

        // Act
        var result = await Sut.ExecuteAsync(new DesktopLaunchBuildCommand(settings), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Message.ShouldContain("chmod exited with code 9");
        Mock.Mock<IDesktopCommandRunner>().Verify();
    }

    [Test]
    public async Task ShouldLaunchPublishedExecutable_WhenLinuxLaunchModeIsPublished()
    {
        // Arrange
        const string rootDirectory = "/repo";
        const string runtimeIdentifier = "linux-x64";
        SetupFileSystem(system =>
        {
            var artifactDirectory = system.Path.Combine(rootDirectory, ".artifacts", runtimeIdentifier);
            var publishDirectory = system.Path.Combine(artifactDirectory, "publish");
            var publishedExecutable = system.Path.Combine(publishDirectory, "Reaparr.AppHost");
            system.AddDirectory(publishDirectory);
            system.AddFile(publishedExecutable, new MockFileData("published"));
        });

        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var artifactDirectory = fileSystem.Path.Combine(rootDirectory, ".artifacts", runtimeIdentifier);
        var publishDirectory = fileSystem.Path.Combine(artifactDirectory, "publish");
        var publishedExecutable = fileSystem.Path.Combine(publishDirectory, "Reaparr.AppHost");

        var settings = new DesktopCommandSettings { RuntimeIdentifier = runtimeIdentifier, LaunchMode = "published" };

        SetupDependencies(builder => builder.Register(_ => rootDirectory).As<string>().SingleInstance());

        Mock.Mock<IDesktopCommandRunner>()
            .Setup(x =>
                x.ExecuteCommandAsync(
                    publishedExecutable,
                    It.Is<IReadOnlyList<string>>(arguments => arguments.Count == 0),
                    null
                )
            )
            .ReturnsAsync(23)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new DesktopLaunchBuildCommand(settings), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(23);
        Mock.Mock<IDesktopCommandRunner>().Verify();
    }

    [Test]
    public async Task ShouldLaunchPublishedExecutable_WhenLinuxPackagingIsSkipped()
    {
        // Arrange
        const string rootDirectory = "/repo";
        const string runtimeIdentifier = "linux-x64";
        SetupFileSystem(system =>
        {
            var artifactDirectory = system.Path.Combine(rootDirectory, ".artifacts", runtimeIdentifier);
            var publishDirectory = system.Path.Combine(artifactDirectory, "publish");
            var publishedExecutable = system.Path.Combine(publishDirectory, "Reaparr.AppHost");
            system.AddDirectory(publishDirectory);
            system.AddFile(publishedExecutable, new MockFileData("published"));
        });

        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var artifactDirectory = fileSystem.Path.Combine(rootDirectory, ".artifacts", runtimeIdentifier);
        var publishDirectory = fileSystem.Path.Combine(artifactDirectory, "publish");
        var publishedExecutable = fileSystem.Path.Combine(publishDirectory, "Reaparr.AppHost");

        var settings = new DesktopCommandSettings
        {
            RuntimeIdentifier = runtimeIdentifier,
            LaunchMode = "packaged",
            SkipPackage = true,
        };

        SetupDependencies(builder => builder.Register(_ => rootDirectory).As<string>().SingleInstance());

        Mock.Mock<IDesktopCommandRunner>()
            .Setup(x =>
                x.ExecuteCommandAsync(
                    publishedExecutable,
                    It.Is<IReadOnlyList<string>>(arguments => arguments.Count == 0),
                    null
                )
            )
            .ReturnsAsync(29)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new DesktopLaunchBuildCommand(settings), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(29);
        Mock.Mock<IDesktopCommandRunner>().Verify();
    }

    [Test]
    public async Task ShouldUseExpectedWindowsLaunchFallback_OnEachHost()
    {
        // Arrange
        const string rootDirectory = "/repo";
        const string runtimeIdentifier = "win-x64";
        SetupFileSystem(system =>
        {
            var artifactDirectory = system.Path.Combine(rootDirectory, ".artifacts", runtimeIdentifier);
            var publishDirectory = system.Path.Combine(artifactDirectory, "publish");
            var publishedExecutable = system.Path.Combine(publishDirectory, "Reaparr.AppHost.exe");
            system.AddDirectory(publishDirectory);
            system.AddFile(publishedExecutable, new MockFileData("published"));
        });

        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var artifactDirectory = fileSystem.Path.Combine(rootDirectory, ".artifacts", runtimeIdentifier);
        var publishDirectory = fileSystem.Path.Combine(artifactDirectory, "publish");
        var publishedExecutable = fileSystem.Path.Combine(publishDirectory, "Reaparr.AppHost.exe");

        var settings = new DesktopCommandSettings { RuntimeIdentifier = runtimeIdentifier };
        SetupDependencies(builder => builder.Register(_ => rootDirectory).As<string>().SingleInstance());

        if (OperatingSystem.IsWindows())
        {
            Mock.Mock<IDesktopCommandRunner>()
                .Setup(x =>
                    x.ExecuteCommandAsync(
                        publishedExecutable,
                        It.Is<IReadOnlyList<string>>(arguments => arguments.Count == 0),
                        null
                    )
                )
                .ReturnsAsync(31)
                .Verifiable(Times.Once());
        }
        else
        {
            Mock.Mock<IDesktopCommandRunner>()
                .Setup(x => x.CommandExistsAsync("wine"))
                .ReturnsAsync(true)
                .Verifiable(Times.Once());

            Mock.Mock<IDesktopCommandRunner>()
                .Setup(x =>
                    x.ExecuteCommandAsync(
                        "wine",
                        It.Is<IReadOnlyList<string>>(arguments =>
                            arguments.Count == 1 && arguments[0] == publishedExecutable
                        ),
                        null
                    )
                )
                .ReturnsAsync(31)
                .Verifiable(Times.Once());
        }

        // Act
        var result = await Sut.ExecuteAsync(new DesktopLaunchBuildCommand(settings), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(31);
        Mock.Mock<IDesktopCommandRunner>().Verify();
    }

    [Test]
    public async Task ShouldUseExpectedWindowsLaunchFallback_WhenWineIsUnavailable()
    {
        // Arrange
        const string rootDirectory = "/repo";
        const string runtimeIdentifier = "win-x64";
        SetupFileSystem(system =>
        {
            var artifactDirectory = system.Path.Combine(rootDirectory, ".artifacts", runtimeIdentifier);
            var publishDirectory = system.Path.Combine(artifactDirectory, "publish");
            var publishedExecutable = system.Path.Combine(publishDirectory, "Reaparr.AppHost.exe");
            system.AddDirectory(publishDirectory);
            system.AddFile(publishedExecutable, new MockFileData("published"));
        });

        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var artifactDirectory = fileSystem.Path.Combine(rootDirectory, ".artifacts", runtimeIdentifier);
        var publishDirectory = fileSystem.Path.Combine(artifactDirectory, "publish");
        var publishedExecutable = fileSystem.Path.Combine(publishDirectory, "Reaparr.AppHost.exe");

        var settings = new DesktopCommandSettings { RuntimeIdentifier = runtimeIdentifier };
        SetupDependencies(builder => builder.Register(_ => rootDirectory).As<string>().SingleInstance());

        if (OperatingSystem.IsWindows())
        {
            Mock.Mock<IDesktopCommandRunner>()
                .Setup(x =>
                    x.ExecuteCommandAsync(
                        publishedExecutable,
                        It.Is<IReadOnlyList<string>>(arguments => arguments.Count == 0),
                        null
                    )
                )
                .ReturnsAsync(0)
                .Verifiable(Times.Once());
        }
        else
        {
            Mock.Mock<IDesktopCommandRunner>()
                .Setup(x => x.CommandExistsAsync("wine"))
                .ReturnsAsync(false)
                .Verifiable(Times.Once());
        }

        // Act
        var result = await Sut.ExecuteAsync(new DesktopLaunchBuildCommand(settings), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(0);
        Mock.Mock<IDesktopCommandRunner>().Verify();
    }
}
