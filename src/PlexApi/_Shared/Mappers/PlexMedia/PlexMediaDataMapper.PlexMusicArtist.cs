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
            Year = source.Year,
            SortIndex = source.SortIndex,
            SearchTitle = source.SearchTitle,
            Guid = source.Guid,
            Guid_IMDB = source.Guids.GetImdbId(),
            Guid_TMDB = source.Guids.GetTmdbId(),
            Guid_TVDB = source.Guids.GetTvdbId(),
            Duration = source.Duration,
            MediaSize = 0,
            ChildCount = source.ChildCount,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            PlexApiRatingKey = source.RatingKey,
            PlexApiMetaDataKey = RetrieveMetaDataKey(source),
            Studio = source.Studio,
            Summary = source.Summary,
            ContentRating = source.ContentRating,
            Rating = source.Rating,
            OriginallyAvailableAt = source.OriginallyAvailableAt.ToDateTime(),
            HasThumb = !string.IsNullOrEmpty(source.Thumb),
            HasArt = !string.IsNullOrEmpty(source.Art),
            HasTheme = !string.IsNullOrEmpty(source.Theme),
            FullTitle = source.Title,
            PlexLibraryId = 0,
            PlexServerId = 0,
            MusicBrainzArtistId = GetMusicBrainzArtistId(source)?.ToString(),
        };

    private static Guid? GetMusicBrainzArtistId(LibraryMediaItemDTO source)
    {
        if (TryParseMusicBrainzId(source.Guid, out var musicBrainzId))
            return musicBrainzId;

        foreach (var guid in source.Guids)
        {
            if (TryParseMusicBrainzId(guid.Id, out musicBrainzId))
                return musicBrainzId;
        }

        return null;
    }

    private static bool TryParseMusicBrainzId(string value, out Guid musicBrainzId)
    {
        musicBrainzId = default;
        return value.StartsWith("mbid://", StringComparison.Ordinal)
            && Guid.TryParse(value.AsSpan(7), out musicBrainzId);
    }
}
