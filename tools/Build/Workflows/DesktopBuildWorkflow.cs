using System.IO.Abstractions;
using FastEndpoints;
using FluentValidation;
using Reaparr.Domain;
using Reaparr.Logging;

namespace Reaparr.Build;

internal sealed record DesktopRunBuildCommand(DesktopCommandSettings Settings) : ICommand<Result<int>>;

internal sealed class DesktopRunBuildCommandValidator : AbstractValidator<DesktopRunBuildCommand>
{
    public DesktopRunBuildCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Settings).NotNull();
    }
}

internal sealed class DesktopRunBuildCommandHandler : ICommandHandler<DesktopRunBuildCommand, Result<int>>
{
    private readonly ILogger _log;
    private readonly BuildPaths _paths;
    private readonly IFileSystem _fileSystem;
    private readonly FileSystemTasks _fileSystemTasks;
    private readonly ICommandExecutor _commandExecutor;

    public DesktopRunBuildCommandHandler(
        ILogger log,
        BuildPaths paths,
        IFileSystem fileSystem,
        FileSystemTasks fileSystemTasks,
        ICommandExecutor commandExecutor
    )
    {
        _log = log.ForContext<DesktopRunBuildCommandHandler>();
        _paths = paths;
        _fileSystem = fileSystem;
        _fileSystemTasks = fileSystemTasks;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<int>> ExecuteAsync(DesktopRunBuildCommand command, CancellationToken cancellationToken)
    {
        var settings = command.Settings;
        var runtime = DesktopRuntimeCatalog.Get(settings.RuntimeIdentifier);

        _log.Here()
            .Information(
                "Starting run workflow for {RuntimeIdentifier} (SkipPackage={SkipPackage}, DryRun={DryRun})",
                settings.RuntimeIdentifier,
                settings.SkipPackage,
                settings.DryRun
            );

        if (settings.SkipPackage)
        {
            var publishResult = await _commandExecutor.Send(
                new DesktopPublishBuildCommand(settings),
                cancellationToken
            );
            if (publishResult.IsFailed)
                return Result.Fail<int>(publishResult.Errors);

            if (runtime.RuntimeIdentifier.StartsWith("win-", StringComparison.OrdinalIgnoreCase) && !settings.DryRun)
            {
                var artifactDirectory = string.IsNullOrWhiteSpace(settings.ArtifactDirectory)
                    ? _paths.ArtifactDirectory(runtime.RuntimeIdentifier)
                    : _fileSystem.Path.GetFullPath(settings.ArtifactDirectory, _paths.RootDirectory);

                _fileSystem.Directory.CreateDirectory(artifactDirectory);
                var publishedExecutable = _fileSystem.Path.Combine(
                    _paths.PublishDirectory(runtime.RuntimeIdentifier),
                    runtime.MainExecutable
                );
                if (_fileSystem.File.Exists(publishedExecutable))
                {
                    var targetExecutable = _fileSystem.Path.Combine(artifactDirectory, runtime.MainExecutable);
                    _fileSystem.File.Copy(publishedExecutable, targetExecutable, overwrite: true);
                    _log.Here().Information("Exported Windows executable artifact to {ArtifactPath}", targetExecutable);
                }

                var sourcePublishDirectory = _paths.PublishDirectory(runtime.RuntimeIdentifier);
                var targetRoot = _fileSystem.Path.Combine(artifactDirectory, "publish");

                var sourcePath = new BuildDirectoryPath(_fileSystem, sourcePublishDirectory);
                var targetPath = new BuildDirectoryPath(_fileSystem, targetRoot);

                if (string.Equals(sourcePath.Value, targetPath.Value, StringComparison.OrdinalIgnoreCase))
                {
                    _log.Here()
                        .Information(
                            "Skipping Windows publish directory export because source and target are the same path: {PublishDirectory}",
                            targetRoot
                        );
                }
                else
                {
                    _fileSystemTasks.ClearDirectory(targetPath);
                    _fileSystemTasks.CopyDirectory(sourcePath, targetPath);
                    _log.Here()
                        .Information("Exported Windows publish directory to {ArtifactPublishDirectory}", targetRoot);
                }
            }
        }
        else
        {
            var packageResult = await _commandExecutor.Send(
                new DesktopPackageBuildCommand(settings),
                cancellationToken
            );
            if (packageResult.IsFailed)
                return Result.Fail<int>(packageResult.Errors);
        }

        if (settings.DryRun)
        {
            _log.Here()
                .Information(
                    "Dry-run enabled; skipping launch step for {RuntimeIdentifier}",
                    settings.RuntimeIdentifier
                );
            return Result.Ok(0);
        }

        return await _commandExecutor.Send(new DesktopLaunchBuildCommand(settings), cancellationToken);
    }
}
