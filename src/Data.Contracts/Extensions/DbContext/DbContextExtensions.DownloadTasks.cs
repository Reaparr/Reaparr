namespace Reaparr.Data.Contracts;

// TODO Look for performance improvements in these extensions and or to clean it up. There are a lot of queries that could be optimized and reduced in number, especially when retrieving download tasks by server or getting download task keys.
public static partial class DbContextExtensions
{
    public static async Task<Result<string>> GetDownloadUrl(
        this IReaparrDbContext dbContext,
        int plexServerId,
        string fileLocationUrl,
        CancellationToken cancellationToken = default
    )
    {
        var plexServerConnectionResult = await dbContext.ChoosePlexServerConnection(plexServerId, cancellationToken);
        if (plexServerConnectionResult.IsFailed)
            return plexServerConnectionResult.ToResult().LogError();

        var plexServerConnection = plexServerConnectionResult.Value;
        var plexServer = plexServerConnection.PlexServer;

        var tokenResult = await dbContext.GetPlexServerTokenAsync(plexServerId, cancellationToken);
        if (tokenResult.IsFailed)
        {
            _log.Here().Error("Could not find a valid token for server {ServerName}", plexServer?.Name ?? "Unknown");
            return tokenResult.ToResult();
        }

        var downloadUrl = plexServerConnection.GetDownloadUrl(fileLocationUrl, tokenResult.Value);
        return Result.Ok(downloadUrl);
    }

    public static async Task<DownloadTaskKey?> GetDownloadTaskKeyAsync(
        this IReaparrDbContext dbContext,
        Guid guid,
        CancellationToken cancellationToken = default
    )
    {
        if (guid == Guid.Empty)
            return null;

        var queries = new List<IQueryable<DownloadTaskKey>>
        {
            dbContext.DownloadTaskTvShow.ProjectToKey(),
            dbContext.DownloadTaskTvShowSeason.ProjectToKey(),
            dbContext.DownloadTaskTvShowEpisode.ProjectToKey(),
            dbContext.DownloadTaskTvShowEpisodeFile.ProjectToKey(),
            dbContext.DownloadTaskMovie.ProjectToKey(),
            dbContext.DownloadTaskMovieFile.ProjectToKey(),
            dbContext.DownloadTaskPhotoAlbums.ProjectToKey(),
            dbContext.DownloadTaskPhotoImages.ProjectToKey(),
            dbContext.DownloadTaskPhotoImageFiles.ProjectToKey(),
            dbContext.DownloadTaskMusicArtists.ProjectToKey(),
            dbContext.DownloadTaskMusicAlbums.ProjectToKey(),
            dbContext.DownloadTaskMusicTracks.ProjectToKey(),
            dbContext.DownloadTaskMusicTrackFiles.ProjectToKey(),
            dbContext.DownloadTaskOtherVideos.ProjectToKey(),
            dbContext.DownloadTaskOtherVideoFiles.ProjectToKey(),
        };

        foreach (var query in queries)
        {
            var downloadTaskKey = await query.FirstOrDefaultAsync(x => x.Id == guid, cancellationToken);
            if (downloadTaskKey is not null)
                return downloadTaskKey;
        }

        return null;
    }

    public static async Task<List<DownloadTaskKey>> GetDownloadTaskKeysAsync(
        this IReaparrDbContext dbContext,
        IReadOnlyList<Guid> guids,
        CancellationToken cancellationToken = default
    )
    {
        if (guids.Count == 0)
            return [];

        var filtered = guids.Where(g => g != Guid.Empty).ToList();
        if (filtered.Count == 0)
            return [];

        // Filter BEFORE projecting so EF Core can translate the UNION across different entity
        // types. Applying .Where() after .ProjectToKey() (which uses Select) would place the
        // predicate after a client projection and cause a translation exception.
        var queries = new[]
        {
            dbContext.DownloadTaskTvShow.Where(x => filtered.Contains(x.Id)).ProjectToKey(),
            dbContext.DownloadTaskTvShowSeason.Where(x => filtered.Contains(x.Id)).ProjectToKey(),
            dbContext.DownloadTaskTvShowEpisode.Where(x => filtered.Contains(x.Id)).ProjectToKey(),
            dbContext.DownloadTaskTvShowEpisodeFile.Where(x => filtered.Contains(x.Id)).ProjectToKey(),
            dbContext.DownloadTaskMovie.Where(x => filtered.Contains(x.Id)).ProjectToKey(),
            dbContext.DownloadTaskMovieFile.Where(x => filtered.Contains(x.Id)).ProjectToKey(),
            dbContext.DownloadTaskPhotoAlbums.Where(x => filtered.Contains(x.Id)).ProjectToKey(),
            dbContext.DownloadTaskPhotoImages.Where(x => filtered.Contains(x.Id)).ProjectToKey(),
            dbContext.DownloadTaskPhotoImageFiles.Where(x => filtered.Contains(x.Id)).ProjectToKey(),
            dbContext.DownloadTaskMusicArtists.Where(x => filtered.Contains(x.Id)).ProjectToKey(),
            dbContext.DownloadTaskMusicAlbums.Where(x => filtered.Contains(x.Id)).ProjectToKey(),
            dbContext.DownloadTaskMusicTracks.Where(x => filtered.Contains(x.Id)).ProjectToKey(),
            dbContext.DownloadTaskMusicTrackFiles.Where(x => filtered.Contains(x.Id)).ProjectToKey(),
            dbContext.DownloadTaskOtherVideos.Where(x => filtered.Contains(x.Id)).ProjectToKey(),
            dbContext.DownloadTaskOtherVideoFiles.Where(x => filtered.Contains(x.Id)).ProjectToKey(),
        };

        var keys = new List<DownloadTaskKey>();

        foreach (var query in queries)
            keys.AddRange(await query.ToListAsync(cancellationToken));

        return keys;
    }

    public static async Task<List<DownloadTaskKey>> GetDownloadTaskKeysByStatusAsync(
        this IReaparrDbContext dbContext,
        IReadOnlyCollection<DownloadTaskKey> keys,
        IReadOnlyCollection<DownloadStatus> statuses,
        CancellationToken cancellationToken = default
    )
    {
        if (keys.Count == 0 || statuses.Count == 0)
            return [];

        var byType = keys.ToLookup(x => x.Type);
        var queries = new List<IQueryable<DownloadTaskKey>>();

        AddDownloadTaskKeysByStatusQuery(
            queries,
            dbContext.DownloadTaskMovie,
            byType[DownloadTaskType.Movie],
            statuses
        );
        AddDownloadTaskKeysByStatusQuery(
            queries,
            dbContext.DownloadTaskMovieFile,
            byType[DownloadTaskType.MovieData].Concat(byType[DownloadTaskType.MoviePart]),
            statuses
        );
        AddDownloadTaskKeysByStatusQuery(
            queries,
            dbContext.DownloadTaskTvShow,
            byType[DownloadTaskType.TvShow],
            statuses
        );
        AddDownloadTaskKeysByStatusQuery(
            queries,
            dbContext.DownloadTaskTvShowSeason,
            byType[DownloadTaskType.Season],
            statuses
        );
        AddDownloadTaskKeysByStatusQuery(
            queries,
            dbContext.DownloadTaskTvShowEpisode,
            byType[DownloadTaskType.Episode],
            statuses
        );
        AddDownloadTaskKeysByStatusQuery(
            queries,
            dbContext.DownloadTaskTvShowEpisodeFile,
            byType[DownloadTaskType.EpisodeData].Concat(byType[DownloadTaskType.EpisodePart]),
            statuses
        );
        AddDownloadTaskKeysByStatusQuery(
            queries,
            dbContext.DownloadTaskPhotoAlbums,
            byType[DownloadTaskType.PhotoAlbum],
            statuses
        );
        AddDownloadTaskKeysByStatusQuery(
            queries,
            dbContext.DownloadTaskPhotoImages,
            byType[DownloadTaskType.PhotoImage],
            statuses
        );
        AddDownloadTaskKeysByStatusQuery(
            queries,
            dbContext.DownloadTaskPhotoImageFiles,
            byType[DownloadTaskType.PhotoData].Concat(byType[DownloadTaskType.PhotoPart]),
            statuses
        );
        AddDownloadTaskKeysByStatusQuery(
            queries,
            dbContext.DownloadTaskMusicArtists,
            byType[DownloadTaskType.MusicArtist],
            statuses
        );
        AddDownloadTaskKeysByStatusQuery(
            queries,
            dbContext.DownloadTaskMusicAlbums,
            byType[DownloadTaskType.MusicAlbum],
            statuses
        );
        AddDownloadTaskKeysByStatusQuery(
            queries,
            dbContext.DownloadTaskMusicTracks,
            byType[DownloadTaskType.MusicTrack],
            statuses
        );
        AddDownloadTaskKeysByStatusQuery(
            queries,
            dbContext.DownloadTaskMusicTrackFiles,
            byType[DownloadTaskType.MusicTrackData].Concat(byType[DownloadTaskType.MusicTrackPart]),
            statuses
        );
        AddDownloadTaskKeysByStatusQuery(
            queries,
            dbContext.DownloadTaskOtherVideos,
            byType[DownloadTaskType.OtherVideo],
            statuses
        );
        AddDownloadTaskKeysByStatusQuery(
            queries,
            dbContext.DownloadTaskOtherVideoFiles,
            byType[DownloadTaskType.OtherVideoData].Concat(byType[DownloadTaskType.OtherVideoPart]),
            statuses
        );

        if (queries.Count == 0)
            return [];

        var query = queries.Skip(1).Aggregate(queries[0], (currentQuery, nextQuery) => currentQuery.Concat(nextQuery));

        return [.. (await query.ToListAsync(cancellationToken)).Distinct()];
    }

