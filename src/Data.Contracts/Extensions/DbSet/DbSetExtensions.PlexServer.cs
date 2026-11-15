namespace Reaparr.Data.Contracts;

public static class DbSetExtensionsPlexServer
{
    public static IQueryable<PlexServer> IncludeLibraries(this IQueryable<PlexServer> plexServer) =>
        plexServer.Include(x => x.PlexLibraries).AsQueryable();

    public static IQueryable<PlexServer> IncludeLibrariesWithMedia(this IQueryable<PlexServer> plexServer) =>
        plexServer
            .Include(x => x.PlexLibraries)
                .ThenInclude(x => x.Movies)
            .Include(x => x.PlexLibraries)
                .ThenInclude(x => x.TvShows)
                    .ThenInclude(x => x.Seasons)
                        .ThenInclude(x => x.Episodes);

    public static IQueryable<PlexServer> IncludeConnections(this IQueryable<PlexServer> plexServer) =>
        plexServer.Include(x => x.PlexServerConnections).AsQueryable();

    public static IQueryable<PlexServer> IgnoreIsEnabledFilter(this IQueryable<PlexServer> query) =>
        query.IgnoreQueryFilters(["IsEnabled"]);

    public static IQueryable<PlexServer> WhereIsOwned(this IQueryable<PlexServer> query) => query.IsOwnedHelper(true);

    public static IQueryable<PlexServer> WhereIsNotOwned(this IQueryable<PlexServer> query) =>
        query.IsOwnedHelper(false);

    private static IQueryable<PlexServer> IsOwnedHelper(this IQueryable<PlexServer> query, bool owned)
    {
        if (owned)
            return query.Where(x =>
                x.OwnedOverride == true || (x.OwnedOverride == null && x.PlexAccountServers.Any(y => y.IsServerOwned))
            );

        return query.Where(x =>
            x.OwnedOverride == false || (x.OwnedOverride == null && !x.PlexAccountServers.Any(y => y.IsServerOwned))
        );
    }
}
