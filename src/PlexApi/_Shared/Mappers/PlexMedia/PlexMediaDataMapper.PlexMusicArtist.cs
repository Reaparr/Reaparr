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
            Type = PlexMediaType.Artist,
            PlexLibraryId = library.Id,
            PlexServerId = library.PlexServerId,
            MusicBrainzArtistId =
                source.Guid.StartsWith("mbid://", StringComparison.Ordinal)
                && System.Guid.TryParse(source.Guid.AsSpan(7), out var mbid)
                    ? mbid.ToString()
                    : null,
        };
}
