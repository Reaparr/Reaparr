namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static List<PlexOtherVideo> ToPlexOtherVideos(this List<LibraryMediaItemDTO> source) =>
        source.Select(value => value.ToPlexOtherVideo()).ToList();

    public static PlexOtherVideo ToPlexOtherVideo(this LibraryMediaItemDTO source)
    {
        var mediaDataList = source
            .Media.SelectMany(x =>
                x.Parts.Select(
                    (part, partIndex) =>
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
                            Streams = part.ToPlexOtherVideoDataStreams(),
                            VideoQuality = x.VideoResolution,
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
                            Source = x.DetermineReleaseSource(),
                            PlexLibraryId = 0,
                            PlexServerId = 0,
                        };
                    }
                )
            )
            .ToList();

        var video = new PlexOtherVideo
        {
            Id = 0,
            Title = source.Title,
            Year = source.Year,
            SortIndex = source.SortIndex,
            SearchTitle = source.SearchTitle,
            Guid = source.Guid,
            Duration = source.Duration,
            MediaSize = source.Media.Sum(x => x.Parts.Sum(p => p.Size)),
            VideoQuality = mediaDataList.Count == 0 ? VideoQuality.Unknown : mediaDataList.Max(x => x.VideoQuality),
            MediaDataList = mediaDataList,
            ChildCount = source.ChildCount,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            PlexApiRatingKey = source.RatingKey,
            PlexApiMetaDataKey = RetrieveMetaDataKey(source),
            Summary = source.Summary,
            OriginallyAvailableAt = source.OriginallyAvailableAt.ToDateTime(),
            HasThumb = !string.IsNullOrEmpty(source.Thumb),
            HasArt = !string.IsNullOrEmpty(source.Art),
            Actors = source.Role.ToPlexActor(),
            Genres = source.Genre.ToPlexGenreVideo(),
            Countries = source.Country.ToPlexCountry(),
            PlexLibraryId = 0,
            PlexServerId = 0,
        };

        foreach (var mediaData in mediaDataList)
            mediaData.PlexOtherVideo = video;

        return video;
    }
}
