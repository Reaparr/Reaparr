using FlexQuery.NET.Models;

namespace Reaparr.Application;

public record GetAllMediaByTypeRequest
{
    [QueryParam, BindFrom("mediaType")]
    public required PlexMediaType MediaType { get; init; }

    /// <summary>
    /// Is > 0 when a specific <see cref="PlexLibrary"/> is requested, and 0 when all are requested.
    /// </summary>
    [QueryParam, BindFrom("plexLibraryId")]
    public int? PlexLibraryId { get; init; }

    /// <summary>The page number (1-indexed).</summary>
    [QueryParam, BindFrom("page")]
    public int? Page { get; init; }

    /// <summary>The number of items per page.</summary>
    [QueryParam, BindFrom("size")]
    public int? PageSize { get; init; }

    [QueryParam, BindFrom("q")]
    public string? Search { get; init; }

    [QueryParam, BindFrom("countryId")]
    public int? CountryId { get; init; }

    [QueryParam, BindFrom("genreId")]
    public int? GenreId { get; init; }

    [QueryParam, BindFrom("roleId")]
    public int? RoleId { get; init; }

    [QueryParam, BindFrom("qualityId")]
    public int? QualityId { get; init; }

    [QueryParam, BindFrom("comparisonState")]
    public PlexMediaComparisonState? ComparisonState { get; init; }

    /// <summary>The sorting expression (e.g., "sortIndex:asc").</summary>
    [QueryParam, BindFrom("sort")]
    public string? Sort { get; init; }

    [QueryParam, BindFrom("filterOfflineMedia")]
    [DefaultValue(false)]
    public bool FilterOfflineMedia { get; init; }

    [QueryParam, BindFrom("filterOwnedMedia")]
    [DefaultValue(false)]
    public bool FilterOwnedMedia { get; init; }
}

public class GetAllMediaByTypeRequestValidator : Validator<GetAllMediaByTypeRequest>
{
    public GetAllMediaByTypeRequestValidator()
    {
        RuleFor(x => x.MediaType)
            .Must(type => type is PlexMediaType.TvShow or PlexMediaType.Movie)
            .WithMessage(x => $"Media type {x.MediaType} is not allowed.");
        RuleFor(x => x.PlexLibraryId).GreaterThanOrEqualTo(0).When(x => x.PlexLibraryId.HasValue);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).When(x => x.Page.HasValue);
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1).When(x => x.PageSize.HasValue);
        RuleFor(x => x.CountryId).GreaterThan(0).When(x => x.CountryId.HasValue);
        RuleFor(x => x.GenreId).GreaterThan(0).When(x => x.GenreId.HasValue);
        RuleFor(x => x.RoleId).GreaterThan(0).When(x => x.RoleId.HasValue);
        RuleFor(x => x.QualityId).GreaterThan(0).When(x => x.QualityId.HasValue);
        RuleFor(x => x.ComparisonState).IsInEnum().When(x => x.ComparisonState.HasValue);
    }
}

public class GetAllMediaByTypeEndpoint : Endpoint<GetAllMediaByTypeRequest, PlexMediaStatisticsDTO>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;

    public GetAllMediaByTypeEndpoint(ILogger log, ICommandExecutor commandExecutor)
    {
        _log = log.ForContext<GetAllMediaByTypeEndpoint>();
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexMediaController);

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PlexMediaStatisticsDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(GetAllMediaByTypeRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var stopWatch = Stopwatch.StartNew();
        var result = await _commandExecutor.Send(
            new GetMediaOverviewCommand(
                new MediaQueryFilter
                {
                    MediaType = req.MediaType,
                    PlexLibraryId = req.PlexLibraryId ?? 0,
                    FilterOfflineMedia = req.FilterOfflineMedia,
                    FilterOwnedMedia = req.FilterOwnedMedia,
                    ComparisonState = req.ComparisonState,
                    Parameters = new FlexQueryParameters
                    {
                        Filter = BuildFilter(req),
                        Sort = req.Sort,
                        Page = req.Page,
                        PageSize = req.PageSize,
                    },
                }
            ),
            ct
        );
        stopWatch.StopAndLog($"GetAllMediaByTypeEndpoint - Retrieved media with filter: {req}");

        result.LogIfFailed();

        await Send.FluentResult(result.IsFailed ? result.ToResult() : Result.Ok(ToStatisticsDTO(result.Value)), ct);
    }

    private static string BuildFilter(GetAllMediaByTypeRequest req)
    {
        var filters = new List<string>();

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var searchTerms = req
                .Search.ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.Length >= 2)
                .Distinct();

            foreach (var term in searchTerms)
                filters.Add($"SearchTitle:contains:{EscapeFlexValue(term)}");
        }

        if (req.CountryId is > 0)
            filters.Add($"Countries:any:Id:eq:{req.CountryId}");

        if (req.RoleId is > 0)
            filters.Add($"Actors:any:Id:eq:{req.RoleId}");

        if (req.GenreId is > 0)
            filters.Add($"Genres:any:Id:eq:{req.GenreId}");

        if (req.QualityId is > 0)
            filters.Add($"MediaDataList:any:Quality:eq:{req.QualityId.Value.ToVideoQuality()}");

        return filters.Count == 0 ? string.Empty : string.Join('&', filters);
    }

    private static string EscapeFlexValue(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace(":", "\\:").Replace("&", "\\&");

    public static PlexMediaStatisticsDTO ToStatisticsDTO(PagedMediaQueryResult source) =>
        new()
        {
            QueryHash = source.QueryHash,
            Page = source.Page,
            PageSize = source.PageSize,
            MovieCount = source.MovieCount,
            TvShowCount = source.TvShowCount,
            SeasonCount = source.SeasonCount,
            EpisodeCount = source.EpisodeCount,
            TotalMovieCount = source.TotalMovieCount,
            TotalTvShowCount = source.TotalTvShowCount,
            TotalSeasonCount = source.TotalSeasonCount,
            TotalEpisodeCount = source.TotalEpisodeCount,
            MediaSize = source.MediaSize,
            TotalMediaSize = source.TotalMediaSize,
            MediaCount = source.MediaCount,
            MediaList = source.Items,
            NavigationIndexes = source.NavigationIndexes,
            Roles = source.Roles,
            Countries = source.Countries,
            Genres = source.Genres,
            Qualities = source.Qualities,
            TotalCount = source.TotalCount,
        };
}
