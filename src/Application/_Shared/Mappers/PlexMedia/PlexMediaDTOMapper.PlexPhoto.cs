namespace Reaparr.Application;

public static partial class PlexMediaDTOMapper
{
    public static PlexMediaDTO ToDTO(this PlexPhotoAlbum source) =>
        new()
        {
            Id = source.Id,
            ParentId = null,
            TvShowId = default,
            TvShowSeasonId = default,
            MediaData = [],
            Title = source.Title,
            SearchTitle = source.SearchTitle,
            SortIndex = source.SortIndex,
            Year = source.Year,
            Duration = source.Duration,
            MediaSize = source.MediaSize,
            ChildCount = source.ChildCount,
            GrandChildCount = 0,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            PlexLibraryId = source.PlexLibraryId,
            PlexServerId = source.PlexServerId,
            Type = source.Type,
            HasThumb = source.HasThumb,
            Qualities = [],
            ComparisonId = source.ComparisonState.ToComparisonId(),
            PlexApiRatingKey = source.PlexApiRatingKey,
            PlexApiMetaDataKey = source.PlexApiMetaDataKey,
            HasArt = source.HasArt,
            HasTheme = source.HasTheme,
            Studio = source.Studio,
            Summary = source.Summary,
            ContentRating = source.ContentRating,
            Rating = source.Rating,
            OriginallyAvailableAt = source.OriginallyAvailableAt,
            Children = source.Photos.OrderBy(x => x.SortIndex).Select(x => x.ToDTO()).ToList(),
            DiscNumber = null,
            TrackNumber = null,
        };

    public static PlexMediaDTO ToDTO(this PlexPhotoImage source) =>
        new()
        {
            Id = source.Id,
            ParentId = source.PlexPhotoAlbumId,
            TvShowId = default,
            TvShowSeasonId = default,
            MediaData = source.MediaDataList.ToDTO(),
            Title = source.Title,
            SearchTitle = source.SearchTitle,
            SortIndex = source.SortIndex,
            Year = source.Year,
            Duration = source.Duration,
            MediaSize = source.MediaSize,
            ChildCount = source.ChildCount,
            GrandChildCount = 0,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            PlexLibraryId = source.PlexLibraryId,
            PlexServerId = source.PlexServerId,
            Type = source.Type,
            HasThumb = source.HasThumb,
            Qualities = source
                .MediaDataList.GroupBy(x => x.PlexApiMediaId)
                .Select(group =>
                    group
                        .OrderBy(x => x.Id)
                        .Select(x => new PlexMediaQualityDTO
                        {
                            Quality = VideoQuality.Unknown,
                            MediaDataType = PlexMediaType.PhotoImage,
                            DataId = x.Id,
                            MediaId = source.Id,
                        })
                        .First()
                )
                .ToList(),
            ComparisonId = source.ComparisonState.ToComparisonId(),
            PlexApiRatingKey = source.PlexApiRatingKey,
            PlexApiMetaDataKey = source.PlexApiMetaDataKey,
            HasArt = source.HasArt,
            HasTheme = source.HasTheme,
            Studio = source.Studio,
            Summary = source.Summary,
            ContentRating = source.ContentRating,
            Rating = source.Rating,
            OriginallyAvailableAt = source.OriginallyAvailableAt,
            Children = [],
            DiscNumber = null,
            TrackNumber = null,
        };

    public static List<PlexMediaDataDTO> ToDTO(this ICollection<PlexPhotoMediaData> source) =>
        source.Select(x => x.ToDTO()).ToList();

    public static PlexMediaDataDTO ToDTO(this PlexPhotoMediaData source) =>
        new()
        {
            Id = source.Id,
            PlexApiMediaId = source.PlexApiMediaId,
            PlexApiPartId = source.PlexApiPartId,
            FileName = source.GetFileName,
            Duration = source.Duration,
            Size = source.Size,
            VideoResolution = source.VideoResolution,
            VideoCodec = source.VideoCodec,
            AudioCodec = source.AudioCodec,
        };
}
