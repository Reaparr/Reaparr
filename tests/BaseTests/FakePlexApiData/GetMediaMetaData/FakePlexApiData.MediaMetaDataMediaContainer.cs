using Bogus.Hollywood;
using LukeHagar.PlexAPI.SDK.Models.Components;
using NodaTime;
using Stream = LukeHagar.PlexAPI.SDK.Models.Components.Stream;
using StreamType = LukeHagar.PlexAPI.SDK.Models.Components.StreamType;

namespace Reaparr.BaseTests;

public partial class FakePlexApiData
{
    private static readonly Faker<MediaContainerWithMetadataMediaContainer> _getMediaMetaDataMediaContainer =
        new Faker<MediaContainerWithMetadataMediaContainer>()
            .StrictMode(true)
            .RuleFor(x => x.Identifier, _ => "com.plexapp.plugins.library")
            .Ignore(x => x.Metadata) // Generated in FinishWith
            .Ignore(x => x.TotalSize)
            .Ignore(x => x.Offset)
            .Ignore(x => x.Size); // Generated in FinishWith

    private static readonly Faker<Metadata> _getMediaMetaDataMetadata = new Faker<Metadata>()
        .StrictMode(false)
        .RuleFor(l => l.RatingKey, _ => GetUniqueNumber().ToString())
        .Ignore(l => l.ParentRatingKey) // Generated in FinishWith
        .RuleFor(l => l.Key, (_, x) => $"/library/metadata/{x.RatingKey}")
        .RuleFor(l => l.Type, _ => "movie") // Generated in FinishWith
        .Ignore(l => l.Guid) // Generated in FinishWith
        .RuleFor(l => l.Studio, f => f.Movies().Production())
        .RuleFor(l => l.Title, f => f.Movies().MovieTitle())
        .RuleFor(l => l.TitleSort, (_, x) => x.Title.ToLower())
        .RuleFor(l => l.ContentRating, _ => "nl/6")
        .RuleFor(l => l.Summary, f => f.Movies().MovieOverview())
        .RuleFor(l => l.Rating, f => f.Random.Float() * 10)
        .RuleFor(l => l.AudienceRating, f => f.Random.Float() * 10)
        .RuleFor(l => l.ViewOffset, f => f.Random.Int(1))
        .RuleFor(l => l.LastViewedAt, _ => 0)
        .RuleFor(l => l.Year, f => f.Random.Int(0, DateTime.UtcNow.Year))
        .RuleFor(l => l.AddedAt, f => f.Date.Past().ToUnixLong())
        .RuleFor(l => l.UpdatedAt, f => f.Date.Recent().ToUnixLong())
        .RuleFor(l => l.Thumb, (_, x) => $"/library/metadata/{x.RatingKey}/thumb/{x.UpdatedAt}")
        .RuleFor(l => l.Art, (_, x) => $"/library/metadata/{x.RatingKey}/art/{x.UpdatedAt}")
        .RuleFor(l => l.Theme, (f, x) => $"/library/metadata/{x.RatingKey}/theme/{f.Random.Int(100000, 1000000)}")
        .RuleFor(l => l.Duration, f => f.Random.Int(1))
        .RuleFor(l => l.OriginallyAvailableAt, _ => LocalDate.MinIsoValue)
        .RuleFor(l => l.AudienceRatingImage, _ => "rottentomatoes://image.rating.upright")
        .RuleFor(l => l.Index, f => f.Random.Int(1))
        .RuleFor(l => l.LeafCount, f => f.Random.Int(1))
        .RuleFor(l => l.ViewedLeafCount, f => f.Random.Int(1))
        .RuleFor(l => l.ChildCount, f => f.Random.Int(1, 10))
        .RuleFor(l => l.SkipChildren, _ => SkipChildren.CreateBoolean(false))
        .RuleFor(l => l.SkipParent, _ => SkipParent.CreateBoolean(false))
        .RuleFor(l => l.ViewCount, _ => default)
        .Ignore(l => l.Media) // Generated in FinishWith
        .Ignore(l => l.Role) // Generated in FinishWith
        .Ignore(l => l.Genre) // Generated in FinishWith
        .Ignore(l => l.Country) // Generated in FinishWith
        .RuleFor(
            x => x.Guids,
            f =>
                [
                    new Guids { Id = $"imdb://tt{f.Random.Number(1_000_000, 9_999_999)}" },
                    new Guids { Id = $"tmdb://tt{f.Random.Number(100_000, 999_999)}" },
                    new Guids { Id = $"tvdb://{f.Random.Number(10_000, 99_999)}" },
                ]
        );

