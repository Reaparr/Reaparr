namespace Reaparr.BaseTests;

public static partial class FakeData
{
    #region LibraryMediaItemDTO

    private static readonly Faker<LibraryMediaItemDTO> _libraryMediaItemDTO = new Faker<LibraryMediaItemDTO>()
        .StrictMode(true)
        .RuleFor(x => x.RatingKey, _ => GetUniqueNumber())
        .RuleFor(x => x.Key, (_, x) => $"/library/metadata/{x.RatingKey}")
        .RuleFor(x => x.Type, f => f.PickRandom(PlexMediaType.Movie, PlexMediaType.Episode))
        .RuleFor(x => x.Title, (f, x) => f.PlexMedia().MediaTitle(x.Type))
        .RuleFor(x => x.Summary, f => f.Lorem.Sentences(2))
        .RuleFor(x => x.Year, f => f.Random.Int(1900, 2030))
        .RuleFor(x => x.ParentIndex, f => f.Random.Int(1, 10))
        .RuleFor(x => x.Index, f => f.Random.Int(1, 100))
        .RuleFor(x => x.Studio, f => f.Company.CompanyName())
        .RuleFor(x => x.ContentRating, f => f.PickRandom("G", "PG", "PG-13", "R", "NC-17", "TV-MA", "TV-14"))
        .RuleFor(x => x.SortTitle, (_, x) => x.Title.ToSortTitle())
        .RuleFor(x => x.SearchTitle, (_, x) => x.Title.ToSearchTitle())
        .RuleFor(x => x.SortIndex, f => f.Random.Int(1, 100000))
        .RuleFor(x => x.OriginalTitle, (_, x) => x.Title)
        .RuleFor(x => x.ChildCount, f => f.Random.Int(0, 10))
        .RuleFor(x => x.Duration, f => f.Random.Int(1000, 3000000))
        .RuleFor(x => x.Rating, f => f.Random.Float(0.1f, 10.0f))
        .RuleFor(x => x.Thumb, f => f.Image.PicsumUrl())
        .RuleFor(x => x.Art, f => f.Image.PicsumUrl())
        .RuleFor(x => x.Theme, f => f.Image.PicsumUrl())
        .RuleFor(x => x.Guid, (f, x) => f.PlexMedia().Guid(x.Type))
        .RuleFor(
            x => x.GrandparentTitle,
            f => f.Random.Bool() ? f.PlexMedia().MediaTitle(PlexMediaType.TvShow) : string.Empty
        )
        .RuleFor(x => x.ParentTitle, f => f.Random.Bool() ? $"Season {f.Random.Int(1, 10)}" : string.Empty)
        .RuleFor(x => x.ParentGuid, f => f.Random.Bool() ? f.PlexMedia().Guid(PlexMediaType.Season) : string.Empty)
        .RuleFor(x => x.ParentRatingKey, f => f.Random.Bool() ? GetUniqueNumber().ToString() : string.Empty)
        .RuleFor(x => x.AudienceRating, f => f.Random.Bool() ? f.Random.Double(0.1, 10.0) : null)
        .RuleFor(x => x.AddedAt, f => f.Date.Recent(30))
        .RuleFor(x => x.UpdatedAt, f => f.Date.Recent(30))
        .RuleFor(x => x.OriginallyAvailableAt, f => f.Date.Recent(365).ToString("yyyy-MM-dd"))
        .RuleFor(x => x.Ratings, _ => [])
        .RuleFor(x => x.Guids, _ => [])
        .RuleFor(x => x.Media, _ => [])
        .RuleFor(x => x.Genre, _ => [])
        .RuleFor(x => x.Country, _ => [])
        .RuleFor(x => x.Role, _ => []);

