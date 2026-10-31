namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<PlexOtherVideo> _plexOtherVideo = new Faker<PlexOtherVideo>()
        .ApplyBasePlexMedia()
        .RuleFor(x => x.Title, f => f.Lorem.Sentence(3))
        .RuleFor(x => x.Year, f => f.Random.Int(1900, 2030))
        .RuleFor(x => x.Summary, f => f.Lorem.Sentences(2))
        .RuleFor(x => x.OriginallyAvailableAt, f => f.Date.Recent(30))
        .RuleFor(x => x.Guid, (_, x) => $"com.plexapp.agents.none://{x.PlexApiRatingKey}")
        .RuleFor(x => x.ChildCount, _ => 0)
        .RuleFor(x => x.SortIndex, f => f.IndexFaker + 1)
        .Ignore(x => x.MediaDataList)
        .Ignore(x => x.Actors)
        .Ignore(x => x.Genres)
        .Ignore(x => x.Countries)
        .FinishWith(
            (_, video) =>
            {
                var original = video.MediaDataList.Single();
                original.PlexOtherVideo = video;
                original.PlexOtherVideoId = video.Id;
                original.UpdateInitProperty(nameof(original.PlexApiRatingKey), video.PlexApiRatingKey);
                video.Duration = original.Duration / 1000;
                video.MediaSize = original.Size;
                video.VideoQuality = original.VideoQuality;
            }
        );

    public static Faker<PlexOtherVideo> GetPlexOtherVideos(Seed seed, Action<FakeDataConfig>? options = null)
    {
        var mediaDataFaker = GetPlexOtherVideoMediaData(seed, options);
        return _plexOtherVideo
            .Clone() // Clone to isolate per-call media-data rules and seed state.
            .RuleFor(x => x.MediaDataList, _ => mediaDataFaker.Generate(1))
            .UseSeed(seed.Next());
    }

    private static readonly Faker<PlexOtherVideoMediaData> _plexOtherVideoMediaData =
        new Faker<PlexOtherVideoMediaData>()
            .ApplyBasePlexMediaData(new FakeDataConfig())
            .Ignore(x => x.PlexOtherVideoId)
            .Ignore(x => x.PlexOtherVideo)
            .Ignore(x => x.Streams)
            .RuleFor(x => x.PartIndex, _ => 0)
            .RuleFor(x => x.VideoCodec, _ => "h264")
            .RuleFor(x => x.AudioCodec, _ => "aac")
            .RuleFor(x => x.VideoQuality, _ => VideoQuality.FullHD)
            .RuleFor(x => x.Container, _ => "mp4")
            .RuleFor(x => x.Source, _ => ReleaseSource.None)
            .RuleFor(x => x.OriginalFilename, (_, x) => $"video-{x.PlexApiPartId}.mp4")
            .RuleFor(x => x.Key, (_, x) => $"/library/parts/{x.PlexApiPartId}/file.mp4");

    public static Faker<PlexOtherVideoMediaData> GetPlexOtherVideoMediaData(
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        return _plexOtherVideoMediaData
            .Clone() // Clone to isolate per-call size rules and caller overrides.
            .RuleFor(
                x => x.Size,
                _ =>
                    config.DownloadFileSizeInMb > 0
                        ? (long)ByteSize.FromMebiBytes(config.DownloadFileSizeInMb).Bytes
                        : (long)ByteSize.FromMebiBytes(10).Bytes
            )
            .UseSeed(seed.Next());
    }
}
