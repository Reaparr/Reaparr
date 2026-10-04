namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static PlexPhoto ToPlexPhoto(this LibraryMediaItemDTO source, PlexPhotoAlbum album, PlexLibrary library)
    {
        var photo = new PlexPhoto
        {
            PlexApiRatingKey = source.RatingKey,
            PlexApiMetaDataKey = RetrieveMetaDataKey(source),
            Title = source.Title,
            Year = source.Year,
            SortIndex = source.SortIndex,
            SearchTitle = source.SearchTitle,
            Duration = source.Duration,
            MediaSize = source.Media.Sum(x => x.Parts.Sum(p => p.Size)),
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
            PlexPhotoAlbumId = album.Id,
            PlexPhotoAlbum = album,
        };

        foreach (var media in source.Media)
        {
            foreach (var part in media.Parts)
            {
                photo.MediaDataList.Add(
                    new PlexPhotoMediaData
                    {
                        PlexApiRatingKey = source.RatingKey,
                        PlexApiMediaId = media.Id,
                        PlexApiPartId = part.Id,
                        PlexPhotoId = 0,
                        PlexPhoto = photo,
                        PlexLibraryId = library.Id,
                        PlexServerId = library.PlexServerId,
                        VideoResolution = VideoQuality.Unknown,
                        Quality = VideoQuality.Unknown,
                        Container = part.Container,
                        VideoCodec = media.VideoCodec,
                        AudioCodec = media.AudioCodec,
                        Duration =
                            part.Duration >= 0 ? part.Duration
                            : media.Parts.Count == 1 && media.Duration > 0 ? media.Duration
                            : -1,
                        Size = part.Size,
                        Key = part.Key,
                        OriginalFilename = part.File.GetFileName(),
                        Width = media.Width > 0 ? media.Width : null,
                        Height = media.Height > 0 ? media.Height : null,
                        Source = ReleaseSource.None,
                        NeedsGeneratedName = false,
                        GeneratedFilename = null,
                    }
                );
            }
        }

        return photo;
    }
}
