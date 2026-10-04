namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static List<PlexOtherVideo> ToPlexOtherVideos(this List<LibraryMediaItemDTO> source) =>
        source.Select(value => value.ToPlexOtherVideo()).ToList();

    public static PlexOtherVideo ToPlexOtherVideo(this LibraryMediaItemDTO source)
    {
        var mediaDataList = source.Media.SelectMany(x => x.Parts.Select((part, partIndex) =>
        {
            var fileName = part.File.GetFileName();
            return new PlexOtherVideoMediaData
            {
                PlexApiRatingKey = source.RatingKey,
                PlexApiMediaId = x.Id,
                PlexApiPartId = part.Id,
                PartIndex = partIndex,
                PlexOtherVideoId = 0,
                PlexOtherVideo = default,
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
                OriginalFilePath = string.IsNullOrEmpty(part.File) ? null : part.File,
                SourceRelativePath = null,
                Width = x.Width > 0 ? x.Width : null,
                Height = x.Height > 0 ? x.Height : null,
                VideoProfile = string.IsNullOrEmpty(x.VideoProfile) ? null : x.VideoProfile,
                VideoFrameRate = GetVideoFrameRate(part, x.VideoFrameRate),
                Source = x.DetermineReleaseSource(),
                NeedsGeneratedName = !fileName.IsValidMediaFileName(),
                GeneratedFilename = null,
                PlexLibraryId = 0,
                PlexServerId = 0,
            };
        })).ToList();

        var video = new PlexOtherVideo
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
            PlexLibraryId = 0,
            PlexServerId = 0,
        };

        foreach (var mediaData in mediaDataList)
            mediaData.PlexOtherVideo = video;

        return video;

    }

    private static decimal? GetVideoFrameRate(LibraryMediaItemPartDTO part, string mediaFrameRate)
    {
        foreach (var stream in part.Stream)
        {
            if (
                stream.StreamType == StreamType.Video
                && stream.FrameRate is float frameRate
                && frameRate > 0
                && float.IsFinite(frameRate)
                && frameRate < (float)decimal.MaxValue
            )
                return (decimal)frameRate;
        }

        return decimal.TryParse(
            mediaFrameRate,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var fallbackFrameRate
        )
            ? fallbackFrameRate
            : null;
    }
}
