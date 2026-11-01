using System.IO.Abstractions;

namespace Reaparr.Build;

internal readonly record struct BuildDirectoryPath
{
    public BuildDirectoryPath(IFileSystem fileSystem, string path)
    {
        var fullPath = fileSystem.Path.GetFullPath(path);
        var root = fileSystem.Path.GetPathRoot(fullPath)!;
        Value = fullPath.Length == root.Length
            ? fullPath
            : fullPath.TrimEnd(fileSystem.Path.DirectorySeparatorChar, fileSystem.Path.AltDirectorySeparatorChar);
    }

    public string Value { get; }

    public bool IsParentOf(BuildDirectoryPath other, char separator) =>
        other.Value.StartsWith(
            Value.EndsWith(separator) ? Value : Value + separator,
            StringComparison.OrdinalIgnoreCase
        );
}
