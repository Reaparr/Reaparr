namespace Reaparr.Data.Contracts;

public static class DbSetExtensionsPlexMovie
{
    public static IQueryable<PlexMovie> IncludeAll(this IQueryable<PlexMovie> plexMovies) =>
        plexMovies.IncludePlexServer().IncludePlexLibrary().IncludeMediaData();

    public static IQueryable<PlexMovie> IncludePlexLibrary(this IQueryable<PlexMovie> plexMovie) =>
        plexMovie.Include(x => x.PlexLibrary);

    public static IQueryable<PlexMovie> IncludePlexServer(this IQueryable<PlexMovie> plexMovie) =>
        plexMovie.Include(x => x.PlexServer).ThenInclude(x => x!.PlexServerConnections);

    public static IQueryable<PlexMovie> IncludeMediaData(this IQueryable<PlexMovie> plexMovie) =>
        plexMovie.Include(x => x.MediaDataList);
}