    private static readonly Faker<Tag> _getMediaMetaDataRole = new Faker<Tag>()
        .StrictMode(true)
        .RuleFor(x => x.Id, _ => GetUniqueNumber())
        .RuleFor(x => x.TagValue, f => f.Movies().ActorName())
        .RuleFor(x => x.Role, f => f.Movies().MovieTagline())
        .RuleFor(x => x.Filter, (_, x) => $"actor={x.Id}")
        .RuleFor(x => x.TagKey, f => f.Random.AlphaNumeric(24))
        .RuleFor(x => x.Thumb, f => f.Image.PicsumUrl())
        .RuleFor(x => x.RatingKey, f => f.Random.Number(100000).ToString())
        .Ignore(x => x.TagType)
        .Ignore(x => x.Confidence)
        .Ignore(x => x.Context);

    private static readonly Faker<Tag> _getMediaMetaDataGenre = new Faker<Tag>()
        .StrictMode(true)
        .RuleFor(x => x.Id, _ => GetUniqueNumber())
        .RuleFor(x => x.TagValue, f => f.PlexMedia().MediaGenre())
        .RuleFor(x => x.Filter, (_, x) => $"genre={x.Id}")
        .Ignore(x => x.Role)
        .Ignore(x => x.RatingKey)
        .Ignore(x => x.TagKey)
        .Ignore(x => x.TagType)
        .Ignore(x => x.Thumb)
        .Ignore(x => x.Confidence)
        .Ignore(x => x.Context);

    private static readonly Faker<Tag> _getMediaMetaDataCountry = new Faker<Tag>()
        .StrictMode(true)
        .RuleFor(x => x.Id, _ => GetUniqueNumber())
        .RuleFor(x => x.TagValue, f => f.Address.Country())
        .RuleFor(x => x.Filter, (_, x) => $"country={x.Id}")
        .Ignore(x => x.Role)
        .Ignore(x => x.RatingKey)
        .Ignore(x => x.TagKey)
        .Ignore(x => x.TagType)
        .Ignore(x => x.Thumb)
        .Ignore(x => x.Confidence)
        .Ignore(x => x.Context);

    private static readonly Faker<Media> _getMediaMetaDataMedia = new Faker<Media>()
        .StrictMode(true)
        .RuleFor(l => l.Id, _ => GetUniqueNumber())
        .RuleFor(l => l.Duration, f => f.Random.Int(1))
        .RuleFor(l => l.Bitrate, f => f.Random.Int(1))
        .RuleFor(l => l.Width, f => f.Random.Int(1))
        .RuleFor(l => l.Height, f => f.Random.Int(1))
        .RuleFor(l => l.AspectRatio, f => f.Random.Float())
        .RuleFor(l => l.AudioChannels, f => f.Random.Int(1))
        .RuleFor(l => l.AudioCodec, f => f.Lorem.Word())
        .RuleFor(l => l.VideoCodec, f => f.Lorem.Word())
        .RuleFor(l => l.Container, f => f.Lorem.Word())
        .RuleFor(l => l.VideoFrameRate, _ => "24p")
        .RuleFor(l => l.AudioProfile, _ => "dts")
        .RuleFor(l => l.VideoProfile, _ => "high")
        .RuleFor(l => l.HasVoiceActivity, (f, _) => HasVoiceActivity.CreateBoolean(f.Random.Bool()))
        .RuleFor(l => l.Container, f => f.Lorem.Word())
        .RuleFor(l => l.VideoResolution, f => f.Lorem.Word())
        .RuleFor(
            l => l.OptimizedForStreaming,
            // set to null to avoid Unable to cast object of type 'System.Int64' to type 'System.String'.
            _ => null
        )
        .RuleFor(l => l.Has64bitOffsets, f => f.Random.Bool())
        .RuleFor(l => l.Part, _ => []) // Generated in FinishWith
        .Ignore(x => x.Selected)
        .Ignore(x => x.Uuid)
        .Ignore(x => x.AdditionalProperties);

