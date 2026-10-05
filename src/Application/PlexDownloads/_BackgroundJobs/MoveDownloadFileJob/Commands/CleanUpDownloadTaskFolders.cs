namespace Reaparr.Application;

public record CleanUpDownloadTaskFoldersCommand(DownloadTaskKey DownloadTaskKey) : ICommand<Result>;

public class CleanUpDownloadTaskFoldersValidator : AbstractValidator<CleanUpDownloadTaskFoldersCommand>
{
    public CleanUpDownloadTaskFoldersValidator()
    {
        RuleFor(x => x).NotNull();
    }
}

public class CleanUpDownloadTaskFoldersHandler : ICommandHandler<CleanUpDownloadTaskFoldersCommand, Result>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IPath _path;
    private readonly IDirectory _directory;

    public CleanUpDownloadTaskFoldersHandler(ILogger log, IReaparrDbContext dbContext, IPath path, IDirectory directory)
    {
        _log = log.ForContext<CleanUpDownloadTaskFoldersHandler>();
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

        // Remove the empty media folder without touching active sibling downloads.
        var result = DeleteDirectoryFromFilePath(filePath);
        if (result.IsFailed)
            return result;

        if (
            downloadTask.DownloadTaskType
            is DownloadTaskType.EpisodeData
                or DownloadTaskType.MusicTrackData
                or DownloadTaskType.PhotoData
                or DownloadTaskType.OtherVideoData
        )
        {
            var deleteResult = DeleteDirectoryFromFilePath(downloadTask.DownloadDirectory);
            if (deleteResult.IsFailed)
                return deleteResult;
        }

        return Result.Ok();
    }

    private async Task<bool> HasOtherActiveTasksInDirectory(
        DownloadTaskFileBase downloadTask,
        CancellationToken cancellationToken
    )
    {
        var activeMovieTasks = await _dbContext
            .DownloadTaskMovieFile.AsNoTracking()
            .Where(x =>
                x.Id != downloadTask.Id
                && x.DownloadStatus != DownloadStatus.Completed
                && x.DownloadStatus != DownloadStatus.Deleted
            )
            .ToListAsync(cancellationToken);
        var hasActiveMovieTasks = activeMovieTasks.Any(x => x.DownloadDirectory == downloadTask.DownloadDirectory);
        if (hasActiveMovieTasks)
            return true;

        var activeEpisodeTasks = await _dbContext
            .DownloadTaskTvShowEpisodeFile.AsNoTracking()
            .Where(x =>
                x.Id != downloadTask.Id
                && x.DownloadStatus != DownloadStatus.Completed
                && x.DownloadStatus != DownloadStatus.Deleted
            )
            .ToListAsync(cancellationToken);

        if (activeEpisodeTasks.Any(x => x.DownloadDirectory == downloadTask.DownloadDirectory))
            return true;

        var activePhotoTasks = await _dbContext
            .DownloadTaskPhotoImageFiles.Where(x =>
                x.Id != downloadTask.Id
                && x.DownloadStatus != DownloadStatus.Completed
                && x.DownloadStatus != DownloadStatus.Deleted
            )
            .ToListAsync(cancellationToken);
        if (activePhotoTasks.Any(x => x.DownloadDirectory == downloadTask.DownloadDirectory))
            return true;

        var activeMusicTasks = await _dbContext
            .DownloadTaskMusicTrackFiles.Where(x =>
                x.Id != downloadTask.Id
                && x.DownloadStatus != DownloadStatus.Completed
                && x.DownloadStatus != DownloadStatus.Deleted
            )
            .ToListAsync(cancellationToken);
        if (activeMusicTasks.Any(x => x.DownloadDirectory == downloadTask.DownloadDirectory))
            return true;

        var activeOtherVideoTasks = await _dbContext
            .DownloadTaskOtherVideoFiles.Where(x =>
                x.Id != downloadTask.Id
                && x.DownloadStatus != DownloadStatus.Completed
                && x.DownloadStatus != DownloadStatus.Deleted
            )
            .ToListAsync(cancellationToken);
        return activeOtherVideoTasks.Any(x => x.DownloadDirectory == downloadTask.DownloadDirectory);
    }

    private Result DeleteDirectoryFromFilePath(string filePath)
    {
        var directoryNameResult = Result.Try(() => _path.GetDirectoryName(filePath));

        if (directoryNameResult.IsFailed || string.IsNullOrEmpty(directoryNameResult.Value))
            return ResultExtensions.IsEmpty(nameof(directoryNameResult.Value)).LogError();

        var parentDirectory = directoryNameResult.Value;

        if (!_directory.Exists(parentDirectory))
            return Result.Ok();

        var entriesResult = Result.Try(() => _directory.GetFileSystemEntries(parentDirectory).ToList());
        if (entriesResult.IsFailed)
        {
            return entriesResult.ToResult().LogError();
        }

        if (!entriesResult.Value.Any())
        {
            var deleteResult = Result.Try(() => _directory.Delete(parentDirectory));
            if (deleteResult.IsFailed)
            {
                return deleteResult.ToResult().LogError();
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
}
