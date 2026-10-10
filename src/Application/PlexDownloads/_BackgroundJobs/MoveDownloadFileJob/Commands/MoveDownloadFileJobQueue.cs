namespace Reaparr.Application;

public class MoveDownloadFileJobQueue : IMoveDownloadFileQueue
{
    private readonly ILogger _log;
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly IMoveDownloadFileScheduler _moveDownloadFileScheduler;

    public MoveDownloadFileJobQueue(
        ILogger log,
        IReaparrDbContextFactory dbContextFactory,
        IMoveDownloadFileScheduler moveDownloadFileScheduler
    )
    {
        _log = log.ForContext<MoveDownloadFileJobQueue>();
        _dbContextFactory = dbContextFactory;
        _moveDownloadFileScheduler = moveDownloadFileScheduler;
    }

    /// <inheritdoc/>
    public Task<Result> CheckMoveDownloadFileJobQueue() => CheckMoveDownloadFileJobQueue(CancellationToken.None);

    /// <inheritdoc/>
    public async Task<Result> CheckMoveDownloadFileJobQueue(CancellationToken cancellationToken)
    {
        // Create a new DbContext for this operation to avoid threading issues
        using var dbContext = await _dbContextFactory.CreateAsync();

        // Find the oldest ready-to-move task (DownloadFinished preferred, MoveError as retry)
        var movieCandidates = dbContext
            .DownloadTaskMovieFile.Where(x =>
                x.DownloadStatus == DownloadStatus.DownloadFinished || x.DownloadStatus == DownloadStatus.MoveError
            )
            .Select(x => new
            {
                x.Id,
                x.PlexServerId,
                x.PlexLibraryId,
                Type = DownloadTaskType.MovieData,
                x.CreatedAt,
                IsDownloadFinished = x.DownloadStatus == DownloadStatus.DownloadFinished,
            });

        var episodeCandidates = dbContext
            .DownloadTaskTvShowEpisodeFile.Where(x =>
                x.DownloadStatus == DownloadStatus.DownloadFinished || x.DownloadStatus == DownloadStatus.MoveError
            )
            .Select(x => new
            {
                x.Id,
                x.PlexServerId,
                x.PlexLibraryId,
                Type = DownloadTaskType.EpisodeData,
                x.CreatedAt,
                IsDownloadFinished = x.DownloadStatus == DownloadStatus.DownloadFinished,
            });

        var photoCandidates = dbContext
            .DownloadTaskPhotoImageFiles.Where(x =>
                x.DownloadStatus == DownloadStatus.DownloadFinished || x.DownloadStatus == DownloadStatus.MoveError
            )
            .Select(x => new
            {
                x.Id,
                x.PlexServerId,
                x.PlexLibraryId,
                Type = DownloadTaskType.PhotoData,
                x.CreatedAt,
                IsDownloadFinished = x.DownloadStatus == DownloadStatus.DownloadFinished,
            });

        var musicCandidates = dbContext
            .DownloadTaskMusicTrackFiles.Where(x =>
                x.DownloadStatus == DownloadStatus.DownloadFinished || x.DownloadStatus == DownloadStatus.MoveError
            )
            .Select(x => new
            {
                x.Id,
                x.PlexServerId,
                x.PlexLibraryId,
                Type = DownloadTaskType.MusicTrackData,
                x.CreatedAt,
                IsDownloadFinished = x.DownloadStatus == DownloadStatus.DownloadFinished,
            });

        var otherVideoCandidates = dbContext
            .DownloadTaskOtherVideoFiles.Where(x =>
                x.DownloadStatus == DownloadStatus.DownloadFinished || x.DownloadStatus == DownloadStatus.MoveError
            )
            .Select(x => new
            {
                x.Id,
                x.PlexServerId,
                x.PlexLibraryId,
                Type = DownloadTaskType.OtherVideoData,
                x.CreatedAt,
                IsDownloadFinished = x.DownloadStatus == DownloadStatus.DownloadFinished,
            });
        var key = await movieCandidates
            .Concat(episodeCandidates)
            .Concat(photoCandidates)
            .Concat(musicCandidates)
            .Concat(otherVideoCandidates)
            .OrderByDescending(x => x.IsDownloadFinished)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new DownloadTaskKey
            {
                Id = x.Id,
                PlexServerId = x.PlexServerId,
                PlexLibraryId = x.PlexLibraryId,
                Type = x.Type,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (key is null)
        {
            _log.Here().Debug("No DownloadTask with status DownloadFinished or MoveError found, nothing to move");
            return Result.Ok();
        }

        var startResult = await _moveDownloadFileScheduler.StartMoveDownloadFileJob(key, cancellationToken);
        return startResult.IsSuccess ? Result.Ok() : startResult;
    }
}