    private static void AddDownloadTaskKeysByStatusQuery<T>(
        List<IQueryable<DownloadTaskKey>> queries,
        IQueryable<T> set,
        IEnumerable<DownloadTaskKey> keys,
        IReadOnlyCollection<DownloadStatus> statuses
    )
        where T : DownloadTaskBase
    {
        var ids = keys.Select(x => x.Id).ToList();
        if (ids.Count == 0)
            return;

        queries.Add(set.Where(x => ids.Contains(x.Id) && statuses.Contains(x.DownloadStatus)).ProjectToKey());
    }

    public static async Task<DownloadTaskType> GetDownloadTaskTypeAsync(
        this IReaparrDbContext dbContext,
        Guid guid,
        CancellationToken cancellationToken = default
    )
    {
        if (guid == Guid.Empty)
            return DownloadTaskType.None;

        if (await dbContext.DownloadTaskTvShow.AnyAsync(x => x.Id == guid, cancellationToken))
            return DownloadTaskType.TvShow;

        if (await dbContext.DownloadTaskMovie.AnyAsync(x => x.Id == guid, cancellationToken))
            return DownloadTaskType.Movie;

        if (await dbContext.DownloadTaskTvShowSeason.AnyAsync(x => x.Id == guid, cancellationToken))
            return DownloadTaskType.Season;

        if (await dbContext.DownloadTaskTvShowEpisode.AnyAsync(x => x.Id == guid, cancellationToken))
            return DownloadTaskType.Episode;

        if (await dbContext.DownloadTaskTvShowEpisodeFile.AnyAsync(x => x.Id == guid, cancellationToken))
            return DownloadTaskType.EpisodeData;

        if (await dbContext.DownloadTaskMovieFile.AnyAsync(x => x.Id == guid, cancellationToken))
            return DownloadTaskType.MovieData;

        if (await dbContext.DownloadTaskPhotoAlbums.AnyAsync(x => x.Id == guid, cancellationToken))
            return DownloadTaskType.PhotoAlbum;

        if (await dbContext.DownloadTaskPhotoImages.AnyAsync(x => x.Id == guid, cancellationToken))
            return DownloadTaskType.PhotoImage;

        if (await dbContext.DownloadTaskPhotoImageFiles.AnyAsync(x => x.Id == guid, cancellationToken))
            return DownloadTaskType.PhotoData;

        if (await dbContext.DownloadTaskMusicArtists.AnyAsync(x => x.Id == guid, cancellationToken))
            return DownloadTaskType.MusicArtist;

        if (await dbContext.DownloadTaskMusicAlbums.AnyAsync(x => x.Id == guid, cancellationToken))
            return DownloadTaskType.MusicAlbum;

        if (await dbContext.DownloadTaskMusicTracks.AnyAsync(x => x.Id == guid, cancellationToken))
            return DownloadTaskType.MusicTrack;

        if (await dbContext.DownloadTaskMusicTrackFiles.AnyAsync(x => x.Id == guid, cancellationToken))
            return DownloadTaskType.MusicTrackData;

        if (await dbContext.DownloadTaskOtherVideos.AnyAsync(x => x.Id == guid, cancellationToken))
            return DownloadTaskType.OtherVideo;

        if (await dbContext.DownloadTaskOtherVideoFiles.AnyAsync(x => x.Id == guid, cancellationToken))
            return DownloadTaskType.OtherVideoData;

        return DownloadTaskType.None;
    }

    /// <summary>
    /// Retrieves a <see cref="DownloadTaskGeneric"/> from the database based on the <paramref name="key"/> with all its children and related entities.
    /// </summary>
    /// <param name="dbContext"> The <see cref="IReaparrDbContext"/> to query. </param>
    /// <param name="key"> The <see cref="DownloadTaskKey"/> to retrieve the <see cref="DownloadTaskGeneric"/> by. </param>
    /// <param name="cancellationToken"> The token to monitor for cancellation requests. </param>
    /// <returns> The <see cref="DownloadTaskGeneric"/> if found, otherwise null. </returns>
    public static Task<DownloadTaskGeneric?> GetDownloadTaskAsync(
        this IReaparrDbContext dbContext,
        DownloadTaskKey key,
        CancellationToken cancellationToken = default
    ) => dbContext.GetDownloadTaskAsync(key.Id, key.Type, cancellationToken);

    /// <summary>
    /// Retrieves a <see cref="DownloadTaskGeneric"/> from the database based on the <paramref name="id"/> and <paramref name="type"/> with all its children and related entities.
    /// </summary>
    /// <param name="dbContext"> The <see cref="IReaparrDbContext"/> to query. </param>
    /// <param name="id"> The id of the <see cref="DownloadTaskGeneric"/> to retrieve. </param>
    /// <param name="type"> The type of the root <see cref="DownloadTaskGeneric"/> to retrieve. </param>
    /// <param name="cancellationToken"> The token to monitor for cancellation requests. </param>
    /// <returns> The <see cref="DownloadTaskGeneric"/> if found, otherwise null. </returns>
    public static async Task<DownloadTaskGeneric?> GetDownloadTaskAsync(
        this IReaparrDbContext dbContext,
        Guid id,
        DownloadTaskType type = DownloadTaskType.None,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (id == Guid.Empty)
                return null;

            if (type == DownloadTaskType.None)
                type = await dbContext.GetDownloadTaskTypeAsync(id, cancellationToken);

            switch (type)
            {
                // DownloadTaskType.Movie
                case DownloadTaskType.Movie:
                    return (
                            await dbContext
                                .DownloadTaskMovie.IncludeAll()
                                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                        )?.ToGeneric() ?? null;

                // DownloadTaskType.MovieData
                case DownloadTaskType.MovieData:
                case DownloadTaskType.MoviePart:
                    return (
                            await dbContext
                                .DownloadTaskMovieFile.Include(x => x.PlexServer)
                                .Include(x => x.PlexLibrary)
                                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                        )?.ToGeneric() ?? null;

                // DownloadTaskType.TvShow
                case DownloadTaskType.TvShow:
                    return (
                            await dbContext
                                .DownloadTaskTvShow.Include(x => x.PlexServer)
                                .Include(x => x.PlexLibrary)
                                .Include(x => x.Children)
                                    .ThenInclude(x => x.Children)
                                        .ThenInclude(x => x.Children)
                                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                        )?.ToGeneric() ?? null;

                // DownloadTaskType.TvShowSeason
                case DownloadTaskType.Season:
                    return (
                            await dbContext
                                .DownloadTaskTvShowSeason.Include(x => x.PlexServer)
                                .Include(x => x.PlexLibrary)
                                .Include(x => x.Children)
                                    .ThenInclude(x => x.Children)
                                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                        )?.ToGeneric() ?? null;

                // DownloadTaskType.Episode
                case DownloadTaskType.Episode:
                    return (
                            await dbContext
                                .DownloadTaskTvShowEpisode.Include(x => x.PlexServer)
                                .Include(x => x.PlexLibrary)
                                .Include(x => x.Children)
                                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                        )?.ToGeneric() ?? null;

                // DownloadTaskType.EpisodeData
                case DownloadTaskType.EpisodeData:
                case DownloadTaskType.EpisodePart:
                    return (
                            await dbContext
                                .DownloadTaskTvShowEpisodeFile.Include(x => x.PlexServer)
                                .Include(x => x.PlexLibrary)
                                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                        )?.ToGeneric() ?? null;

                case DownloadTaskType.PhotoAlbum:
                    return (
                        await dbContext
                            .DownloadTaskPhotoAlbums.IncludeAll()
                            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    )?.ToGeneric();

                case DownloadTaskType.PhotoImage:
                    return (
                        await dbContext
                            .DownloadTaskPhotoImages.IncludeAll()
                            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    )?.ToGeneric();

                case DownloadTaskType.PhotoData:
                case DownloadTaskType.PhotoPart:
                    return (
                        await dbContext
                            .DownloadTaskPhotoImageFiles.Include(x => x.PlexServer)
                            .Include(x => x.PlexLibrary)
                            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    )?.ToGeneric();

                case DownloadTaskType.MusicArtist:
                    return (
                        await dbContext
                            .DownloadTaskMusicArtists.IncludeAll()
                            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    )?.ToGeneric();
                case DownloadTaskType.MusicAlbum:
                    return (
                        await dbContext
                            .DownloadTaskMusicAlbums.IncludeAll()
                            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    )?.ToGeneric();
                case DownloadTaskType.MusicTrack:
                    return (
                        await dbContext
                            .DownloadTaskMusicTracks.IncludeAll()
                            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    )?.ToGeneric();
                case DownloadTaskType.MusicTrackData:
                case DownloadTaskType.MusicTrackPart:
                    return (
                        await dbContext
                            .DownloadTaskMusicTrackFiles.Include(x => x.PlexServer)
                            .Include(x => x.PlexLibrary)
                            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    )?.ToGeneric();
                case DownloadTaskType.OtherVideo:
                    return (
                        await dbContext
                            .DownloadTaskOtherVideos.IncludeAll()
                            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    )?.ToGeneric();
                case DownloadTaskType.OtherVideoData:
                case DownloadTaskType.OtherVideoPart:
                    return (
                        await dbContext
                            .DownloadTaskOtherVideoFiles.Include(x => x.PlexServer)
                            .Include(x => x.PlexLibrary)
                            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    )?.ToGeneric();

                default:
                    return null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Here().ErrorResult(ex);
            throw;
        }
    }

