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
    )
    {
        if (filter.PlexLibraryId > 0)
        {
            return await context
                .PlexLibraries.IgnoreIsEnabledFilter()
                .Where(x => x.Id == filter.PlexLibraryId && x.IsEnabled && x.Type == filter.MediaType)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
        }

        var libraries = context.PlexLibraries.Where(x => x.Type == filter.MediaType);
        if (await context.PlexAccounts.AnyAsync(cancellationToken))
            libraries = libraries.Where(x => x.PlexServer!.PlexAccountServers.Any() && x.PlexAccountLibraries.Any());

        if (filter.FilterOwnedMedia)
            libraries = libraries.WhereIsNotOwned();

        if (filter.FilterOfflineMedia)
        {
            var onlineServerIds = await context
                .PlexServerStatuses.Where(x => x.IsSuccessful)
                .Select(x => x.PlexServerId)
                .Distinct()
                .ToListAsync(cancellationToken);
            libraries = libraries.Where(x => onlineServerIds.Contains(x.PlexServerId));
        }

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
            SeasonCount =
                filter.MediaType == PlexMediaType.TvShow && !hasUserFilters
                    ? totalSeasonCount
                    : items.Sum(x => x.ChildCount),
            EpisodeCount =
                filter.MediaType == PlexMediaType.TvShow && !hasUserFilters
                    ? totalEpisodeCount
                    : items.Sum(x => x.GrandChildCount),
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
