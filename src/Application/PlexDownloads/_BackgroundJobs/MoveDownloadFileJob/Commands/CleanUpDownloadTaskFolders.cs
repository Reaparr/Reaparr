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

        var hasOtherActiveTasksInDirectory = await HasOtherActiveTasksInDirectory(downloadTask, cancellationToken);
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

        // Remove empty media folders without crossing the category root or active sibling downloads.
        var result = DeleteDirectoryFromFilePath(filePath, protectedCategoryDirectory);
        if (result.IsFailed)
            return result;

        if (!downloadTask.DownloadTaskType.IsDataOrPart())
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

    private Result DeleteDirectoryFromFilePath(string filePath, string? protectedDirectory = null)
    {
        var directoryNameResult = Result.Try(() => _path.GetDirectoryName(filePath));

        if (directoryNameResult.IsFailed)
            return Result.Fail(directoryNameResult.Errors).LogError();

        if (string.IsNullOrEmpty(directoryNameResult.Value))
            return ResultExtensions.IsEmpty(nameof(directoryNameResult.Value)).LogError();

        var parentDirectory = directoryNameResult.Value;

        if (IsAtOrAboveProtectedDirectory(parentDirectory, protectedDirectory))
            return Result.Ok();

        if (!_directory.Exists(parentDirectory))
            return Result.Ok();

        var entriesResult = Result.Try(() => _directory.GetFileSystemEntries(parentDirectory).ToList());
        if (entriesResult.IsFailed)
        {
            return Result.Fail(entriesResult.Errors).LogError();
        }

        if (!entriesResult.Value.Any())
        {
            var deleteResult = Result.Try(() => _directory.Delete(parentDirectory));
            if (deleteResult.IsFailed)
            {
                return Result.Fail(deleteResult.Errors).LogError();
            }

            return Result.Ok();
        }

        var entries = string.Join(", ", entriesResult.Value.Select(entry => _path.GetFileName(entry)));
        _log.Here()
            .Debug(
                "Skipping delete for {DirectoryPath} because it still contains: {DirectoryEntries}",
                parentDirectory,
                entries
            );

        return Result.Ok();
    }

    private static bool IsAtOrAboveProtectedDirectory(string directory, string? protectedDirectory)
    {
        if (string.IsNullOrEmpty(protectedDirectory))
            return false;

        var normalizedDirectory = Path
            .GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedProtectedDirectory = Path
            .GetFullPath(protectedDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(normalizedDirectory, normalizedProtectedDirectory, StringComparison.OrdinalIgnoreCase)
            || normalizedProtectedDirectory.StartsWith(
                normalizedDirectory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            );
    }
}