    public static Faker<LibraryMediaItemDTO> GetLibraryMediaItemDTO(
        Seed seed,
        Action<FakeDataConfig>? options = null,
        PlexMediaType? mediaType = null
    )
    {
        var typeToUse = mediaType ?? PlexMediaType.Movie;

        return _libraryMediaItemDTO
            .Clone()
            .RuleFor(x => x.Type, _ => typeToUse)
            .RuleFor(x => x.Title, (f, x) => f.PlexMedia().MediaTitle(x.Type))
            .RuleFor(x => x.Guid, (f, x) => f.PlexMedia().Guid(x.Type))
            .RuleFor(
                x => x.GrandparentTitle,
                (f, x) =>
                    x.Type == PlexMediaType.Episode ? f.PlexMedia().MediaTitle(PlexMediaType.TvShow) : string.Empty
            )
            .RuleFor(
                x => x.ParentTitle,
                (f, x) => x.Type == PlexMediaType.Episode ? $"Season {f.Random.Int(1, 10)}" : string.Empty
            )
            .RuleFor(
                x => x.ParentGuid,
                (f, x) => x.Type == PlexMediaType.Episode ? f.PlexMedia().Guid(PlexMediaType.Season) : string.Empty
            )
            .RuleFor(
                x => x.ParentRatingKey,
                (_, x) => x.Type == PlexMediaType.Episode ? GetUniqueNumber().ToString() : string.Empty
            )
            .RuleFor(x => x.Ratings, f => GetMetaDataRatingsDTO(seed).Generate(f.Random.Int(1, 3)))
            .RuleFor(x => x.Guids, f => GetMetaDataGuidsDTO(seed).Generate(f.Random.Int(1, 4)))
            .RuleFor(x => x.Media, _ => GetLibraryMediaItemMediaDTO(seed, options).Generate(1))
            .RuleFor(x => x.Genre, f => GetLibraryMediaItemGenreDTO(seed).Generate(f.Random.Int(1, 5)))
            .RuleFor(x => x.Country, f => GetLibraryMediaItemCountryDTO(seed).Generate(f.Random.Int(1, 3)))
            .RuleFor(x => x.Role, f => GetLibraryMediaItemRoleDTO(seed).Generate(f.Random.Int(1, 10)))
            .RuleFor(x => x.Duration, (_, x) => x.Media.Sum(y => y.Duration))
            .UseSeed(seed.Next());
    }

    #endregion

    #region MetaDataRatingsDTO

    private static readonly Faker<MetaDataRatingsDTO> _metaDataRatingsDTO = new Faker<MetaDataRatingsDTO>()
        .StrictMode(true)
        .RuleFor(x => x.Image, f => f.Image.PicsumUrl())
        .RuleFor(x => x.Value, f => f.Random.Float(0.1f, 10.0f))
        .RuleFor(x => x.Type, f => f.PickRandom("audience", "critic", "imdb", "tmdb", "tvdb"));

    public static Faker<MetaDataRatingsDTO> GetMetaDataRatingsDTO(Seed seed) =>
        _metaDataRatingsDTO.UseSeed(seed.Next());

    #endregion

    #region MetaDataGuidsDTO

    private static readonly Faker<MetaDataGuidsDTO> _metaDataGuidsDTO = new Faker<MetaDataGuidsDTO>()
        .StrictMode(true)
        .Ignore(x => x.Id)
        .CustomInstantiator(f =>
        {
            var guidType = f.PickRandom("imdb", "tmdb", "tvdb", "plex");
            var id = guidType switch
            {
                "imdb" => "tt" + f.Random.Int(10000, 99999),
                "tmdb" => f.Random.Int(10000, 99999).ToString(),
                "tvdb" => f.Random.Int(10000, 99999).ToString(),
                "plex" => $"plex://{f.PlexMedia().Guid(PlexMediaType.Movie)}",
                _ => f.Random.Guid().ToString(),
            };
            return new MetaDataGuidsDTO(id);
        });

    public static Faker<MetaDataGuidsDTO> GetMetaDataGuidsDTO(Seed seed) => _metaDataGuidsDTO.UseSeed(seed.Next());

    #endregion

    #region LibraryMediaItemCountryDTO

