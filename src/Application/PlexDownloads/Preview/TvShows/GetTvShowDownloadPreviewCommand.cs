namespace Reaparr.Application;

public record GetTvShowDownloadPreviewCommand(List<DownloadMediaDTO> DownloadMedias)
    : ICommand<Result<IEnumerable<DownloadPreview>>>;

public class GetTvShowDownloadPreviewCommandValidator : AbstractValidator<GetTvShowDownloadPreviewCommand>
{
    public GetTvShowDownloadPreviewCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.DownloadMedias).NotNull();
        RuleForEach(x => x.DownloadMedias).NotNull().SetValidator(new DownloadMediaDTOValidator());
        RuleForEach(x => x.DownloadMedias)
            .Must(x => x is null || x.Type is PlexMediaType.TvShow or PlexMediaType.Season or PlexMediaType.Episode)
            .WithMessage("TV preview selections must contain only TV media.");
    }
}

public class GetTvShowDownloadPreviewCommandHandler
    : ICommandHandler<GetTvShowDownloadPreviewCommand, Result<IEnumerable<DownloadPreview>>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ILogger _log;

    public GetTvShowDownloadPreviewCommandHandler(IReaparrDbContext dbContext, ILogger log)
    {
        _dbContext = dbContext;
        _log = log.ForContext<GetTvShowDownloadPreviewCommandHandler>();
    }

    public Task<Result<IEnumerable<DownloadPreview>>> ExecuteAsync(
        GetTvShowDownloadPreviewCommand command,
        CancellationToken cancellationToken
    ) => CreateTvShowPreviews(command.DownloadMedias, cancellationToken);

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
            _log.Here().Error(ex, "Failed to create TV show previews");
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
                            SelectionKind = null,
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
                season.Qualities.AddRange(seasonEpisodes.SelectMany(x => x.Qualities).DistinctBy(x => x.VideoQuality));
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
                tvShow.Qualities.AddRange(tvShowSeasons.SelectMany(x => x.Qualities).DistinctBy(x => x.VideoQuality));
            }
        }
    }
}
