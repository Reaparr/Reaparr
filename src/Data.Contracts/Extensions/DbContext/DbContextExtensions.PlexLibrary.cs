namespace Reaparr.Data.Contracts;

public static partial class DbContextExtensions
{
    public static async Task<string> GetPlexLibraryNameById(this IReaparrDbContext dbContext, int plexLibraryId)
    {
        var plexLibraryName = await dbContext
            .PlexLibraries.IgnoreIsEnabledFilter()
            .Where(x => x.Id == plexLibraryId)
            .Select(x => x.Title)
            .FirstOrDefaultAsync(CancellationToken.None);
        return plexLibraryName ?? "Library Name Not Found";
    }

    public static async Task<int> GetPlexServerIdFromPlexLibraryId(this IReaparrDbContext dbContext, int plexLibraryId)
    {
        return await dbContext
            .PlexLibraries.Where(x => x.Id == plexLibraryId)
            .Select(x => x.PlexServerId)
            .FirstOrDefaultAsync(CancellationToken.None);
    }

    public static Task<HashSet<int>> GetCurrentOwnedLibraryIds(
        this IReaparrDbContext dbContext,
        int remoteLibraryId,
        PlexMediaType mediaType,
        CancellationToken cancellationToken = default
    ) => GetCurrentLibraryIds(dbContext, CurrentLibraryRole.Owned, remoteLibraryId, mediaType, cancellationToken);

    public static Task<HashSet<int>> GetCurrentRemoteLibraryIds(
        this IReaparrDbContext dbContext,
        int ownedLibraryId,
        PlexMediaType mediaType,
        CancellationToken cancellationToken = default
    ) => GetCurrentLibraryIds(dbContext, CurrentLibraryRole.Remote, ownedLibraryId, mediaType, cancellationToken);

    private static async Task<HashSet<int>> GetCurrentLibraryIds(
        IReaparrDbContext dbContext,
        CurrentLibraryRole role,
        int libraryId,
        PlexMediaType mediaType,
        CancellationToken cancellationToken
    )
    {
        var libraryExists = await dbContext.PlexLibraries.AnyAsync(
            x => x.Id == libraryId && !x.Outdated,
            cancellationToken
        );
        if (!libraryExists)
            return [];

        var ids =
            role == CurrentLibraryRole.Owned
                ? await dbContext
                    .PlexComparisonScopes.Where(x => x.RemotePlexLibraryId == libraryId && x.MediaType == mediaType)
                    .Join(
                        dbContext.PlexLibraries.WhereIsOwned().Where(x => x.Type == mediaType && !x.Outdated),
                        scope => scope.OwnedPlexLibraryId,
                        library => library.Id,
                        (_, library) => library.Id
                    )
                    .ToListAsync(cancellationToken)
                : await dbContext
                    .PlexComparisonScopes.Where(x => x.OwnedPlexLibraryId == libraryId && x.MediaType == mediaType)
                    .Join(
                        dbContext.PlexLibraries.WhereIsNotOwned().Where(x => x.Type == mediaType && !x.Outdated),
                        scope => scope.RemotePlexLibraryId,
                        library => library.Id,
                        (_, library) => library.Id
                    )
                    .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    private enum CurrentLibraryRole
    {
        Owned,
        Remote,
    }

    public static async Task SetLibraryMetaData(
        this IReaparrDbContext dbContext,
        int plexLibraryId,
        int actorsCount,
        int genreCount,
        int countryCount
    )
    {
        await dbContext
            .PlexLibraries.Where(x => x.Id == plexLibraryId)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.ActorsCount, actorsCount)
                        .SetProperty(x => x.GenresCount, genreCount)
                        .SetProperty(x => x.CountriesCount, countryCount),
                CancellationToken.None
            );
    }

    public static async Task SetMovieMediaMetrics(
        this IReaparrDbContext dbContext,
        int plexLibraryId,
        int movieCount,
        int movieMediaDataCount,
        long mediaSize
    )
    {
        await dbContext
            .PlexLibraries.Where(x => x.Id == plexLibraryId)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.MovieCount, movieCount)
                        .SetProperty(x => x.MovieMediaDataCount, movieMediaDataCount)
                        .SetProperty(x => x.MediaSize, mediaSize),
                CancellationToken.None
            );
    }

    public static async Task SetTvShowMediaMetrics(
        this IReaparrDbContext dbContext,
        int plexLibraryId,
        int tvShowCount,
        int seasonCount,
        int episodeCount,
        int episodeMediaDataCount,
        long mediaSize
    )
    {
        await dbContext
            .PlexLibraries.Where(x => x.Id == plexLibraryId)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.TvShowCount, tvShowCount)
                        .SetProperty(x => x.SeasonCount, seasonCount)
                        .SetProperty(x => x.EpisodeCount, episodeCount)
                        .SetProperty(x => x.EpisodeMediaDataCount, episodeMediaDataCount)
                        .SetProperty(x => x.MediaSize, mediaSize),
                CancellationToken.None
            );
    }
}
