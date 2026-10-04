using FlexQuery.NET.Models;

namespace Reaparr.Application;

internal static class MediaOverviewExtensions
{
    public static (string Field, bool Descending)? ResolveSort(this QueryOptions options)
    {
        if (options.Sort.Count == 0)
            return (nameof(BasePlexMedia.SortIndex), false);

        if (options.Sort.Count != 1)
            return null;

        var sort = options.Sort[0];
        return sort.Field.ToLowerInvariant() switch
        {
            "sortindex" or "title" or "sorttitle" or "searchtitle" => (
                nameof(BasePlexMedia.SortIndex),
                sort.Descending
            ),
            "year" => (nameof(BasePlexMedia.Year), sort.Descending),
            "addedat" => (nameof(BasePlexMedia.AddedAt), sort.Descending),
            "updatedat" => (nameof(BasePlexMedia.UpdatedAt), sort.Descending),
            "duration" => (nameof(BasePlexMedia.Duration), sort.Descending),
            "mediasize" => (nameof(BasePlexMedia.MediaSize), sort.Descending),
            "quality" => ("Quality", sort.Descending),
            _ => null,
        };
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

    public static PagedMediaQueryResult CreateEmptyPage(MediaQueryFilter filter) => new()
    {
        QueryHash = filter.QueryHash,
        Page = filter.Page,
        PageSize = filter.PageSize,
    };

    public static IQueryable<TSnapshot> ApplyOrder<TSnapshot>(
        this IQueryable<TSnapshot> query,
        string field,
        bool descending
    ) where TSnapshot : BaseMediaOverviewSnapshot => field switch
    {
        nameof(BasePlexMedia.Year) => descending ? query.OrderByDescending(x => x.YearRank) : query.OrderBy(x => x.YearRank),
        nameof(BasePlexMedia.AddedAt) => descending ? query.OrderByDescending(x => x.AddedAtRank) : query.OrderBy(x => x.AddedAtRank),
        nameof(BasePlexMedia.UpdatedAt) => descending ? query.OrderByDescending(x => x.UpdatedAtRank) : query.OrderBy(x => x.UpdatedAtRank),
        nameof(BasePlexMedia.Duration) => descending ? query.OrderByDescending(x => x.DurationRank) : query.OrderBy(x => x.DurationRank),
        nameof(BasePlexMedia.MediaSize) => descending ? query.OrderByDescending(x => x.MediaSizeRank) : query.OrderBy(x => x.MediaSizeRank),
        _ => descending ? query.OrderByDescending(x => x.TitleRank) : query.OrderBy(x => x.TitleRank),
    };

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
