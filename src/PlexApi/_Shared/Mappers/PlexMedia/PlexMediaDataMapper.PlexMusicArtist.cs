namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static List<PlexMusicArtist> ToPlexMusicArtists(this List<LibraryMediaItemDTO> source) =>
        source.Select(value => value.ToPlexMusicArtist()).ToList();

    public static PlexMusicArtist ToPlexMusicArtist(this LibraryMediaItemDTO source) =>
        new()
        {
            Id = 0,
            Title = source.Title,
            SortIndex = source.SortIndex,
            SearchTitle = source.SearchTitle,
            Duration = source.Duration,
            MediaSize = 0,
            ChildCount = source.ChildCount,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            PlexApiRatingKey = source.RatingKey,
            PlexApiIdentityId = source.Guid.Replace("plex://artist/", string.Empty),
            PlexApiMetaDataKey = RetrieveMetaDataKey(source),
            Summary = source.Summary,
            HasThumb = !string.IsNullOrEmpty(source.Thumb),
            HasArt = !string.IsNullOrEmpty(source.Art),
            Actors = source.Role.ToPlexActor(),
            Genres = source.Genre.ToPlexGenreAudio(),
            Countries = source.Country.ToPlexCountry(),
            PlexLibraryId = 0,
            PlexServerId = 0,
            MusicBrainzArtistId = GetMusicBrainzId(source),
        };
}
