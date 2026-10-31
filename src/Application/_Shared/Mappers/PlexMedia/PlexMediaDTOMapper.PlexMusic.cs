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
            Year = default,
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
            Qualities = source.Qualities.ToDTO(),
            ComparisonId = source.ComparisonState.ToComparisonId(),
            PlexApiRatingKey = source.PlexApiRatingKey,
            PlexApiMetaDataKey = source.PlexApiMetaDataKey,
            HasArt = source.HasArt,
            HasTheme = false,
            Studio = string.Empty,
            Summary = source.Summary,
            ContentRating = null,
            Rating = default,
            OriginallyAvailableAt = null,
            RecordLabel = null,
            Genres = source.Genres.ToDTO(),
            Children = source.Albums.OrderBy(x => x.SortIndex).ThenBy(x => x.Id).Select(x => x.ToDTO()).ToList(),
            DiscNumber = null,
            TrackNumber = null,
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
            Qualities = source.Qualities.ToDTO(),
            ComparisonId = source.ComparisonState.ToComparisonId(),
            PlexApiRatingKey = source.PlexApiRatingKey,
            PlexApiMetaDataKey = source.PlexApiMetaDataKey,
            HasArt = source.HasArt,
            HasTheme = false,
            Studio = string.Empty,
            Summary = source.Summary,
            ContentRating = null,
            Rating = source.Rating,
            OriginallyAvailableAt = source.OriginallyAvailableAt,
            RecordLabel = source.RecordLabel,
            Genres = [],
            Children = source.Tracks.OrderBy(x => x.SortIndex).ThenBy(x => x.Id).Select(x => x.ToDTO()).ToList(),
            DiscNumber = null,
            TrackNumber = null,
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
            Year = default,
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
                .Select(group => ToMusicQuality(source.Id, group))
                .ToList(),
            ComparisonId = source.ComparisonState.ToComparisonId(),
            PlexApiRatingKey = source.PlexApiRatingKey,
            PlexApiMetaDataKey = source.PlexApiMetaDataKey,
            HasArt = source.HasArt,
            HasTheme = false,
            Studio = string.Empty,
            Summary = string.Empty,
            ContentRating = null,
            Rating = default,
            OriginallyAvailableAt = null,
            RecordLabel = null,
            Genres = [],
            DiscNumber = source.DiscNumber,
            TrackNumber = source.TrackNumber,
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
            VideoQuality = VideoQuality.None,
            VideoCodec = string.Empty,
            AudioCodec = source.AudioCodec,
            AudioQuality = source.AudioQuality,
        };

    private static PlexMediaQualityDTO ToMusicQuality(int trackId, IEnumerable<PlexMusicTrackMediaData> files)
    {
        var (representative, quality) = files.PickAudioQuality();

        return new PlexMediaQualityDTO
        {
            VideoQuality = VideoQuality.Unknown,
            AudioQuality = quality,
            MediaDataType = PlexMediaType.MusicTrack,
            DataId = representative!.Id,
            MediaId = trackId,
        };
    }
}
