namespace Reaparr.Application;

public class DeleteDownloadTasksByKeyCommandValidator : AbstractValidator<DeleteDownloadTasksByKeyCommand>
{
    public DeleteDownloadTasksByKeyCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Keys).NotEmpty();
    }
}

public class DeleteDownloadTasksByKeyCommandHandler : ICommandHandler<DeleteDownloadTasksByKeyCommand, Result>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly IDownloadTaskUpdateDispatcher _downloadTaskUpdateDispatcher;

    public DeleteDownloadTasksByKeyCommandHandler(
        IReaparrDbContext dbContext,
        IDownloadTaskUpdateDispatcher downloadTaskUpdateDispatcher
    )
    {
        _dbContext = dbContext;
        _downloadTaskUpdateDispatcher = downloadTaskUpdateDispatcher;
    }

    public async Task<Result> ExecuteAsync(DeleteDownloadTasksByKeyCommand command, CancellationToken ct)
    {
        var affectedRootIds = await _dbContext.GetAffectedRootDownloadTaskIdsAsync(
            command.Keys.Select(k => k.Id).ToList(),
            ct
        );
        var orphanParentCandidates = await GetOrphanParentCandidatesAsync(affectedRootIds, ct);

        // Group by type so each delete targets its own task table.
        var byType = command.Keys.ToLookup(k => k.Type, k => k.Id);

        await DeleteAsync(_dbContext.DownloadTaskMovie, DownloadTaskType.Movie);
        await DeleteAsync(_dbContext.DownloadTaskMovieFile, DownloadTaskType.MovieData, DownloadTaskType.MoviePart);
        await DeleteAsync(_dbContext.DownloadTaskTvShow, DownloadTaskType.TvShow);
        await DeleteAsync(_dbContext.DownloadTaskTvShowSeason, DownloadTaskType.Season);
        await DeleteAsync(_dbContext.DownloadTaskTvShowEpisode, DownloadTaskType.Episode);
        await DeleteAsync(
            _dbContext.DownloadTaskTvShowEpisodeFile,
            DownloadTaskType.EpisodeData,
            DownloadTaskType.EpisodePart
        );
        await DeleteAsync(_dbContext.DownloadTaskPhotoAlbums, DownloadTaskType.PhotoAlbum);
        await DeleteAsync(_dbContext.DownloadTaskPhotoImages, DownloadTaskType.PhotoImage);
        await DeleteAsync(
            _dbContext.DownloadTaskPhotoImageFiles,
            DownloadTaskType.PhotoData,
            DownloadTaskType.PhotoPart
        );
        await DeleteAsync(_dbContext.DownloadTaskMusicArtists, DownloadTaskType.MusicArtist);
        await DeleteAsync(_dbContext.DownloadTaskMusicAlbums, DownloadTaskType.MusicAlbum);
        await DeleteAsync(_dbContext.DownloadTaskMusicTracks, DownloadTaskType.MusicTrack);
        await DeleteAsync(
            _dbContext.DownloadTaskMusicTrackFiles,
            DownloadTaskType.MusicTrackData,
            DownloadTaskType.MusicTrackPart
        );
        await DeleteAsync(_dbContext.DownloadTaskOtherVideos, DownloadTaskType.OtherVideo);
        await DeleteAsync(
            _dbContext.DownloadTaskOtherVideoFiles,
            DownloadTaskType.OtherVideoData,
            DownloadTaskType.OtherVideoPart
        );

        // Exclude roots that were already directly deleted above — orphan cleanup only
        // applies to roots whose children were removed, not roots deleted explicitly.
        var directlyDeletedRootIds = new HashSet<Guid>(
            byType[DownloadTaskType.Movie]
                .Concat(byType[DownloadTaskType.TvShow])
                .Concat(byType[DownloadTaskType.MusicArtist])
                .Concat(byType[DownloadTaskType.PhotoAlbum])
                .Concat(byType[DownloadTaskType.OtherVideo])
        );
        var orphanRootIds = affectedRootIds.Where(id => !directlyDeletedRootIds.Contains(id)).ToList();
        await _dbContext.DeleteOrphanedParentTasksByRootIdsAsync(orphanRootIds, ct);

        var orphanDeletedParentKeys = await GetDeletedParentKeysAsync(orphanParentCandidates, ct);

        // Notify front-end as well
        var notified = new HashSet<(Guid Id, DownloadTaskType Type)>();
        foreach (var key in command.Keys)
        {
            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(key, DownloadStatus.Deleted, ct);

            notified.Add((key.Id, key.Type));
        }

        foreach (var parentKey in orphanDeletedParentKeys)
        {
            if (!notified.Contains((parentKey.Id, parentKey.Type)))
            {
                await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(parentKey, DownloadStatus.Deleted, ct);
            }
        }

        return Result.Ok();

        Task DeleteAsync<T>(IQueryable<T> tasks, DownloadTaskType type, DownloadTaskType? partType = null)
            where T : DownloadTaskBase =>
            _dbContext.BulkDeleteByIdsAsync(
                partType.HasValue ? byType[type].Concat(byType[partType.Value]) : byType[type],
                (_, ids) => tasks.Where(x => ids.Contains(x.Id)),
                ct
            );
    }

    private async Task<List<DownloadTaskKey>> GetOrphanParentCandidatesAsync(
        IReadOnlyCollection<Guid> rootIds,
        CancellationToken ct
    )
    {
        if (rootIds.Count == 0)
            return [];

        var candidates = new List<DownloadTaskKey>();
        await AddCandidatesAsync(_dbContext.DownloadTaskMovie.Where(x => rootIds.Contains(x.Id)).ProjectToKey());
        await AddCandidatesAsync(_dbContext.DownloadTaskTvShow.Where(x => rootIds.Contains(x.Id)).ProjectToKey());
        await AddCandidatesAsync(
            _dbContext.DownloadTaskTvShowSeason.Where(x => rootIds.Contains(x.ParentId)).ProjectToKey()
        );
        await AddCandidatesAsync(
            _dbContext.DownloadTaskTvShowEpisode.Where(x => rootIds.Contains(x.Parent!.ParentId)).ProjectToKey()
        );
        await AddCandidatesAsync(_dbContext.DownloadTaskPhotoAlbums.Where(x => rootIds.Contains(x.Id)).ProjectToKey());
        await AddCandidatesAsync(
            _dbContext.DownloadTaskPhotoImages.Where(x => rootIds.Contains(x.ParentId)).ProjectToKey()
        );
        await AddCandidatesAsync(_dbContext.DownloadTaskMusicArtists.Where(x => rootIds.Contains(x.Id)).ProjectToKey());
        await AddCandidatesAsync(
            _dbContext.DownloadTaskMusicAlbums.Where(x => rootIds.Contains(x.ParentId)).ProjectToKey()
        );
        await AddCandidatesAsync(
            _dbContext.DownloadTaskMusicTracks.Where(x => rootIds.Contains(x.Parent!.ParentId)).ProjectToKey()
        );
        await AddCandidatesAsync(_dbContext.DownloadTaskOtherVideos.Where(x => rootIds.Contains(x.Id)).ProjectToKey());

        return candidates.DistinctBy(x => (x.Id, x.Type, x.PlexServerId, x.PlexLibraryId)).ToList();

        async Task AddCandidatesAsync(IQueryable<DownloadTaskKey> query) =>
            candidates.AddRange(await query.ToListAsync(ct));
    }

    private async Task<List<DownloadTaskKey>> GetDeletedParentKeysAsync(
        IReadOnlyCollection<DownloadTaskKey> candidates,
        CancellationToken ct
    )
    {
        if (candidates.Count == 0)
            return [];

        var idsByType = candidates.GroupBy(x => x.Type).ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var existing = new HashSet<Guid>();

        await AddExistingAsync(_dbContext.DownloadTaskMovie, DownloadTaskType.Movie);
        await AddExistingAsync(_dbContext.DownloadTaskTvShow, DownloadTaskType.TvShow);
        await AddExistingAsync(_dbContext.DownloadTaskTvShowSeason, DownloadTaskType.Season);
        await AddExistingAsync(_dbContext.DownloadTaskTvShowEpisode, DownloadTaskType.Episode);
        await AddExistingAsync(_dbContext.DownloadTaskPhotoAlbums, DownloadTaskType.PhotoAlbum);
        await AddExistingAsync(_dbContext.DownloadTaskPhotoImages, DownloadTaskType.PhotoImage);
        await AddExistingAsync(_dbContext.DownloadTaskMusicArtists, DownloadTaskType.MusicArtist);
        await AddExistingAsync(_dbContext.DownloadTaskMusicAlbums, DownloadTaskType.MusicAlbum);
        await AddExistingAsync(_dbContext.DownloadTaskMusicTracks, DownloadTaskType.MusicTrack);
        await AddExistingAsync(_dbContext.DownloadTaskOtherVideos, DownloadTaskType.OtherVideo);

        return candidates.Where(x => !existing.Contains(x.Id)).ToList();

        async Task AddExistingAsync<T>(IQueryable<T> tasks, DownloadTaskType type)
            where T : DownloadTaskBase
        {
            if (!idsByType.TryGetValue(type, out var ids) || ids.Count == 0)
                return;

            existing.UnionWith(await tasks.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct));
        }
    }
}
