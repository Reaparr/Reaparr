namespace Reaparr.PublicAPI;

public record SearchGenericCommand(TorznabRequest Request) : ICommand<Result<TorznabMediaSearchResponseDTO>>;

public class SearchGenericCommandValidator : AbstractValidator<SearchGenericCommand>
{
    public SearchGenericCommandValidator()
    {
        RuleFor(x => x.Request.Limit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.Limit).LessThanOrEqualTo(TorznabSearchHelpers.MaxPageSize);
        RuleFor(x => x.Request.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request)
            .Must(x => TorznabSearchHelpers.IsPaginationWithinLimit(x.Offset, x.Limit))
            .WithMessage($"The combined offset and limit must not exceed {TorznabSearchHelpers.MaxPaginationWindow}.");
        RuleFor(x => x.Request.TvdbId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.TmdbId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.ImdbId).NotNull();
        RuleFor(x => x.Request.Categories).NotNull();
        RuleFor(x => x.Request.Attributes).NotNull();
    }
}


public class SearchGenericCommandHandler : ICommandHandler<SearchGenericCommand, Result<TorznabMediaSearchResponseDTO>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly INetworkSettings _networkSettings;

    public SearchGenericCommandHandler(IReaparrDbContext dbContext, INetworkSettings networkSettings)
    {
        _dbContext = dbContext;
        _networkSettings = networkSettings;
    }

    public async Task<Result<TorznabMediaSearchResponseDTO>> ExecuteAsync(
        SearchGenericCommand command,
        CancellationToken cancellationToken
    )
    {
        var request = command.Request;
        var description = $"Search results for {request.Query}";
        if (!request.IncludesMovies && !request.IncludesEpisodes)
            return Result.Ok(TorznabSearchHelpers.CreateResponse(description, request.Offset, 0, []));

        var searchTitle = request.Query.ToSearchTitle();
        if (!string.IsNullOrWhiteSpace(request.Query) && string.IsNullOrWhiteSpace(searchTitle))
            return Result.Ok(TorznabSearchHelpers.CreateResponse(description, request.Offset, 0, []));

        var imdbId = TorznabImdbId.Normalize(request.ImdbId);
        if (!string.IsNullOrWhiteSpace(request.ImdbId) && imdbId is null)
            return Result.Ok(TorznabSearchHelpers.CreateResponse(description, request.Offset, 0, []));

        var onlineServerIds = await _dbContext.GetDownloadableServerIds();
        if (onlineServerIds.Count == 0)
            return Result.Ok(TorznabSearchHelpers.CreateResponse(description, request.Offset, 0, []));

        var accessibleLibraryIds = await _dbContext.GetAccessibleLibraryIds(onlineServerIds);
        IQueryable<TorznabFeedItemProjection>? query = null;

        if (request.IncludesMovies)
        {
            if (accessibleLibraryIds.Count > 0)
            {
                var movieCandidates = _dbContext.PlexMovies.Where(x => accessibleLibraryIds.Contains(x.PlexLibraryId));
                if (!string.IsNullOrWhiteSpace(searchTitle))
                    movieCandidates = movieCandidates.Where(x => EF.Functions.Like(x.SearchTitle, $"{searchTitle}%"));
                if (imdbId is not null)
                    movieCandidates = movieCandidates.Where(x => x.Guid_IMDB == imdbId);
                if (request.TmdbId > 0)
                    movieCandidates = movieCandidates.Where(x => x.Guid_TMDB == request.TmdbId);

                var movieRows = _dbContext
                    .PlexMovieData.Where(x => movieCandidates.Select(movie => movie.Id).Contains(x.PlexMovieId))
                    .ApplyTorznabCategories(request.Categories)
                    .ProjectToTorznabFeedItems();
                query = movieRows;
            }
        }

        if (request.IncludesEpisodes)
        {
            if (accessibleLibraryIds.Count > 0)
            {
                var tvShowCandidates = _dbContext.PlexTvShows.Where(x =>
                    accessibleLibraryIds.Contains(x.PlexLibraryId)
                );
                if (!string.IsNullOrWhiteSpace(searchTitle))
                    tvShowCandidates = tvShowCandidates.Where(x => EF.Functions.Like(x.SearchTitle, $"{searchTitle}%"));
                if (imdbId is not null)
                    tvShowCandidates = tvShowCandidates.Where(x => x.Guid_IMDB == imdbId);
                if (request.TmdbId > 0)
                    tvShowCandidates = tvShowCandidates.Where(x => x.Guid_TMDB == request.TmdbId);
                if (request.TvdbId > 0)
                    tvShowCandidates = tvShowCandidates.Where(x => x.Guid_TVDB == request.TvdbId);
                var episodeCandidates = _dbContext.PlexTvShowEpisodes.Where(x =>
                    tvShowCandidates.Select(tvShow => tvShow.Id).Contains(x.TvShowId)
                );

                var episodeRows = _dbContext
                    .PlexTvShowEpisodeData.Where(x =>
                        episodeCandidates.Select(episode => episode.Id).Contains(x.PlexTvShowEpisodeId)
                    )
                    .ApplyTorznabCategories(request.Categories)
                    .ProjectToTorznabFeedItems();
                query = query is null ? episodeRows : query.Concat(episodeRows);
            }
        }

        if (query is null)
            return Result.Ok(TorznabSearchHelpers.CreateResponse(description, request.Offset, 0, []));

        var total = await query.TagWith("Torznab/ActiveGeneric/Count").CountAsync(cancellationToken);
        var rows = await query
            .TagWith("Torznab/ActiveGeneric/Page")
            .OrderForTorznab()
            .Skip(request.Offset)
            .Take(request.Limit)
            .ToListAsync(cancellationToken);
        var enrichedRows = await rows.EnrichGenresAsync(_dbContext, cancellationToken);
        var requestedAttributes = TorznabSearchHelpers.GetRequestedAttributes(
            request.IncludeAllAttributes,
            request.Attributes
        );
        var baseUrl = _networkSettings.Url;
        var items = enrichedRows
            .Select(x => x.ToTorznabItem(request.Integration, request.ApiKey, baseUrl, requestedAttributes))
            .ToList();

        return Result.Ok(TorznabSearchHelpers.CreateResponse(description, request.Offset, total, items));
    }
}
