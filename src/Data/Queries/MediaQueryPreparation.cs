using FlexQuery.NET;
using FlexQuery.NET.Constants;
using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;

namespace Reaparr.Data;

internal static class MediaQueryPreparation
{
    internal static async Task<(QueryOptions Options, List<int> LibraryIds, bool HasUserFilters)> PrepareAsync(
        IReaparrDbContext dbContext,
        MediaQueryFilter filter,
        CancellationToken ct
    )
    {
        var libraryIds = await ResolveAllowedLibraryIdsAsync(dbContext, filter, ct);
        if (libraryIds.Count == 0)
            return (new QueryOptions(), libraryIds, false);

        var options = QueryOptionsParser.Parse(filter.Parameters);
        options.Paging.Disabled = filter.PageSize == 0;
        options.Paging.PageSize = filter.PageSize;
        var hasUserFilters = options.HasFiltersApplied() || filter.ComparisonState.HasValue;
        ApplyLibraryScope(options, libraryIds);
        foreach (var sort in options.Sort)
            sort.Field = sort.Field.ToSortType().ToSortValue();

        if (options.Sort.Count == 0 && filter.PlexLibraryId > 0)
            options.Sort.Add(new SortNode { Field = SortType.Title.ToSortValue(), Descending = false });

        if (filter.PlexLibraryId == 0)
            foreach (var sort in options.Sort)
                if (sort.Field == SortType.Title.ToSortValue())
                    sort.Field = nameof(BasePlexMedia.SearchTitle);

        return (options, libraryIds, hasUserFilters);
    }

    private static async Task<List<int>> ResolveAllowedLibraryIdsAsync(
        IReaparrDbContext dbContext,
        MediaQueryFilter filter,
        CancellationToken ct
    )
    {
        // Specific-library browsing verifies the library, but deliberately preserves its existing access semantics.
        if (filter.PlexLibraryId > 0)
        {
            var library = await dbContext
                .PlexLibraries.IgnoreIsEnabledFilter()
                .Select(x => new { x.Id, x.IsEnabled })
                .FirstOrDefaultAsync(x => x.Id == filter.PlexLibraryId, ct);
            return library is not null && library.IsEnabled ? [library.Id] : [];
        }

        var hasAccounts = await dbContext.PlexAccounts.AnyAsync(ct);
        var servers = await dbContext
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
        var libraryIds = servers.SelectMany(server => server.LibraryIds).ToList();

        if (filter.FilterOwnedMedia)
        {
            var ownedLibraryIds = await dbContext
                .PlexLibraries.WhereIsOwned()
                .Select(library => library.Id)
                .ToListAsync(ct);
            libraryIds.RemoveAll(ownedLibraryIds.Contains);
        }

        if (filter.FilterOfflineMedia)
        {
            foreach (var server in servers)
                if (!await dbContext.IsServerOnline(server.Id))
                    libraryIds.RemoveAll(server.LibraryIds.Contains);
        }

        return libraryIds;
    }

    /// <summary>Restricts query filters to the permitted Plex library scope.</summary>
    private static void ApplyLibraryScope(QueryOptions options, List<int> libraryIds)
    {
        var scopeFilter = new FilterGroup
        {
            Logic = LogicOperator.And,
            Filters =
            [
                new FilterCondition
                {
                    Field = nameof(BasePlexMedia.PlexLibraryId),
                    Operator = libraryIds.Count == 1 ? FilterOperators.Equal : FilterOperators.In,
                    Value = libraryIds.Count == 1 ? libraryIds[0].ToString() : string.Join(',', libraryIds),
                },
            ],
        };
        options.Filter = options.Filter is null
            ? scopeFilter
            : new FilterGroup { Logic = LogicOperator.And, Groups = [options.Filter, scopeFilter] };
    }
}
