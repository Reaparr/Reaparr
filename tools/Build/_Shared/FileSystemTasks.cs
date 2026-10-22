using System.IO.Abstractions;

namespace Reaparr.Build;

internal sealed class FileSystemTasks
{
    private readonly IFileSystem _fileSystem;

    public FileSystemTasks(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public void CopyDirectory(BuildDirectoryPath source, BuildDirectoryPath target)
    {
        EnsureCopyTargetIsNotNestedInsideSource(source, target);
        _fileSystem.Directory.CreateDirectory(target.Value);

        foreach (var directory in _fileSystem.Directory.EnumerateDirectories(source.Value))
        {
            CopyDirectory(
                new BuildDirectoryPath(_fileSystem, directory),
                new BuildDirectoryPath(
                    _fileSystem, _fileSystem.Path.Combine(target.Value, _fileSystem.Path.GetFileName(directory)))
            );
        }

        foreach (var file in _fileSystem.Directory.EnumerateFiles(source.Value))
        {
            _fileSystem.File.Copy(
                file,
                _fileSystem.Path.Combine(target.Value, _fileSystem.Path.GetFileName(file)),
                overwrite: true
            );
        }
    }

    public void ClearDirectory(BuildDirectoryPath directory, BuildDirectoryPath? preservedDirectory = null)
    {
        _fileSystem.Directory.CreateDirectory(directory.Value);

        foreach (var file in _fileSystem.Directory.EnumerateFiles(directory.Value))
        {
            _fileSystem.File.Delete(file);
        }

        foreach (var childDirectory in _fileSystem.Directory.EnumerateDirectories(directory.Value))
        {
            if (!string.Equals(childDirectory, preservedDirectory?.Value, StringComparison.OrdinalIgnoreCase))
                _fileSystem.Directory.Delete(childDirectory, recursive: true);
        }
    }

    public void ClearArtifactDirectory(
        BuildDirectoryPath repositoryRoot,
        BuildDirectoryPath artifactDirectory,
        BuildDirectoryPath? preservedDirectory = null
    )
    {
        EnsureSafeArtifactClearTarget(repositoryRoot, artifactDirectory);
        if (preservedDirectory is { } preserved)
        {
            if (
                !string.Equals(
                    _fileSystem.Path.GetDirectoryName(preserved.Value),
                    artifactDirectory.Value,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new InvalidOperationException("The preserved directory must be a direct artifact child.");
            }

            ClearDirectory(artifactDirectory, preserved);
            return;
        }

        ClearDirectory(artifactDirectory);
    }

    private void EnsureCopyTargetIsNotNestedInsideSource(BuildDirectoryPath source, BuildDirectoryPath target)
    {
        if (source.IsParentOf(target, _fileSystem.Path.DirectorySeparatorChar))
        {
            throw new InvalidOperationException($"Refusing to copy '{source.Value}' into nested target '{target.Value}'.");
        }
    }

    private void EnsureSafeArtifactClearTarget(BuildDirectoryPath repositoryRoot, BuildDirectoryPath artifactDirectory)
    {
        if (string.Equals(repositoryRoot.Value, artifactDirectory.Value, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to clear the repository root as an artifact directory.");
        }

        var defaultArtifactsRoot = new BuildDirectoryPath(
            _fileSystem, _fileSystem.Path.Combine(repositoryRoot.Value, ".artifacts"));
        if (!defaultArtifactsRoot.IsParentOf(artifactDirectory, _fileSystem.Path.DirectorySeparatorChar))
        {
            throw new InvalidOperationException(
                $"Refusing to clear custom artifact directory '{artifactDirectory.Value}'. Choose a path under '{defaultArtifactsRoot.Value}' or use --preserve-existing-artifacts."
            );
        }
    }
}
