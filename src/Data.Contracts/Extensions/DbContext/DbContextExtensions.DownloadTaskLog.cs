namespace Reaparr.Data.Contracts;

public static partial class DbContextExtensions
{
    public static async Task<Result<List<DownloadTaskLogBase>>> GetDownloadTaskLogsAsync(
        this IReaparrDbContext dbContext,
        DownloadTaskKey downloadTaskKey,
        int? sinceId,
        int? take,
        CancellationToken ct
    ) =>
        downloadTaskKey.Type switch
        {
            DownloadTaskType.Movie => await Result.Try(async Task<List<DownloadTaskLogBase>> () =>
                await dbContext
                    .DownloadTaskMovieFileLogs.Where(x => x.DownloadTaskMovieId == downloadTaskKey.Id)
                    .ApplyWhere(sinceId != null, x => x.Id > sinceId)
                    .OrderBy(x => x.Id)
                    .Select(x => (DownloadTaskLogBase)x)
                    .ApplyTake(take ?? 0)
                    .ToListAsync(ct)
            ),
            DownloadTaskType.MovieData => await Result.Try(async Task<List<DownloadTaskLogBase>> () =>
                await dbContext
                    .DownloadTaskMovieFileLogs.Where(x => x.DownloadTaskFileId == downloadTaskKey.Id)
                    .Where(x => sinceId == null || x.Id > sinceId)
                    .OrderBy(x => x.Id)
                    .Select(x => (DownloadTaskLogBase)x)
                    .ApplyTake(take ?? 0)
                    .ToListAsync(ct)
            ),
            DownloadTaskType.TvShow => await Result.Try(async Task<List<DownloadTaskLogBase>> () =>
                await dbContext
                    .DownloadTaskTvShowEpisodeFileLogs.Where(x => x.DownloadTaskTvShowId == downloadTaskKey.Id)
                    .ApplyWhere(sinceId != null, x => x.Id > sinceId)
                    .OrderBy(x => x.Id)
                    .Select(x => (DownloadTaskLogBase)x)
                    .ApplyTake(take ?? 0)
                    .ToListAsync(ct)
            ),
            DownloadTaskType.Season => await Result.Try(async Task<List<DownloadTaskLogBase>> () =>
                await dbContext
                    .DownloadTaskTvShowEpisodeFileLogs.Where(x => x.DownloadTaskTvShowSeasonId == downloadTaskKey.Id)
                    .ApplyWhere(sinceId != null, x => x.Id > sinceId)
                    .OrderBy(x => x.Id)
                    .Select(x => (DownloadTaskLogBase)x)
                    .ApplyTake(take ?? 0)
                    .ToListAsync(ct)
            ),
            DownloadTaskType.Episode => await Result.Try(async Task<List<DownloadTaskLogBase>> () =>
                await dbContext
                    .DownloadTaskTvShowEpisodeFileLogs.Where(x => x.DownloadTaskTvShowEpisodeId == downloadTaskKey.Id)
                    .ApplyWhere(sinceId != null, x => x.Id > sinceId)
                    .OrderBy(x => x.Id)
                    .Select(x => (DownloadTaskLogBase)x)
                    .ApplyTake(take ?? 0)
                    .ToListAsync(ct)
            ),
            DownloadTaskType.EpisodeData => await Result.Try(async Task<List<DownloadTaskLogBase>> () =>
                await dbContext
                    .DownloadTaskTvShowEpisodeFileLogs.Where(x => x.DownloadTaskFileId == downloadTaskKey.Id)
                    .ApplyWhere(sinceId != null, x => x.Id > sinceId)
                    .OrderBy(x => x.Id)
                    .Select(x => (DownloadTaskLogBase)x)
                    .ApplyTake(take ?? 0)
                    .ToListAsync(ct)
            ),
            DownloadTaskType.MusicArtist => await Result.Try(async Task<List<DownloadTaskLogBase>> () =>
                await dbContext
                    .DownloadTaskTrackFileLogs.Where(x => x.DownloadTaskArtistId == downloadTaskKey.Id)
                    .ApplyWhere(sinceId != null, x => x.Id > sinceId)
                    .OrderBy(x => x.Id)
                    .Select(x => (DownloadTaskLogBase)x)
                    .ApplyTake(take ?? 0)
                    .ToListAsync(ct)
            ),
            DownloadTaskType.MusicAlbum => await Result.Try(async Task<List<DownloadTaskLogBase>> () =>
                await dbContext
                    .DownloadTaskTrackFileLogs.Where(x => x.DownloadTaskAlbumId == downloadTaskKey.Id)
                    .ApplyWhere(sinceId != null, x => x.Id > sinceId)
                    .OrderBy(x => x.Id)
                    .Select(x => (DownloadTaskLogBase)x)
                    .ApplyTake(take ?? 0)
                    .ToListAsync(ct)
            ),
            DownloadTaskType.MusicTrack => await Result.Try(async Task<List<DownloadTaskLogBase>> () =>
                await dbContext
                    .DownloadTaskTrackFileLogs.Where(x => x.DownloadTaskTrackId == downloadTaskKey.Id)
                    .ApplyWhere(sinceId != null, x => x.Id > sinceId)
                    .OrderBy(x => x.Id)
                    .Select(x => (DownloadTaskLogBase)x)
                    .ApplyTake(take ?? 0)
                    .ToListAsync(ct)
            ),
            DownloadTaskType.MusicTrackData => await Result.Try(async Task<List<DownloadTaskLogBase>> () =>
                await dbContext
                    .DownloadTaskTrackFileLogs.Where(x => x.DownloadTaskFileId == downloadTaskKey.Id)
                    .ApplyWhere(sinceId != null, x => x.Id > sinceId)
                    .OrderBy(x => x.Id)
                    .Select(x => (DownloadTaskLogBase)x)
                    .ApplyTake(take ?? 0)
                    .ToListAsync(ct)
            ),
            DownloadTaskType.PhotoFolder => await Result.Try(async Task<List<DownloadTaskLogBase>> () =>
            {
                var fileIds = await GetPhotoFolderFileIdsAsync(dbContext, downloadTaskKey.Id, ct);
                return await dbContext
                    .DownloadTaskPhotoImageFileLogs.Where(x => fileIds.Contains(x.DownloadTaskFileId))
                    .ApplyWhere(sinceId != null, x => x.Id > sinceId)
                    .OrderBy(x => x.Id)
                    .Select(x => (DownloadTaskLogBase)x)
                    .ApplyTake(take ?? 0)
                    .ToListAsync(ct);
            }),
            DownloadTaskType.PhotoImage => await Result.Try(async Task<List<DownloadTaskLogBase>> () =>
                await dbContext
                    .DownloadTaskPhotoImageFileLogs.Where(x => x.DownloadTaskFileId == downloadTaskKey.Id)
                    .ApplyWhere(sinceId != null, x => x.Id > sinceId)
                    .OrderBy(x => x.Id)
                    .Select(x => (DownloadTaskLogBase)x)
                    .ApplyTake(take ?? 0)
                    .ToListAsync(ct)
            ),
            DownloadTaskType.OtherVideo => await Result.Try(async Task<List<DownloadTaskLogBase>> () =>
                await dbContext
                    .DownloadTaskOtherVideoFileLogs.Where(x => x.DownloadTaskOtherVideoId == downloadTaskKey.Id)
                    .ApplyWhere(sinceId != null, x => x.Id > sinceId)
                    .OrderBy(x => x.Id)
                    .Select(x => (DownloadTaskLogBase)x)
                    .ApplyTake(take ?? 0)
                    .ToListAsync(ct)
            ),
            DownloadTaskType.OtherVideoData => await Result.Try(async Task<List<DownloadTaskLogBase>> () =>
                await dbContext
                    .DownloadTaskOtherVideoFileLogs.Where(x => x.DownloadTaskFileId == downloadTaskKey.Id)
                    .ApplyWhere(sinceId != null, x => x.Id > sinceId)
                    .OrderBy(x => x.Id)
                    .Select(x => (DownloadTaskLogBase)x)
                    .ApplyTake(take ?? 0)
                    .ToListAsync(ct)
            ),
            _ => Result
                .Fail("DownloadTaskLog of type {DownloadTaskType} not implemented", downloadTaskKey.Type)
                .LogError(),
        };

