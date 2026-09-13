using FlexQuery.NET;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using Reaparr.Application.Contracts;

namespace Reaparr.Data;

public class GetMediaByTypeCommandValidator : AbstractValidator<GetMediaByTypeCommand>
{
    public GetMediaByTypeCommandValidator()
    {
        RuleFor(x => x).NotNull();

        RuleFor(x => x.Filter).NotNull();

        RuleFor(x => x.Filter.MediaType)
            .Must(mediaType => mediaType is not PlexMediaType.None and not PlexMediaType.Unknown)
            .When(x => x.Filter.PlexLibraryId == 0)
            .WithMessage("MediaType is required when PlexLibraryId is 0.");
    }
}

public class GetMediaByTypeCommandHandler : ICommandHandler<GetMediaByTypeCommand, Result<PagedMediaQueryResult>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public GetMediaByTypeCommandHandler(IReaparrDbContext dbContext, ICommandExecutor commandExecutor)
    {
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<PagedMediaQueryResult>> ExecuteAsync(GetMediaByTypeCommand command, CancellationToken ct)
    {
        var filter = command.Filter;
        var page = Math.Max(filter.Parameters.Page ?? 1, 1);
        var pageSize = Math.Max(filter.Parameters.PageSize ?? 0, 0);
        var response = new PagedMediaQueryResult
        {
            QueryHash = filter.QueryHash,
            Page = page,
            PageSize = pageSize,
        };
        var plexLibraryId = filter.PlexLibraryId;

        // For a specific library request, verify the library is enabled.
        if (plexLibraryId > 0)
        {
            var requestedLibrary = await _dbContext
                .PlexLibraries.IgnoreIsEnabledFilter()
                .Select(x => new { x.Id, x.IsEnabled })
                .FirstOrDefaultAsync(x => x.Id == plexLibraryId, ct);

            if (requestedLibrary is null || !requestedLibrary.IsEnabled)
                return Result.Ok(response);
        }

        var allowedPlexLibraryIds = new List<int>();

        if (plexLibraryId > 0)
        {
            allowedPlexLibraryIds.Add(plexLibraryId);
        }
        else
        {
            var hasPlexAccounts = await _dbContext.PlexAccounts.AnyAsync(ct);

            // Get only enabled servers and libraries that still have access through at least one Plex account.
            var serverList = await _dbContext
                .PlexServers.Where(server => !hasPlexAccounts || server.PlexAccountServers.Any())
                .Select(server => new
                {
                    server.Id,
                    PlexLibraryIds = server
                        .PlexLibraries.Where(library => !hasPlexAccounts || library.PlexAccountLibraries.Any())
                        .Select(library => library.Id)
                        .ToList(),
                })
                .ToListAsync(ct);

            allowedPlexLibraryIds = serverList.SelectMany(x => x.PlexLibraryIds).ToList();
            if (filter.FilterOwnedMedia)
            {
                var ownedPlexLibraryIds = await _dbContext
                    .PlexLibraries.WhereIsOwned()
                    .Select(x => x.Id)
                    .ToListAsync(ct);

                allowedPlexLibraryIds.RemoveAll(ownedPlexLibraryIds.Contains);
            }

            if (filter.FilterOfflineMedia)
            {
                foreach (var server in serverList)
                {
                    var isServerOnline = await _dbContext.IsServerOnline(server.Id);
                    if (!isServerOnline)
                    {
                        allowedPlexLibraryIds.RemoveAll(x => server.PlexLibraryIds.Contains(x));
                    }
                }
            }
        }

        var options = QueryOptionsParser.Parse(filter.Parameters);
        options.Paging.Disabled = pageSize == 0;

        if (!allowedPlexLibraryIds.Any())
            return Result.Ok(response);

        var hasUserFilters = options.HasFiltersApplied() || filter.ComparisonState.HasValue;
        options = WithServerLibraryScope(options, allowedPlexLibraryIds, plexLibraryId);

        ApplyDefaultMediaSort(options, plexLibraryId);
        NormalizeAllLibrarySort(options, plexLibraryId);

        // TODO Deduplicate
        switch (filter.MediaType)
        {
            case PlexMediaType.Movie:
            {
                var movieQuery = _dbContext
                    .PlexMovies.IncludeMediaData()
                    .Include(x => x.Actors)
                    .Include(x => x.Countries)
                    .Include(x => x.Genres)
                    .ApplyFilter(options)
                    .ApplySort(options);

                if (filter.ComparisonState.HasValue)
                {
                    var movies = await movieQuery.ToListAsync(ct);
                    var movieDtos = movies.Select(x => x.ToSlimDTO()).ToList();
                    var movieComparisonResult = await ApplyComparisonStateAsync(
                        movieDtos,
                        plexLibraryId,
                        PlexMediaType.Movie,
                        ct
                    );
                    if (movieComparisonResult.IsFailed)
                        return Result.Fail<PagedMediaQueryResult>(movieComparisonResult.Errors);

                    var filteredDtos = movieDtos
                        .Where(x => x.ComparisonId == filter.ComparisonState.Value.ToComparisonId())
                        .ToList();
                    var filteredIds = filteredDtos.Select(x => x.Id).ToHashSet();
                    var filteredMovies = movies.Where(x => filteredIds.Contains(x.Id)).ToList();

                    SetNavigationIndexes(
                        response,
                        filteredMovies.Select(x => new MediaNavigationIndexRow(
                            x.SearchTitle,
                            x.Year,
                            (int?)x.Quality,
                            x.Duration,
                            x.AddedAt,
                            x.UpdatedAt,
                            x.MediaSize
                        )),
                        options
                    );

                    response.TotalCount = filteredDtos.Count;
                    response.MediaSize = filteredDtos.Sum(x => x.MediaSize);
                    response.TotalMediaSize = response.MediaSize;

                    var pagedDtos = ApplyDtoPaging(filteredDtos, page, pageSize);
                    var pagedIds = pagedDtos.Select(x => x.Id).ToHashSet();
                    var pagedMovies = filteredMovies.Where(x => pagedIds.Contains(x.Id)).ToList();

                    response.Items = pagedDtos;
                    response.Roles.AddRange(
                        pagedMovies.SelectMany(x => x.Actors).Select(x => x.Id).Distinct().OrderBy(x => x)
                    );
                    response.Countries.AddRange(
                        pagedMovies.SelectMany(x => x.Countries).Select(x => x.Id).Distinct().OrderBy(x => x)
                    );
                    response.Genres.AddRange(
                        pagedMovies.SelectMany(x => x.Genres).Select(x => x.Id).Distinct().OrderBy(x => x)
                    );
                    response.Qualities.AddRange(
                        pagedMovies
                            .SelectMany(x => x.MediaDataList)
                            .Select(x => x.Quality.ToId())
                            .Distinct()
                            .OrderBy(x => x)
                    );
                }
                else
                {
                    response.TotalCount = await movieQuery.CountAsync(ct);
                    response.MediaSize = await movieQuery.SumAsync(x => x.MediaSize, ct);
                    response.TotalMediaSize = response.MediaSize;

                    await SetNavigationIndexes(
                        response,
                        movieQuery.Select(x => new MediaNavigationIndexRow(
                            x.SearchTitle,
                            x.Year,
                            (int?)x.Quality,
                            x.Duration,
                            x.AddedAt,
                            x.UpdatedAt,
                            x.MediaSize
                        )),
                        options,
                        ct
                    );

                    var movies = await movieQuery.ApplyPaging(options).ToListAsync(ct);

                    var movieDtos = movies.Select(x => x.ToSlimDTO()).ToList();
                    var movieComparisonResult = await ApplyComparisonStateAsync(
                        movieDtos,
                        plexLibraryId,
                        PlexMediaType.Movie,
                        ct
                    );
                    if (movieComparisonResult.IsFailed)
                        return Result.Fail<PagedMediaQueryResult>(movieComparisonResult.Errors);

                    response.Items = movieDtos;
                    response.Roles.AddRange(
                        movies.SelectMany(x => x.Actors).Select(x => x.Id).Distinct().OrderBy(x => x)
                    );
                    response.Countries.AddRange(
                        movies.SelectMany(x => x.Countries).Select(x => x.Id).Distinct().OrderBy(x => x)
                    );
                    response.Genres.AddRange(
                        movies.SelectMany(x => x.Genres).Select(x => x.Id).Distinct().OrderBy(x => x)
                    );
                    response.Qualities.AddRange(
                        movies.SelectMany(x => x.MediaDataList).Select(x => x.Quality.ToId()).Distinct().OrderBy(x => x)
                    );
                }

                break;
            }
            case PlexMediaType.TvShow:
            {
                var tvShowQuery = _dbContext
                    .PlexTvShows.Include(x => x.Qualities)
                    .Include(x => x.Actors)
                    .Include(x => x.Countries)
                    .Include(x => x.Genres)
                    .ApplyFilter(options)
                    .ApplySort(options);

                if (filter.ComparisonState.HasValue)
                {
                    var tvShows = await tvShowQuery.ToListAsync(ct);
                    var tvShowDtos = tvShows.Select(x => x.ToSlimDTOMapper()).ToList();
                    var tvShowComparisonResult = await ApplyComparisonStateAsync(
                        tvShowDtos,
                        plexLibraryId,
                        PlexMediaType.TvShow,
                        ct
                    );
                    if (tvShowComparisonResult.IsFailed)
                        return Result.Fail<PagedMediaQueryResult>(tvShowComparisonResult.Errors);

                    var filteredDtos = tvShowDtos
                        .Where(x => x.ComparisonId == filter.ComparisonState.Value.ToComparisonId())
                        .ToList();
                    var filteredIds = filteredDtos.Select(x => x.Id).ToHashSet();
                    var filteredTvShows = tvShows.Where(x => filteredIds.Contains(x.Id)).ToList();

                    SetNavigationIndexes(
                        response,
                        filteredTvShows.Select(x => new MediaNavigationIndexRow(
                            x.SearchTitle,
                            x.Year,
                            (int?)x.Quality,
                            x.Duration,
                            x.AddedAt,
                            x.UpdatedAt,
                            x.MediaSize
                        )),
                        options
                    );

                    response.TotalCount = filteredDtos.Count;
                    response.MediaSize = filteredDtos.Sum(x => x.MediaSize);
                    response.TotalMediaSize = response.MediaSize;

                    var pagedDtos = ApplyDtoPaging(filteredDtos, page, pageSize);
                    var pagedIds = pagedDtos.Select(x => x.Id).ToHashSet();
                    var pagedTvShows = filteredTvShows.Where(x => pagedIds.Contains(x.Id)).ToList();

                    response.Items = pagedDtos;
                    response.Roles.AddRange(
                        pagedTvShows.SelectMany(x => x.Actors).Select(x => x.Id).Distinct().OrderBy(x => x)
                    );
                    response.Countries.AddRange(
                        pagedTvShows.SelectMany(x => x.Countries).Select(x => x.Id).Distinct().OrderBy(x => x)
                    );
                    response.Genres.AddRange(
                        pagedTvShows.SelectMany(x => x.Genres).Select(x => x.Id).Distinct().OrderBy(x => x)
                    );
                    response.Qualities.AddRange(
                        pagedTvShows
                            .SelectMany(x => x.Qualities)
                            .Select(x => x.Quality.ToId())
                            .Distinct()
                            .OrderBy(x => x)
                    );
                }
                else
                {
                    response.TotalCount = await tvShowQuery.CountAsync(ct);
                    response.MediaSize = await tvShowQuery.SumAsync(x => x.MediaSize, ct);
                    response.TotalMediaSize = response.MediaSize;

                    await SetNavigationIndexes(
                        response,
                        tvShowQuery.Select(x => new MediaNavigationIndexRow(
                            x.SearchTitle,
                            x.Year,
                            (int?)x.Quality,
                            x.Duration,
                            x.AddedAt,
                            x.UpdatedAt,
                            x.MediaSize
                        )),
                        options,
                        ct
                    );

                    var tvShows = await tvShowQuery.ApplyPaging(options).ToListAsync(ct);

                    var tvShowDtos = tvShows.Select(x => x.ToSlimDTOMapper()).ToList();
                    var tvShowComparisonResult = await ApplyComparisonStateAsync(
                        tvShowDtos,
                        plexLibraryId,
                        PlexMediaType.TvShow,
                        ct
                    );
                    if (tvShowComparisonResult.IsFailed)
                        return Result.Fail<PagedMediaQueryResult>(tvShowComparisonResult.Errors);

                    response.Items = tvShowDtos;
                    response.Roles.AddRange(
                        tvShows.SelectMany(x => x.Actors).Select(x => x.Id).Distinct().OrderBy(x => x)
                    );
                    response.Countries.AddRange(
                        tvShows.SelectMany(x => x.Countries).Select(x => x.Id).Distinct().OrderBy(x => x)
                    );
                    response.Genres.AddRange(
                        tvShows.SelectMany(x => x.Genres).Select(x => x.Id).Distinct().OrderBy(x => x)
                    );
                    response.Qualities.AddRange(
                        tvShows.SelectMany(x => x.Qualities).Select(x => x.Quality.ToId()).Distinct().OrderBy(x => x)
                    );
                }

                break;
            }
            default:
                return Result.Fail(
                    "Type {FilterMediaType} is not supported for retrieving the PlexMedia data by library id",
                    filter.MediaType
                );
        }

        var effectivePageSize = response.PageSize > 0 ? response.PageSize : response.Items.Count;
        var offset = (response.Page - 1) * effectivePageSize;

        for (var i = 0; i < response.Items.Count; i++)
        {
            var slimDTO = response.Items[i];

            slimDTO.SortIndex = offset + i + 1;
        }

        await SetCounts(response, hasUserFilters, allowedPlexLibraryIds, filter.MediaType, ct);

        return Result.Ok(response);
    }

    /// <summary>
    /// Filter by allowed <see cref="PlexLibrary"/> based on previous filtering
    /// </summary>
    private QueryOptions WithServerLibraryScope(
        QueryOptions options,
        List<int> allowedPlexLibraryIds,
        int requestedPlexLibraryId
    )
    {
        var scopedLibraryIds =
            requestedPlexLibraryId > 0
                ? allowedPlexLibraryIds.Where(x => x == requestedPlexLibraryId).ToList()
                : allowedPlexLibraryIds;

        if (scopedLibraryIds.Count == 0)
        {
            options.Filter = new FilterGroup
            {
                Logic = LogicOperator.And,
                Filters =
                [
                    new FilterCondition
                    {
                        Field = nameof(BasePlexMedia.PlexLibraryId),
                        Operator = FilterOperators.Equal,
                        Value = int.MinValue.ToString(),
                    },
                ],
            };

            return options;
        }

        var serverFilter = new FilterGroup
        {
            Logic = LogicOperator.And,
            Filters =
            [
                new FilterCondition
                {
                    Field = nameof(BasePlexMedia.PlexLibraryId),
                    Operator = scopedLibraryIds.Count == 1 ? FilterOperators.Equal : FilterOperators.In,
                    Value =
                        scopedLibraryIds.Count == 1
                            ? scopedLibraryIds.First().ToString()
                            : string.Join(',', scopedLibraryIds),
                },
            ],
        };

        if (options.Filter is null)
        {
            options.Filter = serverFilter;
            return options;
        }

        options.Filter = new FilterGroup { Logic = LogicOperator.And, Groups = [options.Filter, serverFilter] };

        return options;
    }

    private async Task SetNavigationIndexes(
        PagedMediaQueryResult response,
        IQueryable<MediaNavigationIndexRow> rows,
        QueryOptions options,
        CancellationToken ct
    )
    {
        SetNavigationIndexes(response, await rows.ToListAsync(ct), options);
    }

    private async Task<Result> ApplyComparisonStateAsync(
        List<PlexMediaSlimDTO> items,
        int plexLibraryId,
        PlexMediaType mediaType,
        CancellationToken ct
    )
    {
        if (items.Count == 0)
            return Result.Ok();

        if (plexLibraryId > 0)
        {
            var result = await _commandExecutor.Send(
                new ApplyComparisonStateCommand(items, plexLibraryId, mediaType),
                ct
            );
            if (result.IsFailed)
                return Result.Fail(result.Errors);

            return Result.Ok();
        }

        foreach (var group in items.Where(x => x.PlexLibraryId > 0).GroupBy(x => x.PlexLibraryId))
        {
            var libraryItems = group.ToList();
            var result = await _commandExecutor.Send(
                new ApplyComparisonStateCommand(libraryItems, group.Key, mediaType),
                ct
            );
            if (result.IsFailed)
                return Result.Fail(result.Errors);
        }

        return Result.Ok();
    }

    private static void SetNavigationIndexes(
        PagedMediaQueryResult response,
        IEnumerable<MediaNavigationIndexRow> rows,
        QueryOptions options
    )
    {
        response.NavigationIndexes = MediaNavigationIndexBuilder.Build(rows, options.Sort.FirstOrDefault()?.Field);
    }

    private static List<PlexMediaSlimDTO> ApplyDtoPaging(List<PlexMediaSlimDTO> items, int page, int pageSize)
    {
        if (pageSize == 0)
            return items;

        return items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
    }

    private void NormalizeAllLibrarySort(QueryOptions options, int plexLibraryId)
    {
        if (plexLibraryId > 0 || !options.Sort.Any())
            return;

        foreach (var sort in options.Sort.Where(x => IsSortIndexField(x.Field)))
            sort.Field = nameof(BasePlexMedia.SearchTitle);
    }

    private static bool IsSortIndexField(string? field) => field is nameof(BasePlexMedia.SortIndex) or "sortIndex";

    private void ApplyDefaultMediaSort(QueryOptions options, int plexLibraryId)
    {
        if (options.Sort.Count > 0 || plexLibraryId == 0)
            return;

        options.Sort.Add(new SortNode { Field = nameof(BasePlexMedia.SortIndex), Descending = false });
    }

    private async Task SetCounts(
        PagedMediaQueryResult response,
        bool hasUserFilters,
        List<int> allowedPlexLibraryIds,
        PlexMediaType mediaType,
        CancellationToken ct
    )
    {
        var allScopedLibraries = await _dbContext
            .PlexLibraries.Where(x => allowedPlexLibraryIds.Contains(x.Id))
            .ToListAsync(ct);

        response.TotalMovieCount =
            mediaType == PlexMediaType.Movie ? response.TotalCount : allScopedLibraries.Sum(x => x.MovieCount);
        response.TotalTvShowCount =
            mediaType == PlexMediaType.TvShow ? response.TotalCount : allScopedLibraries.Sum(x => x.TvShowCount);
        response.TotalSeasonCount = allScopedLibraries.Sum(x => x.SeasonCount);
        response.TotalEpisodeCount = allScopedLibraries.Sum(x => x.EpisodeCount);

        if (hasUserFilters)
        {
            response.MediaCount = response.TotalCount;
            response.MovieCount = mediaType == PlexMediaType.Movie ? response.TotalCount : 0;
            response.TvShowCount = mediaType == PlexMediaType.TvShow ? response.TotalCount : 0;
            response.SeasonCount = response.Items.Where(x => x.Type == PlexMediaType.TvShow).Sum(x => x.ChildCount);
            response.EpisodeCount = response
                .Items.Where(x => x.Type == PlexMediaType.TvShow)
                .Sum(x => x.GrandChildCount);
            return;
        }

        response.MovieCount = mediaType == PlexMediaType.Movie ? response.TotalCount : 0;
        response.TvShowCount = mediaType == PlexMediaType.TvShow ? response.TotalCount : 0;
        response.SeasonCount = mediaType == PlexMediaType.TvShow ? response.TotalSeasonCount : 0;
        response.EpisodeCount = mediaType == PlexMediaType.TvShow ? response.TotalEpisodeCount : 0;
        response.MediaCount = response.TotalCount;
    }
}
