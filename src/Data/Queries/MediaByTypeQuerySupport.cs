using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;

namespace Reaparr.Data;

internal static class MediaByTypeQuerySupport
{
    private static readonly IReadOnlyDictionary<SortType, Func<PlexMediaSlimDTO, int>> _intSelectors = new Dictionary<
        SortType,
        Func<PlexMediaSlimDTO, int>
    >
    {
        [SortType.Year] = static item => item.Year,
        [SortType.Duration] = static item => item.Duration,
        [SortType.ComparisonState] = static item => item.ComparisonId,
        [SortType.VideoQuality] = item =>
            item.Qualities.Any() ? item.Qualities.Max(quality => quality.VideoQuality.ToId()) : 0,
        [SortType.AudioQuality] = item =>
            item.Qualities.Any() ? item.Qualities.Max(quality => (int)quality.AudioQuality) : 0,
    };

    private static readonly IReadOnlyDictionary<SortType, Func<PlexMediaSlimDTO, DateTime?>> _dateSelectors =
        new Dictionary<SortType, Func<PlexMediaSlimDTO, DateTime?>>
        {
            [SortType.AddedAt] = static item => item.AddedAt,
            [SortType.UpdatedAt] = static item => item.UpdatedAt,
        };

    internal static PagedMediaQueryResult CreateResponse(MediaQueryFilter filter) =>
        new()
        {
            QueryHash = filter.QueryHash,
            Page = Math.Max(filter.Parameters.Page ?? 1, 1),
            PageSize = filter.PageSize,
        };

    internal static async Task<Result<List<PlexMediaSlimDTO>>> LoadComparisonCandidatesAsync(
        IQueryable<PlexMediaSlimDTO> query,
        ICommandExecutor executor,
        MediaQueryFilter filter,
        QueryOptions options,
        CancellationToken ct
    )
    {
        var candidates = await query.ToListAsync(ct);
        Result ret;
        if (candidates.Count == 0)
            ret = Result.Ok();
        else
        {
            ret = await executor.Send(
                new ApplyComparisonStateCommand(
                    candidates,
                    filter.MediaType,
                    filter.PlexLibraryId > 0 ? filter.PlexLibraryId : null
                ),
                ct
            );
        }

        var projection = ret;
        if (projection.IsCancelledOrFailed)
            return projection.ToResult<List<PlexMediaSlimDTO>>();

        if (filter.ComparisonState is PlexMediaComparisonState comparisonState)
            candidates.RemoveAll(item => item.ComparisonId != comparisonState.ToComparisonId());

        if (!options.Sort.Any(sort => sort.Field.ToSortType() == SortType.ComparisonState))
            return Result.Ok(candidates);

        var sorts = options
            .Sort.Select(sort => new Sort(
                sort.Field.ToSortType(),
                sort.Descending ? SortDirection.Desc : SortDirection.Asc
            ))
            .ToArray();
        return Result.Ok(SortComparisonCandidates(candidates, sorts, filter.PlexLibraryId == 0));
    }

    internal static void SetComparisonSummary(
        PagedMediaQueryResult response,
        IReadOnlyList<PlexMediaSlimDTO> candidates,
        IReadOnlyList<SortNode> sorts,
        bool includeQuality
    )
    {
        var sortType = sorts.Count == 0 ? SortType.Title : sorts[0].Field.ToSortType();
        response.TotalCount = candidates.Count;
        response.MediaSize = candidates.Sum(item => item.MediaSize);
        response.TotalMediaSize = response.MediaSize;
        response.NavigationIndexes = MediaNavigationIndexBuilder.Build(
            candidates.Select(item =>
                (MediaNavigationIndexRow)
                    new()
                    {
                        SearchTitle = item.SearchTitle,
                        Year = item.Year,
                        QualityValue =
                            sortType == SortType.AudioQuality
                                ? _intSelectors[SortType.AudioQuality](item)
                                : includeQuality && item.Qualities.Count > 0
                                    ? item.Qualities.Max(quality => quality.VideoQuality.ToId())
                                    : null,
                        Duration = item.Duration,
                        AddedAt = item.AddedAt,
                        UpdatedAt = item.UpdatedAt,
                        MediaSize = item.MediaSize,
                    }
            ),
            sortType
        );
    }

    internal static void SetComparisonPage(
        PagedMediaQueryResult response,
        List<PlexMediaSlimDTO> pageCandidates,
        (List<int> Roles, List<int> Countries, List<int> Genres) metadata,
        bool includeQuality
    )
    {
        response.Items = pageCandidates;
        response.Roles = metadata.Roles;
        response.Countries = metadata.Countries;
        response.Genres = metadata.Genres;
        if (includeQuality)
        {
            response.Qualities = pageCandidates
                .SelectMany(item => item.Qualities)
                .Select(quality => quality.VideoQuality.ToId())
                .Distinct()
                .Order()
                .ToList();
        }
        else
        {
            response.Qualities = [];
        }
    }

    private static List<PlexMediaSlimDTO> SortComparisonCandidates(
        List<PlexMediaSlimDTO> candidates,
        IReadOnlyList<Sort> requestedSort,
        bool allLibraries
    )
    {
        IEnumerable<PlexMediaSlimDTO> ordered = candidates;
        foreach (var sort in requestedSort)
            ordered = ApplyComparisonOrder(ordered, sort, allLibraries);

        if (requestedSort.All(sort => sort.Field != SortType.Title))
        {
            if (allLibraries)
                ordered = ApplyOrder(ordered, static item => item.SearchTitle, false, StringComparer.Ordinal);
            else
                ordered = ApplyOrder(ordered, static item => item.SortIndex, false);
        }

        return ApplyOrder(ordered, static item => item.Id, false).ToList();
    }

    private static IEnumerable<PlexMediaSlimDTO> ApplyComparisonOrder(
        IEnumerable<PlexMediaSlimDTO> source,
        Sort sort,
        bool allLibraries
    )
    {
        var descending = sort.Direction == SortDirection.Desc;
        if (sort.Field == SortType.Title)
        {
            if (allLibraries)
                return ApplyOrder(source, static item => item.SearchTitle, descending, StringComparer.Ordinal);

            return ApplyOrder(source, static item => item.SortIndex, descending);
        }

        if (_intSelectors.TryGetValue(sort.Field, out var intSelector))
            return ApplyOrder(source, intSelector, descending);
        if (_dateSelectors.TryGetValue(sort.Field, out var dateSelector))
            return ApplyOrder(source, dateSelector, descending);
        if (sort.Field == SortType.MediaSize)
            return ApplyOrder(source, static item => item.MediaSize, descending);

        throw new ArgumentOutOfRangeException(nameof(sort), sort.Field, null);
    }

    private static IOrderedEnumerable<PlexMediaSlimDTO> ApplyOrder<TKey>(
        IEnumerable<PlexMediaSlimDTO> source,
        Func<PlexMediaSlimDTO, TKey> selector,
        bool descending,
        IComparer<TKey>? comparer = null
    )
    {
        comparer ??= Comparer<TKey>.Default;
        if (source is IOrderedEnumerable<PlexMediaSlimDTO> ordered)
            return descending ? ordered.ThenByDescending(selector, comparer) : ordered.ThenBy(selector, comparer);

        return descending ? source.OrderByDescending(selector, comparer) : source.OrderBy(selector, comparer);
    }
}
