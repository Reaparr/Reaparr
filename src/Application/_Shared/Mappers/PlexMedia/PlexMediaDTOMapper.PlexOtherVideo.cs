namespace Reaparr.Application;

public static partial class PlexMediaDTOMapper
{
    public static PlexMediaDTO ToDTO(this PlexOtherVideo source) =>
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
                .MediaDataList.GroupBy(x => x.PlexApiMediaId)
                .Select(group => group.OrderBy(x => x.PartIndex).ThenBy(x => x.Id).First())
                .OrderByDescending(x => x.Quality)
                .ThenBy(x => x.PlexApiMediaId)
                .Select(x => new PlexMediaQualityDTO
                {
                    Quality = x.Quality,
                    MediaDataType = PlexMediaType.OtherVideos,
                    DataId = x.Id,
                    MediaId = source.Id,
                })
                .ToList(),
            ComparisonId = PlexMediaComparisonState.NotCompared.ToComparisonId(),
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
        };

    public static List<PlexMediaDataDTO> ToDTO(this ICollection<PlexOtherVideoMediaData> source) =>
        source.Select(x => x.ToDTO()).ToList();

    public static PlexMediaDataDTO ToDTO(this PlexOtherVideoMediaData source) =>
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
