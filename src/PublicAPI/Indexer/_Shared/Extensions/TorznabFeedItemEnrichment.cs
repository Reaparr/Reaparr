namespace Reaparr.PublicAPI;

public static class TorznabFeedItemEnrichment
{
    public static async Task<List<TorznabFeedItemProjection>> EnrichGenresAsync(
        this IReadOnlyList<TorznabFeedItemProjection> items,
        IReaparrDbContext dbContext,
        CancellationToken cancellationToken
    )
    {
        if (items.Count == 0)
            return [];

        var movieIds = items
            .Where(x => x.MediaType == PlexMediaType.Movie)
            .Select(x => x.GenreOwnerId)
            .Distinct()
            .ToList();
        var tvShowIds = items
            .Where(x => x.MediaType == PlexMediaType.Episode)
            .Select(x => x.GenreOwnerId)
            .Distinct()
            .ToList();
        var genresByMediaId = new Dictionary<
            (PlexMediaType MediaType, int MediaId),
            IReadOnlyCollection<PlexGenreType>
        >(movieIds.Count + tvShowIds.Count);

        if (movieIds.Count > 0)
        {
            var movieGenres = await (
                from movieGenre in dbContext.PlexMovieGenres
                join genre in dbContext.PlexGenres on movieGenre.GenresId equals genre.Id
                where movieIds.Contains(movieGenre.PlexMovieId)
                select new { movieGenre.PlexMovieId, genre.Type }
            ).ToListAsync(cancellationToken);

            foreach (var group in movieGenres.GroupBy(x => x.PlexMovieId))
                genresByMediaId[(PlexMediaType.Movie, group.Key)] = [.. group.Select(x => x.Type).Distinct()];
        }

        if (tvShowIds.Count > 0)
        {
            var tvShowGenres = await (
                from tvShowGenre in dbContext.PlexTvShowGenres
                join genre in dbContext.PlexGenres on tvShowGenre.GenresId equals genre.Id
                where tvShowIds.Contains(tvShowGenre.PlexTvShowId)
                select new { tvShowGenre.PlexTvShowId, genre.Type }
            ).ToListAsync(cancellationToken);

            foreach (var group in tvShowGenres.GroupBy(x => x.PlexTvShowId))
                genresByMediaId[(PlexMediaType.Episode, group.Key)] = [.. group.Select(x => x.Type).Distinct()];
        }

        var enrichedItems = new List<TorznabFeedItemProjection>(items.Count);
        foreach (var item in items)
        {
            genresByMediaId.TryGetValue((item.MediaType, item.GenreOwnerId), out var genreTypes);
            enrichedItems.Add(item with { GenreTypes = genreTypes ?? [] });
        }

        return enrichedItems;
    }
}
