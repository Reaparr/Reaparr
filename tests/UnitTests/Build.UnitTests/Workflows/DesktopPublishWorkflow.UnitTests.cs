using System.Reflection;
using System.IO.Abstractions;
using System.Runtime.Loader;
using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
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
            TelemetryHost = "https://telemetry.example.test/",
            TelemetryProjectToken = "phc_public-token_123",
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
            "-p:ReaparrTelemetryHost=https://telemetry.example.test",
            "-p:ReaparrTelemetryProjectToken=phc_public-token_123",
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
    public async Task ShouldEmbedUpdatedTelemetryMetadataInEntryAssembly_WhenPublishingConfiguredCiBuild()
    {
        // Arrange
        using var container = CreateSdkFixture(includeAppHostBuildConfiguration: true);
        var paths = container.Resolve<BuildPaths>();
        var settings = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
            TelemetryHost = "https://telemetry.eu.example.test:8443/",
            TelemetryProjectToken = "phc_public-token_123",
            SkipFrontend = true,
            SkipRestore = false,
        };

        // Act
        var executor = container.Resolve<ICommandExecutor>();
        var result = await executor.Send(new DesktopPublishBuildCommand(DesktopCiPackageCommand.CreateCiSettings(settings)), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var assemblyPath = Path.Combine(paths.PublishDirectory("linux-x64"), "AppHost.dll");
        var metadata = ReadAssemblyMetadata(assemblyPath);
        metadata["ReaparrTelemetryHost"].ShouldBe("https://telemetry.eu.example.test:8443");
        metadata["ReaparrTelemetryProjectToken"].ShouldBe("phc_public-token_123");
        // Act
        var runtime = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = paths.PublishDirectory("linux-x64"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        runtime.ArgumentList.Add(Path.GetFileName(assemblyPath));
        runtime.Environment["TELEMETRY_HOST"] = "runtime-override";
        runtime.Environment["TELEMETRY_PROJECT_TOKEN"] = "runtime-override";
        runtime.Environment["PostHog__HostUrl"] = "runtime-override";
        runtime.Environment["PostHog__ProjectToken"] = "runtime-override";
        runtime.Environment["ReaparrTelemetryHost"] = "runtime-override";
        runtime.Environment["ReaparrTelemetryProjectToken"] = "runtime-override";
        var startedProcess = Process.Start(runtime);
        startedProcess.ShouldNotBeNull();
        using var process = startedProcess!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync(CancellationToken);

        // Assert
        process.ExitCode.ShouldBe(0);
        output.Trim().ShouldBe("https://telemetry.eu.example.test:8443|phc_public-token_123");

        // Act
        var updatedSettings = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
            TelemetryHost = "https://telemetry.eu.example.test:8443",
            TelemetryProjectToken = "phc_changed-token_456",
            SkipFrontend = true,
            SkipRestore = false,
        };
        var updatedResult = await executor.Send(
            new DesktopPublishBuildCommand(DesktopCiPackageCommand.CreateCiSettings(updatedSettings)),
            CancellationToken
        );

        // Assert
        updatedResult.IsSuccess.ShouldBeTrue();
        updatedResult.Errors.Count.ShouldBe(0);
        var refreshedMetadata = ReadAssemblyMetadata(assemblyPath);
        refreshedMetadata["ReaparrTelemetryHost"].ShouldBe("https://telemetry.eu.example.test:8443");
        refreshedMetadata["ReaparrTelemetryProjectToken"].ShouldBe("phc_changed-token_456");

        // Act
        var unconfiguredResult = await executor.Send(
            new DesktopPublishBuildCommand(new DesktopCommandSettings
            {
                RuntimeIdentifier = "linux-x64",
                Version = "1.2.3",
                InformationalVersion = "1.2.3-dev.1",
                SkipFrontend = true,
                SkipRestore = false,
            }),
            CancellationToken
        );

        // Assert
        unconfiguredResult.IsSuccess.ShouldBeTrue();
        unconfiguredResult.Errors.Count.ShouldBe(0);
        var unconfiguredMetadata = ReadAssemblyMetadata(assemblyPath);
        unconfiguredMetadata.ContainsKey("ReaparrTelemetryHost").ShouldBeFalse();
        unconfiguredMetadata.ContainsKey("ReaparrTelemetryProjectToken").ShouldBeFalse();
    }

    [Test]
    [Arguments("", "", null)]
    [Arguments("https://telemetry.eu.example.test", "phc_public-token_123", null)]
    [Arguments("https://telemetry.self-hosted.example:8443/", "phc_public-token_123", null)]
    [Arguments("https://telemetry.example.test:443", "phc_public-token_123", null)]
    [Arguments("https://telemetry.example.test", "", "must be supplied together")]
    [Arguments("", "phc_public-token_123", "must be supplied together")]
    [Arguments("http://telemetry.example.test", "phc_public-token_123", "ReaparrTelemetryHost must be")]
    [Arguments("https://telemetry.example.test/path", "phc_public-token_123", "ReaparrTelemetryHost must be")]
    [Arguments("https://telemetry.example.test?source=test", "phc_public-token_123", "ReaparrTelemetryHost must be")]
    [Arguments("https://telemetry.example.test#fragment", "phc_public-token_123", "ReaparrTelemetryHost must be")]
    [Arguments("https://user:password@telemetry.example.test", "phc_public-token_123", "ReaparrTelemetryHost must be")]
    [Arguments("https://telemetry.example.test//", "phc_public-token_123", "ReaparrTelemetryHost must be")]
    [Arguments("https://telemetry.example.test:not-a-port", "phc_public-token_123", "ReaparrTelemetryHost must be")]
    [Arguments("https://telemetry.example.test", "phx_private-token", "ReaparrTelemetryProjectToken must be")]
    [Arguments("https://telemetry.example.test", "phc_public token", "ReaparrTelemetryProjectToken must be")]
    [Arguments("https://telemetry.example.test", "phc_", "ReaparrTelemetryProjectToken must be")]
    [Arguments("https://telemetry.example.test", "phc_quoted'token", "ReaparrTelemetryProjectToken must be")]
    public async Task ShouldValidateBuildInputs_WhenAppHostDefinesItsTelemetryConfiguration(
        string host,
        string token,
        string? expectedError
    )
    {
        // Arrange
        using var container = CreateSdkFixture(includeAppHostBuildConfiguration: true);
        var paths = container.Resolve<BuildPaths>();
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = paths.RootDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(paths.AppHostProject);
        start.ArgumentList.Add("-target:ValidateReaparrTelemetryConfiguration");
        start.ArgumentList.Add("-verbosity:quiet");
        start.ArgumentList.Add("-nologo");
        start.ArgumentList.Add($"-property:ReaparrTelemetryHost={host}");
        start.ArgumentList.Add($"-property:ReaparrTelemetryProjectToken={token}");

        // Act
        using var process = Process.Start(start)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(CancellationToken);

        // Assert
        process.ExitCode.ShouldBe(expectedError is null ? 0 : 1, output + error);
        if (expectedError is not null)
        {
            (output + error).ShouldContain(expectedError);
            if (token.Length > 4)
                (output + error).ShouldNotContain(token);
        }
    }

    [Test]
    public async Task ShouldRejectBuildInputs_WhenTelemetryHostPortIsOutOfRange()
    {
        // Arrange
        using var container = CreateSdkFixture(includeAppHostBuildConfiguration: true);
        var paths = container.Resolve<BuildPaths>();
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = paths.RootDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(paths.AppHostProject);
        start.ArgumentList.Add("-target:ValidateReaparrTelemetryConfiguration");
        start.ArgumentList.Add("-verbosity:quiet");
        start.ArgumentList.Add("-nologo");
        start.ArgumentList.Add("-property:ReaparrTelemetryHost=https://telemetry.example.test:99999");
        start.ArgumentList.Add("-property:ReaparrTelemetryProjectToken=phc_public-token_123");

        // Act
        using var process = Process.Start(start)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(CancellationToken);

        // Assert
        process.ExitCode.ShouldBe(1, output + error);
        (output + error).ShouldNotContain("phc_public-token_123");
    }

    [Test]
    public async Task ShouldKeepTelemetryMetadataOutOfOtherProjects_WhenBuildPropertiesAreForwarded()
    {
        // Arrange
        using var container = CreateSdkFixture(includeAppHostBuildConfiguration: true);
        var paths = container.Resolve<BuildPaths>();
        var fileSystem = container.Resolve<IFileSystem>();
        var dependencyDirectory = fileSystem.Path.Combine(paths.RootDirectory, "Dependency");
        var outputDirectory = fileSystem.Path.Combine(paths.RootDirectory, "dependency-publish");
        fileSystem.Directory.CreateDirectory(dependencyDirectory);
        var projectPath = fileSystem.Path.Combine(dependencyDirectory, "Dependency.csproj");
        fileSystem.File.WriteAllText(projectPath, """<Project Sdk="Microsoft.NET.Sdk" />""");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = paths.RootDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("publish");
        start.ArgumentList.Add(projectPath);
        start.ArgumentList.Add("--configuration");
        start.ArgumentList.Add("Debug");
        start.ArgumentList.Add("--output");
        start.ArgumentList.Add(outputDirectory);
        start.ArgumentList.Add("--verbosity");
        start.ArgumentList.Add("quiet");
        start.ArgumentList.Add("-p:ReaparrRuntimeMode=desktop");
        start.ArgumentList.Add("-p:ReaparrTelemetryHost=https://telemetry.example.test");
        start.ArgumentList.Add("-p:ReaparrTelemetryProjectToken=phx_must-not-be-embedded");

        // Act
        using var process = Process.Start(start)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(CancellationToken);

        // Assert
        process.ExitCode.ShouldBe(0, output + error);
        var metadata = ReadAssemblyMetadata(fileSystem.Path.Combine(outputDirectory, "Dependency.dll"));
        metadata["ReaparrRuntimeMode"].ShouldBe("desktop");
        metadata.ContainsKey("ReaparrTelemetryHost").ShouldBeFalse();
        metadata.ContainsKey("ReaparrTelemetryProjectToken").ShouldBeFalse();
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
        runner.Setup(x => x.LogCommand(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()));
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

    private static IReadOnlyDictionary<string, string?> ReadAssemblyMetadata(string assemblyPath)
    {
        var loadContext = new AssemblyLoadContext($"BuildMetadata_{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            using var assemblyStream = File.OpenRead(assemblyPath);
            return loadContext
                .LoadFromStream(assemblyStream)
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static IContainer CreateSdkFixture(bool includeAppHostBuildConfiguration = false)
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
        var repositoryRoot = string.Empty;
        var telemetryConfiguration = string.Empty;
        if (includeAppHostBuildConfiguration)
        {
            var repositoryPropertiesPath = FindRepositoryFile("Directory.Build.props");
            repositoryRoot = Path.GetDirectoryName(repositoryPropertiesPath)!;
            fileSystem.File.WriteAllText(
                fileSystem.Path.Combine(root, "Directory.Build.props"),
                File.ReadAllText(repositoryPropertiesPath)
            );
            var appHostProject = XDocument.Load(fileSystem.Path.Combine(repositoryRoot, "src", "AppHost", "AppHost.csproj")).Root!;
            var metadata = appHostProject.Elements("ItemGroup").Single(group =>
                group.Elements("AssemblyMetadata").Any(item => (string?)item.Attribute("Include") == "ReaparrTelemetryHost"));
            var validation = appHostProject.Elements("Target").Single(target =>
                (string?)target.Attribute("Name") == "ValidateReaparrTelemetryConfiguration");
            telemetryConfiguration = $"{metadata}\n{validation}";
        }
        var linkedEnvironmentSources = includeAppHostBuildConfiguration
            ? $"""
              <ItemGroup>
                <Compile Include="{Path.Combine(repositoryRoot, "src", "Environment", "AppBuildInfo.cs")}" Link="Environment/AppBuildInfo.cs" />
                <Compile Include="{Path.Combine(repositoryRoot, "src", "Environment", "Interfaces", "IAppBuildInfo.cs")}" Link="Environment/Interfaces/IAppBuildInfo.cs" />
                <Compile Include="{Path.Combine(repositoryRoot, "src", "Environment", "_Shared", "Enums", "OperatingSystemPlatform.cs")}" Link="Environment/_Shared/Enums/OperatingSystemPlatform.cs" />
              </ItemGroup>
              """
            : string.Empty;
        fileSystem.File.WriteAllText(
            paths.AppHostProject,
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
                <UseAppHost>false</UseAppHost>
                <SelfContained>false</SelfContained>
              </PropertyGroup>
              {linkedEnvironmentSources}
              {telemetryConfiguration}
            </Project>
            """
        );
        var programSource = includeAppHostBuildConfiguration
            ? """
              using Reaparr.Environment;

              var buildInfo = new AppBuildInfo();
              System.Console.WriteLine($"{buildInfo.TelemetryHost}|{buildInfo.TelemetryProjectToken}");
              """
            : "System.Console.WriteLine(\"desktop-restore-fixture\");";
        fileSystem.File.WriteAllText(
            fileSystem.Path.Combine(projectDirectory, "Program.cs"),
            programSource
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

    private static string FindRepositoryFile(string fileName)
    {
        var directory = new DirectoryInfo(System.Environment.CurrentDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate) && File.Exists(Path.Combine(directory.FullName, "Reaparr.sln")))
                return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate {fileName} from the current directory.");
    }
}
