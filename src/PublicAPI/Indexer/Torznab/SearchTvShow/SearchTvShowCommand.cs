namespace Reaparr.PublicAPI;

public record SearchTvShowCommand(TorznabRequest Request) : ICommand<Result<TorznabMediaSearchResponseDTO>>;

public class SearchTvShowCommandValidator : AbstractValidator<SearchTvShowCommand>
{
    public SearchTvShowCommandValidator()
    {
        RuleFor(x => x.Request.Limit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.Limit)
            .Must(TorznabSearchHelpers.IsPageSizeWithinLimit)
            .WithMessage($"The limit must not exceed {TorznabSearchHelpers.MaxPageSize}.");
        RuleFor(x => x.Request.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request)
            .Must(x => TorznabSearchHelpers.IsPaginationWithinLimit(x.Offset, x.Limit))
            .WithMessage($"The combined offset and limit must not exceed {TorznabSearchHelpers.MaxPaginationWindow}.");
        RuleFor(x => x.Request.Season).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.Episode).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.TmdbId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.TvdbId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.ImdbId).NotNull();
        RuleFor(x => x.Request.Categories).NotNull();
        RuleFor(x => x.Request.Attributes).NotNull();
        When(
            x => x.Request.Season > 0 || x.Request.Episode > 0,
            () =>
            {
                RuleFor(x => x.Request.Season).GreaterThan(0);
                RuleFor(x => x.Request)
                    .Must(x => x.HasAnyExternalId)
                    .WithMessage(
                        "Provide at least one of ImdbId, TmdbId, or TvdbId when Season/Episode are specified."
                    );
            }
        );
    }
}

public class SearchTvShowCommandHandler : ICommandHandler<SearchTvShowCommand, Result<TorznabMediaSearchResponseDTO>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly INetworkSettings _networkSettings;

    public SearchTvShowCommandHandler(ILogger log, IReaparrDbContext dbContext, INetworkSettings networkSettings)
    {
        _log = log.ForContext<SearchTvShowCommandHandler>();
        _dbContext = dbContext;
        _networkSettings = networkSettings;
    }

    public async Task<Result<TorznabMediaSearchResponseDTO>> ExecuteAsync(
        SearchTvShowCommand command,
        CancellationToken cancellationToken
    )
    {
        var request = command.Request;
        var onlineServerIds = await _dbContext.GetDownloadableServerIds();
        if (onlineServerIds.Count == 0)
        {
            _log.Here()
                .Warning("No online Plex servers with downloads enabled were found, returning empty search results");
            return Result.Ok(
                TorznabSearchHelpers.CreateResponse($"TV Search results for {request.Query}", request.Offset, 0, [])
            );
        }

        var accessibleLibraryIds = await _dbContext.GetAccessibleLibraryIds(onlineServerIds, PlexMediaType.TvShow);
        var candidateTvShows = _dbContext.PlexTvShows.Where(x => accessibleLibraryIds.Contains(x.PlexLibraryId));

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var searchTitle = request.Query.ToSearchTitle();
            if (string.IsNullOrWhiteSpace(searchTitle))
                return Result.Ok(
                    TorznabSearchHelpers.CreateResponse($"TV Search results for {request.Query}", request.Offset, 0, [])
                );

            candidateTvShows = candidateTvShows.Where(x => EF.Functions.Like(x.SearchTitle, $"{searchTitle}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.ImdbId))
        {
            var imdbId = TorznabImdbId.Normalize(request.ImdbId);
            candidateTvShows = imdbId is null
                ? candidateTvShows.Where(_ => false)
                : candidateTvShows.Where(x => x.Guid_IMDB == imdbId);
        }

        if (request.TmdbId > 0)
            candidateTvShows = candidateTvShows.Where(x => x.Guid_TMDB == request.TmdbId);
        if (request.TvdbId > 0)
            candidateTvShows = candidateTvShows.Where(x => x.Guid_TVDB == request.TvdbId);
        var candidateEpisodeIds = _dbContext.PlexTvShowEpisodes.Where(x =>
            candidateTvShows.Select(tvShow => tvShow.Id).Contains(x.TvShowId)
        );
        if (request.Season > 0)
            candidateEpisodeIds = candidateEpisodeIds.Where(x => x.TvShowSeason!.SeasonNumber == request.Season);
        if (request.Episode > 0)
            candidateEpisodeIds = candidateEpisodeIds.Where(x => x.EpisodeNumber == request.Episode);

        var query = _dbContext
            .PlexTvShowEpisodeData.Where(x =>
                candidateEpisodeIds.Select(episode => episode.Id).Contains(x.PlexTvShowEpisodeId)
            )
            .ApplyTorznabCategories(request.Categories);

        var total = await query.TagWith("Torznab/ActiveTv/Count").CountAsync(cancellationToken);
        var rows = await query
            .TagWith("Torznab/ActiveTv/Page")
            .ProjectToTorznabFeedItems()
            .OrderForTorznab()
            .Skip(request.Offset)
            .Take(request.Limit)
            .ToListAsync(cancellationToken);
        rows = await rows.EnrichGenresAsync(_dbContext, cancellationToken);
        var requestedAttributes = TorznabSearchHelpers.GetRequestedAttributes(
            request.IncludeAllAttributes,
            request.Attributes
        );
        var baseUrl = _networkSettings.Url;
        var items = rows.Select(x => x.ToTorznabItem(request.Integration, request.ApiKey, baseUrl, requestedAttributes))
            .ToList();
        return Result.Ok(
            TorznabSearchHelpers.CreateResponse($"TV Search results for {request.Query}", request.Offset, total, items)
        );
    }
}