    public static async Task<Result<int>> DeleteDownloadTaskLogsAsync(
        this IReaparrDbContext dbContext,
        DownloadTaskKey downloadTaskKey,
        CancellationToken ct
    ) =>
        downloadTaskKey.Type switch
        {
            DownloadTaskType.Movie => await Result.Try(() =>
                dbContext
                    .DownloadTaskMovieFileLogs.Where(x => x.DownloadTaskMovieId == downloadTaskKey.Id)
                    .ExecuteDeleteAsync(ct)
            ),
            DownloadTaskType.MovieData => await Result.Try(() =>
                dbContext
                    .DownloadTaskMovieFileLogs.Where(x => x.DownloadTaskFileId == downloadTaskKey.Id)
                    .ExecuteDeleteAsync(ct)
            ),
            DownloadTaskType.TvShow => await Result.Try(() =>
                dbContext
                    .DownloadTaskTvShowEpisodeFileLogs.Where(x => x.DownloadTaskTvShowId == downloadTaskKey.Id)
                    .ExecuteDeleteAsync(ct)
            ),
            DownloadTaskType.Season => await Result.Try(() =>
                dbContext
                    .DownloadTaskTvShowEpisodeFileLogs.Where(x => x.DownloadTaskTvShowSeasonId == downloadTaskKey.Id)
                    .ExecuteDeleteAsync(ct)
            ),
            DownloadTaskType.Episode => await Result.Try(() =>
                dbContext
                    .DownloadTaskTvShowEpisodeFileLogs.Where(x => x.DownloadTaskTvShowEpisodeId == downloadTaskKey.Id)
                    .ExecuteDeleteAsync(ct)
            ),
            DownloadTaskType.EpisodeData => await Result.Try(() =>
                dbContext
                    .DownloadTaskTvShowEpisodeFileLogs.Where(x => x.DownloadTaskFileId == downloadTaskKey.Id)
                    .ExecuteDeleteAsync(ct)
            ),
            DownloadTaskType.MusicArtist => await Result.Try(() =>
                dbContext
                    .DownloadTaskTrackFileLogs.Where(x => x.DownloadTaskArtistId == downloadTaskKey.Id)
                    .ExecuteDeleteAsync(ct)
            ),
            DownloadTaskType.MusicAlbum => await Result.Try(() =>
                dbContext
                    .DownloadTaskTrackFileLogs.Where(x => x.DownloadTaskAlbumId == downloadTaskKey.Id)
                    .ExecuteDeleteAsync(ct)
            ),
            DownloadTaskType.MusicTrack => await Result.Try(() =>
                dbContext
                    .DownloadTaskTrackFileLogs.Where(x => x.DownloadTaskTrackId == downloadTaskKey.Id)
                    .ExecuteDeleteAsync(ct)
            ),
            DownloadTaskType.MusicTrackData => await Result.Try(() =>
                dbContext
                    .DownloadTaskTrackFileLogs.Where(x => x.DownloadTaskFileId == downloadTaskKey.Id)
                    .ExecuteDeleteAsync(ct)
            ),
            DownloadTaskType.PhotoFolder => await Result.Try(async Task<int> () =>
            {
                var fileIds = await GetPhotoFolderFileIdsAsync(dbContext, downloadTaskKey.Id, ct);
                return await dbContext
                    .DownloadTaskPhotoImageFileLogs.Where(x => fileIds.Contains(x.DownloadTaskFileId))
                    .ExecuteDeleteAsync(ct);
            }),
            DownloadTaskType.PhotoImage => await Result.Try(() =>
                dbContext
                    .DownloadTaskPhotoImageFileLogs.Where(x => x.DownloadTaskFileId == downloadTaskKey.Id)
                    .ExecuteDeleteAsync(ct)
            ),
            DownloadTaskType.OtherVideo => await Result.Try(() =>
                dbContext
                    .DownloadTaskOtherVideoFileLogs.Where(x => x.DownloadTaskOtherVideoId == downloadTaskKey.Id)
                    .ExecuteDeleteAsync(ct)
            ),
            DownloadTaskType.OtherVideoData => await Result.Try(() =>
                dbContext
                    .DownloadTaskOtherVideoFileLogs.Where(x => x.DownloadTaskFileId == downloadTaskKey.Id)
                    .ExecuteDeleteAsync(ct)
            ),
            _ => Result
                .Fail("DownloadTaskLog of type {DownloadTaskType} not implemented", downloadTaskKey.Type)
                .LogError(),
        };