    private static readonly Faker<Part> _getMediaMetaDataPartFaker = new Faker<Part>()
        .StrictMode(true)
        .RuleFor(l => l.Id, _ => GetUniqueNumber())
        .RuleFor(l => l.Key, f => f.Random.Uuid().ToString())
        .RuleFor(l => l.Duration, f => f.Random.Int(1))
        .RuleFor(l => l.File, f => f.Lorem.Word())
        .RuleFor(l => l.Accessible, _ => true)
        .RuleFor(l => l.Exists, _ => true)
        .RuleFor(l => l.Size, f => f.Random.Long(100_000_000, 8_589_934_592)) // 100MB to 8GB
        .RuleFor(l => l.OptimizedForStreaming, _ => true)
        .RuleFor(l => l.Has64bitOffsets, f => f.Random.Bool())
        .RuleFor(l => l.AudioProfile, _ => "dts")
        .RuleFor(l => l.Container, _ => "mkv")
        .RuleFor(l => l.Indexes, _ => "sd")
        .RuleFor(l => l.VideoProfile, _ => "high")
        .RuleFor(l => l.Stream, _ => [])
        .Ignore(x => x.DeepAnalysisVersion)
        .Ignore(x => x.PacketLength)
        .Ignore(x => x.Protocol)
        .Ignore(x => x.RequiredBandwidths)
        .Ignore(x => x.SyncItemId)
        .Ignore(x => x.SyncState)
        .Ignore(x => x.AdditionalProperties);

