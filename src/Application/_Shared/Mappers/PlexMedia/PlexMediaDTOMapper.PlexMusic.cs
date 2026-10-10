namespace Reaparr.Application;

public static partial class PlexMediaDTOMapper
{
    public static PlexMediaDTO ToDTO(this PlexMusicArtist source) =>
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
            GrandChildCount = source.Albums.Sum(x => x.Tracks.Count),
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
            Children = source.Albums.OrderBy(x => x.SortIndex).ThenBy(x => x.Id).Select(x => x.ToDTO()).ToList(),
        };

    public static PlexMediaDTO ToDTO(this PlexMusicAlbum source) =>
        new()
        {
            Id = source.Id,
            ParentId = source.PlexArtistId,
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
            Children = source.Tracks.OrderBy(x => x.SortIndex).ThenBy(x => x.Id).Select(x => x.ToDTO()).ToList(),
        };

    public static PlexMediaDTO ToDTO(this PlexMusicTrack source) =>
        new()
        {
            Id = source.Id,
            ParentId = source.PlexAlbumId,
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
                .OrderBy(x => x.Key)
                .Select(group => new PlexMediaQualityDTO
                {
                    Quality = VideoQuality.Unknown,
                    MediaDataType = PlexMediaType.MusicTrack,
                    DataId = group.OrderBy(x => x.PartIndex).ThenBy(x => x.Id).First().Id,
                    MediaId = source.Id,
                })
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
        };

    public static List<PlexMediaDataDTO> ToDTO(this ICollection<PlexMusicTrackMediaData> source) =>
        source.Select(x => x.ToDTO()).ToList();

    public static PlexMediaDataDTO ToDTO(this PlexMusicTrackMediaData source) =>
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