    private static readonly Faker<LibraryMediaItemCountryDTO> _libraryMediaItemCountryDTO =
        new Faker<LibraryMediaItemCountryDTO>()
            .StrictMode(true)
            .RuleFor(x => x.PlexId, f => f.Random.Int(1, 10000))
            .RuleFor(x => x.Name, f => f.Address.Country())
            .RuleFor(x => x.Filter, _ => string.Empty)
            .RuleFor(x => x.Key, (_, x) => x.Name.ToMd5Hash());

    public static Faker<LibraryMediaItemCountryDTO> GetLibraryMediaItemCountryDTO(Seed seed) =>
        _libraryMediaItemCountryDTO.UseSeed(seed.Next());

    #endregion

    #region LibraryMediaItemGenreDTO

    private static readonly Faker<LibraryMediaItemGenreDTO> _libraryMediaItemGenreDTO =
        new Faker<LibraryMediaItemGenreDTO>()
            .StrictMode(true)
            .RuleFor(x => x.PlexId, f => f.Random.Int(1, 10000))
            .RuleFor(x => x.Name, f => f.PlexMedia().MediaGenre())
            .RuleFor(x => x.Filter, _ => string.Empty)
            .RuleFor(x => x.Key, (_, x) => x.Name.ToMd5Hash());

    public static Faker<LibraryMediaItemGenreDTO> GetLibraryMediaItemGenreDTO(Seed seed) =>
        _libraryMediaItemGenreDTO.UseSeed(seed.Next());

    #endregion

    #region LibraryMediaItemRoleDTO

    private static readonly Faker<LibraryMediaItemRoleDTO> _libraryMediaItemRoleDTO =
        new Faker<LibraryMediaItemRoleDTO>()
            .StrictMode(true)
            .RuleFor(x => x.PlexId, f => f.Random.Int(1, 10000))
            .RuleFor(x => x.Name, f => f.Name.FullName())
            .RuleFor(x => x.Role, f => f.Random.Bool() ? f.PickRandom("Actor", "Director", "Writer", "Producer") : null)
            .RuleFor(x => x.Filter, f => f.Random.Bool() ? f.Lorem.Word() : null)
            .RuleFor(x => x.Thumb, f => f.Random.Bool() ? f.Image.PicsumUrl() : null)
            .RuleFor(x => x.Key, (_, x) => x.Name.ToMd5Hash());

    public static Faker<LibraryMediaItemRoleDTO> GetLibraryMediaItemRoleDTO(Seed seed) =>
        _libraryMediaItemRoleDTO.UseSeed(seed.Next());

    #endregion

    #region LibraryMediaItemStreamDTO

