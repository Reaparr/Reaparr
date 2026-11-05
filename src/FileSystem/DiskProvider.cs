namespace Reaparr.FileSystem;

public sealed class DiskProvider : IDiskProvider
{
    private readonly HashSet<string> _setToRemove =
    [
        "boot",
        "bootmgr",
        "cache",
        "msocache",
        "recovery",
        "$recycle.bin",
        "recycler",
        "system volume information",
        "temporary internet files",
        "windows",
        // OS X
        ".fseventd",
        ".spotlight",
        ".trashes",
        ".vol",
        "cachedmessages",
        "caches",
        "trash",
        // QNAP
        ".@__thumb",
        // Synology
        "@eadir",
    ];

    public string GetParent(string path)
    {
        var di = new DirectoryInfo(path);

        if (di.Parent != null)
        {
            var parent = di.Parent.FullName;

            if (parent.Last() != Path.DirectorySeparatorChar)
                parent += Path.DirectorySeparatorChar;

            return parent;
        }

        return string.Empty;
    }

    public Result<List<FileSystemModel>> GetFiles(string path)
    {
        var files = GetFileInfos(path);
        if (files.IsFailed)
            return files.ToResult();

        return files.Value.OrderBy(x => x.Name).Select(x => x.ToModel()).ToList();
    }

    public Result<List<FileSystemModel>> GetDirectories(string path)
    {
        var directories = GetDirectoryInfos(path);
        if (directories.IsFailed)
            return directories.ToResult();

        // Mapping probes write permission by creating a temporary file, so exclude hidden directories first.
        var list = directories
            .Value.Where(x => !_setToRemove.Contains(x.Name.ToLowerInvariant()))
            .OrderBy(x => x.Name)
            .Select(x => x.ToModel())
            .ToList();

        return Result.Ok(list);
    }

    public Result<List<DirectoryInfo>> GetDirectoryInfos(string path)
    {
        try
        {
            var di = new DirectoryInfo(path);
            return di.GetDirectories().ToList();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e)).LogError();
        }
    }

    public Result<List<FileInfo>> GetFileInfos(string path)
    {
        try
        {
            var di = new DirectoryInfo(path);

            return di.GetFiles().ToList();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e)).LogError();
        }
    }

    public List<DriveInfo> GetAllMounts()
    {
        return DriveInfo
            .GetDrives()
            .Where(d => d is { IsReady: true, DriveType: DriveType.Fixed or DriveType.Network or DriveType.Removable })
            .ToList();
    }

    public string GetVolumeName(DriveInfo mountInfo)
    {
        if (string.IsNullOrWhiteSpace(mountInfo.VolumeLabel))
            return mountInfo.Name;

        return $"{mountInfo.Name} ({mountInfo.VolumeLabel})";
    }
}
