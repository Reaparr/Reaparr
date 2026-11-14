using FlexQuery.NET.Models;

namespace Reaparr.Application;

public class GetAllMediaByTypeRequestValidator : Validator<GetAllMediaByTypeRequest>
{
    public GetAllMediaByTypeRequestValidator()
    {
        RuleFor(x => x.MediaType)
            .Must(type =>
                type
                    is PlexMediaType.TvShow
                        or PlexMediaType.Movie
                        or PlexMediaType.MusicArtist
                        or PlexMediaType.PhotoAlbum
                        or PlexMediaType.OtherVideos
            )
            .WithMessage(x => $"Media type {x.MediaType} is not allowed.");
        RuleFor(x => x.PlexLibraryId).GreaterThanOrEqualTo(0).When(x => x.PlexLibraryId.HasValue);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).When(x => x.Page.HasValue);
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1).When(x => x.PageSize.HasValue);
        RuleFor(x => x.CountryId).GreaterThan(0).When(x => x.CountryId.HasValue);
        RuleFor(x => x.GenreId).GreaterThan(0).When(x => x.GenreId.HasValue);
        RuleFor(x => x.RoleId).GreaterThan(0).When(x => x.RoleId.HasValue);
        RuleFor(x => x.QualityId).GreaterThan(0).When(x => x.QualityId.HasValue);
        RuleFor(x => x.CountryId).Null().When(x => x.MediaType == PlexMediaType.PhotoAlbum);
        RuleFor(x => x.RoleId).Null().When(x => x.MediaType == PlexMediaType.PhotoAlbum);
        RuleFor(x => x.GenreId).Null().When(x => x.MediaType == PlexMediaType.PhotoAlbum);
        RuleFor(x => x.QualityId)
            .Null()
            .When(x => x.MediaType is PlexMediaType.PhotoAlbum or PlexMediaType.MusicArtist);
        RuleFor(x => x.ComparisonState).IsInEnum().When(x => x.ComparisonState.HasValue);
        RuleFor(x => x.ComparisonState).Null().When(x => !x.MediaType.SupportsComparison());
        RuleFor(x => x.ComparisonState)
            .Must(x =>
                x
                    is null
                        or PlexMediaComparisonState.NotCompared
                        or PlexMediaComparisonState.Pending
                        or PlexMediaComparisonState.Owned
                        or PlexMediaComparisonState.Missing
                        or PlexMediaComparisonState.Partial
            )
            .When(x => x.MediaType == PlexMediaType.MusicArtist);
        RuleFor(x => x.Sort)
            .Must((request, sort) => sort.IsSupportedSort(request.MediaType))
            .WithMessage("Sort fields must be supported media sort types.");
    }
}

public class GetAllMediaByTypeEndpoint : Endpoint<GetAllMediaByTypeRequest, PlexMediaStatisticsDTO>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ITelemetry _telemetry;

    public GetAllMediaByTypeEndpoint(ILogger log, ICommandExecutor commandExecutor, ITelemetry telemetry)
    {
        _log = log.ForContext<GetAllMediaByTypeEndpoint>();
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
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
        var filter = BuildFilter(req);
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
                        Filter = filter,
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

        var statisticsResult = result.IsSuccess
            ? Result.Ok(ToStatisticsDTO(result.Value))
            : result.ToResult<PlexMediaStatisticsDTO>();
        _telemetry.MediaQueryFinished(req, filter, statisticsResult);

        await Send.FluentResult(statisticsResult, ct);
    }

    private static string BuildFilter(GetAllMediaByTypeRequest req)
    {
        var filters = new List<string>();

        var x = string.IsNullOrWhiteSpace(req.Search)
            ? []
            : req
                .Search.ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.Length >= 2)
                .Distinct()
                .ToArray();

        foreach (var term in x)
            filters.Add($"SearchTitle:contains:{EscapeFlexValue(term)}");

        if (req.CountryId is > 0)
            filters.Add($"Countries:any:Id:eq:{req.CountryId}");

        if (req.RoleId is > 0)
            filters.Add($"Actors:any:Id:eq:{req.RoleId}");

        if (req.GenreId is > 0)
            filters.Add($"Genres:any:Id:eq:{req.GenreId}");

        if (req.QualityId is > 0)
        {
            var collection =
                req.MediaType == PlexMediaType.TvShow ? nameof(PlexTvShow.Qualities) : nameof(PlexMovie.MediaDataList);
            filters.Add(
                $"{collection}:any:{nameof(BasePlexMediaQuality.VideoQuality)}:eq:{req.QualityId.Value.ToVideoQuality()}"
            );
        }

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
