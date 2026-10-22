using Metadata = LukeHagar.PlexAPI.SDK.Models.Components.Metadata;

namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static List<PlexPhotoAlbum> ToPlexPhotoAlbums(this List<LibraryMediaItemDTO> source) =>
        source.Select(value => value.ToPlexPhotoAlbum()).ToList();

    internal static PlexPhotoAlbum ToPlexPhotoAlbum(this Metadata source, PlexMediaType mediaType) =>
        source.ToMediaItemDTO(mediaType).ToPlexPhotoAlbum(source.Summary);

    public static PlexPhotoAlbum ToPlexPhotoAlbum(this LibraryMediaItemDTO source) =>
        source.ToPlexPhotoAlbum(source.Summary);

    private static PlexPhotoAlbum ToPlexPhotoAlbum(
        this LibraryMediaItemDTO source,
        string? summary
    ) =>
        new()
        {
            Id = 0,
            PlexLibraryId = 0,
            PlexServerId = 0,
            PlexApiRatingKey = source.RatingKey,
            Title = source.Title,
            SearchTitle = source.SearchTitle,
            Summary = summary,
            HasThumb = !string.IsNullOrWhiteSpace(source.Thumb),
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
        };
}
