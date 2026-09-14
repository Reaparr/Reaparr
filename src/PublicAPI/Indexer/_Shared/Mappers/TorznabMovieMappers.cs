namespace Reaparr.PublicAPI;

public static class TorznabMovieMappers
{
    public static IQueryable<TorznabFeedItemProjection> ProjectToTorznabFeedItems(
        this IQueryable<PlexMovieMediaData> query
    ) =>
        query.Select(x => new TorznabFeedItemProjection
        {
            MediaType = PlexMediaType.Movie,
            MediaId = x.PlexMovieId,
            DataId = x.Id,
            PlexServerId = x.PlexServerId,
            PlexServerMachineIdentifier = x.PlexServer!.MachineIdentifier,
            PlexLibraryId = x.PlexLibraryId,
            PlexApiRatingKey = x.PlexApiRatingKey,
            PlexApiMediaId = x.PlexApiMediaId,
            PlexApiPartId = x.PlexApiPartId,
            Title = !string.IsNullOrEmpty(x.GeneratedFilename) ? x.GeneratedFilename : x.OriginalFilename,
            AddedAt = x.PlexMovie!.AddedAt,
            Size = x.Size,
            Quality = x.Quality,
            VideoResolution = x.VideoResolution,
            Source = x.Source,
            VideoCodec = x.VideoCodec,
            AudioCodec = x.AudioCodec,
            TmdbId = x.PlexMovie.Guid_TMDB,
            ImdbId = x.PlexMovie.Guid_IMDB,
            SeasonNumber = null,
            EpisodeNumber = null,
            TvdbId = null,
            GenreTypes = x.PlexMovie.Genres.Select(genre => genre.Type).ToList(),
        });
}
