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

        var telemetryValidationError = DesktopCommandSettings.GetTelemetryValidationError(
            settings.TelemetryHost,
            settings.TelemetryProjectToken
        );
        if (telemetryValidationError is not null)
            return Result.Fail<int>(telemetryValidationError);

        if (!settings.DryRun)
            await _commandRunner.RequireCommandAsync("dotnet");

        if (!settings.SkipFrontend)
            await PrepareFrontendAsync(settings);

        if (!settings.SkipRestore)
        {
            string[] restoreArguments = ["restore", _paths.AppHostProject, "--runtime", runtime.RuntimeIdentifier];
            if (settings.DryRun)
                _commandRunner.LogCommand("dotnet", restoreArguments);
            else
                await _commandRunner.RunCommandAsync("dotnet", restoreArguments);
        }

        var publishDirectory = _paths.PublishDirectory(runtime.RuntimeIdentifier);
        string[] publishArguments =
        [
            "publish",
            _paths.AppHostProject,
            "--no-restore",
            $"-p:PublishProfile={runtime.PublishProfile}",
            $"-p:Version={settings.Version}",
            $"-p:InformationalVersion={settings.InformationalVersion}",
            $"-p:ReaparrTelemetryHost={(settings.TelemetryHost?.EndsWith("/", StringComparison.Ordinal) == true ? settings.TelemetryHost[..^1] : settings.TelemetryHost ?? string.Empty)}",
            $"-p:ReaparrTelemetryProjectToken={settings.TelemetryProjectToken ?? string.Empty}",
            $"-p:PublishDir={publishDirectory}{_fileSystem.Path.DirectorySeparatorChar}",
            "-p:CSharpier_Bypass=true",
        ];
        if (settings.DryRun)
        {
            _commandRunner.LogCommand("dotnet", publishArguments);
            _log.Here()
                .Information(
                    "Planned {RuntimeIdentifier} desktop publish to {PublishDirectory}",
                    runtime.RuntimeIdentifier,
                    publishDirectory
                );
            return Result.Ok(0);
        }

        _fileSystem.Directory.CreateDirectory(publishDirectory);
        await _commandRunner.RunCommandAsync("dotnet", publishArguments);
        var frontendDirectory = GetFrontendPublicDirectory(settings);
        if (!_fileSystem.Directory.Exists(frontendDirectory))
            return Result.Fail<int>($"Generated frontend output directory was not found at '{frontendDirectory}'.");

        CopyFrontendOutput(frontendDirectory, publishDirectory);

        _log.Here()
            .Information(
                "Published {RuntimeIdentifier} desktop build to {PublishDirectory}",
                runtime.RuntimeIdentifier,
                publishDirectory
            );

        return Result.Ok(0);
    }

    private async Task PrepareFrontendAsync(DesktopCommandSettings settings)
    {
        var sourceDirectory = GetFrontendPublicDirectory(settings);
        if (_fileSystem.Directory.Exists(sourceDirectory))
        {
            _log.Here()
                .Information("Reusing existing frontend output from {FrontendPublicDirectory}", sourceDirectory);
            return;
        }

        if (!settings.DryRun)
            await _commandRunner.RequireCommandAsync("bun");

        if (ShouldInstallFrontendDependencies(_paths.ClientAppDirectory))
        {
            string[] installArguments = ["install", "--frozen-lockfile"];
            if (settings.DryRun)
                _commandRunner.LogCommand("bun", installArguments);
            else
            {
                _log.Here()
                    .Information("Installing frontend dependencies in {ClientAppDirectory}", _paths.ClientAppDirectory);
                await _commandRunner.RunCommandAsync("bun", installArguments, _paths.ClientAppDirectory);
            }
        }

        string[] generateArguments = ["run", "generate", "--fail-on-error"];
        if (settings.DryRun)
            _commandRunner.LogCommand("bun", generateArguments);
        else
            await _commandRunner.RunCommandAsync("bun", generateArguments, _paths.ClientAppDirectory);
    }

    private void CopyFrontendOutput(string sourceDirectory, string publishDirectory)
    {
        var frontendPath = new BuildDirectoryPath(_fileSystem, sourceDirectory);
        var wwwrootPath = new BuildDirectoryPath(_fileSystem, _fileSystem.Path.Combine(publishDirectory, "wwwroot"));
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