    public static async Task<DownloadStatus> GetDownloadTaskStatusAsync(
        this IReaparrDbContext dbContext,
        DownloadTaskKey key,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (!key.IsValid)
            {
                _log.Here()
                    .Error(
                        "Invalid {Name} {Key} in {GetDownloadTaskStatus}",
                        nameof(DownloadTaskKey),
                        key.ToString(),
                        nameof(GetDownloadTaskStatusAsync)
                    );
                return DownloadStatus.Unknown;
            }

            switch (key.Type)
            {
                // DownloadTaskType.Movie
                case DownloadTaskType.Movie:
                    return await dbContext
                        .DownloadTaskMovie.Where(x => x.Id == key.Id)
                        .Take(1)
                        .Select(x => x.DownloadStatus)
                        .FirstOrDefaultAsync(cancellationToken);

                // DownloadTaskType.MovieData
                case DownloadTaskType.MovieData:
                case DownloadTaskType.MoviePart:
                    return await dbContext
                        .DownloadTaskMovieFile.Where(x => x.Id == key.Id)
                        .Take(1)
                        .Select(x => x.DownloadStatus)
                        .FirstOrDefaultAsync(cancellationToken);

                // DownloadTaskType.TvShow
                case DownloadTaskType.TvShow:
                    return await dbContext
                        .DownloadTaskTvShow.Where(x => x.Id == key.Id)
                        .Take(1)
                        .Select(x => x.DownloadStatus)
                        .FirstOrDefaultAsync(cancellationToken);

                // DownloadTaskType.TvShowSeason
                case DownloadTaskType.Season:
                    return await dbContext
                        .DownloadTaskTvShowSeason.Where(x => x.Id == key.Id)
                        .Take(1)
                        .Select(x => x.DownloadStatus)
                        .FirstOrDefaultAsync(cancellationToken);

                // DownloadTaskType.Episode
                case DownloadTaskType.Episode:
                    return await dbContext
                        .DownloadTaskTvShowEpisode.Where(x => x.Id == key.Id)
                        .Take(1)
                        .Select(x => x.DownloadStatus)
                        .FirstOrDefaultAsync(cancellationToken);

                // DownloadTaskType.EpisodeData
                case DownloadTaskType.EpisodeData:
                case DownloadTaskType.EpisodePart:
                    return await dbContext
                        .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == key.Id)
                        .Take(1)
                        .Select(x => x.DownloadStatus)
                        .FirstOrDefaultAsync(cancellationToken);

                case DownloadTaskType.PhotoAlbum:
                    return await dbContext
                        .DownloadTaskPhotoAlbums.Where(x => x.Id == key.Id)
                        .Select(x => x.DownloadStatus)
                        .FirstOrDefaultAsync(cancellationToken);

                case DownloadTaskType.PhotoImage:
                    return await dbContext
                        .DownloadTaskPhotoImages.Where(x => x.Id == key.Id)
                        .Select(x => x.DownloadStatus)
                        .FirstOrDefaultAsync(cancellationToken);

                case DownloadTaskType.PhotoData:
                case DownloadTaskType.PhotoPart:
                    return await dbContext
                        .DownloadTaskPhotoImageFiles.Where(x => x.Id == key.Id)
                        .Select(x => x.DownloadStatus)
                        .FirstOrDefaultAsync(cancellationToken);

                case DownloadTaskType.MusicArtist:
                    return await dbContext
                        .DownloadTaskMusicArtists.Where(x => x.Id == key.Id)
                        .Select(x => x.DownloadStatus)
                        .FirstOrDefaultAsync(cancellationToken);

                case DownloadTaskType.MusicAlbum:
                    return await dbContext
                        .DownloadTaskMusicAlbums.Where(x => x.Id == key.Id)
                        .Select(x => x.DownloadStatus)
                        .FirstOrDefaultAsync(cancellationToken);

                case DownloadTaskType.MusicTrack:
                    return await dbContext
                        .DownloadTaskMusicTracks.Where(x => x.Id == key.Id)
                        .Select(x => x.DownloadStatus)
                        .FirstOrDefaultAsync(cancellationToken);

                case DownloadTaskType.MusicTrackData:
                case DownloadTaskType.MusicTrackPart:
                    return await dbContext
                        .DownloadTaskMusicTrackFiles.Where(x => x.Id == key.Id)
                        .Select(x => x.DownloadStatus)
                        .FirstOrDefaultAsync(cancellationToken);

                case DownloadTaskType.OtherVideo:
                    return await dbContext
                        .DownloadTaskOtherVideos.Where(x => x.Id == key.Id)
                        .Select(x => x.DownloadStatus)
                        .FirstOrDefaultAsync(cancellationToken);

                case DownloadTaskType.OtherVideoData:
                case DownloadTaskType.OtherVideoPart:
                    return await dbContext
                        .DownloadTaskOtherVideoFiles.Where(x => x.Id == key.Id)
                        .Select(x => x.DownloadStatus)
                        .FirstOrDefaultAsync(cancellationToken);

                default:
                    _log.Here()
                        .Error(
                            "Unsupported {Name} {Type} in {GetDownloadTaskStatus}",
                            nameof(DownloadTaskType),
                            key.Type,
                            nameof(GetDownloadTaskStatusAsync)
                        );
                    return DownloadStatus.Unknown;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Here().ErrorResult(ex);
        }