    private static readonly Faker<Stream> _getMediaMetaDataStreamFaker = new Faker<Stream>()
        .StrictMode(true)
        .RuleFor(x => x.Id, _ => GetUniqueNumber())
        .RuleFor(x => x.StreamType, f => f.PickRandom(StreamType.Video, StreamType.Audio, StreamType.Subtitle))
        .RuleFor(x => x.Format, f => f.System.CommonFileExt())
        .RuleFor(x => x.Default, f => f.Random.Bool())
        .RuleFor(x => x.Codec, f => f.Random.Word())
        .RuleFor(x => x.Index, f => f.Random.Int(0, 10))
        .RuleFor(x => x.Bitrate, f => f.Random.Int(64_000, 320_000))
        .RuleFor(x => x.Language, f => f.Language().LanguageName())
        .RuleFor(x => x.LanguageTag, f => f.Language().LanguageTag())
        .RuleFor(x => x.LanguageCode, f => f.Language().LanguageCode())
        .RuleFor(x => x.HeaderCompression, f => f.Random.Bool())
        .RuleFor(x => x.DOVIBLCompatID, f => f.Random.Int(0, 5))
        .RuleFor(x => x.DOVIBLPresent, f => f.Random.Bool())
        .RuleFor(x => x.DOVIELPresent, f => f.Random.Bool())
        .RuleFor(x => x.DOVILevel, f => f.Random.Int(0, 10))
        .RuleFor(x => x.DOVIPresent, f => f.Random.Bool())
        .RuleFor(x => x.DOVIProfile, f => f.Random.Int(0, 10))
        .RuleFor(x => x.DOVIRPUPresent, f => f.Random.Bool())
        .RuleFor(x => x.DOVIVersion, f => f.System.Semver())
        .RuleFor(x => x.BitDepth, f => f.Random.Int(8, 12))
        .RuleFor(x => x.ChromaLocation, f => f.PickRandom("left", "center", "topleft"))
        .RuleFor(x => x.ChromaSubsampling, f => f.PickRandom("4:2:0", "4:2:2", "4:4:4"))
        .RuleFor(x => x.CodedHeight, f => f.Random.Int(240, 2160))
        .RuleFor(x => x.CodedWidth, f => f.Random.Int(320, 3840))
        .RuleFor(x => x.ClosedCaptions, f => f.Random.Bool())
        .RuleFor(x => x.ColorPrimaries, f => f.PickRandom("bt709", "bt2020", "smpte"))
        .RuleFor(x => x.ColorRange, f => f.PickRandom("tv", "pc"))
        .RuleFor(x => x.ColorSpace, f => f.PickRandom("bt709", "bt601", "bt2020"))
        .RuleFor(x => x.ColorTrc, f => f.PickRandom("smpte2084", "arib-std-b67"))
        .RuleFor(x => x.FrameRate, f => f.Random.Float(23.976f, 60f))
        .RuleFor(x => x.Key, f => f.Random.Guid().ToString())
        .RuleFor(x => x.Height, f => f.Random.Int(240, 2160))
        .RuleFor(x => x.Level, f => f.Random.Int(1, 10))
        .RuleFor(x => x.Original, f => f.Random.Bool())
        .RuleFor(x => x.HasScalingMatrix, f => f.Random.Bool())
        .RuleFor(x => x.Profile, f => f.PickRandom("main", "high", "baseline"))
        .RuleFor(x => x.ScanType, f => f.PickRandom("progressive", "interlaced"))
        .RuleFor(x => x.EmbeddedInVideo, f => f.Random.Bool().ToString().ToLower())
        .RuleFor(x => x.RefFrames, f => f.Random.Int(1, 16))
        .RuleFor(x => x.Width, f => f.Random.Int(320, 3840))
        .RuleFor(x => x.DisplayTitle, f => f.Lorem.Sentence(3))
        .RuleFor(x => x.ExtendedDisplayTitle, f => f.Lorem.Sentence(5))
        .RuleFor(x => x.Selected, f => f.Random.Bool())
        .RuleFor(x => x.Forced, f => f.Random.Bool())
        .RuleFor(x => x.Channels, f => f.Random.Int(1, 8))
        .RuleFor(x => x.AudioChannelLayout, f => f.PickRandom("5.1", "7.1", "2.0"))
        .RuleFor(x => x.SamplingRate, f => f.PickRandom(44100, 48000, 96000))
        .RuleFor(x => x.CanAutoSync, f => CanAutoSync.CreateBoolean(f.Random.Bool()))
        .RuleFor(x => x.HearingImpaired, f => f.Random.Bool())
        .RuleFor(x => x.Dub, f => f.Random.Bool())
        .RuleFor(x => x.Title, f => f.Lorem.Word())
        .Ignore(x => x.StreamIdentifier)
        .Ignore(x => x.AlbumGain)
        .Ignore(x => x.AlbumPeak)
        .Ignore(x => x.AlbumRange)
        .Ignore(x => x.BitrateMode)
        .Ignore(x => x.EndRamp)
        .Ignore(x => x.Gain)
        .Ignore(x => x.Loudness)
        .Ignore(x => x.Lra)
        .Ignore(x => x.MinLines)
        .Ignore(x => x.Peak)
        .Ignore(x => x.PerfectMatch)
        .Ignore(x => x.Provider)
        .Ignore(x => x.ProviderTitle)
        .Ignore(x => x.Score)
        .Ignore(x => x.SourceKey)
        .Ignore(x => x.StartRamp)
        .Ignore(x => x.Timed)
        .Ignore(x => x.Transient)
        .Ignore(x => x.UserID)
        .Ignore(x => x.VisualImpaired)
        .Ignore(x => x.AdditionalProperties);
}
