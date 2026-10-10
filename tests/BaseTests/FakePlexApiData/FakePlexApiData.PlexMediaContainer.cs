using Bogus.Hollywood;
using LukeHagar.PlexAPI.SDK.Models.Components;
using Metadata = LukeHagar.PlexAPI.SDK.Models.Components.Metadata;

namespace Reaparr.BaseTests;

public partial class FakePlexApiData
{
    private static readonly Faker<Part> _getLibraryItemsPartFaker = new Faker<Part>()
        .StrictMode(true)
        .RuleFor(l => l.Id, _ => GetUniqueNumber())
        .RuleFor(l => l.Key, f => f.Random.Uuid().ToString())
        .RuleFor(l => l.Duration, f => f.Random.Int(1))
        .RuleFor(l => l.File, f => f.Lorem.Word())
        .RuleFor(l => l.Accessible, _ => true)
        .RuleFor(l => l.Exists, _ => true)
        .RuleFor(l => l.Size, f => f.Random.Long(100_000_000, 8_589_934_592)) // 100MB to 8GB
        .RuleFor(l => l.Stream, _ => [])
        .RuleFor(l => l.OptimizedForStreaming, f => f.Random.Bool())
        .RuleFor(l => l.Has64bitOffsets, f => f.Random.Bool())
        .RuleFor(l => l.AudioProfile, _ => "dts")
        .RuleFor(l => l.Container, _ => "mkv")
        .RuleFor(l => l.Indexes, _ => "sd")
        .RuleFor(l => l.VideoProfile, _ => "high");

    private static readonly Faker<Media> _getLibraryItemsMedia = new Faker<Media>()
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
        .RuleFor(l => l.Part, _ => []); // Generated in FinishWith

    private static readonly Faker<Metadata> _getLibraryItemsMetadata = new Faker<Metadata>()
        .StrictMode(false)
        .RuleFor(l => l.RatingKey, f => f.Random.Number(100000).ToString())
        .RuleFor(l => l.ParentRatingKey, f => f.Random.Number(100000).ToString())
        .RuleFor(l => l.Key, (_, x) => $"/library/metadata/{x.RatingKey}")
        .RuleFor(l => l.Type, _ => "movie") // Generated in FinishWith
        .RuleFor(l => l.Guid, _ => string.Empty) // Generated in FinishWith
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
        .RuleFor(l => l.Banner, (f, x) => $"/library/metadata/{x.RatingKey}/banner/{f.Random.Int(100000, 1000000)}")
        .RuleFor(l => l.Theme, (f, x) => $"/library/metadata/{x.RatingKey}/theme/{f.Random.Int(100000, 1000000)}")
        .RuleFor(l => l.Duration, f => f.Random.Int(1))
        // set to null to avoid serializing the entire LocalDate object, needs to be long
        .RuleFor(l => l.OriginallyAvailableAt, _ => null)
        .RuleFor(l => l.AudienceRatingImage, _ => "rottentomatoes://image.rating.upright")
        .RuleFor(l => l.Index, f => f.Random.Int(1))
        .RuleFor(l => l.LeafCount, f => f.Random.Int(1))
        .RuleFor(l => l.ViewedLeafCount, f => f.Random.Int(1))
        .RuleFor(l => l.ChildCount, f => f.Random.Int(1, 10))
        .RuleFor(l => l.SkipChildren, _ => SkipChildren.CreateBoolean(false))
        .RuleFor(l => l.SkipParent, _ => SkipParent.CreateBoolean(false))
        .RuleFor(l => l.ViewCount, _ => default)
        .RuleFor(l => l.Media, _ => []); // Generated in FinishWith

    /// <summary>
    /// Generates a fake response for the GetLibraryItemsResponse operation
    /// URL: /library/sections/{sectionKey}/{tag}
    /// </summary>
    private static readonly Faker<MediaContainerWithMetadataMediaContainer> _getLibraryItemsMediaContainer =
        new Faker<MediaContainerWithMetadataMediaContainer>()
            .StrictMode(true)
            .RuleFor(x => x.TotalSize, _ => -1L) // Generated in FinishWith
            .RuleFor(x => x.Offset, _ => 0L)
            .RuleFor(x => x.Identifier, _ => "com.plexapp.plugins.library")
            .Ignore(x => x.Metadata) // Generated in FinishWith
            .Ignore(x => x.Size); // Generated in FinishWith

    /// <summary>
    /// Generates a fake response for the GetLibraryItemsResponse operation
    /// URL: /library/sections/{sectionKey}/{tag}
    /// </summary>
    public static MediaContainerWithMetadata GetPlexLibrarySectionAllResponse(
        Seed seed,
        LibrarySection library,
        int mediaCount = 0,
        Action<PlexApiDataConfig>? options = null
    )
    {
        var config = PlexApiDataConfig.FromOptions(options);
        var type = library.Type.ToPlexMediaType();

        var totalSize = type switch
        {
            PlexMediaType.Movie => config.MoviesPerLibraryCount,
            PlexMediaType.TvShow => config.TvShowsPerLibraryCount
                * config.SeasonsPerTvShowCount
                * config.EpisodesPerSeasonCount,
            _ => throw new ArgumentOutOfRangeException(),
        };

        return new MediaContainerWithMetadata
        {
            MediaContainer = _getLibraryItemsMediaContainer
                .UseSeed(seed.Next())
                .FinishWith(
                    (_, x) =>
                    {
                        x.Metadata = GetLibraryMediaMetadata(seed, type, options).Generate(mediaCount);
                        x.Size = x.Metadata!.Count;
                        x.TotalSize = totalSize;
                    }
                )
                .Generate(),
        };
    }

    public static Faker<Metadata> GetLibraryMediaMetadata(
        Seed seed,
        PlexMediaType type,
        Action<PlexApiDataConfig>? options = null
    ) =>
        _getLibraryItemsMetadata
            .UseSeed(seed.Next())
            .FinishWith(
                (f, x) =>
                {
                    x.Type = type.ToPlexApiString();
                    x.Media = [GetPlexMedium(seed, options).Generate()];
                    x.Guid = f.PlexMedia().Guid(type);
                }
            );

    public static Faker<Media> GetPlexMedium(Seed seed, Action<PlexApiDataConfig>? options = null)
    {
        return _getLibraryItemsMedia
            .UseSeed(seed.Next())
            .FinishWith(
                (_, x) =>
                {
                    x.Part = [GetPlexPart(seed, options).Generate()];
                }
            );
    }

    public static Faker<Part> GetPlexPart(Seed seed, Action<PlexApiDataConfig>? options = null) =>
        _getLibraryItemsPartFaker.UseSeed(seed.Next());
}
