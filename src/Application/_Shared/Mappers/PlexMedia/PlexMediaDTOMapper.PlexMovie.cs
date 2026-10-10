namespace Reaparr.Application;

public static partial class PlexMediaDTOMapper
{
    public static PlexMediaDTO ToDTO(this PlexMovie source) =>
        new()
        {
            Id = source.Id,
            ParentId = null,
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
                .MediaDataList.SortByQuality()
                .Select(x => new PlexMediaQualityDTO
                {
                    Quality = x.Quality,
                    MediaDataType = x.Type,
                    DataId = x.Id,
                    MediaId = source.Id,
                })
                .ToList(),
            ComparisonId = source.ComparisonState.ToComparisonId(),
            PlexApiRatingKey = source.PlexApiRatingKey,
            HasArt = source.HasArt,
            HasTheme = source.HasTheme,
            Studio = source.Studio,
            Summary = source.Summary,
            ContentRating = source.ContentRating,
            Rating = source.Rating,
            OriginallyAvailableAt = source.OriginallyAvailableAt,
            Children = [],
            PlexApiMetaDataKey = source.PlexApiMetaDataKey,
        };

    public static List<PlexMediaDataDTO> ToDTO(this ICollection<PlexMovieMediaData> source) =>
        source.Select(x => x.ToDTO()).ToList();

    public static PlexMediaDataDTO ToDTO(this PlexMovieMediaData source) =>
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
