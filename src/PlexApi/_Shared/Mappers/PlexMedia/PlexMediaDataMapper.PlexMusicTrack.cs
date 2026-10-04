namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static List<PlexMusicTrack> ToPlexMusicTracks(this List<LibraryMediaItemDTO> source) =>
        source.Select(value => value.ToPlexMusicTrack()).ToList();

    public static PlexMusicTrack ToPlexMusicTrack(this LibraryMediaItemDTO source)
    {
        var mediaDataList = source.Media.SelectMany(x => x.Parts.Select((part, partIndex) =>
        {
            var fileName = part.File.GetFileName();
            return new PlexMusicTrackMediaData
            {
                PlexApiRatingKey = source.RatingKey,
                PlexApiMediaId = x.Id,
                PlexApiPartId = part.Id,
                PartIndex = partIndex,
                PlexTrackId = 0,
                PlexTrack = default,
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
                VideoFrameRate = decimal.TryParse(
                    x.VideoFrameRate,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var frameRate
                )
                    ? frameRate
                    : null,
                Source = x.DetermineReleaseSource(),
                NeedsGeneratedName = !fileName.IsValidMediaFileName(),
                GeneratedFilename = null,
                PlexLibraryId = 0,
                PlexServerId = 0,
            };
        })).ToList();

        var track = new PlexMusicTrack
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
            PlexAlbumId = 0,
            PlexAlbum = default,
            PlexLibraryId = 0,
            PlexServerId = 0,
            DiscNumber = source.ParentIndex >= 0 ? source.ParentIndex : null,
            TrackNumber = source.Index >= 0 ? source.Index : null,
            MusicBrainzRecordingId = null,
            MusicBrainzReleaseTrackId = null,
        };

        foreach (var mediaData in mediaDataList)
            mediaData.PlexTrack = track;

        return track;
    }

}
