namespace Reaparr.Application;

/// <summary>
/// Gets the <see cref="PlexMediaDTO"/> with all children
/// </summary>
public class GetMediaDetailByIdEndpointRequest
{
    /// <summary>
    /// NOTE: This constructor is needed to make the query param optional in the front-end typescript-api generation.
    /// </summary>
    [SetsRequiredMembers]
    public GetMediaDetailByIdEndpointRequest(int plexMediaId, PlexMediaType type)
    {
        PlexMediaId = plexMediaId;
        Type = type;
    }

    /// <summary>The persisted media identifier within the requested media family.</summary>
    public required int PlexMediaId { get; init; }

    /// <summary> The <see cref="PlexMediaType">Type</see> of the PlexMedia.</summary>
    [QueryParam, BindFrom("type")]
    public required PlexMediaType Type { get; init; }
}

public class GetMediaDetailByIdEndpointRequestValidator : Validator<GetMediaDetailByIdEndpointRequest>
{
    public GetMediaDetailByIdEndpointRequestValidator()
    {
        RuleFor(x => x.PlexMediaId).GreaterThan(0);
        RuleFor(x => x.Type).Must(x => x.IsRootType());
    }
}

public class GetMediaDetailByIdEndpoint : Endpoint<GetMediaDetailByIdEndpointRequest, ResultDTO<PlexMediaDTO>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public GetMediaDetailByIdEndpoint(ILogger log, IReaparrDbContext dbContext, ICommandExecutor commandExecutor)
    {
        _log = log.ForContext<GetMediaDetailByIdEndpoint>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexMediaController + "/detail/{PlexMediaId}");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PlexMediaDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(GetMediaDetailByIdEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        if (req.Type == PlexMediaType.Movie)
        {
            var plexMovie = await _dbContext
                .PlexMovies.IncludeAll()
                .FirstOrDefaultAsync(x => x.Id == req.PlexMediaId, ct);
            if (plexMovie is null)
            {
                await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexMovie), req.PlexMediaId), ct);
                return;
            }

            await SetNestedMovieProperties(plexMovie, ct);
            await ApplyMovieDetailComparisonStateAsync(plexMovie, ct);

            await Send.FluentResult(Result.Ok(plexMovie), x => x.ToDTO(), ct);
        }
        else if (req.Type == PlexMediaType.TvShow)
        {
            var plexTvShowResult = await GetPlexTvShow(req.PlexMediaId, ct);
            if (plexTvShowResult.IsFailed)
            {
                await Send.FluentResult(plexTvShowResult, ct);
                return;
            }

            await ApplyTvShowDetailComparisonStateAsync(plexTvShowResult.Value, ct);

            await Send.FluentResult(plexTvShowResult, x => x.ToDTO(), ct);
        }
        else if (req.Type == PlexMediaType.MusicArtist)
        {
            var plexMusicArtistResult = GetPlexMusicArtist(req.PlexMediaId);
            if (plexMusicArtistResult.IsFailed)
            {
                await Send.FluentResult(plexMusicArtistResult.ToResult(), ct);
                return;
            }

            await ApplyMusicDetailComparisonStateAsync(plexMusicArtistResult.Value, ct);

            await Send.FluentResult(plexMusicArtistResult, x => x.ToDTO(), ct);
        }
        else if (req.Type == PlexMediaType.PhotoAlbum)
        {
            var plexPhotoAlbumResult = await GetPlexPhotoAlbum(req.PlexMediaId, ct);
            if (plexPhotoAlbumResult.IsFailed)
            {
                await Send.FluentResult(plexPhotoAlbumResult.ToResult(), ct);
                return;
            }

            await Send.FluentResult(plexPhotoAlbumResult, x => x.ToDTO(), ct);
        }
        else if (req.Type == PlexMediaType.OtherVideos)
        {
            var plexOtherVideo = await _dbContext
                .PlexOtherVideos.Include(x => x.MediaDataList)
                .FirstOrDefaultAsync(x => x.Id == req.PlexMediaId, ct);
            if (plexOtherVideo is null)
            {
                await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexOtherVideo), req.PlexMediaId), ct);
                return;
            }

            await Send.FluentResult(Result.Ok(plexOtherVideo), x => x.ToDTO(), ct);
        }
    }

    private async Task<Result<PlexTvShow>> GetPlexTvShow(int plexTvShowId, CancellationToken ct)
    {
        var plexTvShow = _dbContext.PlexTvShows.FirstOrDefault(x => x.Id == plexTvShowId);

        if (plexTvShow is null)
            return ResultExtensions.EntityNotFound(nameof(PlexTvShow), plexTvShowId).LogError();

        plexTvShow.Seasons = _dbContext
            .PlexTvShowSeason.Where(x => x.TvShowId == plexTvShowId)
            .Take(plexTvShow.ChildCount)
            .ToList();

        plexTvShow.Seasons = plexTvShow.Seasons.OrderBy(x => x.SortIndex).ToList();

        foreach (var season in plexTvShow.Seasons)
            season.Episodes = _dbContext
                .PlexTvShowEpisodes.Include(x => x.MediaDataList)
                .Where(x => x.TvShowSeasonId == season.Id)
                .Take(season.ChildCount)
                .ToList();

        await SetNestedTvShowProperties(plexTvShow, ct);

        return Result.Ok(plexTvShow);
    }

    private Result<PlexMusicArtist> GetPlexMusicArtist(int plexMusicArtistId)
    {
        var plexMusicArtist = _dbContext
            .PlexMusicArtists.Include(x => x.Qualities)
            .Include(x => x.Genres)
            .FirstOrDefault(x => x.Id == plexMusicArtistId);

        if (plexMusicArtist is null)
            return ResultExtensions.EntityNotFound(nameof(PlexMusicArtist), plexMusicArtistId).LogError();

        plexMusicArtist.Albums = _dbContext
            .PlexMusicAlbums.Include(x => x.Qualities)
            .Where(x => x.PlexArtistId == plexMusicArtistId)
            .Take(plexMusicArtist.ChildCount)
            .ToList();

        plexMusicArtist.Albums = plexMusicArtist.Albums.OrderBy(x => x.SortIndex).ToList();

        foreach (var album in plexMusicArtist.Albums)
            album.Tracks = _dbContext
                .PlexMusicTracks.Include(x => x.MediaDataList)
                .Where(x => x.PlexAlbumId == album.Id)
                .Take(album.ChildCount)
                .ToList();

        return Result.Ok(plexMusicArtist);
    }

    private async Task<Result<PlexPhotoAlbum>> GetPlexPhotoAlbum(int plexPhotoAlbumId, CancellationToken ct)
    {
        var plexPhotoAlbum = await _dbContext
            .PlexPhotoAlbums.Include(x => x.Memberships)
                .ThenInclude(x => x.PlexPhotoMediaData)
            .FirstOrDefaultAsync(x => x.Id == plexPhotoAlbumId, ct);

        if (plexPhotoAlbum is null)
            return ResultExtensions.EntityNotFound(nameof(PlexPhotoAlbum), plexPhotoAlbumId).LogError();

        return Result.Ok(plexPhotoAlbum);
    }

    private async Task ApplyTvShowDetailComparisonStateAsync(PlexTvShow plexTvShow, CancellationToken ct)
    {
        var items = new List<PlexMediaSlimDTO> { plexTvShow.ToSlimDTOMapper() };
        var episodes = plexTvShow.Seasons.SelectMany(s => s.Episodes).ToList();
        foreach (var episode in episodes)
            items.Add(episode.ToSlimDTO());

        var result = await _commandExecutor.Send(
            new ApplyComparisonStateCommand(items, PlexMediaType.TvShow, plexTvShow.PlexLibraryId),
            ct
        );

        if (result.IsFailed)
        {
            result.LogError();
            return;
        }

        plexTvShow.ComparisonState = items[0].ComparisonId.ToComparisonState();

        for (var i = 0; i < episodes.Count; i++)
            episodes[i].ComparisonState = items[i + 1].ComparisonId.ToComparisonState();
    }

    private async Task ApplyMusicDetailComparisonStateAsync(PlexMusicArtist artist, CancellationToken ct)
    {
        var media = new List<BasePlexMedia> { artist };
        media.AddRange(artist.Albums);
        media.AddRange(artist.Albums.SelectMany(x => x.Tracks));
        var items = new List<PlexMediaSlimDTO> { artist.ToSlimDTOMapper() };
        items.AddRange(artist.Albums.Select(x => x.ToSlimDTOMapper()));
        items.AddRange(artist.Albums.SelectMany(x => x.Tracks).Select(x => x.ToSlimDTOMapper()));
        var result = await _commandExecutor.Send(
            new ApplyComparisonStateCommand(items, PlexMediaType.MusicArtist, artist.PlexLibraryId),
            ct
        );

        if (result.IsFailed)
        {
            result.LogIfFailed();
            return;
        }

        for (var i = 0; i < media.Count; i++)
            media[i].ComparisonState = items[i].ComparisonId.ToComparisonState();
    }

    private async Task ApplyMovieDetailComparisonStateAsync(PlexMovie plexMovie, CancellationToken ct)
    {
        var items = new List<PlexMediaSlimDTO> { plexMovie.ToSlimDTO() };
        var result = await _commandExecutor.Send(
            new ApplyComparisonStateCommand(items, PlexMediaType.Movie, plexMovie.PlexLibraryId),
            ct
        );

        if (result.IsFailed)
        {
            result.LogError();
            return;
        }

        plexMovie.ComparisonState = items[0].ComparisonId.ToComparisonState();
    }

    private async Task SetNestedMovieProperties(PlexMovie plexMovie, CancellationToken ct = default)
    {
        var plexServerConnection = await _dbContext.ChoosePlexServerConnection(plexMovie.PlexServerId, ct);
        if (plexServerConnection.IsFailed)
        {
            plexServerConnection.ToResult().LogError();
            return;
        }

        var plexServerToken = await _dbContext.GetPlexServerTokenAsync(plexMovie.PlexServerId, ct);
        plexServerToken.LogIfFailed();
    }

    private async Task SetNestedTvShowProperties(PlexTvShow plexTvShow, CancellationToken ct = default)
    {
        var plexServerConnection = await _dbContext.ChoosePlexServerConnection(plexTvShow.PlexServerId, ct);
        if (plexServerConnection.IsFailed)
        {
            plexServerConnection.ToResult().LogError();
            return;
        }

        var plexServerToken = await _dbContext.GetPlexServerTokenAsync(plexTvShow.PlexServerId, ct);
        plexServerToken.LogIfFailed();
    }

}