    private static Faker<LibraryMediaItemStreamDTO> GetLibraryMediaItemStreamDTOBase(Seed seed, StreamType streamType)
    {
        var baseFaker = new Faker<LibraryMediaItemStreamDTO>()
            .StrictMode(true)
            .RuleFor(x => x.Id, _ => GetUniqueNumber())
            .RuleFor(x => x.StreamType, _ => streamType)
            .RuleFor(x => x.Default, f => f.Random.Bool())
            .RuleFor(x => x.Codec, f => GetCodecForStreamType(f, streamType))
            .RuleFor(x => x.Index, f => f.Random.Int(0, 10))
            .RuleFor(x => x.Bitrate, f => f.Random.Int(1000, 10000000))
            .RuleFor(x => x.Language, f => f.Address.CountryCode())
            .RuleFor(x => x.LanguageTag, (_, x) => x.Language.ToLowerInvariant())
            .RuleFor(x => x.LanguageCode, (_, x) => x.Language.ToLowerInvariant())
            .RuleFor(x => x.DisplayTitle, (_, x) => GetDisplayTitleForStream(x, streamType))
            .RuleFor(x => x.ExtendedDisplayTitle, (_, x) => GetExtendedDisplayTitleForStream(x, streamType))
            .RuleFor(x => x.Selected, f => streamType == StreamType.Audio ? f.Random.Bool() : null)
            .RuleFor(x => x.Forced, f => streamType == StreamType.Subtitle ? f.Random.Bool() : null)
            .RuleFor(x => x.CanAutoSync, f => streamType == StreamType.Subtitle ? f.Random.Bool() : null)
            .RuleFor(x => x.HearingImpaired, f => streamType == StreamType.Subtitle ? f.Random.Bool() : null)
            .RuleFor(x => x.Dub, f => streamType == StreamType.Audio ? f.Random.Bool() : null)
            .RuleFor(x => x.Title, f => f.Random.Bool() ? f.Lorem.Sentence() : null);

        // Video stream specific properties
        if (streamType == StreamType.Video)
        {
            baseFaker
                .RuleFor(x => x.DOVIBLCompatID, f => f.Random.Bool() ? f.Random.Int(1, 10) : null)
                .RuleFor(x => x.DOVIBLPresent, f => f.Random.Bool())
                .RuleFor(x => x.DOVIELPresent, f => f.Random.Bool())
                .RuleFor(x => x.DOVILevel, f => f.Random.Bool() ? f.Random.Int(1, 10) : null)
                .RuleFor(x => x.DOVIPresent, f => f.Random.Bool())
                .RuleFor(x => x.DOVIProfile, f => f.Random.Bool() ? f.Random.Int(1, 10) : null)
                .RuleFor(x => x.DOVIRPUPresent, f => f.Random.Bool())
                .RuleFor(x => x.DOVIVersion, f => f.Random.Bool() ? f.Random.Float(1.0f, 2.0f).ToString("F1") : null)
                .RuleFor(x => x.BitDepth, f => f.PickRandom(8, 10, 12))
                .RuleFor(x => x.ChromaLocation, f => f.PickRandom("left", "center", "top", "topleft"))
                .RuleFor(x => x.ChromaSubsampling, f => f.PickRandom("4:2:0", "4:2:2", "4:4:4"))
                .RuleFor(x => x.CodedHeight, f => f.PickRandom(480, 720, 1080, 2160))
                .RuleFor(x => x.CodedWidth, f => f.PickRandom(640, 1280, 1920, 3840))
                .RuleFor(x => x.ColorPrimaries, f => f.PickRandom("bt709", "bt2020", "smpte170m"))
                .RuleFor(x => x.ColorRange, f => f.PickRandom("tv", "pc"))
                .RuleFor(x => x.ColorSpace, f => f.PickRandom("bt709", "bt2020nc"))
                .RuleFor(x => x.ColorTrc, f => f.PickRandom("bt709", "smpte2084", "arib-std-b67"))
                .RuleFor(
                    x => x.FrameRate,
                    f => f.PickRandom(23.976f, 24.0f, 25.0f, 29.97f, 30.0f, 50.0f, 59.94f, 60.0f)
                )
                .RuleFor(x => x.Height, f => f.PickRandom(480, 720, 1080, 2160))
                .RuleFor(x => x.Level, f => f.Random.Int(30, 60))
                .RuleFor(x => x.Original, f => f.Random.Bool())
                .RuleFor(x => x.HasScalingMatrix, f => f.Random.Bool())
                .RuleFor(x => x.Profile, f => f.PickRandom("baseline", "main", "high", "high 10"))
                .RuleFor(x => x.ScanType, f => f.PickRandom("progressive", "interlaced"))
                .RuleFor(x => x.RefFrames, f => f.Random.Int(1, 16))
                .RuleFor(x => x.Width, f => f.PickRandom(640, 1280, 1920, 3840));
        }
        else
        {
            baseFaker
                .RuleFor(x => x.DOVIBLCompatID, _ => null)
                .RuleFor(x => x.DOVIBLPresent, _ => null)
                .RuleFor(x => x.DOVIELPresent, _ => null)
                .RuleFor(x => x.DOVILevel, _ => null)
                .RuleFor(x => x.DOVIPresent, _ => null)
                .RuleFor(x => x.DOVIProfile, _ => null)
                .RuleFor(x => x.DOVIRPUPresent, _ => null)
                .RuleFor(x => x.DOVIVersion, _ => null)
                .RuleFor(x => x.BitDepth, _ => null)
                .RuleFor(x => x.ChromaLocation, _ => null)
                .RuleFor(x => x.ChromaSubsampling, _ => null)
                .RuleFor(x => x.CodedHeight, _ => null)
                .RuleFor(x => x.CodedWidth, _ => null)
                .RuleFor(x => x.ColorPrimaries, _ => null)
                .RuleFor(x => x.ColorRange, _ => null)
                .RuleFor(x => x.ColorSpace, _ => null)
                .RuleFor(x => x.ColorTrc, _ => null)
                .RuleFor(x => x.FrameRate, _ => null)
                .RuleFor(x => x.Height, _ => null)
                .RuleFor(x => x.Level, _ => null)
                .RuleFor(x => x.Original, _ => null)
                .RuleFor(x => x.HasScalingMatrix, _ => null)
                .RuleFor(x => x.Profile, _ => null)
                .RuleFor(x => x.ScanType, _ => null)
                .RuleFor(x => x.RefFrames, _ => null)
                .RuleFor(x => x.Width, _ => null);
        }

        // Audio stream specific properties
        if (streamType == StreamType.Audio)
        {
            baseFaker
                .RuleFor(x => x.Channels, f => f.PickRandom(1, 2, 5, 6, 7, 8))
                .RuleFor(x => x.AudioChannelLayout, f => f.PickRandom("mono", "stereo", "5.1", "7.1"))
                .RuleFor(x => x.SamplingRate, f => f.PickRandom(44100, 48000, 96000, 192000));
        }
        else
        {
            baseFaker
                .RuleFor(x => x.Channels, _ => null)
                .RuleFor(x => x.AudioChannelLayout, _ => null)
                .RuleFor(x => x.SamplingRate, _ => null);
        }

        return baseFaker.UseSeed(seed.Next());
    }

