using System.IO.Abstractions;

namespace Reaparr.FileSystem.Contracts;

// ReSharper disable once InconsistentNaming
public static class IPathExtensions
{
    public static Result<long> GetAvailableSpaceByDirectory(this IPath path, string directory)
    {
        try
        {
            // On Windows, DriveInfo expects a drive root (C:\ or \\server\share), not a full path.
            // UNC paths like \\server\share\subdir throw ArgumentException.
            // On Linux/macOS, the full path works because DriveInfo resolves to the containing filesystem.
            var drivePath = OperatingSystem.IsWindows() ? Path.GetPathRoot(directory) ?? directory : directory;
            var drive = path.FileSystem.DriveInfo.New(drivePath);
            return Result.Ok(drive.AvailableFreeSpace);
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e)).LogError();
        }
    }
}
