using System.IO.Abstractions;
using System.Text.Json;
using Reaparr.Domain;
using System.IO.Abstractions.TestingHelpers;
using Autofac;

namespace Reaparr.Build.UnitTests;

internal class DesktopPublishWorkflowUnitTests : BaseUnitTest<DesktopPublishBuildCommandHandler>
{
    [Test]
    [Arguments("", "1.2.3-dev.1", "--version")]
    [Arguments("1.2.3", "", "--informational-version")]
    public async Task ShouldFailBeforeExecutingCommands_WhenRequiredVersionIsMissing(
        string version,
        string informationalVersion,
        string requiredOption
    )
    {
        // Arrange
        var settings = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = version,
            InformationalVersion = informationalVersion,
            DryRun = true,
        };

        // BuildPaths requires a rootDirectory string matching the mocked filesystem.
        SetupDependencies(builder => builder.Register(_ => "/repo").As<string>().SingleInstance());
        Mock.Mock<IDesktopCommandRunner>()
            .Setup(x => x.RequireCommandAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());
        Mock.Mock<IDesktopCommandRunner>()
            .Setup(x => x.RunCommandAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());

        // Act
        var result = await Sut.ExecuteAsync(new DesktopPublishBuildCommand(settings), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().Message.ShouldContain(requiredOption);
        Mock.Mock<IDesktopCommandRunner>().Verify();
    }

    [Test]
    public async Task ShouldReuseExistingFrontendOutput_WhenFrontendPublicDirectoryAlreadyExists()
    {
        // Arrange
        SetupFileSystem(system =>
        {
            system.AddDirectory("/repo/src/AppHost/ClientApp/.output/public");
            system.AddFile("/repo/src/AppHost/ClientApp/.output/public/index.html", new MockFileData("frontend"));
        });

        var settings = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
            DryRun = false,
            SkipRestore = true,
        };

        SetupDependencies(builder => builder.Register(_ => "/repo").As<string>().SingleInstance());

        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var publishDirectory = fileSystem.Path.Combine("/repo", ".artifacts", "linux-x64", "publish");
        var publishedIndex = fileSystem.Path.Combine(publishDirectory, "wwwroot", "index.html");
        string[] publishArguments =
        [
            "publish",
            fileSystem.Path.Combine("/repo", "src", "AppHost", "AppHost.csproj"),
            "--no-restore",
            "-p:PublishProfile=Desktop-linux-x64",
            "-p:Version=1.2.3",
            "-p:InformationalVersion=1.2.3-dev.1",
            $"-p:PublishDir={publishDirectory}{fileSystem.Path.DirectorySeparatorChar}",
            "-p:CSharpier_Bypass=true",
        ];

