namespace Reaparr.Application;

public record CleanUpDownloadTaskFoldersCommand(DownloadTaskKey DownloadTaskKey) : ICommand<Result>;

public class CleanUpDownloadTaskFoldersCommandValidator : AbstractValidator<CleanUpDownloadTaskFoldersCommand>
{
    public CleanUpDownloadTaskFoldersCommandValidator()
    {
        RuleFor(x => x).NotNull();
    }
}

public class CleanUpDownloadTaskFoldersCommandHandler : ICommandHandler<CleanUpDownloadTaskFoldersCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IPath _path;
    private readonly IDirectory _directory;
    private static readonly StringComparer _pathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private static readonly StringComparison _pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public CleanUpDownloadTaskFoldersCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        IPath path,
        IDirectory directory
    )
    {
        _log = log.ForContext<CleanUpDownloadTaskFoldersCommandHandler>();
        _dbContext = dbContext;
        _path = path;
        _directory = directory;
    }

    public async Task<Result> ExecuteAsync(
        CleanUpDownloadTaskFoldersCommand command,
        CancellationToken cancellationToken
    )
    {
        var downloadTaskKey = command.DownloadTaskKey;
        var downloadTask = await _dbContext.GetDownloadTaskFileAsync(downloadTaskKey, cancellationToken);

        if (downloadTask is null)
            return ResultExtensions.EntityNotFound(nameof(DownloadTaskGeneric), downloadTaskKey.Id).LogError();

        var filePath = downloadTask.DownloadFilePath;
        if (string.IsNullOrEmpty(filePath))
            return ResultExtensions.IsEmpty(nameof(filePath)).LogError();

        HashSet<string>? activeDirectories = null;
        var hasOtherActiveTasksInDirectory = false;
        if (downloadTask.DownloadTaskType is DownloadTaskType.PhotoImage)
        {
            var rawActiveDirectories = await _dbContext.GetActiveDownloadDirectoriesAsync(
                [downloadTask.ToKey()],
                cancellationToken
            );
            activeDirectories = rawActiveDirectories.Select(NormalizeDirectory).ToHashSet(_pathComparer);
            hasOtherActiveTasksInDirectory = activeDirectories.Contains(
                NormalizeDirectory(downloadTask.DownloadDirectory)
            );
        }
        else
        {
            hasOtherActiveTasksInDirectory = await HasOtherActiveTasksInDirectory(downloadTask, cancellationToken);
        }

        if (hasOtherActiveTasksInDirectory)
        {
            _log.Here()
                .Debug(
                    "Skipping cleanup for {DownloadTaskKey} because other active tasks still use {DownloadDirectory}",
                    downloadTaskKey,
                    downloadTask.DownloadDirectory
                );
            return Result.Ok();
        }

        var protectedCategoryDirectory = downloadTask.DirectoryMeta.GetDownloadCategoryDirectory(
            downloadTask.DownloadTaskType
        );

        if (downloadTask.DownloadTaskType is DownloadTaskType.PhotoImage)
            return DeletePhotoDirectoriesFromFilePath(filePath, protectedCategoryDirectory, activeDirectories!);

        // Remove empty media folders without crossing the category root or active sibling downloads.
        var result = DeleteDirectoryFromFilePath(filePath, protectedCategoryDirectory);
        if (result.IsFailed)
            return result;

        if (downloadTask.DownloadTaskType is DownloadTaskType.MovieData)
            return Result.Ok();

        var deleteResult = DeleteDirectoryFromFilePath(downloadTask.DownloadDirectory, protectedCategoryDirectory);
        if (deleteResult.IsFailed)
            return deleteResult;

        return Result.Ok();
    }

    private async Task<bool> HasOtherActiveTasksInDirectory(
        DownloadTaskFileBase downloadTask,
        CancellationToken cancellationToken
    )
    {
        var activeDirectories = await _dbContext.GetActiveDownloadDirectoriesAsync(
            [downloadTask.ToKey()],
            cancellationToken
        );
        return activeDirectories.Contains(downloadTask.DownloadDirectory);
    }

    private Result DeletePhotoDirectoriesFromFilePath(
        string filePath,
        string protectedDirectory,
        IReadOnlySet<string> activeDirectories
    )
    {
        var directoryNameResult = Result.Try(() => _path.GetDirectoryName(filePath));

        if (directoryNameResult.IsFailed)
            return Result.Fail(directoryNameResult.Errors).LogError();

        if (string.IsNullOrEmpty(directoryNameResult.Value))
            return ResultExtensions.IsEmpty(nameof(directoryNameResult.Value)).LogError();

        var directory = directoryNameResult.Value;
        while (!string.IsNullOrEmpty(directory))
        {
            var deleteResult = DeleteDirectoryIfEmpty(
                directory,
                protectedDirectory,
                activeDirectories,
                enforceProtectedBoundary: true
            );
            if (deleteResult.IsFailed)
                return Result.Fail(deleteResult.Errors).LogError();

            if (!deleteResult.Value)
                return Result.Ok();

            var parentDirectoryResult = Result.Try(() => _path.GetDirectoryName(directory));
            if (parentDirectoryResult.IsFailed)
                return Result.Fail(parentDirectoryResult.Errors).LogError();

            if (string.IsNullOrEmpty(parentDirectoryResult.Value))
                return Result.Ok();

            directory = parentDirectoryResult.Value;
        }

        return Result.Ok();
    }

    private Result DeleteDirectoryFromFilePath(string filePath, string? protectedDirectory = null)
    {
        var directoryNameResult = Result.Try(() => _path.GetDirectoryName(filePath));

        if (directoryNameResult.IsFailed)
            return Result.Fail(directoryNameResult.Errors).LogError();

        if (string.IsNullOrEmpty(directoryNameResult.Value))
            return ResultExtensions.IsEmpty(nameof(directoryNameResult.Value)).LogError();

        var deleteResult = DeleteDirectoryIfEmpty(directoryNameResult.Value, protectedDirectory);
        if (deleteResult.IsFailed)
            return Result.Fail(deleteResult.Errors).LogError();

        return Result.Ok();
    }

    private Result<bool> DeleteDirectoryIfEmpty(
        string directory,
        string? protectedDirectory,
        IReadOnlySet<string>? activeDirectories = null,
        bool enforceProtectedBoundary = false
    )
    {
        if (
            (
                enforceProtectedBoundary
                    ? IsAtOrAbovePhotoProtectedDirectory(directory, protectedDirectory)
                        || !IsWithinProtectedDirectory(directory, protectedDirectory)
                    : IsAtOrAboveProtectedDirectory(directory, protectedDirectory)
            ) || (activeDirectories is not null && activeDirectories.Contains(NormalizeDirectory(directory)))
        )
            return Result.Ok(false);

        if (!_directory.Exists(directory))
            return Result.Ok(true);

        var entriesResult = Result.Try(() => _directory.GetFileSystemEntries(directory).ToList());
        if (entriesResult.IsFailed)
            return Result.Fail<bool>(entriesResult.Errors);

        if (!entriesResult.Value.Any())
        {
            var deleteResult = Result.Try(() => _directory.Delete(directory));
            if (deleteResult.IsFailed)
                return Result.Fail<bool>(deleteResult.Errors);

            return Result.Ok(true);
        }

        var entries = string.Join(", ", entriesResult.Value.Select(entry => _path.GetFileName(entry)));
        _log.Here()
            .Debug(
                "Skipping delete for {DirectoryPath} because it still contains: {DirectoryEntries}",
                directory,
                entries
            );

        return Result.Ok(false);
    }

    private static string NormalizeDirectory(string directory) =>
        Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool IsAtOrAboveProtectedDirectory(string directory, string? protectedDirectory)
    {
        if (string.IsNullOrEmpty(protectedDirectory))
            return false;

        var normalizedDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedProtectedDirectory = Path.GetFullPath(protectedDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(normalizedDirectory, normalizedProtectedDirectory, StringComparison.OrdinalIgnoreCase)
            || normalizedProtectedDirectory.StartsWith(
                normalizedDirectory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            );
    }

    private static bool IsAtOrAbovePhotoProtectedDirectory(string directory, string? protectedDirectory)
    {
        if (string.IsNullOrEmpty(protectedDirectory))
            return false;

        var normalizedDirectory = NormalizeDirectory(directory);
        var normalizedProtectedDirectory = NormalizeDirectory(protectedDirectory);

        return string.Equals(normalizedDirectory, normalizedProtectedDirectory, _pathComparison)
            || normalizedProtectedDirectory.StartsWith(
                normalizedDirectory + Path.DirectorySeparatorChar,
                _pathComparison
            );
    }

    private static bool IsWithinProtectedDirectory(string directory, string? protectedDirectory)
    {
        if (string.IsNullOrEmpty(protectedDirectory))
            return true;

        var normalizedDirectory = NormalizeDirectory(directory);
        var normalizedProtectedDirectory = NormalizeDirectory(protectedDirectory);

        return !string.Equals(normalizedDirectory, normalizedProtectedDirectory, _pathComparison)
            && normalizedDirectory.StartsWith(
                normalizedProtectedDirectory + Path.DirectorySeparatorChar,
                _pathComparison
            );
    }
}
