namespace Reaparr.Data.Contracts;

public static partial class DbSetExtensions
{
    public static IQueryable<PlexAccount> IncludeServerAccess(this IQueryable<PlexAccount> plexAccount) =>
        plexAccount.Include(v => v.PlexAccountServers).ThenInclude(x => x.PlexServer);

    public static IQueryable<PlexAccount> IncludeLibraryAccess(this IQueryable<PlexAccount> plexAccount) =>
        plexAccount.Include(x => x.PlexAccountLibraries).ThenInclude(x => x.PlexLibrary);

    /// <summary>
    /// Filters Plex media to libraries that remain accessible through a configured Plex account.
    /// </summary>
    /// <remarks>
    /// A single account must retain access to both the server and library. Media without account access cannot be
    /// downloaded and must not be advertised to integrations.
    /// </remarks>
    public static IQueryable<T> WhereHasPlexAccountAccess<T>(this IQueryable<T> query)
        where T : BasePlexMedia =>
        query.Where(x =>
            x.PlexLibrary!.PlexAccountLibraries.Any(libraryAccess =>
                x.PlexServer!.PlexAccountServers.Any(serverAccess =>
                    serverAccess.PlexAccountId == libraryAccess.PlexAccountId
                )
            )
        );

    public static IQueryable<PlexTvShowEpisode> WhereHasPlexAccountAccess(this IQueryable<PlexTvShowEpisode> query) =>
        query.Where(x =>
            x.TvShow!.PlexLibrary!.PlexAccountLibraries.Any(libraryAccess =>
                x.TvShow.PlexServer!.PlexAccountServers.Any(serverAccess =>
                    serverAccess.PlexAccountId == libraryAccess.PlexAccountId
                )
            )
        );
}