        return DownloadStatus.Unknown;
    }

    public static async Task<DownloadTaskFileBase?> GetDownloadTaskFileAsync(
        this IReaparrDbContext dbContext,
        DownloadTaskKey key,
        CancellationToken cancellationToken = default
    )
    {
        switch (key.Type)
        {
            // DownloadTaskType.MovieData
            case DownloadTaskType.MovieData:
            case DownloadTaskType.MoviePart:
                return await dbContext
                    .DownloadTaskMovieFile.Include(x => x.PlexServer)
                    .Include(x => x.PlexLibrary)
                    .FirstOrDefaultAsync(x => x.Id == key.Id, cancellationToken);

            // DownloadTaskType.EpisodeData
            case DownloadTaskType.EpisodeData:
            case DownloadTaskType.EpisodePart:
                return await dbContext
                    .DownloadTaskTvShowEpisodeFile.Include(x => x.PlexServer)
                    .Include(x => x.PlexLibrary)
                    .FirstOrDefaultAsync(x => x.Id == key.Id, cancellationToken);
            case DownloadTaskType.PhotoData:
            case DownloadTaskType.PhotoPart:
                return await dbContext
                    .DownloadTaskPhotoImageFiles.Include(x => x.PlexServer)
                    .Include(x => x.PlexLibrary)
                    .FirstOrDefaultAsync(x => x.Id == key.Id, cancellationToken);
            case DownloadTaskType.MusicTrackData:
            case DownloadTaskType.MusicTrackPart:
                return await dbContext
                    .DownloadTaskMusicTrackFiles.Include(x => x.PlexServer)
                    .Include(x => x.PlexLibrary)
                    .FirstOrDefaultAsync(x => x.Id == key.Id, cancellationToken);
            case DownloadTaskType.OtherVideoData:
            case DownloadTaskType.OtherVideoPart:
                return await dbContext
                    .DownloadTaskOtherVideoFiles.Include(x => x.PlexServer)
                    .Include(x => x.PlexLibrary)
                    .FirstOrDefaultAsync(x => x.Id == key.Id, cancellationToken);

            default:
                return null;
        }
    }

    public static async Task<List<DownloadTaskGeneric>> GetAllDownloadTasksByServerAsync(
        this IReaparrDbContext dbContext,
        int plexServerId = 0,
        bool asTracking = false,
        CancellationToken cancellationToken = default
    )
    {
        var downloadTasks = new List<DownloadTaskGeneric>();

        var downloadTasksMovies = await dbContext
            .DownloadTaskMovie.AsTracking(
                asTracking ? QueryTrackingBehavior.TrackAll : QueryTrackingBehavior.NoTracking
            )
            .IncludeAll()
            .Where(x => plexServerId <= 0 || x.PlexServerId == plexServerId)
            .ToListAsync(cancellationToken);

        var downloadTasksTvShows = await dbContext
            .DownloadTaskTvShow.AsTracking(
                asTracking ? QueryTrackingBehavior.TrackAll : QueryTrackingBehavior.NoTracking
            )
            .IncludeAll()
            .Where(x => plexServerId <= 0 || x.PlexServerId == plexServerId)
            .ToListAsync(cancellationToken);

        var downloadTasksMusicArtists = await dbContext
            .DownloadTaskMusicArtists.AsTracking(
                asTracking ? QueryTrackingBehavior.TrackAll : QueryTrackingBehavior.NoTracking
            )
            .IncludeAll()
            .Where(x => plexServerId <= 0 || x.PlexServerId == plexServerId)
            .ToListAsync(cancellationToken);

        var downloadTasksPhotoAlbums = await dbContext
            .DownloadTaskPhotoAlbums.AsTracking(
                asTracking ? QueryTrackingBehavior.TrackAll : QueryTrackingBehavior.NoTracking
            )
            .IncludeAll()
            .Where(x => plexServerId <= 0 || x.PlexServerId == plexServerId)
            .ToListAsync(cancellationToken);
        var downloadTasksOtherVideos = await dbContext
            .DownloadTaskOtherVideos.AsTracking(
                asTracking ? QueryTrackingBehavior.TrackAll : QueryTrackingBehavior.NoTracking
            )
            .IncludeAll()
            .Where(x => plexServerId <= 0 || x.PlexServerId == plexServerId)
            .ToListAsync(cancellationToken);

        downloadTasks.AddRange(downloadTasksMovies.Select(x => x.ToGeneric()));
        downloadTasks.AddRange(downloadTasksTvShows.Select(x => x.ToGeneric()));
        downloadTasks.AddRange(downloadTasksMusicArtists.Select(x => x.ToGeneric()));
        downloadTasks.AddRange(downloadTasksPhotoAlbums.Select(x => x.ToGeneric()));
        downloadTasks.AddRange(downloadTasksOtherVideos.Select(x => x.ToGeneric()));

        // Sort by CreatedAt
        downloadTasks.Sort((x, y) => DateTime.Compare(x.CreatedAt, y.CreatedAt));

        return downloadTasks;
    }

    public static async Task<List<DownloadTaskGeneric>> GetDownloadProgressTasksByServerAsync(
        this IReaparrDbContext dbContext,
        int plexServerId,
        CancellationToken cancellationToken = default
    )
    {
        var isFilteredByServer = plexServerId > 0;

        var rows = await dbContext
            .DownloadTaskMovie.Where(x => !isFilteredByServer || x.PlexServerId == plexServerId)
            .Select(x => new DownloadProgressRow
            {
                Id = x.Id,
                ParentId = null,
                PlexApiRatingKey = x.PlexApiRatingKey,
                Title = x.Title,
                FullTitle = x.FullTitle,
                MediaType = PlexMediaType.Movie,
                DownloadTaskType = DownloadTaskType.Movie,
                DownloadStatus = x.DownloadStatus,
                CreatedAt = x.CreatedAt,
                PlexServerId = x.PlexServerId,
                PlexLibraryId = x.PlexLibraryId,
                DataReceived = 0,
                DataTotal = 0,
                Percentage = 0,
                DownloadSpeed = 0,
                TimeRemaining = 0,
                FileTransferSpeed = 0,
                FileDataTransferred = 0,
            })
            .Concat(
                dbContext
                    .DownloadTaskMovieFile.Where(x => !isFilteredByServer || x.PlexServerId == plexServerId)
                    .Select(x => new DownloadProgressRow
                    {
                        Id = x.Id,
                        ParentId = x.ParentId,
                        PlexApiRatingKey = x.PlexApiRatingKey,
                        Title = x.Title,
                        FullTitle = x.FullTitle,
                        MediaType = PlexMediaType.Movie,
                        DownloadTaskType = DownloadTaskType.MovieData,
                        DownloadStatus = x.DownloadStatus,
                        CreatedAt = x.CreatedAt,
                        PlexServerId = x.PlexServerId,
                        PlexLibraryId = x.PlexLibraryId,
                        DataReceived = x.DataReceived,
                        DataTotal = x.DataTotal,
                        Percentage = x.Percentage,
                        DownloadSpeed = x.DownloadSpeed,
                        TimeRemaining = x.TimeRemaining,
                        FileTransferSpeed = x.FileTransferSpeed,
                        FileDataTransferred = x.FileDataTransferred,
                    })
            )
            .Concat(
                dbContext
                    .DownloadTaskTvShow.Where(x => !isFilteredByServer || x.PlexServerId == plexServerId)
                    .Select(x => new DownloadProgressRow
                    {
                        Id = x.Id,
                        ParentId = null,
                        PlexApiRatingKey = x.PlexApiRatingKey,
                        Title = x.Title,
                        FullTitle = x.FullTitle,
                        MediaType = PlexMediaType.TvShow,
                        DownloadTaskType = DownloadTaskType.TvShow,
                        DownloadStatus = x.DownloadStatus,
                        CreatedAt = x.CreatedAt,
                        PlexServerId = x.PlexServerId,
                        PlexLibraryId = x.PlexLibraryId,
                        DataReceived = 0,
                        DataTotal = 0,
                        Percentage = 0,
                        DownloadSpeed = 0,
                        TimeRemaining = 0,
                        FileTransferSpeed = 0,
                        FileDataTransferred = 0,
                    })
            )
            .Concat(
                dbContext
                    .DownloadTaskTvShowSeason.Where(x => !isFilteredByServer || x.PlexServerId == plexServerId)
                    .Select(x => new DownloadProgressRow
                    {
                        Id = x.Id,
                        ParentId = x.ParentId,
                        PlexApiRatingKey = x.PlexApiRatingKey,
                        Title = x.Title,
                        FullTitle = x.FullTitle,
                        MediaType = PlexMediaType.Season,
                        DownloadTaskType = DownloadTaskType.Season,
                        DownloadStatus = x.DownloadStatus,
                        CreatedAt = x.CreatedAt,
                        PlexServerId = x.PlexServerId,
                        PlexLibraryId = x.PlexLibraryId,
                        DataReceived = 0,
                        DataTotal = 0,
                        Percentage = 0,
                        DownloadSpeed = 0,
                        TimeRemaining = 0,
                        FileTransferSpeed = 0,
                        FileDataTransferred = 0,
                    })
            )
            .Concat(
                dbContext
                    .DownloadTaskTvShowEpisode.Where(x => !isFilteredByServer || x.PlexServerId == plexServerId)
                    .Select(x => new DownloadProgressRow
                    {
                        Id = x.Id,
                        ParentId = x.ParentId,
                        PlexApiRatingKey = x.PlexApiRatingKey,
                        Title = x.Title,
                        FullTitle = x.FullTitle,
                        MediaType = PlexMediaType.Episode,
                        DownloadTaskType = DownloadTaskType.Episode,
                        DownloadStatus = x.DownloadStatus,
                        CreatedAt = x.CreatedAt,
                        PlexServerId = x.PlexServerId,
                        PlexLibraryId = x.PlexLibraryId,
                        DataReceived = 0,
                        DataTotal = 0,
                        Percentage = 0,
                        DownloadSpeed = 0,
                        TimeRemaining = 0,
                        FileTransferSpeed = 0,
                        FileDataTransferred = 0,
                    })
            )
            .Concat(
                dbContext
                    .DownloadTaskTvShowEpisodeFile.Where(x => !isFilteredByServer || x.PlexServerId == plexServerId)
                    .Select(x => new DownloadProgressRow
                    {
                        Id = x.Id,
                        ParentId = x.ParentId,
                        PlexApiRatingKey = x.PlexApiRatingKey,
                        Title = x.Title,
                        FullTitle = x.FullTitle,
                        MediaType = PlexMediaType.Episode,
                        DownloadTaskType = DownloadTaskType.EpisodeData,
                        DownloadStatus = x.DownloadStatus,
                        CreatedAt = x.CreatedAt,
                        PlexServerId = x.PlexServerId,
                        PlexLibraryId = x.PlexLibraryId,
                        DataReceived = x.DataReceived,
                        DataTotal = x.DataTotal,
                        Percentage = x.Percentage,
                        DownloadSpeed = x.DownloadSpeed,
                        TimeRemaining = x.TimeRemaining,
                        FileTransferSpeed = x.FileTransferSpeed,
                        FileDataTransferred = x.FileDataTransferred,
                    })
            )
            .Concat(
                dbContext
                    .DownloadTaskPhotoAlbums.Where(x => !isFilteredByServer || x.PlexServerId == plexServerId)
                    .Select(x => new DownloadProgressRow
                    {
                        Id = x.Id,
                        ParentId = null,
                        PlexApiRatingKey = x.PlexApiRatingKey,
                        Title = x.Title,
                        FullTitle = x.FullTitle,
                        MediaType = PlexMediaType.PhotoAlbum,
                        DownloadTaskType = DownloadTaskType.PhotoAlbum,
                        DownloadStatus = x.DownloadStatus,
                        CreatedAt = x.CreatedAt,
                        PlexServerId = x.PlexServerId,
                        PlexLibraryId = x.PlexLibraryId,
                        DataReceived = 0,
                        DataTotal = 0,
                        Percentage = 0,
                        DownloadSpeed = 0,
                        TimeRemaining = 0,
                        FileTransferSpeed = 0,
                        FileDataTransferred = 0,
                    })
            )
            .Concat(
                dbContext
                    .DownloadTaskPhotoImages.Where(x => !isFilteredByServer || x.PlexServerId == plexServerId)
                    .Select(x => new DownloadProgressRow
                    {
                        Id = x.Id,
                        ParentId = x.ParentId,
                        PlexApiRatingKey = x.PlexApiRatingKey,
                        Title = x.Title,
                        FullTitle = x.FullTitle,
                        MediaType = PlexMediaType.PhotoImage,
                        DownloadTaskType = DownloadTaskType.PhotoImage,
                        DownloadStatus = x.DownloadStatus,
                        CreatedAt = x.CreatedAt,
                        PlexServerId = x.PlexServerId,
                        PlexLibraryId = x.PlexLibraryId,
                        DataReceived = 0,
                        DataTotal = 0,
                        Percentage = 0,
                        DownloadSpeed = 0,
                        TimeRemaining = 0,
                        FileTransferSpeed = 0,
                        FileDataTransferred = 0,
                    })
            )
            .Concat(
                dbContext
                    .DownloadTaskPhotoImageFiles.Where(x => !isFilteredByServer || x.PlexServerId == plexServerId)
                    .Select(x => new DownloadProgressRow
                    {
                        Id = x.Id,
                        ParentId = x.ParentId,
                        PlexApiRatingKey = x.PlexApiRatingKey,
                        Title = x.Title,
                        FullTitle = x.FullTitle,
                        MediaType = PlexMediaType.PhotoImage,
                        DownloadTaskType = DownloadTaskType.PhotoData,
                        DownloadStatus = x.DownloadStatus,
                        CreatedAt = x.CreatedAt,
                        PlexServerId = x.PlexServerId,
                        PlexLibraryId = x.PlexLibraryId,
                        DataReceived = x.DataReceived,
                        DataTotal = x.DataTotal,
                        Percentage = x.Percentage,
                        DownloadSpeed = x.DownloadSpeed,
                        TimeRemaining = x.TimeRemaining,
                        FileTransferSpeed = x.FileTransferSpeed,
                        FileDataTransferred = x.FileDataTransferred,
                    })
            )
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        rows.AddRange(
            await dbContext
                .DownloadTaskMusicArtists.Where(x => !isFilteredByServer || x.PlexServerId == plexServerId)
                .Select(x => new DownloadProgressRow
                {
                    Id = x.Id,
                    ParentId = null,
                    PlexApiRatingKey = x.PlexApiRatingKey,
                    Title = x.Title,
                    FullTitle = x.FullTitle,
                    MediaType = PlexMediaType.MusicArtist,
                    DownloadTaskType = DownloadTaskType.MusicArtist,
                    DownloadStatus = x.DownloadStatus,
                    CreatedAt = x.CreatedAt,
                    PlexServerId = x.PlexServerId,
                    PlexLibraryId = x.PlexLibraryId,
                    DataReceived = 0,
                    DataTotal = 0,
                    Percentage = 0,
                    DownloadSpeed = 0,
                    TimeRemaining = 0,
                    FileTransferSpeed = 0,
                    FileDataTransferred = 0,
                })
                .ToListAsync(cancellationToken)
        );
        rows.AddRange(
            await dbContext
                .DownloadTaskMusicAlbums.Where(x => !isFilteredByServer || x.PlexServerId == plexServerId)
                .Select(x => new DownloadProgressRow
                {
                    Id = x.Id,
                    ParentId = x.ParentId,
                    PlexApiRatingKey = x.PlexApiRatingKey,
                    Title = x.Title,
                    FullTitle = x.FullTitle,
                    MediaType = PlexMediaType.MusicAlbum,
                    DownloadTaskType = DownloadTaskType.MusicAlbum,
                    DownloadStatus = x.DownloadStatus,
                    CreatedAt = x.CreatedAt,
                    PlexServerId = x.PlexServerId,
                    PlexLibraryId = x.PlexLibraryId,
                    DataReceived = 0,
                    DataTotal = 0,
                    Percentage = 0,
                    DownloadSpeed = 0,
                    TimeRemaining = 0,
                    FileTransferSpeed = 0,
                    FileDataTransferred = 0,
                })
                .ToListAsync(cancellationToken)
        );
        rows.AddRange(
            await dbContext
                .DownloadTaskMusicTracks.Where(x => !isFilteredByServer || x.PlexServerId == plexServerId)
                .Select(x => new DownloadProgressRow
                {
                    Id = x.Id,
                    ParentId = x.ParentId,
                    PlexApiRatingKey = x.PlexApiRatingKey,
                    Title = x.Title,
                    FullTitle = x.FullTitle,
                    MediaType = PlexMediaType.MusicTrack,
                    DownloadTaskType = DownloadTaskType.MusicTrack,
                    DownloadStatus = x.DownloadStatus,
                    CreatedAt = x.CreatedAt,
                    PlexServerId = x.PlexServerId,
                    PlexLibraryId = x.PlexLibraryId,
                    DataReceived = 0,
                    DataTotal = 0,
                    Percentage = 0,
                    DownloadSpeed = 0,
                    TimeRemaining = 0,
                    FileTransferSpeed = 0,
                    FileDataTransferred = 0,
                })
                .ToListAsync(cancellationToken)
        );
        rows.AddRange(
            await dbContext
                .DownloadTaskMusicTrackFiles.Where(x => !isFilteredByServer || x.PlexServerId == plexServerId)
                .Select(x => new DownloadProgressRow
                {
                    Id = x.Id,
                    ParentId = x.ParentId,
                    PlexApiRatingKey = x.PlexApiRatingKey,
                    Title = x.Title,
                    FullTitle = x.FullTitle,
                    MediaType = PlexMediaType.MusicTrack,
                    DownloadTaskType = DownloadTaskType.MusicTrackData,
                    DownloadStatus = x.DownloadStatus,
                    CreatedAt = x.CreatedAt,
                    PlexServerId = x.PlexServerId,
                    PlexLibraryId = x.PlexLibraryId,
                    DataReceived = x.DataReceived,
                    DataTotal = x.DataTotal,
                    Percentage = x.Percentage,
                    DownloadSpeed = x.DownloadSpeed,
                    TimeRemaining = x.TimeRemaining,
                    FileTransferSpeed = x.FileTransferSpeed,
                    FileDataTransferred = x.FileDataTransferred,
                })
                .ToListAsync(cancellationToken)
        );
        rows.AddRange(
            await dbContext
                .DownloadTaskOtherVideos.Where(x => !isFilteredByServer || x.PlexServerId == plexServerId)
                .Select(x => new DownloadProgressRow
                {
                    Id = x.Id,
                    ParentId = null,
                    PlexApiRatingKey = x.PlexApiRatingKey,
                    Title = x.Title,
                    FullTitle = x.FullTitle,
                    MediaType = PlexMediaType.OtherVideos,
                    DownloadTaskType = DownloadTaskType.OtherVideo,
                    DownloadStatus = x.DownloadStatus,
                    CreatedAt = x.CreatedAt,
                    PlexServerId = x.PlexServerId,
                    PlexLibraryId = x.PlexLibraryId,
                    DataReceived = 0,
                    DataTotal = 0,
                    Percentage = 0,
                    DownloadSpeed = 0,
                    TimeRemaining = 0,
                    FileTransferSpeed = 0,
                    FileDataTransferred = 0,
                })
                .ToListAsync(cancellationToken)
        );
        rows.AddRange(
            await dbContext
                .DownloadTaskOtherVideoFiles.Where(x => !isFilteredByServer || x.PlexServerId == plexServerId)
                .Select(x => new DownloadProgressRow
                {
                    Id = x.Id,
                    ParentId = x.ParentId,
                    PlexApiRatingKey = x.PlexApiRatingKey,
                    Title = x.Title,
                    FullTitle = x.FullTitle,
                    MediaType = PlexMediaType.OtherVideos,
                    DownloadTaskType = DownloadTaskType.OtherVideoData,
                    DownloadStatus = x.DownloadStatus,
                    CreatedAt = x.CreatedAt,
                    PlexServerId = x.PlexServerId,
                    PlexLibraryId = x.PlexLibraryId,
                    DataReceived = x.DataReceived,
                    DataTotal = x.DataTotal,
                    Percentage = x.Percentage,
                    DownloadSpeed = x.DownloadSpeed,
                    TimeRemaining = x.TimeRemaining,
                    FileTransferSpeed = x.FileTransferSpeed,
                    FileDataTransferred = x.FileDataTransferred,
                })
                .ToListAsync(cancellationToken)
        );

        if (rows.Count == 0)
            return [];

        var byId = rows.ToDictionary(x => x.Id, CreateNode);
        foreach (var row in rows.Where(x => x.ParentId.HasValue))
        {
            if (!byId.TryGetValue(row.ParentId!.Value, out var parent))
                continue;

            parent.Children.Add(byId[row.Id]);
        }

        var roots = rows.Where(x => !x.ParentId.HasValue).Select(x => byId[x.Id]).OrderBy(x => x.CreatedAt).ToList();

        foreach (var root in roots)
            root.Calculate();

        return roots;

        static DownloadTaskGeneric CreateNode(DownloadProgressRow row) =>
            new()
            {
                Id = row.Id,
                RatingKey = row.PlexApiRatingKey,
                Title = row.Title,
                FullTitle = row.FullTitle,
                MediaType = row.MediaType,
                DownloadTaskType = row.DownloadTaskType,
                DownloadStatus = row.DownloadStatus,
                CreatedAt = row.CreatedAt,
                FileName = string.Empty,
                IsDownloadable =
                    row.ParentId.HasValue
                    && (
                        row.DownloadTaskType == DownloadTaskType.MovieData
                        || row.DownloadTaskType == DownloadTaskType.EpisodeData
                        || row.DownloadTaskType == DownloadTaskType.PhotoData
                        || row.DownloadTaskType == DownloadTaskType.MusicTrackData
                        || row.DownloadTaskType == DownloadTaskType.OtherVideoData
                    ),
                DownloadDirectory = string.Empty,
                Quality = VideoQuality.None,
                DestinationDirectory = string.Empty,
                FileLocationUrl = string.Empty,
                DataReceived = row.DataReceived,
                DataTotal = row.DataTotal,
                Percentage = row.Percentage,
                DownloadSpeed = row.DownloadSpeed,
                TimeRemaining = row.TimeRemaining,
                FileTransferSpeed = row.FileTransferSpeed,
                FileDataTransferred = row.FileDataTransferred,
                CurrentFileTransferBytesOffset = 0,
                Children = [],
                ParentId = row.ParentId ?? Guid.Empty,
                PlexServer = null,
                PlexServerId = row.PlexServerId,
                PlexLibrary = null,
                PlexLibraryId = row.PlexLibraryId,
            };
    }

    public static async Task UpdateDownloadProgress(
        this IReaparrDbContext dbContext,
        DownloadTaskKey key,
        IDownloadTaskProgress progress,
        DirectDownloadSnapshot? snapshot = null,
        CancellationToken cancellationToken = default
    )
    {
        switch (key.Type)
        {
            case DownloadTaskType.MovieData:
            case DownloadTaskType.MoviePart:
                await dbContext
                    .DownloadTaskMovieFile.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p =>
                            p.SetProperty(x => x.DownloadSpeed, progress.DownloadSpeed)
                                .SetProperty(x => x.DataReceived, progress.DataReceived)
                                .SetProperty(x => x.DataTotal, progress.DataTotal)
                                .SetProperty(x => x.Percentage, progress.Percentage)
                                .SetProperty(x => x.TimeRemaining, progress.TimeRemaining)
                                .SetProperty(x => x.DirectDownloadSnapshot, snapshot),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.EpisodeData:
            case DownloadTaskType.EpisodePart:
                await dbContext
                    .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p =>
                            p.SetProperty(x => x.DownloadSpeed, progress.DownloadSpeed)
                                .SetProperty(x => x.DataReceived, progress.DataReceived)
                                .SetProperty(x => x.DataTotal, progress.DataTotal)
                                .SetProperty(x => x.Percentage, progress.Percentage)
                                .SetProperty(x => x.TimeRemaining, progress.TimeRemaining)
                                .SetProperty(x => x.DirectDownloadSnapshot, snapshot),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.PhotoData:
            case DownloadTaskType.PhotoPart:
                await dbContext
                    .DownloadTaskPhotoImageFiles.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p =>
                            p.SetProperty(x => x.DownloadSpeed, progress.DownloadSpeed)
                                .SetProperty(x => x.DataReceived, progress.DataReceived)
                                .SetProperty(x => x.DataTotal, progress.DataTotal)
                                .SetProperty(x => x.Percentage, progress.Percentage)
                                .SetProperty(x => x.TimeRemaining, progress.TimeRemaining)
                                .SetProperty(x => x.DirectDownloadSnapshot, snapshot),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.MusicTrackData:
            case DownloadTaskType.MusicTrackPart:
                await dbContext
                    .DownloadTaskMusicTrackFiles.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p =>
                            p.SetProperty(x => x.DownloadSpeed, progress.DownloadSpeed)
                                .SetProperty(x => x.DataReceived, progress.DataReceived)
                                .SetProperty(x => x.DataTotal, progress.DataTotal)
                                .SetProperty(x => x.Percentage, progress.Percentage)
                                .SetProperty(x => x.TimeRemaining, progress.TimeRemaining)
                                .SetProperty(x => x.DirectDownloadSnapshot, snapshot),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.OtherVideoData:
            case DownloadTaskType.OtherVideoPart:
                await dbContext
                    .DownloadTaskOtherVideoFiles.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p =>
                            p.SetProperty(x => x.DownloadSpeed, progress.DownloadSpeed)
                                .SetProperty(x => x.DataReceived, progress.DataReceived)
                                .SetProperty(x => x.DataTotal, progress.DataTotal)
                                .SetProperty(x => x.Percentage, progress.Percentage)
                                .SetProperty(x => x.TimeRemaining, progress.TimeRemaining)
                                .SetProperty(x => x.DirectDownloadSnapshot, snapshot),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.Movie:
            case DownloadTaskType.TvShow:
            case DownloadTaskType.Season:
            case DownloadTaskType.Episode:
            case DownloadTaskType.PhotoAlbum:
            case DownloadTaskType.PhotoImage:
                _log.Here()
                    .Error(
                        "{Name} of type {Type} is not supported in {MethodName}",
                        nameof(DownloadTaskType),
                        key.Type,
                        nameof(UpdateDownloadProgress)
                    );
                return;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public static async Task<Result> ResetDownloadTaskProgress(
        this IReaparrDbContext dbContext,
        DownloadTaskKey key,
        DownloadStatus downloadStatus,
        CancellationToken cancellationToken = default
    )
    {
        switch (key.Type)
        {
            case DownloadTaskType.MovieData:
            case DownloadTaskType.MoviePart:
                await dbContext
                    .DownloadTaskMovieFile.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p =>
                            p.SetProperty(x => x.DownloadSpeed, 0)
                                .SetProperty(x => x.DataReceived, 0)
                                .SetProperty(x => x.Percentage, 0)
                                .SetProperty(x => x.TimeRemaining, 0)
                                .SetProperty(x => x.FileTransferSpeed, 0)
                                .SetProperty(x => x.FileDataTransferred, 0)
                                .SetProperty(x => x.CurrentFileTransferBytesOffset, 0)
                                .SetProperty(x => x.DirectDownloadSnapshot, (DirectDownloadSnapshot?)null)
                                .SetProperty(x => x.DownloadStatus, downloadStatus),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.EpisodeData:
            case DownloadTaskType.EpisodePart:
                await dbContext
                    .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p =>
                            p.SetProperty(x => x.DownloadSpeed, 0)
                                .SetProperty(x => x.DataReceived, 0)
                                .SetProperty(x => x.Percentage, 0)
                                .SetProperty(x => x.TimeRemaining, 0)
                                .SetProperty(x => x.FileTransferSpeed, 0)
                                .SetProperty(x => x.FileDataTransferred, 0)
                                .SetProperty(x => x.CurrentFileTransferBytesOffset, 0)
                                .SetProperty(x => x.DirectDownloadSnapshot, (DirectDownloadSnapshot?)null)
                                .SetProperty(x => x.DownloadStatus, downloadStatus),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.PhotoData:
            case DownloadTaskType.PhotoPart:
                await dbContext
                    .DownloadTaskPhotoImageFiles.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p =>
                            p.SetProperty(x => x.DownloadSpeed, 0)
                                .SetProperty(x => x.DataReceived, 0)
                                .SetProperty(x => x.Percentage, 0)
                                .SetProperty(x => x.TimeRemaining, 0)
                                .SetProperty(x => x.FileTransferSpeed, 0)
                                .SetProperty(x => x.FileDataTransferred, 0)
                                .SetProperty(x => x.CurrentFileTransferBytesOffset, 0)
                                .SetProperty(x => x.DirectDownloadSnapshot, (DirectDownloadSnapshot?)null)
                                .SetProperty(x => x.DownloadStatus, downloadStatus),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.MusicTrackData:
            case DownloadTaskType.MusicTrackPart:
                await dbContext
                    .DownloadTaskMusicTrackFiles.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p =>
                            p.SetProperty(x => x.DownloadSpeed, 0)
                                .SetProperty(x => x.DataReceived, 0)
                                .SetProperty(x => x.Percentage, 0)
                                .SetProperty(x => x.TimeRemaining, 0)
                                .SetProperty(x => x.FileTransferSpeed, 0)
                                .SetProperty(x => x.FileDataTransferred, 0)
                                .SetProperty(x => x.CurrentFileTransferBytesOffset, 0)
                                .SetProperty(x => x.DirectDownloadSnapshot, (DirectDownloadSnapshot?)null)
                                .SetProperty(x => x.DownloadStatus, downloadStatus),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.OtherVideoData:
            case DownloadTaskType.OtherVideoPart:
                await dbContext
                    .DownloadTaskOtherVideoFiles.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p =>
                            p.SetProperty(x => x.DownloadSpeed, 0)
                                .SetProperty(x => x.DataReceived, 0)
                                .SetProperty(x => x.Percentage, 0)
                                .SetProperty(x => x.TimeRemaining, 0)
                                .SetProperty(x => x.FileTransferSpeed, 0)
                                .SetProperty(x => x.FileDataTransferred, 0)
                                .SetProperty(x => x.CurrentFileTransferBytesOffset, 0)
                                .SetProperty(x => x.DirectDownloadSnapshot, (DirectDownloadSnapshot?)null)
                                .SetProperty(x => x.DownloadStatus, downloadStatus),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.Movie:
            case DownloadTaskType.TvShow:
            case DownloadTaskType.Season:
            case DownloadTaskType.Episode:
            case DownloadTaskType.PhotoAlbum:
            case DownloadTaskType.PhotoImage:
                return _log.Here()
                    .ErrorResult(
                        "{Name} of type {Type} is not supported in {MethodName}",
                        nameof(DownloadTaskType),
                        key.Type,
                        nameof(ResetDownloadTaskProgress)
                    );
            case DownloadTaskType.None:
            default:
                return Result.Fail("Unsupported DownloadTaskType {DownloadTaskType}", key.Type).LogError();
        }

        return Result.Ok();
    }

    public static async Task ClearDownloadSpeed(
        this IReaparrDbContext dbContext,
        DownloadTaskKey key,
        CancellationToken cancellationToken = default
    )
    {
        switch (key.Type)
        {
            case DownloadTaskType.MovieData:
            case DownloadTaskType.MoviePart:
                await dbContext
                    .DownloadTaskMovieFile.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p => p.SetProperty(x => x.DownloadSpeed, 0).SetProperty(x => x.TimeRemaining, 0),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.EpisodeData:
            case DownloadTaskType.EpisodePart:
                await dbContext
                    .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p => p.SetProperty(x => x.DownloadSpeed, 0).SetProperty(x => x.TimeRemaining, 0),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.PhotoData:
            case DownloadTaskType.PhotoPart:
                await dbContext
                    .DownloadTaskPhotoImageFiles.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p => p.SetProperty(x => x.DownloadSpeed, 0).SetProperty(x => x.TimeRemaining, 0),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.MusicTrackData:
            case DownloadTaskType.MusicTrackPart:
                await dbContext
                    .DownloadTaskMusicTrackFiles.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p => p.SetProperty(x => x.DownloadSpeed, 0).SetProperty(x => x.TimeRemaining, 0),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.OtherVideoData:
            case DownloadTaskType.OtherVideoPart:
                await dbContext
                    .DownloadTaskOtherVideoFiles.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p => p.SetProperty(x => x.DownloadSpeed, 0).SetProperty(x => x.TimeRemaining, 0),
                        cancellationToken
                    );
                break;
        }
    }

    public static async Task UpdateDownloadFileTransferProgress(
        this IReaparrDbContext dbContext,
        DownloadTaskKey key,
        IDownloadFileTransferProgress progress,
        CancellationToken cancellationToken = default
    )
    {
        switch (key.Type)
        {
            case DownloadTaskType.MovieData:
                await dbContext
                    .DownloadTaskMovieFile.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p =>
                            p.SetProperty(x => x.FileTransferSpeed, progress.FileTransferSpeed)
                                .SetProperty(x => x.FileDataTransferred, progress.FileDataTransferred)
                                .SetProperty(
                                    x => x.CurrentFileTransferBytesOffset,
                                    progress.CurrentFileTransferBytesOffset
                                )
                                .SetProperty(x => x.TimeRemaining, progress.TimeRemaining)
                                .SetProperty(
                                    x => x.Percentage,
                                    x =>
                                        x.DataTotal > 0
                                            ? progress.CurrentFileTransferBytesOffset * 100m / x.DataTotal
                                            : 0m
                                ),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.EpisodeData:
                await dbContext
                    .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p =>
                            p.SetProperty(x => x.FileTransferSpeed, progress.FileTransferSpeed)
                                .SetProperty(x => x.FileDataTransferred, progress.FileDataTransferred)
                                .SetProperty(
                                    x => x.CurrentFileTransferBytesOffset,
                                    progress.CurrentFileTransferBytesOffset
                                )
                                .SetProperty(x => x.TimeRemaining, progress.TimeRemaining)
                                .SetProperty(
                                    x => x.Percentage,
                                    x =>
                                        x.DataTotal > 0
                                            ? progress.CurrentFileTransferBytesOffset * 100m / x.DataTotal
                                            : 0m
                                ),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.PhotoData:
            case DownloadTaskType.PhotoPart:
                await dbContext
                    .DownloadTaskPhotoImageFiles.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p =>
                            p.SetProperty(x => x.FileTransferSpeed, progress.FileTransferSpeed)
                                .SetProperty(x => x.FileDataTransferred, progress.FileDataTransferred)
                                .SetProperty(
                                    x => x.CurrentFileTransferBytesOffset,
                                    progress.CurrentFileTransferBytesOffset
                                )
                                .SetProperty(x => x.TimeRemaining, progress.TimeRemaining)
                                .SetProperty(
                                    x => x.Percentage,
                                    x =>
                                        x.DataTotal > 0
                                            ? progress.CurrentFileTransferBytesOffset * 100m / x.DataTotal
                                            : 0m
                                ),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.MusicTrackData:
            case DownloadTaskType.MusicTrackPart:
                await dbContext
                    .DownloadTaskMusicTrackFiles.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p =>
                            p.SetProperty(x => x.FileTransferSpeed, progress.FileTransferSpeed)
                                .SetProperty(x => x.FileDataTransferred, progress.FileDataTransferred)
                                .SetProperty(
                                    x => x.CurrentFileTransferBytesOffset,
                                    progress.CurrentFileTransferBytesOffset
                                )
                                .SetProperty(x => x.TimeRemaining, progress.TimeRemaining)
                                .SetProperty(
                                    x => x.Percentage,
                                    x =>
                                        x.DataTotal > 0
                                            ? progress.CurrentFileTransferBytesOffset * 100m / x.DataTotal
                                            : 0m
                                ),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.OtherVideoData:
            case DownloadTaskType.OtherVideoPart:
                await dbContext
                    .DownloadTaskOtherVideoFiles.Where(x => x.Id == key.Id)
                    .ExecuteUpdateAsync(
                        p =>
                            p.SetProperty(x => x.FileTransferSpeed, progress.FileTransferSpeed)
                                .SetProperty(x => x.FileDataTransferred, progress.FileDataTransferred)
                                .SetProperty(
                                    x => x.CurrentFileTransferBytesOffset,
                                    progress.CurrentFileTransferBytesOffset
                                )
                                .SetProperty(x => x.TimeRemaining, progress.TimeRemaining)
                                .SetProperty(
                                    x => x.Percentage,
                                    x =>
                                        x.DataTotal > 0
                                            ? progress.CurrentFileTransferBytesOffset * 100m / x.DataTotal
                                            : 0m
                                ),
                        cancellationToken
                    );
                break;
            case DownloadTaskType.Movie:
            case DownloadTaskType.TvShow:
            case DownloadTaskType.Season:
            case DownloadTaskType.Episode:
            case DownloadTaskType.PhotoAlbum:
            case DownloadTaskType.PhotoImage:
                _log.Here()
                    .Error(
                        "{Name} of type {Type} is not supported in {MethodName}",
                        nameof(DownloadTaskType),
                        key.Type,
                        nameof(UpdateDownloadFileTransferProgress)
                    );
                return;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public static async Task<List<DownloadTaskKey>> GetDownloadableChildTaskKeys(
        this IReaparrDbContext dbContext,
        DownloadTaskKey key,
        CancellationToken cancellationToken = default
    )
    {
        var keys = new List<DownloadTaskKey>();
        var downloadTask = await dbContext.GetDownloadTaskAsync(key, cancellationToken);
        if (downloadTask is null)
            return keys;

        FindDownloadableTaskKeys([downloadTask]);

        return keys;

        void FindDownloadableTaskKeys(ICollection<DownloadTaskGeneric> tasks)
        {
            if (!tasks.Any())
                return;

            foreach (var task in tasks.OrderByNatural(x => x.FullTitle))
            {
                if (task.IsDownloadable)
                    keys.Add(task.ToKey());

                if (task.Children.Any())
                    FindDownloadableTaskKeys(task.Children);
            }
        }
    }

    public static async Task<List<DownloadTaskGeneric>> GetDownloadableChildTasks(
        this IReaparrDbContext dbContext,
        DownloadTaskKey key,
        CancellationToken cancellationToken = default
    )
    {
        var keys = await dbContext.GetDownloadableChildTaskKeys(key, cancellationToken);

        var results = await Task.WhenAll(keys.Select(x => dbContext.GetDownloadTaskAsync(x, cancellationToken)));

        return results.Where(x => x != null).ToList()!;
    }

    public static async Task<int> DeleteOrphanedParentTasksAsync(this IReaparrDbContext dbContext, CancellationToken ct)
    {
        var totalRowsDeleted = 0;

        totalRowsDeleted += await dbContext
            .DownloadTaskMovie.Where(x => !dbContext.DownloadTaskMovieFile.Any(y => y.ParentId == x.Id))
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await dbContext
            .DownloadTaskTvShowEpisode.Where(x => !dbContext.DownloadTaskTvShowEpisodeFile.Any(y => y.ParentId == x.Id))
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await dbContext
            .DownloadTaskTvShowSeason.Where(x => !dbContext.DownloadTaskTvShowEpisode.Any(y => y.ParentId == x.Id))
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await dbContext
            .DownloadTaskTvShow.Where(x => !dbContext.DownloadTaskTvShowSeason.Any(y => y.ParentId == x.Id))
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await dbContext
            .DownloadTaskPhotoImages.Where(x => !dbContext.DownloadTaskPhotoImageFiles.Any(y => y.ParentId == x.Id))
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await dbContext
            .DownloadTaskPhotoAlbums.Where(x => !dbContext.DownloadTaskPhotoImages.Any(y => y.ParentId == x.Id))
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await dbContext
            .DownloadTaskMusicTracks.Where(x => !dbContext.DownloadTaskMusicTrackFiles.Any(y => y.ParentId == x.Id))
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await dbContext
            .DownloadTaskMusicAlbums.Where(x => !dbContext.DownloadTaskMusicTracks.Any(y => y.ParentId == x.Id))
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await dbContext
            .DownloadTaskMusicArtists.Where(x => !dbContext.DownloadTaskMusicAlbums.Any(y => y.ParentId == x.Id))
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await dbContext
            .DownloadTaskOtherVideos.Where(x => !dbContext.DownloadTaskOtherVideoFiles.Any(y => y.ParentId == x.Id))
            .ExecuteDeleteAsync(ct);

        return totalRowsDeleted;
    }

    public static async Task<int> DeleteOrphanedParentTasksByServerIdAsync(
        this IReaparrDbContext dbContext,
        int plexServerId,
        CancellationToken ct
    )
    {
        var totalRowsDeleted = 0;

        totalRowsDeleted += await dbContext
            .DownloadTaskMovie.Where(x =>
                x.PlexServerId == plexServerId && !dbContext.DownloadTaskMovieFile.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await dbContext
            .DownloadTaskTvShowEpisode.Where(x =>
                x.PlexServerId == plexServerId && !dbContext.DownloadTaskTvShowEpisodeFile.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await dbContext
            .DownloadTaskTvShowSeason.Where(x =>
                x.PlexServerId == plexServerId && !dbContext.DownloadTaskTvShowEpisode.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await dbContext
            .DownloadTaskTvShow.Where(x =>
                x.PlexServerId == plexServerId && !dbContext.DownloadTaskTvShowSeason.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await dbContext
            .DownloadTaskPhotoImages.Where(x =>
                x.PlexServerId == plexServerId && !dbContext.DownloadTaskPhotoImageFiles.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await dbContext
            .DownloadTaskPhotoAlbums.Where(x =>
                x.PlexServerId == plexServerId && !dbContext.DownloadTaskPhotoImages.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await dbContext
            .DownloadTaskMusicTracks.Where(x =>
                x.PlexServerId == plexServerId && !dbContext.DownloadTaskMusicTrackFiles.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await dbContext
            .DownloadTaskMusicAlbums.Where(x =>
                x.PlexServerId == plexServerId && !dbContext.DownloadTaskMusicTracks.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await dbContext
            .DownloadTaskMusicArtists.Where(x =>
                x.PlexServerId == plexServerId && !dbContext.DownloadTaskMusicAlbums.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await dbContext
            .DownloadTaskOtherVideos.Where(x =>
                x.PlexServerId == plexServerId && !dbContext.DownloadTaskOtherVideoFiles.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);

        return totalRowsDeleted;
    }

    public static async Task<int> DeleteOrphanedParentTasksByRootIdsAsync(
        this IReaparrDbContext dbContext,
        IReadOnlyCollection<Guid> rootIds,
        CancellationToken ct
    )
    {
        if (rootIds.Count == 0)
            return 0;

        var totalRowsDeleted = 0;

        totalRowsDeleted += await dbContext
            .DownloadTaskMovie.Where(x =>
                rootIds.Contains(x.Id) && !dbContext.DownloadTaskMovieFile.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await dbContext
            .DownloadTaskTvShowEpisode.Where(x =>
                rootIds.Contains(x.Parent!.ParentId)
                && !dbContext.DownloadTaskTvShowEpisodeFile.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await dbContext
            .DownloadTaskTvShowSeason.Where(x =>
                rootIds.Contains(x.ParentId) && !dbContext.DownloadTaskTvShowEpisode.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await dbContext
            .DownloadTaskTvShow.Where(x =>
                rootIds.Contains(x.Id) && !dbContext.DownloadTaskTvShowSeason.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await dbContext
            .DownloadTaskPhotoImages.Where(x =>
                rootIds.Contains(x.ParentId) && !dbContext.DownloadTaskPhotoImageFiles.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);

        totalRowsDeleted += await dbContext
            .DownloadTaskPhotoAlbums.Where(x =>
                rootIds.Contains(x.Id) && !dbContext.DownloadTaskPhotoImages.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await dbContext
            .DownloadTaskMusicTracks.Where(x =>
                rootIds.Contains(x.Parent!.ParentId)
                && !dbContext.DownloadTaskMusicTrackFiles.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await dbContext
            .DownloadTaskMusicAlbums.Where(x =>
                rootIds.Contains(x.ParentId) && !dbContext.DownloadTaskMusicTracks.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await dbContext
            .DownloadTaskMusicArtists.Where(x =>
                rootIds.Contains(x.Id) && !dbContext.DownloadTaskMusicAlbums.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);
        totalRowsDeleted += await dbContext
            .DownloadTaskOtherVideos.Where(x =>
                rootIds.Contains(x.Id) && !dbContext.DownloadTaskOtherVideoFiles.Any(y => y.ParentId == x.Id)
            )
            .ExecuteDeleteAsync(ct);

        return totalRowsDeleted;
    }

    public static async Task<HashSet<Guid>> GetAffectedRootDownloadTaskIdsAsync(
        this IReaparrDbContext dbContext,
        IReadOnlyCollection<Guid> downloadTaskIds,
        CancellationToken ct
    )
    {
        // TODO ugly inconsistent method, needs to be cleaned up and refactored to be more consistent with the rest of the codebase
        if (downloadTaskIds.Count == 0)
            return [];

        var movieRootIdsTask = dbContext
            .DownloadTaskMovie.Where(x => downloadTaskIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(ct);

        var movieFileRootIdsTask = dbContext
            .DownloadTaskMovieFile.Where(x => downloadTaskIds.Contains(x.Id))
            .Select(x => x.ParentId)
            .ToListAsync(ct);

        var tvShowRootIdsTask = dbContext
            .DownloadTaskTvShow.Where(x => downloadTaskIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(ct);

        var seasonRootIdsTask = dbContext
            .DownloadTaskTvShowSeason.Where(x => downloadTaskIds.Contains(x.Id))
            .Select(x => x.ParentId)
            .ToListAsync(ct);

        var episodeSeasonIdsTask = dbContext
            .DownloadTaskTvShowEpisode.Where(x => downloadTaskIds.Contains(x.Id))
            .Select(x => x.ParentId)
            .ToListAsync(ct);

        var episodeFileEpisodeIdsTask = dbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => downloadTaskIds.Contains(x.Id))
            .Select(x => x.ParentId)
            .ToListAsync(ct);

        await Task.WhenAll(
            movieRootIdsTask,
            movieFileRootIdsTask,
            tvShowRootIdsTask,
            seasonRootIdsTask,
            episodeSeasonIdsTask,
            episodeFileEpisodeIdsTask
        );

        var rootIds = new HashSet<Guid>(
            movieRootIdsTask
                .Result.Concat(movieFileRootIdsTask.Result)
                .Concat(tvShowRootIdsTask.Result)
                .Concat(seasonRootIdsTask.Result)
        );

        var seasonIds = episodeSeasonIdsTask.Result;
        if (episodeFileEpisodeIdsTask.Result.Count > 0)
        {
            var episodeDerivedSeasonIds = await dbContext
                .DownloadTaskTvShowEpisode.Where(x => episodeFileEpisodeIdsTask.Result.Contains(x.Id))
                .Select(x => x.ParentId)
                .ToListAsync(ct);
            seasonIds = seasonIds.Concat(episodeDerivedSeasonIds).Distinct().ToList();
        }

        if (seasonIds.Count > 0)
        {
            var seasonRootIds = await dbContext
                .DownloadTaskTvShowSeason.Where(x => seasonIds.Contains(x.Id))
                .Select(x => x.ParentId)
                .ToListAsync(ct);
            rootIds.UnionWith(seasonRootIds);
        }

        var photoRootIds = await dbContext
            .DownloadTaskPhotoAlbums.Where(x =>
                downloadTaskIds.Contains(x.Id)
                || x.Children.Any(image =>
                    downloadTaskIds.Contains(image.Id) || image.Children.Any(file => downloadTaskIds.Contains(file.Id))
                )
            )
            .Select(x => x.Id)
            .ToListAsync(ct);
        rootIds.UnionWith(photoRootIds);
        rootIds.UnionWith(
            await dbContext
                .DownloadTaskMusicArtists.Where(x =>
                    downloadTaskIds.Contains(x.Id)
                    || x.Children.Any(album =>
                        downloadTaskIds.Contains(album.Id)
                        || album.Children.Any(track =>
                            downloadTaskIds.Contains(track.Id)
                            || track.Children.Any(file => downloadTaskIds.Contains(file.Id))
                        )
                    )
                )
                .Select(x => x.Id)
                .ToListAsync(ct)
        );
        rootIds.UnionWith(
            await dbContext
                .DownloadTaskOtherVideos.Where(x =>
                    downloadTaskIds.Contains(x.Id) || x.Children.Any(file => downloadTaskIds.Contains(file.Id))
                )
                .Select(x => x.Id)
                .ToListAsync(ct)
        );

        return rootIds;
    }

    public static async Task<DownloadTaskKey?> GetRootDownloadTaskKeyAsync(
        this IReaparrDbContext dbContext,
        DownloadTaskKey key,
        IntegrationIdentity? integration = null,
        CancellationToken cancellationToken = default
    )
    {
        switch (key.Type)
        {
            case DownloadTaskType.Movie:
            case DownloadTaskType.TvShow:
                return key;
            case DownloadTaskType.MovieData:
            case DownloadTaskType.MoviePart:
                return await dbContext
                    .DownloadTaskMovieFile.WhereIntegrationIs(integration)
                    .Where(x => x.Id == key.Id)
                    .Select(x => new DownloadTaskKey
                    {
                        Id = x.ParentId,
                        PlexServerId = x.PlexServerId,
                        PlexLibraryId = x.PlexLibraryId,
                        Type = DownloadTaskType.Movie,
                    })
                    .FirstOrDefaultAsync(cancellationToken);
            case DownloadTaskType.Season:
                return await dbContext
                    .DownloadTaskTvShowSeason.WhereIntegrationIs(integration)
                    .Where(x => x.Id == key.Id)
                    .Select(x => new DownloadTaskKey
                    {
                        Id = x.ParentId,
                        PlexServerId = x.PlexServerId,
                        PlexLibraryId = x.PlexLibraryId,
                        Type = DownloadTaskType.TvShow,
                    })
                    .FirstOrDefaultAsync(cancellationToken);
            case DownloadTaskType.Episode:
            {
                var season = await dbContext
                    .DownloadTaskTvShowEpisode.WhereIntegrationIs(integration)
                    .Where(x => x.Id == key.Id)
                    .Select(x => new
                    {
                        x.ParentId,
                        x.PlexServerId,
                        x.PlexLibraryId,
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (season is null)
                    return null;

                return await dbContext
                    .DownloadTaskTvShowSeason.WhereIntegrationIs(integration)
                    .Where(x => x.Id == season.ParentId)
                    .Select(x => new DownloadTaskKey
                    {
                        Id = x.ParentId,
                        PlexServerId = season.PlexServerId,
                        PlexLibraryId = season.PlexLibraryId,
                        Type = DownloadTaskType.TvShow,
                    })
                    .FirstOrDefaultAsync(cancellationToken);
            }
            case DownloadTaskType.EpisodeData:
            case DownloadTaskType.EpisodePart:
            {
                var episode = await dbContext
                    .DownloadTaskTvShowEpisodeFile.WhereIntegrationIs(integration)
                    .Where(x => x.Id == key.Id)
                    .Select(x => new
                    {
                        x.ParentId,
                        x.PlexServerId,
                        x.PlexLibraryId,
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (episode is null)
                    return null;

                var season = await dbContext
                    .DownloadTaskTvShowEpisode.Where(x => x.Id == episode.ParentId)
                    .Select(x => x.ParentId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (season == Guid.Empty)
                    return null;

                return await dbContext
                    .DownloadTaskTvShowSeason.WhereIntegrationIs(integration)
                    .Where(x => x.Id == season)
                    .Select(x => new DownloadTaskKey
                    {
                        Id = x.ParentId,
                        PlexServerId = episode.PlexServerId,
                        PlexLibraryId = episode.PlexLibraryId,
                        Type = DownloadTaskType.TvShow,
                    })
                    .FirstOrDefaultAsync(cancellationToken);
            }
            case DownloadTaskType.PhotoAlbum when integration is null:
                return key;
            case DownloadTaskType.PhotoImage when integration is null:
                return await dbContext
                    .DownloadTaskPhotoImages.Where(x => x.Id == key.Id)
                    .ProjectToParentKey()
                    .FirstOrDefaultAsync(cancellationToken);
            case DownloadTaskType.PhotoData when integration is null:
            case DownloadTaskType.PhotoPart when integration is null:
                return await dbContext
                    .DownloadTaskPhotoImageFiles.Where(x => x.Id == key.Id)
                    .Select(x => new DownloadTaskKey
                    {
                        Id = x.Parent!.ParentId,
                        PlexServerId = x.PlexServerId,
                        PlexLibraryId = x.PlexLibraryId,
                        Type = DownloadTaskType.PhotoAlbum,
                    })
                    .FirstOrDefaultAsync(cancellationToken);
            case DownloadTaskType.MusicArtist when integration is null:
            case DownloadTaskType.OtherVideo when integration is null:
                return key;
            case DownloadTaskType.MusicAlbum when integration is null:
                return await dbContext
                    .DownloadTaskMusicAlbums.Where(x => x.Id == key.Id)
                    .ProjectToParentKey()
                    .FirstOrDefaultAsync(cancellationToken);
            case DownloadTaskType.MusicTrack when integration is null:
                return await dbContext
                    .DownloadTaskMusicTracks.Where(x => x.Id == key.Id)
                    .Select(x => new DownloadTaskKey
                    {
                        Id = x.Parent!.ParentId,
                        PlexServerId = x.PlexServerId,
                        PlexLibraryId = x.PlexLibraryId,
                        Type = DownloadTaskType.MusicArtist,
                    })
                    .FirstOrDefaultAsync(cancellationToken);
            case DownloadTaskType.MusicTrackData when integration is null:
            case DownloadTaskType.MusicTrackPart when integration is null:
                return await dbContext
                    .DownloadTaskMusicTrackFiles.Where(x => x.Id == key.Id)
                    .Select(x => new DownloadTaskKey
                    {
                        Id = x.Parent!.Parent!.ParentId,
                        PlexServerId = x.PlexServerId,
                        PlexLibraryId = x.PlexLibraryId,
                        Type = DownloadTaskType.MusicArtist,
                    })
                    .FirstOrDefaultAsync(cancellationToken);
            case DownloadTaskType.OtherVideoData when integration is null:
            case DownloadTaskType.OtherVideoPart when integration is null:
                return await dbContext
                    .DownloadTaskOtherVideoFiles.Where(x => x.Id == key.Id)
                    .ProjectToParentKey()
                    .FirstOrDefaultAsync(cancellationToken);
            default:
                return null;
        }
    }
}
