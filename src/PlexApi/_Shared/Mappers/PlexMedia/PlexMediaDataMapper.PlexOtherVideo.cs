namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static PlexOtherVideo ToPlexOtherVideo(this LibraryMediaItemDTO source, PlexLibrary library)
    {
        var video = new PlexOtherVideo
        {
            PlexApiRatingKey = source.RatingKey,
            PlexApiMetaDataKey = RetrieveMetaDataKey(source),
            Title = source.Title,
            Year = source.Year,
            SortIndex = source.SortIndex,
            SearchTitle = source.SearchTitle,
            Duration = source.Duration,
            MediaSize = source.Media.Sum(x => x.Parts.Sum(p => p.Size)),
            Quality = source.Media.Count == 0 ? VideoQuality.Unknown : source.Media.Max(x => x.VideoResolution),
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
        };

        foreach (var media in source.Media)
        {
            for (var partIndex = 0; partIndex < media.Parts.Count; partIndex++)
            {
                var part = media.Parts[partIndex];
                video.MediaDataList.Add(
                    new PlexOtherVideoMediaData
                    {
                        PlexApiRatingKey = source.RatingKey,
                        PlexApiMediaId = media.Id,
                        PlexApiPartId = part.Id,
                        PartIndex = partIndex,
                        PlexOtherVideoId = 0,
                        PlexOtherVideo = video,
                        PlexLibraryId = library.Id,
                        PlexServerId = library.PlexServerId,
                        VideoResolution = media.VideoResolution,
                        Quality = media.VideoResolution,
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
                        OriginalFilePath = string.IsNullOrEmpty(part.File) ? null : part.File,
                        SourceRelativePath = null,
                        Width = media.Width > 0 ? media.Width : null,
                        Height = media.Height > 0 ? media.Height : null,
                        VideoProfile = string.IsNullOrEmpty(media.VideoProfile) ? null : media.VideoProfile,
                        VideoFrameRate = GetVideoFrameRate(part, media.VideoFrameRate),
                        Source = ReleaseSource.None,
                        NeedsGeneratedName = false,
                        GeneratedFilename = null,
                    }
                );
            }
        }

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
