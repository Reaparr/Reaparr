using System.IO.Abstractions;
using FastEndpoints;
using FluentValidation;
using Reaparr.Logging;

namespace Reaparr.Build;

internal sealed record DesktopLaunchBuildCommand(DesktopCommandSettings Settings) : ICommand<Result<int>>;

internal sealed class DesktopLaunchBuildCommandValidator : AbstractValidator<DesktopLaunchBuildCommand>
{
    public DesktopLaunchBuildCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Settings).NotNull();
    }
}

internal sealed class DesktopLaunchBuildCommandHandler : ICommandHandler<DesktopLaunchBuildCommand, Result<int>>
{
    private readonly ILogger _log;
    private readonly BuildPaths _paths;
    private readonly IDesktopCommandRunner _commandRunner;
    private readonly IFileSystem _fileSystem;

    public DesktopLaunchBuildCommandHandler(
        ILogger log,
        BuildPaths paths,
        IDesktopCommandRunner commandRunner,
        IFileSystem fileSystem
    )
    {
        _log = log.ForContext<DesktopLaunchBuildCommandHandler>();
        _paths = paths;
        _commandRunner = commandRunner;
        _fileSystem = fileSystem;
    }

    public async Task<Result<int>> ExecuteAsync(DesktopLaunchBuildCommand command, CancellationToken cancellationToken)
    {
        var settings = command.Settings;
        var runtime = DesktopRuntimeCatalog.Get(settings.RuntimeIdentifier);

        if (
            runtime.RuntimeIdentifier.StartsWith("linux-", StringComparison.OrdinalIgnoreCase)
            && !settings.SkipPackage
            && string.Equals(settings.LaunchMode, "packaged", StringComparison.OrdinalIgnoreCase)
        )
        {
            return await ExecutePackagedLinuxArtifactAsync(settings, runtime);
        }

        var publishedExecutable = _fileSystem.Path.Combine(
            _paths.PublishDirectory(runtime.RuntimeIdentifier),
            runtime.MainExecutable
        );
        _log.Here()
            .Information(
                "Resolved published executable for {RuntimeIdentifier} to {PublishedExecutable} (launch mode: {LaunchMode})",
                runtime.RuntimeIdentifier,
                publishedExecutable,
                settings.LaunchMode
            );

        if (!_fileSystem.File.Exists(publishedExecutable))
            return Result.Fail<int>(
                $"Published executable for runtime '{runtime.RuntimeIdentifier}' was not found at '{publishedExecutable}'."
            );

        if (
            runtime.RuntimeIdentifier.StartsWith("win-", StringComparison.OrdinalIgnoreCase)
            && !OperatingSystem.IsWindows()
        )
        {
            if (await _commandRunner.CommandExistsAsync("wine"))
            {
                var wineExitCode = await _commandRunner.ExecuteCommandAsync("wine", [publishedExecutable]);
                _log.Here()
                    .Information(
                        "Wine launch for {RuntimeIdentifier} exited with code {ExitCode}",
                        runtime.RuntimeIdentifier,
                        wineExitCode
                    );
                return Result.Ok(wineExitCode);
            }

            _log.Here().Warning("Wine is not available on this host, so the Windows build was not launched");
            return Result.Ok(0);
        }

        var exitCode = await _commandRunner.ExecuteCommandAsync(publishedExecutable, []);
        _log.Here()
            .Information(
                "Published executable for {RuntimeIdentifier} exited with code {ExitCode}",
                runtime.RuntimeIdentifier,
                exitCode
            );
        return Result.Ok(exitCode);
    }

    private async Task<Result<int>> ExecutePackagedLinuxArtifactAsync(
        DesktopCommandSettings settings,
        DesktopRuntime runtime
    )
    {
        var artifactDirectory = string.IsNullOrWhiteSpace(settings.ArtifactDirectory)
            ? _paths.ArtifactDirectory(runtime.RuntimeIdentifier)
            : _fileSystem.Path.GetFullPath(settings.ArtifactDirectory, _paths.RootDirectory);

        if (!_fileSystem.Directory.Exists(artifactDirectory))
        {
            return Result.Fail<int>(
                $"Packaged Linux artifact directory for runtime '{runtime.RuntimeIdentifier}' was not found at '{artifactDirectory}'."
            );
        }

        var appImage = _fileSystem
            .Directory.EnumerateFiles(artifactDirectory, "*.AppImage", SearchOption.TopDirectoryOnly)
            .OrderByDescending(path => _fileSystem.File.GetLastWriteTimeUtc(path))
            .ThenBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault();

        if (appImage is null)
        {
            return Result.Fail<int>($"No packaged Linux artifact (*.AppImage) was found in '{artifactDirectory}'.");
        }

        _log.Here()
            .Information(
                "Resolved packaged executable for {RuntimeIdentifier} to {PackagedExecutable}",
                runtime.RuntimeIdentifier,
                appImage
            );

        var chmodExitCode = await _commandRunner.ExecuteCommandAsync("chmod", ["+x", appImage]);
        if (chmodExitCode != 0)
        {
            _log.Here()
                .Warning(
                    "Could not make packaged executable {PackagedExecutable} executable; chmod exited with code {ExitCode}",
                    appImage,
                    chmodExitCode
                );
            return Result.Fail<int>(
                $"Could not make packaged Linux artifact '{appImage}' executable; chmod exited with code {chmodExitCode}."
            );
        }

        var exitCode = await _commandRunner.ExecuteCommandAsync(appImage, []);
        _log.Here()
            .Information(
                "Packaged executable for {RuntimeIdentifier} exited with code {ExitCode}",
                runtime.RuntimeIdentifier,
                exitCode
            );
        return Result.Ok(exitCode);
    }
}
