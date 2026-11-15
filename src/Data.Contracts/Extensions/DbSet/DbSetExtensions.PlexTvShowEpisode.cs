namespace Reaparr.Data.Contracts;

public static class DbSetExtensionsPlexTvShowEpisode
{
    public static IQueryable<PlexTvShowEpisode> IncludeAll(this IQueryable<PlexTvShowEpisode> plexTvShowEpisodes) =>
        plexTvShowEpisodes
            .IncludePlexServer()
            .IncludePlexLibrary()
            .IncludeMediaData()
            .Include($"{nameof(PlexTvShowEpisode.TvShowSeason)}.{nameof(PlexTvShowSeason.PlexServer)}")
            .Include($"{nameof(PlexTvShowEpisode.TvShowSeason)}.{nameof(PlexTvShowSeason.PlexLibrary)}")
            .Include($"{nameof(PlexTvShowEpisode.TvShow)}.{nameof(PlexTvShow.PlexServer)}")
            .Include($"{nameof(PlexTvShowEpisode.TvShow)}.{nameof(PlexTvShow.PlexLibrary)}");

    public static IQueryable<PlexTvShowEpisode> IncludePlexLibrary(
        this IQueryable<PlexTvShowEpisode> plexTvShowEpisode
    ) => plexTvShowEpisode.Include(x => x.PlexLibrary);

    public static IQueryable<PlexTvShowEpisode> IncludePlexServer(
        this IQueryable<PlexTvShowEpisode> plexTvShowEpisode
    ) => plexTvShowEpisode.Include(x => x.PlexServer).ThenInclude(x => x!.PlexServerConnections);

    public static IQueryable<PlexTvShowEpisode> IncludeMediaData(
        this IQueryable<PlexTvShowEpisode> plexTvShowEpisode
    ) => plexTvShowEpisode.Include(x => x.MediaDataList);
}