    public static async Task CreateDownloadClientLog(
        this IReaparrDbContext dbContext,
        DownloadTaskKey downloadTaskKey,
        NotificationLevel logLevel,
        DownloadStatus status,
        string message
    )
    {
        if (downloadTaskKey.Type is DownloadTaskType.MovieData)
        {
            var parentId = await dbContext
                .DownloadTaskMovieFile.Where(x => x.Id == downloadTaskKey.Id)
                .Select(x => x.ParentId)
                .FirstOrDefaultAsync(CancellationToken.None);
            dbContext.DownloadTaskMovieFileLogs.Add(
                new DownloadTaskMovieFileLog
                {
                    Message = message,
                    LogLevel = logLevel,
                    Status = status,
                    DownloadTaskFileId = downloadTaskKey.Id,
                    DownloadTaskMovieId = parentId,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        if (downloadTaskKey.Type is DownloadTaskType.EpisodeData)
        {
            var ids = await dbContext
                .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == downloadTaskKey.Id)
                .Select(x => new
                {
                    EpisodeId = x.ParentId,
                    SeasonId = x.Parent!.ParentId,
                    TvShowId = x.Parent.Parent!.ParentId,
                })
                .FirstOrDefaultAsync(CancellationToken.None);

            dbContext.DownloadTaskTvShowEpisodeFileLogs.Add(
                new DownloadTaskTvShowEpisodeFileLog
                {
                    Message = message,
                    LogLevel = logLevel,
                    Status = status,
                    DownloadTaskFileId = downloadTaskKey.Id,
                    DownloadTaskTvShowEpisodeId = ids?.EpisodeId ?? Guid.Empty,
                    DownloadTaskTvShowSeasonId = ids?.SeasonId ?? Guid.Empty,
                    DownloadTaskTvShowId = ids?.TvShowId ?? Guid.Empty,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        if (downloadTaskKey.Type is DownloadTaskType.MusicTrackData)
        {
            var ids = await dbContext
                .DownloadTaskMusicTrackFiles.Where(x => x.Id == downloadTaskKey.Id)
                .Select(x => new
                {
                    TrackId = x.ParentId,
                    AlbumId = x.Parent!.ParentId,
                    ArtistId = x.Parent.Parent!.ParentId,
                })
                .FirstOrDefaultAsync(CancellationToken.None);

            await dbContext.DownloadTaskTrackFileLogs.AddAsync(
                new DownloadTaskTrackFileLog
                {
                    Message = message,
                    LogLevel = logLevel,
                    Status = status,
                    DownloadTaskFileId = downloadTaskKey.Id,
                    DownloadTaskTrackId = ids?.TrackId ?? Guid.Empty,
                    DownloadTaskAlbumId = ids?.AlbumId ?? Guid.Empty,
                    DownloadTaskArtistId = ids?.ArtistId ?? Guid.Empty,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        if (downloadTaskKey.Type is DownloadTaskType.PhotoImage)
        {
            await dbContext.DownloadTaskPhotoImageFileLogs.AddAsync(
                new DownloadTaskPhotoImageFileLog
                {
                    Message = message,
                    LogLevel = logLevel,
                    Status = status,
                    DownloadTaskFileId = downloadTaskKey.Id,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        if (downloadTaskKey.Type is DownloadTaskType.OtherVideoData)
        {
            var parentId = await dbContext
                .DownloadTaskOtherVideoFiles.Where(x => x.Id == downloadTaskKey.Id)
                .Select(x => x.ParentId)
                .FirstOrDefaultAsync(CancellationToken.None);
            await dbContext.DownloadTaskOtherVideoFileLogs.AddAsync(
                new DownloadTaskOtherVideoFileLog
                {
                    Message = message,
                    LogLevel = logLevel,
                    Status = status,
                    DownloadTaskFileId = downloadTaskKey.Id,
                    DownloadTaskOtherVideoId = parentId,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    // TODO Refactor this method to use the same approach as CreateDownloadClientLog
    public static async Task CreateDownloadClientLogs(
        this IReaparrDbContext dbContext,
        IEnumerable<DownloadTaskLogBase> logs,
        CancellationToken cancellationToken = default
    )
    {
        if (logs is IList<DownloadTaskMovieFileLog> movieLogs)
        {
            await dbContext.BulkInsertAsync(movieLogs, cancellationToken: cancellationToken);
            return;
        }
        if (logs is IList<DownloadTaskTvShowEpisodeFileLog> episodeLogs)
        {
            await dbContext.BulkInsertAsync(episodeLogs, cancellationToken: cancellationToken);
            return;
        }
        if (logs is IList<DownloadTaskTrackFileLog> trackLogs)
        {
            await dbContext.BulkInsertAsync(trackLogs, cancellationToken: cancellationToken);
            return;
        }
        if (logs is IList<DownloadTaskPhotoImageFileLog> photoLogs)
        {
            await dbContext.BulkInsertAsync(photoLogs, cancellationToken: cancellationToken);
            return;
        }
        if (logs is IList<DownloadTaskOtherVideoFileLog> videoLogs)
        {
            await dbContext.BulkInsertAsync(videoLogs, cancellationToken: cancellationToken);
            return;
        }

        List<DownloadTaskMovieFileLog>? movies = null;
        List<DownloadTaskTvShowEpisodeFileLog>? episodes = null;
        List<DownloadTaskTrackFileLog>? tracks = null;
        List<DownloadTaskPhotoImageFileLog>? photos = null;
        List<DownloadTaskOtherVideoFileLog>? videos = null;
        foreach (var log in logs)
        {
            switch (log)
            {
                case DownloadTaskMovieFileLog movie:
                    (movies ??= []).Add(movie);
                    break;
                case DownloadTaskTvShowEpisodeFileLog episode:
                    (episodes ??= []).Add(episode);
                    break;
                case DownloadTaskTrackFileLog track:
                    (tracks ??= []).Add(track);
                    break;
                case DownloadTaskPhotoImageFileLog photo:
                    (photos ??= []).Add(photo);
                    break;
                case DownloadTaskOtherVideoFileLog video:
                    (videos ??= []).Add(video);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logs));
            }
        }
        if (movies is not null)
            await dbContext.BulkInsertAsync(movies, cancellationToken: cancellationToken);
        if (episodes is not null)
            await dbContext.BulkInsertAsync(episodes, cancellationToken: cancellationToken);
        if (tracks is not null)
            await dbContext.BulkInsertAsync(tracks, cancellationToken: cancellationToken);
        if (photos is not null)
            await dbContext.BulkInsertAsync(photos, cancellationToken: cancellationToken);
        if (videos is not null)
            await dbContext.BulkInsertAsync(videos, cancellationToken: cancellationToken);
    }

    private static async Task<List<Guid>> GetPhotoFolderFileIdsAsync(
        IReaparrDbContext dbContext,
        Guid folderId,
        CancellationToken cancellationToken
    )
    {
        var scope = await dbContext
            .DownloadTaskPhotoFolders.Where(x => x.Id == folderId)
            .Select(x => new { x.PlexServerId, x.PlexLibraryId })
            .FirstOrDefaultAsync(cancellationToken);
        if (scope is null)
            return [];

        var folders = await dbContext
            .DownloadTaskPhotoFolders.Where(x =>
                x.PlexServerId == scope.PlexServerId && x.PlexLibraryId == scope.PlexLibraryId
            )
            .Select(x => new { x.Id, x.ParentId })
            .ToListAsync(cancellationToken);

        var childrenByParent = folders
            .Where(x => x.ParentId.HasValue)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToList());
        var folderIds = new HashSet<Guid> { folderId };
        var pending = new Stack<Guid>();
        pending.Push(folderId);
        while (pending.TryPop(out var currentId))
        {
            if (!childrenByParent.TryGetValue(currentId, out var children))
                continue;

            foreach (var childId in children.Where(childId => folderIds.Add(childId)))
            {
                pending.Push(childId);
            }
        }

        return await dbContext
            .DownloadTaskPhotoImageFiles.Where(x => folderIds.Contains(x.ParentId))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}
