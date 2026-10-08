namespace Reaparr.Application;

// TODO split this into multiple command handlers per media type to reduce complexity and improve maintainability. And then keep this one to orchestrate the calls to the other handlers and merge the results.
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
    /// Handles the GetDownloadPreviewQuery by generating download previews for supported media selections.
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

            var musicResult = await CreateMusicPreviews(request.DownloadMedias, cancellationToken);
            if (musicResult.IsFailed)
                return musicResult.ToResult().LogIfFailed();

            downloadPreviews.AddRange(musicResult.Value);

            var otherVideoResult = await CreateOtherVideoPreviews(request.DownloadMedias, cancellationToken);
            if (otherVideoResult.IsFailed)
                return otherVideoResult.ToResult().LogIfFailed();

            downloadPreviews.AddRange(otherVideoResult.Value);

            var photoResult = await CreatePhotoPreviews(request.DownloadMedias, cancellationToken);
            if (photoResult.IsFailed)
                return photoResult.ToResult().LogIfFailed();

            downloadPreviews.AddRange(photoResult.Value);

            return Result.Ok(downloadPreviews);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.TaskIsCancelled(nameof(GetDownloadPreviewQuery)).LogWarning();
        }
        catch (Exception ex)
        {
            _log.Here().Error("Failed to create download previews: {ExceptionMsg}", ex.Message);
            return Result.Fail(new ExceptionalError(ex)).LogError();
        }
    }

    private async Task<Result<IEnumerable<DownloadPreview>>> CreateMusicPreviews(
        List<DownloadMediaDTO> downloadMedias,
        CancellationToken cancellationToken
    )
    {
        var selections = downloadMedias
            .MergeAndGroupList()
            .Where(x => x.Type is PlexMediaType.MusicArtist or PlexMediaType.MusicAlbum or PlexMediaType.MusicTrack)
            .OrderBy(x =>
                x.Type == PlexMediaType.MusicArtist ? 0
                : x.Type == PlexMediaType.MusicAlbum ? 1
                : 2
            );
        var tracksById = new Dictionary<int, PlexMusicTrack>();
        var sourceSelections = new Dictionary<int, DownloadMediaDTO>();
        foreach (var selection in selections)
        {
            var library = await _dbContext.PlexLibraries.GetAsync(selection.PlexLibraryId, cancellationToken);
            if (
                library is null
                || library.Type != PlexMediaType.MusicArtist
                || library.PlexServerId != selection.PlexServerId
            )
                return ResultExtensions.Create400BadRequestResult(
                    "The selected library does not match the requested server or media type."
                );

            var ids = selection.MediaIds.Distinct().ToList();
            int selectedCount;
            List<PlexMusicTrack> tracks;
            switch (selection.Type)
            {
                case PlexMediaType.MusicArtist:
                    var artists = await _dbContext
                        .PlexArtists.Where(x =>
                            ids.Contains(x.Id)
                            && x.PlexLibraryId == library.Id
                            && x.PlexServerId == library.PlexServerId
                        )
                        .Include(x => x.Albums)
                            .ThenInclude(x => x.Tracks)
                                .ThenInclude(x => x.MediaDataList)
                        .ToListAsync(cancellationToken);
                    selectedCount = artists.Count;
                    tracks = artists.SelectMany(x => x.Albums).SelectMany(x => x.Tracks).ToList();
                    break;
                case PlexMediaType.MusicAlbum:
                    var albums = await _dbContext
                        .PlexAlbums.Where(x =>
                            ids.Contains(x.Id)
                            && x.PlexLibraryId == library.Id
                            && x.PlexServerId == library.PlexServerId
                        )
                        .Include(x => x.PlexArtist)
                        .Include(x => x.Tracks)
                            .ThenInclude(x => x.MediaDataList)
                        .ToListAsync(cancellationToken);
                    selectedCount = albums.Count;
                    tracks = albums.SelectMany(x => x.Tracks).ToList();
                    break;
                default:
                    tracks = await _dbContext
                        .PlexTracks.Where(x =>
                            ids.Contains(x.Id)
                            && x.PlexLibraryId == library.Id
                            && x.PlexServerId == library.PlexServerId
                        )
                        .Include(x => x.PlexAlbum)
                            .ThenInclude(x => x!.PlexArtist)
                        .Include(x => x.MediaDataList)
                        .ToListAsync(cancellationToken);
                    selectedCount = tracks.Count;
                    break;
            }

            if (
                selectedCount != ids.Count
                || tracks.Any(x => x.PlexLibraryId != library.Id || x.PlexServerId != library.PlexServerId)
            )
                return ResultExtensions.Create400BadRequestResult(
                    "Every selected media item must belong to the requested library and server."
                );

            foreach (var selector in selection.Qualities)
            {
                var track = tracks.SingleOrDefault(x =>
                    x.Id == selector.MediaId && selector.MediaDataType == PlexMediaType.MusicTrack
                );
                if (
                    track is null
                    || !track.MediaDataList.Any(x =>
                        x.Id == selector.DataId
                        && x.PlexLibraryId == selection.PlexLibraryId
                        && x.PlexServerId == selection.PlexServerId
                    )
                )
                    return ResultExtensions.Create400BadRequestResult(
                        "A selected original does not belong to the requested media selection."
                    );
            }

            foreach (var track in tracks)
            {
                tracksById[track.Id] = track;
                sourceSelections[track.Id] =
                    sourceSelections.TryGetValue(track.Id, out var previous)
                    && !selection.Qualities.Any(x =>
                        x.MediaId == track.Id && x.MediaDataType == PlexMediaType.MusicTrack
                    )
                    && previous.Qualities.Any(x => x.MediaId == track.Id && x.MediaDataType == PlexMediaType.MusicTrack)
                        ? selection with
                        {
                            Qualities = previous
                                .Qualities.Where(x =>
                                    x.MediaId == track.Id && x.MediaDataType == PlexMediaType.MusicTrack
                                )
                                .ToList(),
                        }
                        : selection;
            }
        }

        var previews = new List<DownloadPreview>();
        var artistPreviews = new Dictionary<int, DownloadPreview>();
        var albumPreviews = new Dictionary<int, DownloadPreview>();
        foreach (var track in tracksById.Values)
        {
            var data = track.MediaDataList;
            var groups = data.GroupBy(x => x.PlexApiMediaId).ToList();
            var selectors = sourceSelections[track.Id]
                .Qualities.Where(x => x.MediaId == track.Id && x.MediaDataType == PlexMediaType.MusicTrack)
                .ToList();
            var selectedMediaIds = selectors
                .Select(x => data.Single(y => y.Id == x.DataId).PlexApiMediaId)
                .Distinct()
                .ToList();
            if (selectedMediaIds.Count > 1)
                return ResultExtensions.Create400BadRequestResult(
                    "Select exactly one original version per media item."
                );
            if (selectedMediaIds.Count == 1)
                groups.RemoveAll(x => x.Key != selectedMediaIds[0]);

            var album = track.PlexAlbum;
            var artist = album?.PlexArtist;
            if (
                album is null
                || artist is null
                || album.PlexLibraryId != track.PlexLibraryId
                || album.PlexServerId != track.PlexServerId
                || artist.PlexLibraryId != track.PlexLibraryId
                || artist.PlexServerId != track.PlexServerId
            )
                return ResultExtensions.Create400BadRequestResult("The selected track hierarchy is invalid.");

            var preview = track.ProjectToDownloadPreviewMapper(groups);

            if (!artistPreviews.TryGetValue(artist.Id, out var artistPreview))
            {
                artistPreview = artist.ProjectToDownloadPreviewMapper();
                artistPreviews.Add(artist.Id, artistPreview);
                previews.Add(artistPreview);
            }
            if (!albumPreviews.TryGetValue(album.Id, out var albumPreview))
            {
                albumPreview = album.ProjectToDownloadPreviewMapper();
                albumPreviews.Add(album.Id, albumPreview);
                artistPreview.Children.Add(albumPreview);
                artistPreview.ChildCount++;
            }
            albumPreview.Children.Add(preview);
            albumPreview.ChildCount++;
            albumPreview.Size += preview.Size;
            artistPreview.Size += preview.Size;
        }

        foreach (var parent in albumPreviews.Values.Concat(artistPreviews.Values))
        {
            var children = parent.Children.OrderByNatural(x => x.Title).ToList();
            parent.Children.Clear();
            parent.Children.AddRange(children);
        }

        return Result.Ok<IEnumerable<DownloadPreview>>(previews);
    }

    private async Task<Result<IEnumerable<DownloadPreview>>> CreateOtherVideoPreviews(
        List<DownloadMediaDTO> downloadMedias,
        CancellationToken cancellationToken
    )
    {
        var selections = downloadMedias.MergeAndGroupList().Where(x => x.Type == PlexMediaType.OtherVideos);
        var videosById = new Dictionary<int, PlexOtherVideo>();
        var sourceSelections = new Dictionary<int, DownloadMediaDTO>();
        foreach (var selection in selections)
        {
            var library = await _dbContext.PlexLibraries.GetAsync(selection.PlexLibraryId, cancellationToken);
            if (
                library is null
                || library.Type != PlexMediaType.OtherVideos
                || library.PlexServerId != selection.PlexServerId
            )
                return ResultExtensions.Create400BadRequestResult(
                    "The selected library does not match the requested server or media type."
                );

            var ids = selection.MediaIds.Distinct().ToList();
            var videos = await _dbContext
                .PlexOtherVideos.Where(x =>
                    ids.Contains(x.Id) && x.PlexLibraryId == library.Id && x.PlexServerId == library.PlexServerId
                )
                .Include(x => x.MediaDataList)
                .ToListAsync(cancellationToken);
            if (
                videos.Count != ids.Count
                || videos.Any(x => x.PlexLibraryId != library.Id || x.PlexServerId != library.PlexServerId)
            )
                return ResultExtensions.Create400BadRequestResult(
                    "Every selected media item must belong to the requested library and server."
                );

            foreach (var selector in selection.Qualities)
            {
                var video = videos.SingleOrDefault(x =>
                    x.Id == selector.MediaId && selector.MediaDataType == PlexMediaType.OtherVideos
                );
                if (
                    video is null
                    || !video.MediaDataList.Any(x =>
                        x.Id == selector.DataId
                        && x.PlexLibraryId == selection.PlexLibraryId
                        && x.PlexServerId == selection.PlexServerId
                    )
                )
                    return ResultExtensions.Create400BadRequestResult(
                        "A selected original does not belong to the requested media selection."
                    );
            }

            foreach (var video in videos)
            {
                videosById[video.Id] = video;
                sourceSelections[video.Id] =
                    sourceSelections.TryGetValue(video.Id, out var previous)
                    && !selection.Qualities.Any(x =>
                        x.MediaId == video.Id && x.MediaDataType == PlexMediaType.OtherVideos
                    )
                    && previous.Qualities.Any(x =>
                        x.MediaId == video.Id && x.MediaDataType == PlexMediaType.OtherVideos
                    )
                        ? selection with
                        {
                            Qualities = previous
                                .Qualities.Where(x =>
                                    x.MediaId == video.Id && x.MediaDataType == PlexMediaType.OtherVideos
                                )
                                .ToList(),
                        }
                        : selection;
            }
        }

        var previews = new List<DownloadPreview>();
        foreach (var video in videosById.Values)
        {
            var data = video.MediaDataList;
            var groups = data.GroupBy(x => x.PlexApiMediaId).ToList();
            var selectors = sourceSelections[video.Id]
                .Qualities.Where(x => x.MediaId == video.Id && x.MediaDataType == PlexMediaType.OtherVideos)
                .ToList();
            var selectedMediaIds = selectors
                .Select(x => data.Single(y => y.Id == x.DataId).PlexApiMediaId)
                .Distinct()
                .ToList();
            if (selectedMediaIds.Count > 1)
                return ResultExtensions.Create400BadRequestResult(
                    "Select exactly one original version per media item."
                );
            if (selectedMediaIds.Count == 1)
                groups.RemoveAll(x => x.Key != selectedMediaIds[0]);

            previews.Add(video.ProjectToDownloadPreviewMapper(groups));
        }

        return Result.Ok<IEnumerable<DownloadPreview>>(previews);
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

    private async Task<Result<IEnumerable<DownloadPreview>>> CreatePhotoPreviews(
        List<DownloadMediaDTO> downloadMedias,
        CancellationToken cancellationToken
    )
    {
        var albumSelections = downloadMedias.MergeAndGroupList().FindAll(x => x.Type == PlexMediaType.PhotoAlbum);
        var imageSelections = downloadMedias.MergeAndGroupList().FindAll(x => x.Type == PlexMediaType.PhotoImage);
        if (albumSelections.Count == 0 && imageSelections.Count == 0)
            return Result.Ok(Enumerable.Empty<DownloadPreview>());

        var groupedSelections = albumSelections.Concat(imageSelections).ToList();
        var libraryIds = groupedSelections.Select(x => x.PlexLibraryId).Distinct().ToList();
        var validLibraries = await _dbContext
            .PlexLibraries.Where(x => libraryIds.Contains(x.Id) && x.Type == PlexMediaType.PhotoAlbum)
            .Select(x => new { x.Id, x.PlexServerId })
            .ToListAsync(cancellationToken);
        if (
            groupedSelections.Any(selection =>
                !validLibraries.Any(x => x.Id == selection.PlexLibraryId && x.PlexServerId == selection.PlexServerId)
            )
        )
            return ResultExtensions.Create400BadRequestResult("The selected photo library does not match the server.");

        var albumIds = albumSelections.SelectMany(x => x.MediaIds).Distinct().ToHashSet();
        var imageIds = imageSelections.SelectMany(x => x.MediaIds).Distinct().ToHashSet();
        foreach (var selection in albumSelections)
        {
            var selectedAlbums = await _dbContext
                .PlexPhotoAlbums.Where(x =>
                    selection.MediaIds.Contains(x.Id)
                    && x.PlexLibraryId == selection.PlexLibraryId
                    && x.PlexServerId == selection.PlexServerId
                )
                .Select(x => new { x.Id, ImageIds = x.Photos.Select(y => y.Id).ToList() })
                .ToListAsync(cancellationToken);
            if (selectedAlbums.Count != selection.MediaIds.Distinct().Count())
                return ResultExtensions.Create400BadRequestResult(
                    "Every selected photo album must belong to the requested library and server."
                );
            var selectedImageIds = selectedAlbums.SelectMany(x => x.ImageIds).ToHashSet();
            if (selection.Qualities.Any(x => !selectedImageIds.Contains(x.MediaId)))
            {
                return ResultExtensions.Create400BadRequestResult(
                    "A selected photo original does not belong to the requested media selection."
                );
            }
            imageIds.UnionWith(selectedImageIds);
        }

        foreach (var selection in imageSelections)
        {
            var selectedImageIds = selection.MediaIds.ToHashSet();
            if (selection.Qualities.Any(x => !selectedImageIds.Contains(x.MediaId)))
            {
                return ResultExtensions.Create400BadRequestResult(
                    "A selected photo original does not belong to the requested media selection."
                );
            }
            var matchingImageCount = await _dbContext.PlexPhotoImages.CountAsync(
                x =>
                    selectedImageIds.Contains(x.Id)
                    && x.PlexLibraryId == selection.PlexLibraryId
                    && x.PlexServerId == selection.PlexServerId,
                cancellationToken
            );
            if (matchingImageCount != selectedImageIds.Count)
                return ResultExtensions.Create400BadRequestResult(
                    "Every selected photo must belong to the requested library and server."
                );
        }

        var images = await _dbContext
            .PlexPhotoImages.Include(x => x.MediaDataList)
            .Where(x => imageIds.Contains(x.Id))
            .ProjectToDownloadPreview()
            .ToListAsync(cancellationToken);
        if (images.Count != imageIds.Count)
            return ResultExtensions.Create400BadRequestResult("Every selected photo must exist.");

        var imageScope = imageSelections
            .SelectMany(selection => selection.MediaIds.Select(id => (Id: id, Selection: selection)))
            .ToDictionary(x => x.Id, x => x.Selection);
        var selectedQualities = groupedSelections
            .SelectMany(x => x.Qualities.Where(y => y.MediaDataType == PlexMediaType.PhotoImage))
            .ToList();
        if (
            groupedSelections.Any(selection =>
                selection.Qualities.Any(x => x.MediaDataType != PlexMediaType.PhotoImage)
            )
        )
            return ResultExtensions.Create400BadRequestResult(
                "Photo original selectors must use the photo media type."
            );
        var selectedDataIds = selectedQualities.Select(x => x.DataId).Distinct().ToList();
        var selectedData = await _dbContext
            .PlexPhotoData.Where(x => selectedDataIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.PlexPhotoId,
                x.PlexApiMediaId,
            })
            .ToListAsync(cancellationToken);
        if (selectedData.Count != selectedDataIds.Count)
            return ResultExtensions.Create400BadRequestResult("A selected photo original no longer exists.");
        if (selectedQualities.Any(x => selectedData.All(y => y.Id != x.DataId || y.PlexPhotoId != x.MediaId)))
            return ResultExtensions.Create400BadRequestResult(
                "A selected photo original does not belong to that photo."
            );

        var selectedMediaIds = selectedData.Select(x => x.PlexApiMediaId).Distinct().ToList();
        var selectedGroupData = await _dbContext
            .PlexPhotoData.Where(x => imageIds.Contains(x.PlexPhotoId) && selectedMediaIds.Contains(x.PlexApiMediaId))
            .GroupBy(x => new { x.PlexPhotoId, x.PlexApiMediaId })
            .ToDictionaryAsync(
                x => (x.Key.PlexPhotoId, x.Key.PlexApiMediaId),
                x => (Size: x.Sum(y => y.Size), DataId: x.Min(y => y.Id)),
                cancellationToken
            );
        foreach (var image in images)
        {
            var selection =
                imageScope.GetValueOrDefault(image.Id)
                ?? albumSelections.FirstOrDefault(x => x.MediaIds.Contains(image.PhotoAlbumId));
            if (selection is null)
                continue;

            var selectors = selection
                .Qualities.Where(x => x.MediaDataType == PlexMediaType.PhotoImage && x.MediaId == image.Id)
                .ToList();
            if (selectors.Count == 0)
                continue;
            var selectedGroups = selectors
                .Select(x => selectedData.Single(y => y.Id == x.DataId))
                .Select(x => x.PlexApiMediaId)
                .Distinct()
                .ToList();
            if (selectedGroups.Count != 1)
                return ResultExtensions.Create400BadRequestResult("Select exactly one original version per photo.");

            var selectedGroup = selectedGroupData[(image.Id, selectedGroups[0])];
            image.Qualities.RemoveAll(x => x.DataId != selectedGroup.DataId);
            image.Size = selectedGroup.Size;
        }

        var allAlbumIds = images.Select(x => x.PhotoAlbumId).Distinct().ToList();
        var albums = await _dbContext
            .PlexPhotoAlbums.Where(x => allAlbumIds.Contains(x.Id))
            .ProjectToDownloadPreview()
            .ToListAsync(cancellationToken);
        var imagesByAlbum = images
            .GroupBy(x => x.PhotoAlbumId)
            .ToDictionary(x => x.Key, x => x.OrderByNatural(y => y.Title).ToList());
        foreach (var album in albums)
        {
            if (!imagesByAlbum.TryGetValue(album.Id, out var children))
                continue;
            album.Children.AddRange(children);
            album.Size = children.Sum(x => x.Size);
            album.ChildCount = children.Count;
        }

        return Result.Ok(albums.OrderByNatural(x => x.Title));
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
