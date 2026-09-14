namespace Reaparr.PublicAPI;

public static class TorznabTvShowMappers
{
    public static IQueryable<TorznabFeedItemProjection> ProjectToTorznabFeedItems(
        this IQueryable<PlexTvShowEpisodeMediaData> query
    ) =>
        query.Select(x => new TorznabFeedItemProjection
        {
            MediaType = PlexMediaType.Episode,
            MediaId = x.PlexTvShowEpisodeId,
            DataId = x.Id,
            PlexServerId = x.PlexServerId,
            PlexServerMachineIdentifier = x.PlexServer!.MachineIdentifier,
            PlexLibraryId = x.PlexLibraryId,
            PlexApiRatingKey = x.PlexApiRatingKey,
            PlexApiMediaId = x.PlexApiMediaId,
            PlexApiPartId = x.PlexApiPartId,
            Title = !string.IsNullOrEmpty(x.GeneratedFilename) ? x.GeneratedFilename : x.OriginalFilename,
            AddedAt = x.PlexTvShowEpisode!.AddedAt,
            Size = x.Size,
            Quality = x.Quality,
            VideoResolution = x.VideoResolution,
            Source = x.Source,
            VideoCodec = x.VideoCodec,
            AudioCodec = x.AudioCodec,
            SeasonNumber = x.PlexTvShowEpisode.TvShowSeason!.SeasonNumber,
            EpisodeNumber = x.PlexTvShowEpisode.EpisodeNumber,
            TvdbId = x.PlexTvShowEpisode.TvShow!.Guid_TVDB,
            TmdbId = x.PlexTvShowEpisode.TvShow.Guid_TMDB,
            ImdbId = x.PlexTvShowEpisode.TvShow.Guid_IMDB,
            GenreTypes = x.PlexTvShowEpisode.TvShow.Genres.Select(genre => genre.Type).ToList(),
        });
}
