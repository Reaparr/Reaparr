namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static PlexMusicTrack ToPlexMusicTrack(
        this LibraryMediaItemDTO source,
        PlexMusicAlbum album,
        PlexLibrary library
    )
    {
        var track = new PlexMusicTrack
        {
            PlexApiRatingKey = source.RatingKey,
            PlexApiMetaDataKey = RetrieveMetaDataKey(source),
            Title = source.Title,
            Year = source.Year,
            SortIndex = source.SortIndex,
            SearchTitle = source.SearchTitle,
            Duration = source.Duration,
            MediaSize = source.Media.Sum(x => x.Parts.Sum(p => p.Size)),
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
            Type = PlexMediaType.Song,
            PlexLibraryId = library.Id,
            PlexServerId = library.PlexServerId,
            PlexAlbumId = album.Id,
            PlexAlbum = album,
            DiscNumber = source.ParentIndex >= 0 ? source.ParentIndex : null,
            TrackNumber = source.Index >= 0 ? source.Index : null,
            MusicBrainzRecordingId = null,
            MusicBrainzReleaseTrackId = null,
        };

        foreach (var media in source.Media)
        {
            for (var partIndex = 0; partIndex < media.Parts.Count; partIndex++)
            {
                var part = media.Parts[partIndex];
                track.MediaDataList.Add(
                    new PlexMusicTrackMediaData
                    {
                        PlexApiRatingKey = source.RatingKey,
                        PlexApiMediaId = media.Id,
                        PlexApiPartId = part.Id,
                        PartIndex = partIndex,
                        PlexTrackId = 0,
                        PlexTrack = track,
                        PlexLibraryId = library.Id,
                        PlexServerId = library.PlexServerId,
                        VideoResolution = VideoQuality.Unknown,
                        Quality = VideoQuality.Unknown,
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
                        VideoFrameRate = decimal.TryParse(
                            media.VideoFrameRate,
                            System.Globalization.NumberStyles.Number,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var frameRate
                        )
                            ? frameRate
                            : null,
                        Source = ReleaseSource.None,
                        NeedsGeneratedName = false,
                        GeneratedFilename = null,
                    }
                );
            }
        }

        return track;
    }
}
