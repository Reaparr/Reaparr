using Reaparr.Environment;

namespace Reaparr.PublicAPI;
 
public record SearchMovieCommand(TorznabRequest Request) : ICommand<Result<TorznabMediaSearchResponseDTO>>;


public class SearchMovieCommandValidator : AbstractValidator<SearchMovieCommand>
{
    public SearchMovieCommandValidator()
    {
        RuleFor(x => x.Request.Limit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.Limit)
            .Must(TorznabSearchHelpers.IsPageSizeWithinLimit)
            .WithMessage($"The limit must not exceed {TorznabSearchHelpers.MaxPageSize}.");
        RuleFor(x => x.Request.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request)
            .Must(x => TorznabSearchHelpers.IsPaginationWithinLimit(x.Offset, x.Limit))
            .WithMessage($"The combined offset and limit must not exceed {TorznabSearchHelpers.MaxPaginationWindow}.");
        RuleFor(x => x.Request.TmdbId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.Categories).NotNull();
        RuleFor(x => x.Request.Attributes).NotNull();
    }
}

public class SearchMovieCommandHandler : ICommandHandler<SearchMovieCommand, Result<TorznabMediaSearchResponseDTO>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IAppRuntimeInfo _appRuntimeInfo;
    private readonly INetworkSettings _networkSettings;

    public SearchMovieCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        IAppRuntimeInfo appRuntimeInfo,
        INetworkSettings networkSettings
    )
    {
        _log = log.ForContext<SearchMovieCommandHandler>();
        _dbContext = dbContext;
        _appRuntimeInfo = appRuntimeInfo;
        _networkSettings = networkSettings;
    }

    public async Task<Result<TorznabMediaSearchResponseDTO>> ExecuteAsync(
        SearchMovieCommand command,
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
                TorznabSearchHelpers.CreateResponse($"Movie Search results for {request.Query}", request.Offset, 0, [])
            );
        }

        var accessibleLibraryIds = await _dbContext.GetAccessibleLibraryIds(onlineServerIds, PlexMediaType.Movie);
        var candidateMovieIds = _dbContext.PlexMovies.Where(x => accessibleLibraryIds.Contains(x.PlexLibraryId));

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var searchTitle = request.Query.ToSearchTitle();
            if (string.IsNullOrWhiteSpace(searchTitle))
                return Result.Ok(
                    TorznabSearchHelpers.CreateResponse(
                        $"Movie Search results for {request.Query}",
                        request.Offset,
                        0,
                        []
                    )
                );

            candidateMovieIds = candidateMovieIds.Where(x => EF.Functions.Like(x.SearchTitle, $"{searchTitle}%"));
        }

        if (!string.IsNullOrWhiteSpace(request.ImdbId))
        {
            var imdbId = TorznabImdbId.Normalize(request.ImdbId);
            candidateMovieIds = imdbId is null
                ? candidateMovieIds.Where(_ => false)
                : candidateMovieIds.Where(x => x.Guid_IMDB == imdbId);
        }

        if (request.TmdbId > 0)
            candidateMovieIds = candidateMovieIds.Where(x => x.Guid_TMDB == request.TmdbId);

        var query = _dbContext
            .PlexMovieData.Where(x => candidateMovieIds.Select(movie => movie.Id).Contains(x.PlexMovieId))
            .ApplyTorznabCategories(request.Categories);

        var total = await query.TagWith("Torznab/ActiveMovie/Count").CountAsync(cancellationToken);
        var rows = await query
            .TagWith("Torznab/ActiveMovie/Page")
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
        var isDevelopmentEnvironment = _appRuntimeInfo.IsDevelopmentEnvironment;
        var items = rows.Select(x =>
                x.ToTorznabItem(
                    request.Integration,
                    request.ApiKey,
                    baseUrl,
                    requestedAttributes,
                    isDevelopmentEnvironment
                )
            )
            .ToList();
        return Result.Ok(
            TorznabSearchHelpers.CreateResponse(
                $"Movie Search results for {request.Query}",
                request.Offset,
                total,
                items
            )
        );
    }
}
