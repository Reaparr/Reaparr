using System.IO.Abstractions;
using FastEndpoints;
using FluentValidation;
using Reaparr.Logging;

namespace Reaparr.Build;

internal sealed record DesktopPublishBuildCommand(DesktopCommandSettings Settings) : ICommand<Result<int>>;

internal sealed class DesktopPublishBuildCommandValidator : AbstractValidator<DesktopPublishBuildCommand>
{
    public DesktopPublishBuildCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Settings).NotNull();
    }
}

internal sealed class DesktopPublishBuildCommandHandler : ICommandHandler<DesktopPublishBuildCommand, Result<int>>
{
    private readonly ILogger _log;
    private readonly BuildPaths _paths;
    private readonly IDesktopCommandRunner _commandRunner;
    private readonly FileSystemTasks _fileSystemTasks;
    private readonly IFileSystem _fileSystem;

    public DesktopPublishBuildCommandHandler(
        ILogger log,
        BuildPaths paths,
        IDesktopCommandRunner commandRunner,
        FileSystemTasks fileSystemTasks,
        IFileSystem fileSystem
    )
    {
        _log = log.ForContext<DesktopPublishBuildCommandHandler>();
        _paths = paths;
        _commandRunner = commandRunner;
        _fileSystemTasks = fileSystemTasks;
        _fileSystem = fileSystem;
    }

    public async Task<Result<int>> ExecuteAsync(DesktopPublishBuildCommand command, CancellationToken cancellationToken)
    {
        var settings = command.Settings;
        var runtime = DesktopRuntimeCatalog.Get(settings.RuntimeIdentifier);

        if (string.IsNullOrWhiteSpace(settings.Version))
            return Result.Fail<int>("A build version is required. Pass --version <VERSION>.");

        if (string.IsNullOrWhiteSpace(settings.InformationalVersion))
            return Result.Fail<int>("An informational version is required. Pass --informational-version <VERSION>.");

        if (!settings.DryRun)
            await _commandRunner.RequireCommandAsync("dotnet");

        if (!settings.SkipFrontend)
        {
            var sourceDirectory = GetFrontendPublicDirectory(settings);
            if (!_fileSystem.Directory.Exists(sourceDirectory))
            {
                if (!settings.DryRun)
                    await _commandRunner.RequireCommandAsync("bun");

                if (ShouldInstallFrontendDependencies(_paths.ClientAppDirectory))
                {
                    _log.Here()
                        .Information(
                            "Installing frontend dependencies in {ClientAppDirectory}",
                            _paths.ClientAppDirectory
                        );
                    if (!settings.DryRun)
                        await _commandRunner.RunCommandAsync(
                            "bun",
                            ["install", "--frozen-lockfile"],
                            _paths.ClientAppDirectory
                        );
                }

                if (!settings.DryRun)
                    await _commandRunner.RunCommandAsync(
                        "bun",
                        ["run", "generate", "--fail-on-error"],
                        _paths.ClientAppDirectory
                    );
            }
            else
            {
                _log.Here()
                    .Information("Reusing existing frontend output from {FrontendPublicDirectory}", sourceDirectory);
            }
        }

        if (!settings.SkipRestore && !settings.DryRun)
            await _commandRunner.RunCommandAsync(
                "dotnet",
                ["restore", _paths.AppHostProject, "--runtime", runtime.RuntimeIdentifier]
            );

        if (!settings.DryRun)
        {
            var publishDirectory = _paths.PublishDirectory(runtime.RuntimeIdentifier);
            _fileSystem.Directory.CreateDirectory(publishDirectory);

            await _commandRunner.RunCommandAsync(
                "dotnet",
                [
                    "publish",
                    _paths.AppHostProject,
                    "--no-restore",
                    $"-p:PublishProfile={runtime.PublishProfile}",
                    $"-p:Version={settings.Version}",
                    $"-p:InformationalVersion={settings.InformationalVersion}",
                    $"-p:PublishDir={publishDirectory}{_fileSystem.Path.DirectorySeparatorChar}",
                    "-p:CSharpier_Bypass=true",
                ]
            );
            var sourceDirectory = GetFrontendPublicDirectory(settings);
            if (!_fileSystem.Directory.Exists(sourceDirectory))
                return Result.Fail<int>($"Generated frontend output directory was not found at '{sourceDirectory}'.");

            CopyFrontendOutput(sourceDirectory, _paths.PublishDirectory(runtime.RuntimeIdentifier));
        }

        _log.Here()
            .Information(
                "Published {RuntimeIdentifier} desktop build to {PublishDirectory}",
                runtime.RuntimeIdentifier,
                _paths.PublishDirectory(runtime.RuntimeIdentifier)
            );

        return Result.Ok(0);
    }

    private void CopyFrontendOutput(string sourceDirectory, string publishDirectory)
    {
        var frontendPath = new BuildDirectoryPath(_fileSystem, sourceDirectory);
        var wwwrootPath = new BuildDirectoryPath(
            _fileSystem, _fileSystem.Path.Combine(publishDirectory, "wwwroot"));
        _fileSystemTasks.ClearArtifactDirectory(new BuildDirectoryPath(_fileSystem, _paths.RootDirectory), wwwrootPath);
        _fileSystemTasks.CopyDirectory(frontendPath, wwwrootPath);
    }

    private string GetFrontendPublicDirectory(DesktopCommandSettings settings) =>
        string.IsNullOrWhiteSpace(settings.FrontendPublicDirectory)
            ? _paths.FrontendPublicDirectory
            : _fileSystem.Path.GetFullPath(settings.FrontendPublicDirectory, _paths.RootDirectory);

    private bool ShouldInstallFrontendDependencies(string clientAppDirectory)
    {
        var bunLockPath = _fileSystem.Path.Combine(clientAppDirectory, "bun.lock");
        var nodeModulesPath = _fileSystem.Path.Combine(clientAppDirectory, "node_modules");
        return _fileSystem.File.Exists(bunLockPath) && !_fileSystem.Directory.Exists(nodeModulesPath);
    }
}
