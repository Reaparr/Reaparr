using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Autofac;

namespace Reaparr.Build.UnitTests;

internal class DesktopBuildWorkflowUnitTests : BaseUnitTest<DesktopRunBuildCommandHandler>
{
    [Test]
    public async Task ShouldNotInvokePackageCommand_WhenSkipPackageIsTrue()
    {
        // Arrange
        SetupFileSystem(system =>
        {
            system.AddFile("/repo/Reaparr.sln", new MockFileData(string.Empty));
            system.AddDirectory("/repo/src/AppHost/ClientApp/.output/public");
            system.AddDirectory("/repo/.artifacts/linux-x64/publish");
            system.AddFile("/repo/.artifacts/linux-x64/publish/Reaparr.AppHost", new MockFileData("bin"));
        });

        var settings = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
            SkipPackage = true,
            DryRun = true,
            SkipRestore = true,
        };

        // DesktopRunBuildCommandHandler resolves BuildPaths from DI. BuildPaths requires a
        // rootDirectory string constructor parameter, so tests must register it explicitly.
        // We use /repo because the test filesystem and assertions are arranged under that root.
        SetupDependencies(builder => builder.Register(_ => "/repo").As<string>().SingleInstance());

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<DesktopPublishBuildCommand>(command => ReferenceEquals(command.Settings, settings)), CancellationToken))
            .ReturnsAsync(Result.Ok(0))
            .Verifiable(Times.Once());

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<DesktopPackageBuildCommand>(command => ReferenceEquals(command.Settings, settings)), CancellationToken))
            .ReturnsAsync(Result.Ok(0))
            .Verifiable(Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<DesktopLaunchBuildCommand>(command => ReferenceEquals(command.Settings, settings)), CancellationToken))
            .ReturnsAsync(Result.Ok(0))
            .Verifiable(Times.Never());

        // Act
        var result = await Sut.ExecuteAsync(new DesktopRunBuildCommand(settings), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldNotInvokeLaunchCommand_WhenDryRunPackagesBuild()
    {
        // Arrange
        SetupFileSystem(system =>
        {
            system.AddFile("/repo/Reaparr.sln", new MockFileData(string.Empty));
        });

        var settings = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
            DryRun = true,
            SkipRestore = true,
            LaunchMode = "packaged",
        };

        SetupDependencies(builder => builder.Register(_ => "/repo").As<string>().SingleInstance());

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<DesktopPackageBuildCommand>(command => ReferenceEquals(command.Settings, settings)), CancellationToken))
            .ReturnsAsync(Result.Ok(0))
            .Verifiable(Times.Once());

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<DesktopLaunchBuildCommand>(command => ReferenceEquals(command.Settings, settings)), CancellationToken))
            .ReturnsAsync(Result.Ok(0))
            .Verifiable(Times.Never());

        // Act
        var result = await Sut.ExecuteAsync(new DesktopRunBuildCommand(settings), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldRejectAmbiguousPreservedAppImagesThroughRunWorkflow_WhenPackagingCompletes()
    {
        // Arrange
        const string rootDirectory = "/repo";
        SetupFileSystem(system =>
        {
            var paths = new BuildPaths(rootDirectory, system);
            system.AddFile(system.Path.Combine(paths.FrontendPublicDirectory, "index.html"), new MockFileData("frontend"));
            var artifacts = system.Path.GetFullPath("release-output", rootDirectory);
            system.AddFile(system.Path.Combine(artifacts, "Reaparr-1.0.AppImage"), new MockFileData("old"));
            system.AddFile(system.Path.Combine(artifacts, "Reaparr-2.0.AppImage"), new MockFileData("new"));
        });
        var settings = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
            SkipFrontend = true,
            SkipRestore = true,
            PreserveExistingArtifacts = true,
            LaunchMode = "packaged",
            ArtifactDirectory = "release-output",
        };
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var paths = new BuildPaths(rootDirectory, fileSystem);
        var artifacts = fileSystem.Path.GetFullPath(settings.ArtifactDirectory, rootDirectory);
        var runner = Mock.Mock<IDesktopCommandRunner>();
        runner.Setup(x => x.RequireCommandAsync("vpk")).Returns(Task.CompletedTask).Verifiable(Times.Once());
        runner.Setup(x => x.RequireCommandAsync("dotnet")).Returns(Task.CompletedTask).Verifiable(Times.Once());
        runner.Setup(x => x.RunCommandAsync("dotnet",
                It.Is<IReadOnlyList<string>>(arguments => arguments[0] == "publish" && arguments.Contains("--no-restore")), null))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());
        runner.Setup(x => x.RunCommandAsync("vpk",
                It.Is<IReadOnlyList<string>>(arguments => arguments[0] == "pack" && arguments[arguments.Count - 1] == artifacts), null))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());
        runner.Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Verifiable(Times.Never());
        using var container = CreateBuildContainer(paths, fileSystem, runner.Object);

        // Act
        var result = await container.Resolve<ICommandExecutor>().Send(new DesktopRunBuildCommand(settings), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Message.ShouldContain("Multiple packaged Linux artifacts");
        fileSystem.File.ReadAllText(fileSystem.Path.Combine(paths.PublishDirectory("linux-x64"), "wwwroot", "index.html"))
            .ShouldBe("frontend");
        fileSystem.Directory.EnumerateFiles(artifacts, "*.AppImage", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Select(path => (fileSystem.Path.GetFileName(path), fileSystem.File.ReadAllText(path)))
            .ShouldBe([("Reaparr-1.0.AppImage", "old"), ("Reaparr-2.0.AppImage", "new")]);
        runner.Verify(x => x.RunCommandAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()),
            Times.Exactly(2));
        runner.Verify();
    }

    private static IContainer CreateBuildContainer(
        BuildPaths paths, IFileSystem fileSystem, IDesktopCommandRunner runner)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<BuildModule>();
        builder.RegisterInstance(paths);
        builder.RegisterInstance(fileSystem);
        builder.RegisterInstance(runner);
        builder.Register(_ => new Serilog.LoggerConfiguration().MinimumLevel.Warning().CreateLogger())
            .As<Serilog.ILogger>().SingleInstance();
        return builder.Build();
    }
}
