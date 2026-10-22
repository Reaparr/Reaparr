using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Autofac;
using FastEndpoints;

namespace Reaparr.Build.UnitTests;

internal class DesktopPackageWorkflowUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldRemoveStaleArtifactsBeforePack_WithoutChangingPublishOrOtherRuntimeFiles()
    {
        // Arrange
        SetupFileSystem(system =>
        {
            system.AddFile("/repo/.artifacts/linux-x64/publish/Reaparr.AppHost", new MockFileData("binary"));
            system.AddFile("/repo/.artifacts/linux-x64/old.AppImage", new MockFileData("old"));
            system.AddFile("/repo/.artifacts/linux-x64/old-output/stale.bin", new MockFileData("stale"));
            system.AddFile("/repo/.artifacts/win-x64/keep.bin", new MockFileData("control"));
        });
        var settings = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
        };
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var paths = new BuildPaths("/repo", fileSystem);
        var publish = paths.PublishDirectory("linux-x64");
        var artifacts = paths.ArtifactDirectory("linux-x64");
        var packArguments = ExpectedPackArguments(publish, artifacts);
        (string Path, string Content)[]? filesAtPack = null;

        var publisher = new Mock<ICommandHandler<DesktopPublishBuildCommand, Result<int>>>(MockBehavior.Strict);
        publisher.Setup(x => x.ExecuteAsync(
                It.Is<DesktopPublishBuildCommand>(c => ReferenceEquals(c.Settings, settings)), CancellationToken))
            .ReturnsAsync(Result.Ok(0)).Verifiable(Times.Once());
        var runner = Mock.Mock<IDesktopCommandRunner>();
        runner.Setup(x => x.RequireCommandAsync("vpk")).Returns(Task.CompletedTask).Verifiable(Times.Once());
        runner.Setup(x => x.RunCommandAsync("vpk",
                It.Is<IReadOnlyList<string>>(arguments => arguments.SequenceEqual(packArguments)), null))
            .Callback(() => filesAtPack = fileSystem.Directory
                .EnumerateFiles("/repo/.artifacts", "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(file => (file, fileSystem.File.ReadAllText(file))).ToArray())
            .Returns(Task.CompletedTask).Verifiable(Times.Once());
        using var container = CreateBuildContainer(paths, fileSystem, runner.Object, publisher.Object);

        // Act
        var result = await container.Resolve<ICommandExecutor>().Send(new DesktopPackageBuildCommand(settings), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(0);
        filesAtPack.ShouldBe([
            (fileSystem.Path.Combine(publish, "Reaparr.AppHost"), "binary"),
            (fileSystem.Path.Combine(paths.ArtifactDirectory("win-x64"), "keep.bin"), "control"),
        ]);
        fileSystem.File.ReadAllText(fileSystem.Path.Combine(publish, "Reaparr.AppHost")).ShouldBe("binary");
        fileSystem.Directory.EnumerateDirectories(artifacts).ShouldBe([publish]);
        runner.Verify();
        publisher.Verify();
    }

    [Test]
    [Arguments(true, false, null)]
    [Arguments(false, true, null)]
    [Arguments(false, false, ".artifacts")]
    [Arguments(false, false, ".artifacts/linux-x64")]
    public async Task ShouldRetainFiles_WhenCleanupIsDisabledOrCustomOutputContainsPublish(
        bool dryRun, bool preserveArtifacts, string? artifactOverride)
    {
        // Arrange
        SetupFileSystem(system =>
        {
            system.AddFile("/repo/.artifacts/linux-x64/publish/Reaparr.AppHost", new MockFileData("binary"));
            system.AddFile("/repo/.artifacts/linux-x64/old.AppImage", new MockFileData("old"));
        });
        var settings = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
            DryRun = dryRun,
            PreserveExistingArtifacts = preserveArtifacts,
            ArtifactDirectory = artifactOverride,
        };
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var paths = new BuildPaths("/repo", fileSystem);
        var artifacts = artifactOverride is null
            ? paths.ArtifactDirectory("linux-x64")
            : fileSystem.Path.GetFullPath(artifactOverride, paths.RootDirectory);
        var packArguments = ExpectedPackArguments(paths.PublishDirectory("linux-x64"), artifacts);
        var before = fileSystem.Directory.EnumerateFiles("/repo", "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(file => (file, fileSystem.File.ReadAllText(file))).ToArray();

        var publisher = new Mock<ICommandHandler<DesktopPublishBuildCommand, Result<int>>>(MockBehavior.Strict);
        publisher.Setup(x => x.ExecuteAsync(
                It.Is<DesktopPublishBuildCommand>(c => ReferenceEquals(c.Settings, settings)), CancellationToken))
            .ReturnsAsync(Result.Ok(0)).Verifiable(Times.Once());
        var runner = Mock.Mock<IDesktopCommandRunner>();
        runner.Setup(x => x.RequireCommandAsync("vpk")).Returns(Task.CompletedTask)
            .Verifiable(dryRun ? Times.Never() : Times.Once());
        runner.Setup(x => x.RunCommandAsync("vpk",
                It.Is<IReadOnlyList<string>>(arguments => arguments.SequenceEqual(packArguments)), null))
            .Returns(Task.CompletedTask).Verifiable(dryRun ? Times.Never() : Times.Once());
        using var container = CreateBuildContainer(paths, fileSystem, runner.Object, publisher.Object);

        // Act
        var result = await container.Resolve<ICommandExecutor>().Send(new DesktopPackageBuildCommand(settings), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(0);
        fileSystem.Directory.EnumerateFiles("/repo", "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(file => (file, fileSystem.File.ReadAllText(file))).ShouldBe(before);
        runner.Verify();
        publisher.Verify();
    }

    [Test]
    public async Task ShouldRetainArtifactsAndSkipPack_WhenPublishFails()
    {
        // Arrange
        SetupFileSystem(system =>
        {
            system.AddFile("/repo/.artifacts/linux-x64/old.AppImage", new MockFileData("old"));
        });
        var settings = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
        };
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var paths = new BuildPaths("/repo", fileSystem);

        var publisher = new Mock<ICommandHandler<DesktopPublishBuildCommand, Result<int>>>(MockBehavior.Strict);
        publisher.Setup(x => x.ExecuteAsync(
                It.Is<DesktopPublishBuildCommand>(c => ReferenceEquals(c.Settings, settings)), CancellationToken))
            .ReturnsAsync(Result.Fail<int>("publish failed")).Verifiable(Times.Once());
        var runner = Mock.Mock<IDesktopCommandRunner>();
        runner.Setup(x => x.RequireCommandAsync("vpk")).Returns(Task.CompletedTask).Verifiable(Times.Once());
        runner.Setup(x => x.RunCommandAsync("vpk", It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask).Verifiable(Times.Never());
        using var container = CreateBuildContainer(paths, fileSystem, runner.Object, publisher.Object);

        // Act
        var result = await container.Resolve<ICommandExecutor>().Send(new DesktopPackageBuildCommand(settings), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        fileSystem.File.ReadAllText("/repo/.artifacts/linux-x64/old.AppImage").ShouldBe("old");
        fileSystem.Directory.EnumerateFiles("/repo", "*", SearchOption.AllDirectories)
            .ShouldBe(["/repo/.artifacts/linux-x64/old.AppImage"]);
        runner.Verify();
        publisher.Verify();
    }

    private static IContainer CreateBuildContainer(
        BuildPaths paths,
        IFileSystem fileSystem,
        IDesktopCommandRunner runner,
        ICommandHandler<DesktopPublishBuildCommand, Result<int>> publisher
    )
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<BuildModule>();
        builder.RegisterInstance(paths);
        builder.RegisterInstance(fileSystem);
        builder.RegisterInstance(runner);
        builder.RegisterInstance(publisher);
        builder.Register(_ => new Serilog.LoggerConfiguration().MinimumLevel.Warning().CreateLogger())
            .As<Serilog.ILogger>().SingleInstance();
        return builder.Build();
    }

    private static IReadOnlyList<string> ExpectedPackArguments(string publish, string artifacts) =>
    [
        "pack", "--packId", "Reaparr", "--packTitle", "Reaparr", "--packVersion", "1.2.3",
        "--packDir", publish, "--mainExe", "Reaparr.AppHost", "--runtime", "linux-x64",
        "--channel", "linux-x64-dev", "--outputDir", artifacts,
    ];
}
