namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static List<PlexPhotoAlbum> ToPlexPhotoAlbums(this List<LibraryMediaItemDTO> source) =>
        source.Select(value => value.ToPlexPhotoAlbum()).ToList();

    public static PlexPhotoAlbum ToPlexPhotoAlbum(this LibraryMediaItemDTO source) =>
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
            OriginallyAvailableAt = source.OriginallyAvailableAt.ToDateTime(),
            HasThumb = !string.IsNullOrEmpty(source.Thumb),
            HasArt = !string.IsNullOrEmpty(source.Art),
            HasTheme = !string.IsNullOrEmpty(source.Theme),
            FullTitle = source.Title,
            PlexApiRatingKey = source.RatingKey,
            PlexApiMetaDataKey = RetrieveMetaDataKey(source),
            Studio = source.Studio,
            Summary = source.Summary,
            ContentRating = source.ContentRating,
            Rating = source.Rating,
            PlexLibraryId = 0,
            PlexServerId = 0,
        };
}
