using FlexQuery.NET;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using static Reaparr.Data.MediaByTypeQuerySupport;

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
        RuleFor(x => x.Filter.ComparisonState).Null().When(x => !x.Filter.MediaType.SupportsComparison());
        RuleFor(x => x.Filter.ComparisonState)
            .Must(x =>
                x
                    is null
                        or PlexMediaComparisonState.NotCompared
                        or PlexMediaComparisonState.Pending
                        or PlexMediaComparisonState.Owned
                        or PlexMediaComparisonState.Missing
                        or PlexMediaComparisonState.Partial
            )
            .When(x => x.Filter.MediaType == PlexMediaType.MusicArtist);
        RuleFor(x => x.Filter.Parameters.Sort)
            .Must((command, sort) => sort.IsSupportedSort(command.Filter.MediaType))
            .WithMessage("Sort contains an unsupported field.")
            .When(x => x.Filter is not null && x.Filter.Parameters is not null);
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
        var allowedLibraryIds = await ResolveAllowedLibraryIdsAsync(filter, ct);
        if (allowedLibraryIds.Count == 0)
            return Result.Ok(CreateResponse(filter));

        var options = QueryOptionsParser.Parse(filter.Parameters);
        options.Paging.Disabled = filter.PageSize == 0;
        options.Paging.PageSize = filter.PageSize;
        var hasUserFilters = options.HasFiltersApplied() || filter.ComparisonState.HasValue;
        PrepareQueryOptions(options, filter, allowedLibraryIds);

        var result = await DispatchAsync(filter, options, ct);
        if (result.IsCancelledOrFailed)
            return result;

        var response = result.Value;
        var offset = response.PageSize > 0 ? (response.Page - 1) * response.PageSize : 0;
        for (var index = 0; index < response.Items.Count; index++)
            response.Items[index].SortIndex = offset + index + 1;

        await SetCounts(response, hasUserFilters, allowedLibraryIds, filter.MediaType, ct);
        return result;
    }

    private Task<Result<PagedMediaQueryResult>> DispatchAsync(
        MediaQueryFilter filter,
        QueryOptions options,
        CancellationToken ct
    ) =>
        filter.MediaType switch
        {
            PlexMediaType.Movie => _commandExecutor.Send(new GetMovieMediaByTypeCommand(filter, options), ct),
            PlexMediaType.TvShow => _commandExecutor.Send(new GetTvShowMediaByTypeCommand(filter, options), ct),
            PlexMediaType.MusicArtist => _commandExecutor.Send(
                new GetMusicArtistMediaByTypeCommand(filter, options),
                ct
            ),
            PlexMediaType.PhotoAlbum => _commandExecutor.Send(new GetPhotoAlbumMediaByTypeCommand(filter, options), ct),
            PlexMediaType.OtherVideos => _commandExecutor.Send(
                new GetOtherVideoMediaByTypeCommand(filter, options),
                ct
            ),
            _ => Task.FromResult(
                Result.Fail<PagedMediaQueryResult>(
                    $"Type {filter.MediaType} is not supported for retrieving the PlexMedia data by library id"
                )
            ),
        };

    private async Task<List<int>> ResolveAllowedLibraryIdsAsync(MediaQueryFilter filter, CancellationToken ct)
    {
        // Specific-library browsing verifies the library, but deliberately preserves its existing access semantics.
        if (filter.PlexLibraryId > 0)
        {
            var library = await _dbContext
                .PlexLibraries.IgnoreIsEnabledFilter()
                .Select(x => new { x.Id, x.IsEnabled })
                .FirstOrDefaultAsync(x => x.Id == filter.PlexLibraryId, ct);
            return library is not null && library.IsEnabled ? [library.Id] : [];
        }

        var hasAccounts = await _dbContext.PlexAccounts.AnyAsync(ct);
        var servers = await _dbContext
            .PlexServers.Where(server => !hasAccounts || server.PlexAccountServers.Any())
            .Select(server => new
            {
                server.Id,
                LibraryIds = server
                    .PlexLibraries.Where(library => !hasAccounts || library.PlexAccountLibraries.Any())
                    .Select(library => library.Id)
                    .ToList(),
            })
            .ToListAsync(ct);
        var allowedLibraryIds = servers.SelectMany(server => server.LibraryIds).ToList();

        if (filter.FilterOwnedMedia)
        {
            var ownedLibraryIds = await _dbContext
                .PlexLibraries.WhereIsOwned()
                .Select(library => library.Id)
                .ToListAsync(ct);
            allowedLibraryIds.RemoveAll(ownedLibraryIds.Contains);
        }

        if (filter.FilterOfflineMedia)
        {
            foreach (var server in servers)
                if (!await _dbContext.IsServerOnline(server.Id))
                    allowedLibraryIds.RemoveAll(server.LibraryIds.Contains);
        }

        return allowedLibraryIds;
    }

    private static void PrepareQueryOptions(QueryOptions options, MediaQueryFilter filter, List<int> allowedLibraryIds)
    {
        WithServerLibraryScope(options, allowedLibraryIds, filter.PlexLibraryId);
        foreach (var sort in options.Sort)
            sort.Field = sort.Field.ToSortType().ToSortValue();

        if (options.Sort.Count == 0 && filter.PlexLibraryId > 0)
            options.Sort.Add(new SortNode { Field = SortType.Title.ToSortValue(), Descending = false });

        if (filter.PlexLibraryId == 0)
            foreach (var sort in options.Sort)
                if (sort.Field == SortType.Title.ToSortValue())
                    sort.Field = nameof(BasePlexMedia.SearchTitle);
    }

    /// <summary>Restricts query filters to the permitted Plex library scope.</summary>
    private static void WithServerLibraryScope(
        QueryOptions options,
        List<int> allowedLibraryIds,
        int requestedLibraryId
    )
    {
        var scopedIds =
            requestedLibraryId > 0
                ? allowedLibraryIds.Where(id => id == requestedLibraryId).ToList()
                : allowedLibraryIds;
        if (scopedIds.Count == 0)
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
            return;
        }

        var scopeFilter = new FilterGroup
        {
            Logic = LogicOperator.And,
            Filters =
            [
                new FilterCondition
                {
                    Field = nameof(BasePlexMedia.PlexLibraryId),
                    Operator = scopedIds.Count == 1 ? FilterOperators.Equal : FilterOperators.In,
                    Value = scopedIds.Count == 1 ? scopedIds[0].ToString() : string.Join(',', scopedIds),
                },
            ],
        };
        options.Filter = options.Filter is null
            ? scopeFilter
            : new FilterGroup { Logic = LogicOperator.And, Groups = [options.Filter, scopeFilter] };
    }

    private async Task SetCounts(
        PagedMediaQueryResult response,
        bool hasUserFilters,
        List<int> allowedLibraryIds,
        PlexMediaType mediaType,
        CancellationToken ct
    )
    {
        var libraries = await _dbContext
            .PlexLibraries.Where(library => allowedLibraryIds.Contains(library.Id))
            .ToListAsync(ct);
        response.TotalMovieCount =
            mediaType == PlexMediaType.Movie ? response.TotalCount : libraries.Sum(x => x.MovieCount);
        response.TotalTvShowCount =
            mediaType == PlexMediaType.TvShow ? response.TotalCount : libraries.Sum(x => x.TvShowCount);
        response.TotalSeasonCount = libraries.Sum(x => x.SeasonCount);
        response.TotalEpisodeCount = libraries.Sum(x => x.EpisodeCount);
        response.MovieCount = mediaType == PlexMediaType.Movie ? response.TotalCount : 0;
        response.TvShowCount = mediaType == PlexMediaType.TvShow ? response.TotalCount : 0;
        response.MediaCount = response.TotalCount;
        if (hasUserFilters)
        {
            response.SeasonCount = response
                .Items.Where(item => item.Type == PlexMediaType.TvShow)
                .Sum(item => item.ChildCount);
            response.EpisodeCount = response
                .Items.Where(item => item.Type == PlexMediaType.TvShow)
                .Sum(item => item.GrandChildCount);
            return;
        }

        response.SeasonCount = mediaType == PlexMediaType.TvShow ? response.TotalSeasonCount : 0;
        response.EpisodeCount = mediaType == PlexMediaType.TvShow ? response.TotalEpisodeCount : 0;
    }
}
