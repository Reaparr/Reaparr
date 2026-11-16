namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static ICollection<PlexMovieDataStream> ToPlexMovieDataStreams(this LibraryMediaItemPartDTO source) =>
        [
            .. source.Stream.Select(stream => new PlexMovieDataStream
            {
                Id = 0,
                MediaDataId = 0,
                PlexApiStreamId = stream.Id,
                StreamType = stream.StreamType,
                LanguageId = 0,
                IsExternal = null,
                SidecarFormat = stream.NormalizeSidecarFormat(),
                Language = stream.ToStreamLanguage(),
            }),
        ];

    public static ICollection<PlexTvShowEpisodeDataStream> ToPlexTvShowEpisodeDataStreams(
        this LibraryMediaItemPartDTO source
    ) =>
        [
            .. source.Stream.Select(stream => new PlexTvShowEpisodeDataStream
            {
                Id = 0,
                MediaDataId = 0,
                PlexApiStreamId = stream.Id,
                StreamType = stream.StreamType,
                LanguageId = 0,
                IsExternal = null,
                SidecarFormat = stream.NormalizeSidecarFormat(),
                Language = stream.ToStreamLanguage(),
            }),
        ];

    public static ICollection<PlexMusicTrackDataStream> ToPlexMusicTrackDataStreams(
        this LibraryMediaItemPartDTO source
    ) =>
        [
            .. source.Stream.Select(stream => new PlexMusicTrackDataStream
            {
                Id = 0,
                MediaDataId = 0,
                PlexApiStreamId = stream.Id,
                StreamType = stream.StreamType,
                LanguageId = 0,
                IsExternal = null,
                SidecarFormat = stream.NormalizeSidecarFormat(),
                Language = stream.ToStreamLanguage(),
            }),
        ];

    public static ICollection<PlexOtherVideoDataStream> ToPlexOtherVideoDataStreams(
        this LibraryMediaItemPartDTO source
    ) =>
        [
            .. source.Stream.Select(stream => new PlexOtherVideoDataStream
            {
                Id = 0,
                MediaDataId = 0,
                PlexApiStreamId = stream.Id,
                StreamType = stream.StreamType,
                LanguageId = 0,
                IsExternal = null,
                SidecarFormat = stream.NormalizeSidecarFormat(),
                Language = stream.ToStreamLanguage(),
            }),
        ];

    private static StreamLanguage ToStreamLanguage(this LibraryMediaItemStreamDTO stream) =>
        new()
        {
            Id = 0,
            LanguageCode = stream.LanguageCode,
            LanguageTag = stream.LanguageTag,
        };

    private static string? NormalizeSidecarFormat(this LibraryMediaItemStreamDTO stream)
    {
        if (stream.StreamType is not (StreamType.Subtitle or StreamType.Lyrics))
            return null;

        var sourceFormat = stream.Format?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(sourceFormat))
            return sourceFormat;

        return stream.Codec.Trim().ToLowerInvariant() switch
        {
            "srt" => "srt",
            "lrc" => "lrc",
            _ => null,
        };
    }
}
