namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static PlexMusicArtist ToPlexMusicArtist(this LibraryMediaItemDTO source, PlexLibrary library) =>
        new()
        {
            PlexApiRatingKey = source.RatingKey,
            PlexApiMetaDataKey = RetrieveMetaDataKey(source),
            Title = source.Title,
            Year = source.Year,
            SortIndex = source.SortIndex,
            SearchTitle = source.SearchTitle,
            Duration = source.Duration,
            MediaSize = 0,
            Studio = source.Studio,
            Summary = source.Summary,
            ContentRating = source.ContentRating,
            Rating = source.Rating,
            ChildCount = 0,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            OriginallyAvailableAt = source.OriginallyAvailableAt.ToDateTime(),
            HasThumb = !string.IsNullOrEmpty(source.Thumb),
            HasArt = !string.IsNullOrEmpty(source.Art),
            HasTheme = !string.IsNullOrEmpty(source.Theme),
            FullTitle = source.Title,
            Guid = source.Guid,
            Guid_IMDB = null,
            Guid_TMDB = null,
            Guid_TVDB = null,
            PlexLibraryId = library.Id,
            PlexServerId = library.PlexServerId,
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
