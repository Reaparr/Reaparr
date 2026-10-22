using System.IO.Abstractions;
using FastEndpoints;
using FluentValidation;
using Reaparr.Domain;
using Reaparr.Logging;

namespace Reaparr.Build;

internal sealed record DesktopPackageBuildCommand(DesktopCommandSettings Settings) : ICommand<Result<int>>;

internal sealed class DesktopPackageBuildCommandValidator : AbstractValidator<DesktopPackageBuildCommand>
{
    public DesktopPackageBuildCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Settings).NotNull();
    }
}

internal sealed class DesktopPackageBuildCommandHandler : ICommandHandler<DesktopPackageBuildCommand, Result<int>>
{
    private readonly ILogger _log;
    private readonly BuildPaths _paths;
    private readonly IDesktopCommandRunner _commandRunner;
    private readonly FileSystemTasks _fileSystemTasks;
    private readonly IFileSystem _fileSystem;
    private readonly ICommandExecutor _commandExecutor;

    public DesktopPackageBuildCommandHandler(
        ILogger log,
        BuildPaths paths,
        IDesktopCommandRunner commandRunner,
        FileSystemTasks fileSystemTasks,
        IFileSystem fileSystem,
        ICommandExecutor commandExecutor
    )
    {
        _log = log.ForContext<DesktopPackageBuildCommandHandler>();
        _paths = paths;
        _commandRunner = commandRunner;
        _fileSystemTasks = fileSystemTasks;
        _fileSystem = fileSystem;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<int>> ExecuteAsync(DesktopPackageBuildCommand command, CancellationToken cancellationToken)
    {
        var settings = command.Settings;
        var runtime = DesktopRuntimeCatalog.Get(settings.RuntimeIdentifier);

        if (!settings.DryRun)
        {
            await _commandRunner.RequireCommandAsync("vpk");
        }

        var publishResult = await _commandExecutor.Send(new DesktopPublishBuildCommand(settings), cancellationToken);
        if (publishResult.IsFailed)
            return Result.Fail<int>(publishResult.Errors);

        var artifactDirectory = GetArtifactDirectory(settings, runtime.RuntimeIdentifier);

        if (!settings.DryRun)
        {
            PrepareArtifactDirectory(settings, runtime.RuntimeIdentifier, artifactDirectory);

            await _commandRunner.RunCommandAsync(
                "vpk",
                [
                    "pack",
                    "--packId",
                    "Reaparr",
                    "--packTitle",
                    "Reaparr",
                    "--packVersion",
                    settings.Version!,
                    "--packDir",
                    _paths.PublishDirectory(runtime.RuntimeIdentifier),
                    "--mainExe",
                    runtime.MainExecutable,
                    "--runtime",
                    runtime.RuntimeIdentifier,
                    "--channel",
                    GetChannel(settings, runtime.RuntimeIdentifier),
                    "--outputDir",
                    artifactDirectory,
                ]
            );
        }

        _log.Here()
            .Information(
                "Packaged {RuntimeIdentifier} desktop artifacts for Velopack channel {Channel} to {ArtifactDirectory}",
                runtime.RuntimeIdentifier,
                GetChannel(settings, runtime.RuntimeIdentifier),
                artifactDirectory
            );

        return Result.Ok(0);
    }

    private void PrepareArtifactDirectory(DesktopCommandSettings settings, string rid, string artifactDirectory)
    {
        if (settings.PreserveExistingArtifacts)
        {
            _fileSystem.Directory.CreateDirectory(artifactDirectory);
            return;
        }

        var publishDirectory = _paths.PublishDirectory(rid);
        if (string.IsNullOrWhiteSpace(settings.ArtifactDirectory))
        {
            _fileSystemTasks.ClearArtifactDirectory(
                new BuildDirectoryPath(_fileSystem, _paths.RootDirectory),
                new BuildDirectoryPath(_fileSystem, artifactDirectory),
                new BuildDirectoryPath(_fileSystem, publishDirectory)
            );
            return;
        }

        var normalizedArtifactDirectory = _fileSystem
            .Path.GetFullPath(artifactDirectory)
            .TrimEnd(_fileSystem.Path.DirectorySeparatorChar, _fileSystem.Path.AltDirectorySeparatorChar);
        var normalizedPublishDirectory = _fileSystem
            .Path.GetFullPath(publishDirectory)
            .TrimEnd(_fileSystem.Path.DirectorySeparatorChar, _fileSystem.Path.AltDirectorySeparatorChar);
        var artifactContainsPublish = normalizedPublishDirectory.StartsWith(
            normalizedArtifactDirectory + _fileSystem.Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase
        );
        if (artifactContainsPublish)
        {
            _log.Here()
                .Information(
                    "Skipping artifact directory cleanup because it contains the publish directory required for packaging: {ArtifactDirectory}",
                    artifactDirectory
                );
            _fileSystem.Directory.CreateDirectory(artifactDirectory);
        }
        else
        {
            _fileSystemTasks.ClearArtifactDirectory(
                new BuildDirectoryPath(_fileSystem, _paths.RootDirectory),
                new BuildDirectoryPath(_fileSystem, artifactDirectory)
            );
        }
    }

    internal string GetArtifactDirectory(DesktopCommandSettings settings, string rid) =>
        string.IsNullOrWhiteSpace(settings.ArtifactDirectory)
            ? _paths.ArtifactDirectory(rid)
            : _fileSystem.Path.GetFullPath(settings.ArtifactDirectory, _paths.RootDirectory);

    internal static string GetChannel(DesktopCommandSettings settings, string rid)
    {
        if (!string.IsNullOrWhiteSpace(settings.Channel))
            return settings.Channel;

        return
            !string.IsNullOrWhiteSpace(settings.InformationalVersion)
            && settings.InformationalVersion.Contains("dev", StringComparison.OrdinalIgnoreCase)
            ? $"{rid}-dev"
            : $"{rid}-stable";
    }
}
