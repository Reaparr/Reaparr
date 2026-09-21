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

    /// <summary>The id of the <see cref="BasePlexMedia"/>.</summary>
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
        RuleFor(x => x.Type).Must(x => x is PlexMediaType.Movie or PlexMediaType.TvShow);
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
        else
            await Send.FluentResult(ResultExtensions.Create400BadRequestResult($"Type {req.Type} is not allowed"), ct);
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
        if (plexServerToken.IsFailed)
        {
            plexServerToken.ToResult().LogError();
        }
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
        if (plexServerToken.IsFailed)
        {
            plexServerToken.ToResult().LogError();
        }
    }
}
