namespace Reaparr.Application;

public class DeleteDownloadTaskFilesCommandValidator : AbstractValidator<DeleteDownloadTaskFilesCommand>
{
    public DeleteDownloadTaskFilesCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Keys).NotEmpty();
    }
}

public class DeleteDownloadTaskFilesCommandHandler : ICommandHandler<DeleteDownloadTaskFilesCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IFile _file;
    private readonly IDirectory _directory;
    private readonly IPath _path;

    public DeleteDownloadTaskFilesCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        IFile file,
        IDirectory directory,
        IPath path
    )
    {
        _log = log.ForContext<DeleteDownloadTaskFilesCommandHandler>();
        _dbContext = dbContext;
        _file = file;
        _directory = directory;
        _path = path;
    }

    public async Task<Result> ExecuteAsync(DeleteDownloadTaskFilesCommand command, CancellationToken cancellationToken)
    {
        var keys = command.Keys;
        var allFileTasks = await _dbContext.GetDownloadTaskFilesAsync(keys, cancellationToken);

        foreach (var task in allFileTasks)
        {
            var deleteFileResult = DeleteFileIfPresent(task);
            if (deleteFileResult.IsFailed)
                return deleteFileResult;
        }

        // After deleting files, clean up any empty directories left behind.
        // Build a deduplicated map of download directory → stop root so recursion is
        // strictly bounded and can never delete the category folder or anything above it.
        var directoryToStopRoot = allFileTasks
            .Where(t => !string.IsNullOrEmpty(t.DownloadDirectory))
            .GroupBy(t => t.DownloadDirectory, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var task = g.First();
                    var downloadRoot = task.DirectoryMeta.DownloadRootPath;
                    if (string.IsNullOrEmpty(downloadRoot))
                        return _path.GetPathRoot(_path.GetFullPath(g.Key)) ?? string.Empty;

                    return task.DirectoryMeta.GetDownloadCategoryDirectory(task.DownloadTaskType);
                },
                StringComparer.OrdinalIgnoreCase
            );

        var activeDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (allFileTasks.Count > 0)
        {
            var deletingFileKeys = allFileTasks.Select(x => x.ToKey()).ToHashSet();
            var downloadTasks = await _dbContext.GetAllDownloadTasksByServerAsync(cancellationToken: cancellationToken);
            activeDirectories.UnionWith(
                downloadTasks
                    .Flatten(x => x.Children)
                    .Where(x =>
                        x.IsDownloadable
                        && !deletingFileKeys.Contains(x.ToKey())
                        && x.DownloadStatus is not (DownloadStatus.Completed or DownloadStatus.Deleted)
                        && !string.IsNullOrEmpty(x.DownloadDirectory)
                    )
                    .Select(x => x.DownloadDirectory)
            );
        }

        foreach (var (directory, stopRoot) in directoryToStopRoot)
        {
            if (activeDirectories.Contains(directory))
                continue;
            var deleteDirectoryResult = DeleteDirectoryIfEmpty(directory, stopRoot);
            if (deleteDirectoryResult.IsFailed)
                return deleteDirectoryResult;
        }

        return Result.Ok();
    }

    /// <summary>
    /// Deletes the source file for the given task from the download directory.
    /// Checks both the <c>.reaptemp</c>-suffixed path (active download) and the plain path
    /// (file already renamed after keep-in-downloads or completed move-to-same-folder step).
    /// </summary>
    private Result DeleteFileIfPresent(DownloadTaskFileBase task)
    {
        // DownloadFilePath includes the .reaptemp suffix — check it first.
        var reapTempPath = task.DownloadFilePath;
        var plainPath = reapTempPath.RemoveReapTempSuffix();
        var deletedAny = false;

        foreach (var candidate in new[] { reapTempPath, plainPath })
        {
            if (string.IsNullOrEmpty(candidate) || !_file.Exists(candidate))
                continue;

            _log.Here()
                .Debug("Deleting download file for {DownloadTaskTitle} at {FilePath}", task.FullTitle, candidate);

            var deleteResult = Result.Try(() => _file.Delete(candidate));
            if (deleteResult.IsFailed)
            {
                deleteResult.LogIfFailed();
                return deleteResult.WithError($"Failed to delete download file '{candidate}' for '{task.FullTitle}'");
            }

            deletedAny = true;
        }

        if (deletedAny)
            return Result.Ok();

        _log.Here()
            .Debug(
                "No download file found to delete for {DownloadTaskTitle} (checked {ReapTempPath} and {PlainPath})",
                task.FullTitle,
                reapTempPath,
                plainPath
            );

        return Result.Ok();
    }

    /// <summary>
    /// Recursively deletes a directory and its parents as long as each one is empty.
    /// Stops before reaching or exceeding <paramref name="stopRoot"/>.
    /// </summary>
    private Result DeleteDirectoryIfEmpty(string directory, string stopRoot)
    {
        if (string.IsNullOrEmpty(directory) || !_directory.Exists(directory))
            return Result.Ok();

        // Never delete at or above the stop root (category folder) — checked on every call,
        // including the very first, so the stopRoot itself can never be accidentally deleted.
        var normalizedDir = _path
            .GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedStop = _path
            .GetFullPath(stopRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (
            string.Equals(normalizedDir, normalizedStop, StringComparison.OrdinalIgnoreCase)
            || !normalizedDir.StartsWith(
                normalizedStop + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            )
        )
            return Result.Ok();

        var entries = Result.Try(() => _directory.GetFileSystemEntries(directory).ToList());
        if (entries.IsFailed)
        {
            entries.LogIfFailed();
            return entries.ToResult().WithError($"Failed to enumerate download directory '{directory}'");
        }

        if (entries.Value.Count > 0)
            return Result.Ok();

        _log.Here().Debug("Deleting empty download directory {Directory}", directory);
        var deleteResult = Result.Try(() => _directory.Delete(directory));
        if (deleteResult.IsFailed)
        {
            deleteResult.LogIfFailed();
            return deleteResult.WithError($"Failed to delete empty download directory '{directory}'");
        }

        // Walk up one level and try again (e.g. remove Season folder, then TvShow folder).
        // The guard at the top of this method will stop recursion before the stop root is reached.
        var parent = Result.Try(() => _path.GetDirectoryName(directory));
        if (parent.IsFailed)
        {
            parent.LogIfFailed();
            return parent.ToResult().WithError($"Failed to resolve parent directory for '{directory}'");
        }

        if (!string.IsNullOrEmpty(parent.Value))
            return DeleteDirectoryIfEmpty(parent.Value, stopRoot);

        return Result.Ok();
    }
}
