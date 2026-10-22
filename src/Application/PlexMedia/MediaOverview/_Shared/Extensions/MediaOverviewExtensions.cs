using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;

namespace Reaparr.Application;

internal static class MediaOverviewExtensions
{
    public static Sort? ResolveSort(this QueryOptions options)
    {
        if (options.Sort.Count == 0)
            return new Sort(SortType.Title, SortDirection.Asc);

        if (options.Sort.Count != 1)
            return null;

        var sort = options.Sort[0];
        return SortTypeExtensions.TryParse(sort.Field, out var sortType)
            && sortType is not SortType.ComparisonState
            ? new Sort(sortType, sort.Descending ? SortDirection.Desc : SortDirection.Asc)
            : null;
    }

    public static bool IsSupportedSort(this string? value, params ReadOnlySpan<SortType> supportedSorts)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var options = QueryOptionsParser.Parse(new FlexQueryParameters { Sort = value });
        var sort = options.ResolveSort();
        if (options.Sort.Count != 1 || sort is null)
            return false;

        foreach (var supportedSort in supportedSorts)
            if (supportedSort == sort.Value.Field)
                return true;

        return false;
    }

    public static QueryOptions BuildSortOptions(
        Sort sort,
        string titleField,
        string idField,
        string? mediaSizeField = null
    )
    {
        var field = sort.Field switch
        {
            SortType.Title => titleField,
            SortType.MediaSize when mediaSizeField is not null => mediaSizeField,
            SortType.MediaSize => throw new ArgumentOutOfRangeException(nameof(mediaSizeField)),
            _ => sort.Field.ToQueryField(),
        };
        var options = new QueryOptions();
        options.Sort.Add(new SortNode { Field = field, Descending = sort.Direction == SortDirection.Desc });
        options.Sort.Add(new SortNode { Field = idField, Descending = sort.Direction == SortDirection.Desc });
        return options;
    }

    public static async Task<IReadOnlyCollection<int>> ResolveAllowedLibraryIdsAsync(
        this IReaparrDbContext context,
        MediaQueryFilter filter,
        CancellationToken cancellationToken
    ) => await context.ResolveAllowedLibraryIdsAsync(filter, filter.MediaType, cancellationToken);

    public static async Task<IReadOnlyCollection<int>> ResolveAllowedLibraryIdsAsync(
        this IReaparrDbContext context,
        MediaQueryFilter filter,
        PlexMediaType libraryType,
        CancellationToken cancellationToken
    )
    {
        if (filter.PlexLibraryId > 0)
        {
            return await context.PlexLibraries.IgnoreIsEnabledFilter()
                .Where(x => x.Id == filter.PlexLibraryId && x.IsEnabled && x.Type == libraryType)
                .Select(x => x.Id).ToListAsync(cancellationToken);
        }

        var libraries = context.PlexLibraries.Where(x =>
            x.Type == libraryType
            && (!context.PlexAccounts.Any() || (x.PlexServer!.PlexAccountServers.Any() && x.PlexAccountLibraries.Any())));
        if (filter.FilterOwnedMedia)
            libraries = libraries.WhereIsNotOwned();
        if (filter.FilterOfflineMedia)
            libraries = libraries.Where(x => context.PlexServerStatuses.Any(status => status.PlexServerId == x.PlexServerId && status.IsSuccessful));
        return await libraries.Select(x => x.Id).ToListAsync(cancellationToken);
    }

    public static List<PlexMediaSlimDTO> RestorePageOrder(
        this IEnumerable<PlexMediaSlimDTO> items,
        IReadOnlyList<int> ids
    )
    {
        var order = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        return items.OrderBy(x => order[x.Id]).ToList();
    }

    public static async Task<PagedMediaQueryResult> CreatePageResultAsync(
        this IReaparrDbContext context,
        MediaQueryFilter filter,
        QueryOptions options,
        IReadOnlyCollection<int> allowedLibraryIds,
        List<PlexMediaSlimDTO> items,
        int totalCount,
        long mediaSize,
        CancellationToken cancellationToken
    )
    {
        var libraries = await context
            .PlexLibraries.Where(x => allowedLibraryIds.Contains(x.Id))
            .Select(x => new
            {
                x.MovieCount,
                x.TvShowCount,
                x.SeasonCount,
                x.EpisodeCount,
            })
            .ToListAsync(cancellationToken);
        var hasUserFilters = options.Filter is not null || filter.ComparisonState.HasValue;
        var totalSeasonCount = libraries.Sum(x => x.SeasonCount);
        var totalEpisodeCount = libraries.Sum(x => x.EpisodeCount);
        var result = new PagedMediaQueryResult
        {
            QueryHash = filter.QueryHash,
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = totalCount,
            MediaCount = totalCount,
            MovieCount = filter.MediaType == PlexMediaType.Movie ? totalCount : 0,
            TvShowCount = filter.MediaType == PlexMediaType.TvShow ? totalCount : 0,
            SeasonCount = filter.MediaType == PlexMediaType.TvShow
                ? (!hasUserFilters ? totalSeasonCount : items.Sum(x => x.ChildCount))
                : 0,
            EpisodeCount = filter.MediaType == PlexMediaType.TvShow
                ? (!hasUserFilters ? totalEpisodeCount : items.Sum(x => x.GrandChildCount))
                : 0,
            TotalMovieCount = filter.MediaType == PlexMediaType.Movie ? totalCount : libraries.Sum(x => x.MovieCount),
            TotalTvShowCount =
                filter.MediaType == PlexMediaType.TvShow ? totalCount : libraries.Sum(x => x.TvShowCount),
            TotalSeasonCount = totalSeasonCount,
            TotalEpisodeCount = totalEpisodeCount,
            MediaSize = mediaSize,
            TotalMediaSize = mediaSize,
            Items = items,
        };

        for (var index = 0; index < result.Items.Count; index++)
            result.Items[index].SortIndex = ((filter.Page - 1) * filter.PageSize) + index + 1;

        return result;
    }
}