    private static string GetCodecForStreamType(Faker f, StreamType streamType) =>
        streamType switch
        {
            StreamType.Video => f.PickRandom("h264", "hevc", "mpeg2video", "vp9", "av1"),
            StreamType.Audio => f.PickRandom("ac3", "dca", "aac", "eac3", "truehd", "flac", "mp3"),
            StreamType.Subtitle => f.PickRandom("srt", "ass", "vtt", "subrip", "pgs"),
            _ => "unknown",
        };

    private static string GetDisplayTitleForStream(LibraryMediaItemStreamDTO stream, StreamType streamType) =>
        streamType switch
        {
            StreamType.Video => $"{stream.Codec} {stream.Width}x{stream.Height}",
            StreamType.Audio => $"{stream.Codec} {stream.AudioChannelLayout}",
            StreamType.Subtitle => $"{stream.Language} ({stream.Codec})",
            _ => stream.Codec,
        };

    private static string GetExtendedDisplayTitleForStream(LibraryMediaItemStreamDTO stream, StreamType streamType) =>
        streamType switch
        {
            StreamType.Video => $"{stream.Codec} {stream.Width}x{stream.Height} {stream.FrameRate} {stream.Profile}",
            StreamType.Audio => $"{stream.Codec} {stream.AudioChannelLayout} {stream.SamplingRate}Hz",
            StreamType.Subtitle => $"{stream.Language} ({stream.Codec})",
            _ => stream.Codec,
        };

    public static Faker<LibraryMediaItemStreamDTO> GetLibraryMediaItemStreamDTO(Seed seed, StreamType streamType) =>
        GetLibraryMediaItemStreamDTOBase(seed, streamType);

    #endregion

    #region LibraryMediaItemPartDTO

