namespace Reaparr.Application;

public class ClearCompletedDownloadTasksByServerIdCommandValidator
    : Validator<ClearCompletedDownloadTasksByServerIdCommand>
{
    public ClearCompletedDownloadTasksByServerIdCommandValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
    }
}

public class ClearCompletedDownloadTasksByServerIdCommandHandler
    : ICommandHandler<ClearCompletedDownloadTasksByServerIdCommand, Result<int>>
{
    private readonly IReaparrDbContext _dbContext;

    public ClearCompletedDownloadTasksByServerIdCommandHandler(IReaparrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<int>> ExecuteAsync(
        ClearCompletedDownloadTasksByServerIdCommand request,
        CancellationToken ct
    )
    {
        var plexServerId = request.PlexServerId;
        var totalRowsDeleted = 0;

        totalRowsDeleted += await _dbContext
            .DownloadTaskMovie.Where(x =>
                x.DownloadStatus == DownloadStatus.Completed && x.PlexServerId == plexServerId
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await _dbContext
            .DownloadTaskMovieFile.Where(x =>
                x.DownloadStatus == DownloadStatus.Completed && x.PlexServerId == plexServerId
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await _dbContext
            .DownloadTaskTvShow.Where(x =>
                x.DownloadStatus == DownloadStatus.Completed && x.PlexServerId == plexServerId
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await _dbContext
            .DownloadTaskTvShowSeason.Where(x =>
                x.DownloadStatus == DownloadStatus.Completed && x.PlexServerId == plexServerId
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await _dbContext
            .DownloadTaskTvShowEpisode.Where(x =>
                x.DownloadStatus == DownloadStatus.Completed && x.PlexServerId == plexServerId
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await _dbContext
            .DownloadTaskTvShowEpisodeFile.Where(x =>
                x.DownloadStatus == DownloadStatus.Completed && x.PlexServerId == plexServerId
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await _dbContext
            .DownloadTaskPhotoAlbums.Where(x =>
                x.DownloadStatus == DownloadStatus.Completed && x.PlexServerId == plexServerId
            )
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await _dbContext
            .DownloadTaskPhotoImages.Where(x =>
                x.DownloadStatus == DownloadStatus.Completed && x.PlexServerId == plexServerId
            )
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await _dbContext
            .DownloadTaskPhotoImageFiles.Where(x =>
                x.DownloadStatus == DownloadStatus.Completed && x.PlexServerId == plexServerId
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await _dbContext
            .DownloadTaskMusicArtists.Where(x =>
                x.DownloadStatus == DownloadStatus.Completed && x.PlexServerId == plexServerId
            )
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await _dbContext
            .DownloadTaskMusicAlbums.Where(x =>
                x.DownloadStatus == DownloadStatus.Completed && x.PlexServerId == plexServerId
            )
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await _dbContext
            .DownloadTaskMusicTracks.Where(x =>
                x.DownloadStatus == DownloadStatus.Completed && x.PlexServerId == plexServerId
            )
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await _dbContext
            .DownloadTaskMusicTrackFiles.Where(x =>
                x.DownloadStatus == DownloadStatus.Completed && x.PlexServerId == plexServerId
            )
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await _dbContext
            .DownloadTaskOtherVideos.Where(x =>
                x.DownloadStatus == DownloadStatus.Completed && x.PlexServerId == plexServerId
            )
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await _dbContext
            .DownloadTaskOtherVideoFiles.Where(x =>
                x.DownloadStatus == DownloadStatus.Completed && x.PlexServerId == plexServerId
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await _dbContext.DeleteOrphanedParentTasksByServerIdAsync(plexServerId, ct);

        return Result.Ok(totalRowsDeleted);
    }
}
