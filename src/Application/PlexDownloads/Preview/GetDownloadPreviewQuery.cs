namespace Reaparr.Application;

public record GetDownloadPreviewQuery(List<DownloadMediaDTO> DownloadMedias) : ICommand<Result<List<DownloadPreview>>>;

public class GetDownloadPreviewQueryValidator : AbstractValidator<GetDownloadPreviewQuery>
{
    public GetDownloadPreviewQueryValidator()
    {
        RuleFor(x => x.DownloadMedias).NotNull().NotEmpty().WithMessage("Download media list cannot be empty");

        RuleForEach(x => x.DownloadMedias).NotNull().SetValidator(new DownloadMediaDTOValidator());
    }
}

public class GetDownloadPreviewQueryHandler : ICommandHandler<GetDownloadPreviewQuery, Result<List<DownloadPreview>>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ILogger _log;

    public GetDownloadPreviewQueryHandler(IReaparrDbContext dbContext, ILogger log)
    {
        _dbContext = dbContext;
        _log = log.ForContext<GetDownloadPreviewQueryHandler>();
    }

    /// <summary>
    /// Handles the GetDownloadPreviewQuery by generating download previews for movies and TV shows.
    /// </summary>
    public async Task<Result<List<DownloadPreview>>> ExecuteAsync(
        GetDownloadPreviewQuery request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var downloadPreviews = new List<DownloadPreview>();

            if (!request.DownloadMedias.Any())
            {
                return Result.Ok(downloadPreviews);
            }

            // Process movies
            var movieResult = await CreateMoviePreviews(request.DownloadMedias, cancellationToken);
            if (movieResult.IsFailed)
                return movieResult.ToResult();

            downloadPreviews.AddRange(movieResult.Value);

            // Process TV shows (including seasons and episodes)
            var tvShowResult = await CreateTvShowPreviews(request.DownloadMedias, cancellationToken);
            if (tvShowResult.IsFailed)
                return tvShowResult.ToResult();

            downloadPreviews.AddRange(tvShowResult.Value);

            return Result.Ok(downloadPreviews);
        }
        catch (Exception ex)
        {
            _log.Here().Error("Failed to create download previews: {ExceptionMsg}", ex.Message);
            return Result.Fail(new ExceptionalError(ex)).LogError();
        }
    }

    /// <summary>
    /// Creates download previews for movies, handling both with and without quality selection.
    /// </summary>
    private async Task<Result<IEnumerable<DownloadPreview>>> CreateMoviePreviews(
        List<DownloadMediaDTO> downloadMedias,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var moviesDownloadMedia = downloadMedias.Merge(PlexMediaType.Movie);
            if (!moviesDownloadMedia.Any() || !moviesDownloadMedia.Any(x => x.MediaIds.Any()))
                return Result.Ok(Enumerable.Empty<DownloadPreview>());

            var previews = new List<DownloadPreview>();
            var movieQualities = moviesDownloadMedia.SelectMany(x => x.Qualities).ToList();
            var movieIdsWithQuality = movieQualities.Select(x => x.MediaId).ToHashSet();

            var baseQuery = _dbContext.PlexMovies.AsNoTracking();

            // Fetch movies with specific qualities
            if (movieQualities.Any())
            {
                var movieMediaDataIds = movieQualities.Select(x => x.DataId).ToHashSet();

                var resultWithQualities = await baseQuery
                    .Include(x => x.MediaDataList.Where(y => movieMediaDataIds.Contains(y.Id)))
                    .Where(x => movieIdsWithQuality.Contains(x.Id))
                    .ProjectToDownloadPreview()
                    .ToListAsync(cancellationToken);

                previews.AddRange(resultWithQualities);
            }

            // Fetch movies without specific qualities (use best available quality)
            var movieIds = moviesDownloadMedia.SelectMany(x => x.MediaIds).Except(movieIdsWithQuality).ToHashSet();
            if (movieIds.Any())
            {
                var result = await baseQuery
                    .Include(x => x.MediaDataList)
                    .Where(x => movieIds.Contains(x.Id))
                    .ProjectToDownloadPreview()
                    .ToListAsync(cancellationToken);

                previews.AddRange(result);
            }

            var sortedPreviews = previews.OrderByNatural(x => x.Title);
            return Result.Ok(sortedPreviews);
        }
        catch (Exception ex)
        {
            return Result.Fail(new ExceptionalError(ex)).LogError();
        }
    }

    /// <summary>
    /// Creates download previews for TV shows, including their seasons and episodes, and builds the hierarchy.
    /// </summary>
    private async Task<Result<IEnumerable<DownloadPreview>>> CreateTvShowPreviews(
        List<DownloadMediaDTO> downloadMedias,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var tvShowDownloadMedia = downloadMedias.Merge(PlexMediaType.TvShow);
            var seasonDownloadMedia = downloadMedias.Merge(PlexMediaType.Season);
            var episodeDownloadMedia = downloadMedias.Merge(PlexMediaType.Episode);

            // Get all episode keys for the requested TV shows, seasons, and episodes
            var allKeysResult = await GetEpisodeKeys(
                tvShowDownloadMedia,
                seasonDownloadMedia,
                episodeDownloadMedia,
                cancellationToken
            );

            if (allKeysResult.IsFailed)
                return allKeysResult.ToResult();

            var allKeys = allKeysResult.Value;
            if (!allKeys.Any())
                return Result.Ok(Enumerable.Empty<DownloadPreview>());

            var tvShowIds = allKeys.Select(x => x.TvShowId).Distinct().ToList();
            var seasonIds = allKeys.Select(x => x.SeasonId).Distinct().ToList();

            // Retrieve TV shows, seasons, and episodes in parallel
            var tvShowsTask = _dbContext
                .PlexTvShows.AsNoTracking()
                .Where(x => tvShowIds.Contains(x.Id))
                .ProjectToDownloadPreview()
                .ToListAsync(cancellationToken);

            var seasonsTask = _dbContext
                .PlexTvShowSeason.AsNoTracking()
                .Where(x => seasonIds.Contains(x.Id))
                .ProjectToDownloadPreview()
                .ToListAsync(cancellationToken);

            var episodesTask = CreateEpisodePreviews(episodeDownloadMedia, allKeys, cancellationToken);

            await Task.WhenAll(tvShowsTask, seasonsTask, episodesTask);

            var tvShows = await tvShowsTask;
            var seasons = await seasonsTask;
            var episodesResult = await episodesTask;

            if (episodesResult.IsFailed)
                return episodesResult.ToResult();

            var episodes = episodesResult.Value.ToList();

            // Build hierarchy: add episodes to seasons, and seasons to TV shows
            BuildHierarchy(tvShows, seasons, episodes);

            var sortedTvShows = tvShows.OrderByNatural(x => x.Title);
            return Result.Ok(sortedTvShows);
        }
        catch (Exception ex)
        {
            _log.Here().Error("Failed to create TV show previews: {ExceptionMsg}", ex.Message);
            return Result.Fail(new ExceptionalError(ex)).LogError();
        }
    }

    private async Task<Result<IEnumerable<DownloadPreview>>> CreateEpisodePreviews(
        List<DownloadMediaDTO> episodeDownloadMedia,
        List<TvShowEpisodeKey> episodeKeys,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // Add missing episode IDs that aren't already in episodeDownloadMedia
            var existingEpisodeIds = episodeDownloadMedia.SelectMany(x => x.MediaIds).ToHashSet();
            var missingEpisodeIds = episodeKeys.Where(x => !existingEpisodeIds.Contains(x.EpisodeId)).ToList();

            if (missingEpisodeIds.Any())
            {
                // Group by server and library to maintain a proper structure
                var groupedEpisodes = missingEpisodeIds.GroupBy(x => new { x.TvShowId, x.SeasonId }).FirstOrDefault();

                if (groupedEpisodes != null)
                {
                    // Get PlexServerId and PlexLibraryId from the first episode in the group
                    var firstEpisodeId = groupedEpisodes.First().EpisodeId;
                    var episodeServerInfo = await _dbContext
                        .PlexTvShowEpisodes.AsNoTracking()
                        .Where(x => x.Id == firstEpisodeId)
                        .Select(x => new { x.PlexServerId, x.PlexLibraryId })
                        .FirstOrDefaultAsync(cancellationToken);

                    episodeDownloadMedia.Add(
                        new DownloadMediaDTO
                        {
                            MediaIds = missingEpisodeIds.Select(x => x.EpisodeId).ToList(),
                            Qualities = missingEpisodeIds.SelectMany(x => x.MediaDataList).ToPlexMediaQuality(),
                            Type = PlexMediaType.Episode,
                            PlexServerId = episodeServerInfo?.PlexServerId ?? 0,
                            PlexLibraryId = episodeServerInfo?.PlexLibraryId ?? 0,
                        }
                    );
                }
            }

            if (!episodeDownloadMedia.Any() || !episodeDownloadMedia.Any(x => x.MediaIds.Any()))
                return Result.Ok(Enumerable.Empty<DownloadPreview>());

            var previews = new List<DownloadPreview>();
            var episodeQualities = episodeDownloadMedia.SelectMany(x => x.Qualities).ToList();
            var episodeIdsWithQuality = episodeQualities.Select(x => x.MediaId).ToHashSet();

            var baseQuery = _dbContext.PlexTvShowEpisodes.AsNoTracking();

            if (episodeQualities.Any())
            {
                var episodeMediaDataIds = episodeQualities.Select(x => x.DataId).ToHashSet();

                var resultWithQualities = await baseQuery
                    .Include(x => x.MediaDataList.Where(y => episodeMediaDataIds.Contains(y.Id)))
                    .Where(x => episodeIdsWithQuality.Contains(x.Id))
                    .ProjectToDownloadPreview()
                    .ToListAsync(cancellationToken);

                previews.AddRange(resultWithQualities);
            }

            // Get episodes without specific qualities
            var episodeIds = episodeDownloadMedia.SelectMany(x => x.MediaIds).Except(episodeIdsWithQuality).ToHashSet();
            if (episodeIds.Any())
            {
                var result = await baseQuery
                    .Include(x => x.MediaDataList)
                    .Where(x => episodeIds.Contains(x.Id))
                    .ProjectToDownloadPreview()
                    .ToListAsync(cancellationToken);

                previews.AddRange(result);
            }

            var sortedPreviews = previews.OrderByNatural(x => x.Title);
            return Result.Ok(sortedPreviews);
        }
        catch (Exception ex)
        {
            return Result.Fail(new ExceptionalError(ex)).LogError();
        }
    }

    /// <summary>
    /// Optimized method to get episode keys from TV shows, seasons, and episodes with a single query approach.
    /// </summary>
    private async Task<Result<List<TvShowEpisodeKey>>> GetEpisodeKeys(
        List<DownloadMediaDTO> tvShowDownloadMedia,
        List<DownloadMediaDTO> seasonDownloadMedia,
        List<DownloadMediaDTO> episodeDownloadMedia,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var tvShowEpisodeKeys = new List<TvShowEpisodeKey>();

            // Collect all episode IDs from different sources
            var allEpisodeIds = new HashSet<int>();

            // Add episodes from TV shows
            if (tvShowDownloadMedia.Any(x => x.MediaIds.Any()))
            {
                var tvShowIds = tvShowDownloadMedia.SelectMany(x => x.MediaIds).ToHashSet();
                var tvShowEpisodeIds = await _dbContext
                    .PlexTvShows.AsNoTracking()
                    .Where(x => tvShowIds.Contains(x.Id))
                    .SelectMany(x => x.Seasons.SelectMany(y => y.Episodes.Select(z => z.Id)))
                    .ToListAsync(cancellationToken);

                allEpisodeIds.UnionWith(tvShowEpisodeIds);
            }

            // Add episodes from seasons
            if (seasonDownloadMedia.Any(x => x.MediaIds.Any()))
            {
                var seasonIds = seasonDownloadMedia.SelectMany(x => x.MediaIds).ToHashSet();
                var seasonEpisodeIds = await _dbContext
                    .PlexTvShowSeason.AsNoTracking()
                    .Where(x => seasonIds.Contains(x.Id))
                    .SelectMany(x => x.Episodes.Select(y => y.Id))
                    .ToListAsync(cancellationToken);

                allEpisodeIds.UnionWith(seasonEpisodeIds);
            }

            // Add direct episode IDs
            if (episodeDownloadMedia.Any(x => x.MediaIds.Any()))
            {
                var directEpisodeIds = episodeDownloadMedia.SelectMany(x => x.MediaIds);
                allEpisodeIds.UnionWith(directEpisodeIds);
            }

            // Single query to get all episode data
            if (allEpisodeIds.Any())
            {
                tvShowEpisodeKeys = await _dbContext
                    .PlexTvShowEpisodes.AsNoTracking()
                    .Include(x => x.MediaDataList)
                    .Where(x => allEpisodeIds.Contains(x.Id))
                    .ProjectToEpisodeKey()
                    .ToListAsync(cancellationToken);
            }

            return Result.Ok(tvShowEpisodeKeys);
        }
        catch (Exception ex)
        {
            return _log.Here().ErrorResult(ex, "Failed to get episode keys");
        }
    }

    /// <summary>
    /// Builds the TV show hierarchy by adding episodes to seasons and seasons to TV shows.
    /// </summary>
    private static void BuildHierarchy(
        List<DownloadPreview> tvShows,
        List<DownloadPreview> seasons,
        List<DownloadPreview> episodes
    )
    {
        // Group episodes by season for an efficient lookup
        var episodesBySeasonId = episodes
            .GroupBy(x => x.SeasonId)
            .ToDictionary(g => g.Key, g => g.OrderByNatural(x => x.Title).ToList());

        // Add episodes to seasons
        foreach (var season in seasons)
        {
            if (episodesBySeasonId.TryGetValue(season.Id, out var seasonEpisodes))
            {
                season.Children.AddRange(seasonEpisodes);
                season.Size = season.Children.Sum(x => x.Size);
                season.ChildCount = season.Children.Count;
                season.Qualities.AddRange(seasonEpisodes.SelectMany(x => x.Qualities).DistinctBy(x => x.Quality));
            }
        }

        // Group seasons by TV show for efficient lookup
        var seasonsByTvShowId = seasons
            .GroupBy(x => x.TvShowId)
            .ToDictionary(g => g.Key, g => g.OrderByNatural(x => x.Title).ToList());

        // Add seasons to TV shows
        foreach (var tvShow in tvShows)
        {
            if (seasonsByTvShowId.TryGetValue(tvShow.Id, out var tvShowSeasons))
            {
                tvShow.Children.AddRange(tvShowSeasons);
                tvShow.Size = tvShow.Children.Sum(x => x.Size);
                tvShow.ChildCount = tvShow.Children.Count;
                tvShow.Qualities.AddRange(tvShowSeasons.SelectMany(x => x.Qualities).DistinctBy(x => x.Quality));
            }
        }
    }
}
