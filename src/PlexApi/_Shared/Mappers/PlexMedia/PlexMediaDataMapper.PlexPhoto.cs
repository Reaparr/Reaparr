namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static List<PlexPhoto> ToPlexPhotos(this List<LibraryMediaItemDTO> source) =>
        source.Select(value => value.ToPlexPhoto()).ToList();

    public static PlexPhoto ToPlexPhoto(this LibraryMediaItemDTO source)
    {
        var mediaDataList = source.Media.SelectMany(x => x.Parts.Select((part, partIndex) =>
        {
            var fileName = part.File.GetFileName();
            return new PlexPhotoMediaData
            {
                PlexApiRatingKey = source.RatingKey,
                PlexApiMediaId = x.Id,
                PlexApiPartId = part.Id,
                PlexPhotoId = 0,
                PlexPhoto = default,
                VideoResolution = x.VideoResolution,
                Quality = x.VideoResolution,
                Container = part.Container,
                VideoCodec = x.VideoCodec,
                AudioCodec = x.AudioCodec,
                Duration =
                    part.Duration >= 0 ? part.Duration
                    : x.Parts.Count == 1 && x.Duration > 0 ? x.Duration
                    : -1,
                Size = part.Size,
                Key = part.Key,
                OriginalFilename = fileName,
                Width = x.Width > 0 ? x.Width : null,
                Height = x.Height > 0 ? x.Height : null,
                Source = x.DetermineReleaseSource(),
                NeedsGeneratedName = !fileName.IsValidMediaFileName(),
                GeneratedFilename = null,
                PlexLibraryId = 0,
                PlexServerId = 0,
            };
        })).ToList();

        var photo = new PlexPhoto
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
            MediaSize = source.Media.Sum(x => x.Parts.Sum(p => p.Size)),
            Quality = mediaDataList.Count == 0 ? VideoQuality.Unknown : mediaDataList.Max(x => x.Quality),
            ChildCount = source.ChildCount,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            ParentKey = source.GetParentKey(),
            MediaDataList = mediaDataList,
            FullTitle = source.Title,
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
            PlexPhotoAlbumId = 0,
            PlexPhotoAlbum = default,
            PlexLibraryId = 0,
            PlexServerId = 0,
        };

        foreach (var mediaData in mediaDataList)
            mediaData.PlexPhoto = photo;

        return photo;
    }

}