        Mock.Mock<IDesktopCommandRunner>()
            .Setup(x => x.RequireCommandAsync("dotnet"))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDesktopCommandRunner>()
            .Setup(x =>
                x.RunCommandAsync(
                    "dotnet",
                    It.Is<IReadOnlyList<string>>(arguments => arguments.SequenceEqual(publishArguments)),
                    null
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDesktopCommandRunner>()
            .Setup(x => x.RequireCommandAsync("bun"))
            .Verifiable(Times.Never());
        Mock.Mock<IDesktopCommandRunner>()
            .Setup(x => x.RunCommandAsync("bun", It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Verifiable(Times.Never());

        // Act
        var result = await Sut.ExecuteAsync(new DesktopPublishBuildCommand(settings), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(0);
        fileSystem.File.ReadAllText(publishedIndex).ShouldBe("frontend");
        fileSystem.File.ReadAllText("/repo/src/AppHost/ClientApp/.output/public/index.html").ShouldBe("frontend");
        Mock.Mock<IDesktopCommandRunner>()
            .Verify(
                x => x.RunCommandAsync(
                    "dotnet",
                    It.Is<IReadOnlyList<string>>(arguments => arguments[0] == "restore"),
                    It.IsAny<string?>()
                ),
                Times.Never()
            );
        Mock.Mock<IDesktopCommandRunner>().Verify();
    }

    [Test]
    public async Task ShouldRestoreAndPublishRealSdkAssets_WhenRestoreIsEnabled()
    {
        // Arrange
        using var container = CreateSdkFixture();
        var paths = container.Resolve<BuildPaths>();
        var fileSystem = container.Resolve<IFileSystem>();
        var assetsPath = fileSystem.Path.Combine(
            fileSystem.Path.GetDirectoryName(paths.AppHostProject)!,
            "obj",
            "project.assets.json"
        );
        var settings = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
            SkipFrontend = true,
            SkipRestore = false,
        };
        fileSystem.File.Exists(assetsPath).ShouldBeFalse();

        // Act
        var result = await container.Resolve<ICommandExecutor>().Send(
            new DesktopPublishBuildCommand(settings),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(0);
        using var assets = JsonDocument.Parse(fileSystem.File.ReadAllText(assetsPath));
        assets.RootElement.GetProperty("targets").TryGetProperty("net10.0/linux-x64", out _).ShouldBeTrue();
        fileSystem.File.Exists(fileSystem.Path.Combine(paths.PublishDirectory("linux-x64"), "AppHost.dll"))
            .ShouldBeTrue();
        fileSystem.File.ReadAllText(
            fileSystem.Path.Combine(paths.PublishDirectory("linux-x64"), "wwwroot", "index.html")
        ).ShouldBe("frontend-fixture");
    }

    [Test]
    public async Task ShouldFailWithoutCreatingRestoreAssets_WhenSkipRestoreHasNoAssets()
    {
        // Arrange
        using var container = CreateSdkFixture();
        var paths = container.Resolve<BuildPaths>();
        var fileSystem = container.Resolve<IFileSystem>();
        var assetsPath = fileSystem.Path.Combine(
            fileSystem.Path.GetDirectoryName(paths.AppHostProject)!,
            "obj",
            "project.assets.json"
        );
        var settings = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
            SkipFrontend = true,
            SkipRestore = true,
        };
        fileSystem.File.Exists(assetsPath).ShouldBeFalse();

        // Act
        var result = await container.Resolve<ICommandExecutor>().Send(
            new DesktopPublishBuildCommand(settings),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().Message.ShouldContain("--no-restore");
        fileSystem.File.Exists(assetsPath).ShouldBeFalse();
        fileSystem.File.Exists(fileSystem.Path.Combine(paths.PublishDirectory("linux-x64"), "AppHost.dll"))
            .ShouldBeFalse();
        fileSystem.File.ReadAllText(fileSystem.Path.Combine(paths.FrontendPublicDirectory, "index.html"))
            .ShouldBe("frontend-fixture");
    }

    [Test]
    public async Task ShouldLeaveFilesUntouched_WhenDryRunIsEnabled()
    {
        // Arrange
        SetupFileSystem(system =>
        {
            system.AddFile("/repo/src/AppHost/AppHost.csproj", new MockFileData("project-sentinel"));
            system.AddFile("/repo/src/AppHost/ClientApp/.output/public/index.html", new MockFileData("frontend-fixture"));
            system.AddFile("/repo/retained.txt", new MockFileData("retained"));
        });
        var runner = new Mock<IDesktopCommandRunner>(MockBehavior.Strict);
        SetupDependencies(builder =>
        {
            builder.Register(_ => "/repo").As<string>().SingleInstance();
            builder.RegisterInstance(runner.Object).As<IDesktopCommandRunner>();
        });
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var paths = Mock.Container.Resolve<BuildPaths>();
        var settings = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
            SkipFrontend = true,
            SkipRestore = false,
            DryRun = true,
        };
        fileSystem.File.ReadAllText(fileSystem.Path.Combine(paths.FrontendPublicDirectory, "index.html"))
            .ShouldBe("frontend-fixture");
        var before = fileSystem.Directory
            .EnumerateFiles(paths.RootDirectory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(file => (fileSystem.Path.GetRelativePath(paths.RootDirectory, file), fileSystem.File.ReadAllText(file)))
            .ToArray();

        // Act
        var result = await Sut.ExecuteAsync(new DesktopPublishBuildCommand(settings), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(0);
        fileSystem.Directory
            .EnumerateFiles(paths.RootDirectory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(file => (fileSystem.Path.GetRelativePath(paths.RootDirectory, file), fileSystem.File.ReadAllText(file)))
            .ShouldBe(before);
        fileSystem.Directory.Exists(paths.ArtifactDirectory("linux-x64")).ShouldBeFalse();
        fileSystem.Directory.Exists(
            fileSystem.Path.Combine(fileSystem.Path.GetDirectoryName(paths.AppHostProject)!, "obj")
        ).ShouldBeFalse();
        runner.Verify(x => x.RequireCommandAsync(It.IsAny<string>()), Times.Never);
        runner.Verify(
            x => x.RunCommandAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()),
            Times.Never
        );
    }

    private static IContainer CreateSdkFixture()
    {
        var fileSystem = new System.IO.Abstractions.FileSystem();
        var root = fileSystem.Path.Combine(
            fileSystem.Path.GetTempPath(),
            nameof(DesktopPublishWorkflowUnitTests),
            Guid.NewGuid().ToString("N")
        );
        var paths = new BuildPaths(root, fileSystem);
        var projectDirectory = fileSystem.Path.GetDirectoryName(paths.AppHostProject)!;
        fileSystem.Directory.CreateDirectory(projectDirectory);
        fileSystem.File.WriteAllText(
            paths.AppHostProject,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
                <UseAppHost>false</UseAppHost>
                <SelfContained>false</SelfContained>
              </PropertyGroup>
            </Project>
            """
        );
        fileSystem.File.WriteAllText(
            fileSystem.Path.Combine(projectDirectory, "Program.cs"),
            "System.Console.WriteLine(\"desktop-restore-fixture\");"
        );
        var profileDirectory = fileSystem.Path.Combine(projectDirectory, "Properties", "PublishProfiles");
        fileSystem.Directory.CreateDirectory(profileDirectory);
        fileSystem.File.WriteAllText(
            fileSystem.Path.Combine(profileDirectory, "Desktop-linux-x64.pubxml"),
            """
            <Project>
              <PropertyGroup>
                <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
                <UseAppHost>false</UseAppHost>
                <SelfContained>false</SelfContained>
              </PropertyGroup>
            </Project>
            """
        );
        fileSystem.File.WriteAllText(
            fileSystem.Path.Combine(root, "NuGet.Config"),
            """
            <configuration>
              <packageSources>
                <clear />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
              </packageSources>
            </configuration>
            """
        );
        fileSystem.Directory.CreateDirectory(paths.FrontendPublicDirectory);
        fileSystem.File.WriteAllText(
            fileSystem.Path.Combine(paths.FrontendPublicDirectory, "index.html"),
            "frontend-fixture"
        );
        var builder = new ContainerBuilder();
        builder.RegisterModule<BuildModule>();
        builder.RegisterInstance(fileSystem).As<IFileSystem>();
        builder.RegisterInstance(paths);
        builder.Register(_ => new Serilog.LoggerConfiguration().MinimumLevel.Warning().CreateLogger())
            .As<Serilog.ILogger>()
            .SingleInstance();
        return builder.Build();
    }
}
