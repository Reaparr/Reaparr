namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static PlexPhotoAlbum ToPlexPhotoAlbum(this LibraryMediaItemDTO source, PlexLibrary library) =>
        new()
        {
            PlexApiRatingKey = source.RatingKey,
            Title = source.Title,
            SearchTitle = source.SearchTitle,
            SortIndex = source.SortIndex,
            Summary = string.IsNullOrEmpty(source.Summary) ? null : source.Summary,
            Year = source.Year > 0 ? source.Year : null,
            Duration = source.Duration > 0 ? source.Duration : null,
            MediaSize = 0,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            PlexLibraryId = library.Id,
            PlexServerId = library.PlexServerId,
        };
}
