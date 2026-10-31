namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static List<PlexMusicTrack> ToPlexMusicTracks(this List<LibraryMediaItemDTO> source) =>
        source.Select(value => value.ToPlexMusicTrack()).ToList();

    public static PlexMusicTrack ToPlexMusicTrack(this LibraryMediaItemDTO source)
    {
        var mediaDataList = source
            .Media.SelectMany(x =>
                x.Parts.Select(
                    (part, partIndex) =>
                    {
                        var fileName = part.File.GetFileName();
                        var audioStream = part
                            .Stream.Where(stream => stream.StreamType == StreamType.Audio)
                            .OrderByDescending(stream => stream.Selected == true)
                            .ThenByDescending(stream => stream.Default == true)
                            .ThenBy(stream => stream.Index ?? int.MaxValue)
                            .ThenBy(stream => stream.Id)
                            .FirstOrDefault();
                        var mediaData = new PlexMusicTrackMediaData
                        {
                            PlexApiRatingKey = source.RatingKey,
                            PlexApiMediaId = x.Id,
                            PlexApiPartId = part.Id,
                            PartIndex = partIndex,
                            PlexTrackId = 0,
                            PlexTrack = default,
                            Streams = part.ToPlexMusicTrackDataStreams(),
                            VideoQuality = VideoQuality.None,
                            Container = part.Container,
                            VideoCodec = string.Empty,
                            AudioCodec = FirstMeaningful(audioStream?.Codec, x.AudioCodec) ?? string.Empty,
                            AudioBitrateKbps = PositiveOrNull(audioStream?.Bitrate) ?? PositiveOrNull(x.Bitrate),
                            AudioBitDepth = PositiveOrNull(audioStream?.BitDepth),
                            AudioSampleRateHz = PositiveOrNull(audioStream?.SamplingRate),
                            AudioChannels = PositiveOrNull(audioStream?.Channels) ?? PositiveOrNull(x.AudioChannels),
                            AudioChannelLayout = FirstMeaningful(audioStream?.AudioChannelLayout),
                            AudioProfile = FirstMeaningful(audioStream?.Profile, x.AudioProfile),
                            AudioQuality = AudioQuality.Unknown,
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
                        mediaData.AudioQuality = mediaData.ClassifyAudioQuality();
                        return mediaData;
                    }
                )
            )
            .ToList();

        var track = new PlexMusicTrack
        {
            Id = 0,
            Title = source.Title,
            SortIndex = source.SortIndex,
            SearchTitle = source.SearchTitle,
            Duration = source.Duration,
            MediaSize = source.Media.Sum(x => x.Parts.Sum(p => p.Size)),
            VideoQuality = VideoQuality.None,
            ChildCount = source.ChildCount,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            ParentKey = source.GetParentKey(),
            MediaDataList = mediaDataList,
            PlexApiRatingKey = source.RatingKey,
            PlexApiIdentityId = source.Guid.Replace("plex://track/", string.Empty),
            PlexApiMetaDataKey = RetrieveMetaDataKey(source),
            HasThumb = !string.IsNullOrEmpty(source.Thumb),
            HasArt = !string.IsNullOrEmpty(source.Art),
            PlexAlbumId = 0,
            PlexAlbum = default,
            PlexLibraryId = 0,
            PlexServerId = 0,
            DiscNumber = source.ParentIndex >= 0 ? source.ParentIndex : null,
            TrackNumber = source.Index >= 0 ? source.Index : null,
            MusicBrainzReleaseTrackId = GetMusicBrainzId(source),
        };

        foreach (var mediaData in mediaDataList)
            mediaData.PlexTrack = track;

        return track;
    }

    private static int? PositiveOrNull(int? value) => value > 0 ? value : null;

    private static string? FirstMeaningful(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