    private static readonly Faker<LibraryMediaItemPartDTO> _libraryMediaItemPartDTO =
        new Faker<LibraryMediaItemPartDTO>()
            .StrictMode(true)
            .RuleFor(x => x.Id, _ => GetUniqueNumber())
            .RuleFor(
                x => x.Key,
                f => $"/library/parts/{f.Random.Int(100000, 999999)}/{f.Random.Int(100000000, 999999999)}/file.mp4"
            )
            .RuleFor(x => x.Duration, f => f.Random.Int(50000, 5512400))
            .RuleFor(x => x.File, f => $"/media/movies/{f.System.FileName()}.mp4")
            .RuleFor(x => x.Size, f => f.Random.Long(1000000, 10000000000))
            .RuleFor(x => x.Container, f => f.PickRandom("mp4", "mkv", "avi", "m4v"))
            .RuleFor(x => x.Stream, _ => []);

    public static Faker<LibraryMediaItemPartDTO> GetLibraryMediaItemPartDTO(
        Seed seed,
        Action<FakeDataConfig>? options = null
    ) =>
        _libraryMediaItemPartDTO
            .RuleFor(
                x => x.Stream,
                f =>
                {
                    var streams = new List<LibraryMediaItemStreamDTO>();
                    streams.Add(GetLibraryMediaItemStreamDTO(seed, StreamType.Video).Generate());
                    streams.Add(GetLibraryMediaItemStreamDTO(seed, StreamType.Audio).Generate());
                    if (f.Random.Bool())
                    {
                        streams.Add(GetLibraryMediaItemStreamDTO(seed, StreamType.Subtitle).Generate());
                    }
                    return streams;
                }
            )
            .UseSeed(seed.Next());

    #endregion

    #region LibraryMediaItemMediaDTO

    private static readonly Faker<LibraryMediaItemMediaDTO> _libraryMediaItemMediaDTO =
        new Faker<LibraryMediaItemMediaDTO>()
            .StrictMode(true)
            .RuleFor(x => x.Id, _ => GetUniqueNumber())
            .RuleFor(x => x.Duration, f => f.Random.Int(50000, 55124400))
            .RuleFor(x => x.Bitrate, f => f.Random.Int(1000000, 50000000))
            .RuleFor(x => x.Width, f => f.PickRandom(640, 1280, 1920, 3840))
            .RuleFor(x => x.Height, f => f.PickRandom(480, 720, 1080, 2160))
            .RuleFor(x => x.AspectRatio, f => f.Random.Float(1.33f, 2.39f))
            .RuleFor(x => x.AudioChannels, f => f.PickRandom(2, 5, 6, 7, 8))
            .RuleFor(x => x.AudioCodec, f => f.PickRandom("ac3", "dca", "aac", "eac3", "truehd"))
            .RuleFor(x => x.VideoCodec, f => f.PickRandom("h264", "hevc", "mpeg2video"))
            .RuleFor(
                x => x.VideoResolution,
                f => f.PickRandom(VideoQuality.SD, VideoQuality.HD, VideoQuality.FullHD, VideoQuality.UHD_4K)
            )
            .RuleFor(x => x.Container, f => f.PickRandom("mp4", "mkv", "avi", "m4v"))
            .RuleFor(x => x.VideoFrameRate, f => f.PickRandom("24p", "25p", "30p", "50p", "60p"))
            .RuleFor(x => x.VideoProfile, f => f.PickRandom("baseline", "main", "high", "high 10"))
            .RuleFor(x => x.AudioProfile, f => f.PickRandom("dts", "dts-hd", "dolby", "dolby digital"))
            .RuleFor(x => x.Parts, _ => []);

    public static Faker<LibraryMediaItemMediaDTO> GetLibraryMediaItemMediaDTO(
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        return _libraryMediaItemMediaDTO
            .Clone()
            .RuleFor(x => x.OptimizedForStreaming, f => f.Random.Bool())
            .RuleFor(
                x => x.Parts,
                _ => GetLibraryMediaItemPartDTO(seed, options).Generate(config.IncludeMultiPartMovies ? 2 : 1)
            )
            .RuleFor(x => x.Duration, f => f.Random.Int(50000, 55124400))
            .UseSeed(seed.Next());
    }

    #endregion
}
