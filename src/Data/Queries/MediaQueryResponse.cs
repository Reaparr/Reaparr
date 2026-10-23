namespace Reaparr.Data;

internal static class MediaQueryResponse
{
    internal static async Task CompleteAsync(
        IReaparrDbContext dbContext,
        PagedMediaQueryResult response,
        MediaQueryFilter filter,
        List<int> libraryIds,
        bool hasUserFilters,
        CancellationToken ct
    )
    {
        var offset = response.PageSize > 0 ? (response.Page - 1) * response.PageSize : 0;
        for (var index = 0; index < response.Items.Count; index++)
            response.Items[index].SortIndex = offset + index + 1;

        var libraries = await dbContext
            .PlexLibraries.Where(library => libraryIds.Contains(library.Id))
            .Select(library => new
            {
                library.MovieCount,
                library.TvShowCount,
                library.SeasonCount,
                library.EpisodeCount,
            })
            .ToListAsync(ct);
        response.TotalMovieCount =
            filter.MediaType == PlexMediaType.Movie ? response.TotalCount : libraries.Sum(x => x.MovieCount);
        response.TotalTvShowCount =
            filter.MediaType == PlexMediaType.TvShow ? response.TotalCount : libraries.Sum(x => x.TvShowCount);
        response.TotalSeasonCount = libraries.Sum(x => x.SeasonCount);
        response.TotalEpisodeCount = libraries.Sum(x => x.EpisodeCount);
        response.MovieCount = filter.MediaType == PlexMediaType.Movie ? response.TotalCount : 0;
        response.TvShowCount = filter.MediaType == PlexMediaType.TvShow ? response.TotalCount : 0;
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

        response.SeasonCount = filter.MediaType == PlexMediaType.TvShow ? response.TotalSeasonCount : 0;
        response.EpisodeCount = filter.MediaType == PlexMediaType.TvShow ? response.TotalEpisodeCount : 0;
    }
}
