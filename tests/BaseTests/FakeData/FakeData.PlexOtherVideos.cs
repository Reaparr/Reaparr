namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<PlexOtherVideo> _plexOtherVideo = new Faker<PlexOtherVideo>()
        .ApplyBasePlexMedia()
        .RuleFor(x => x.Title, f => f.Lorem.Sentence(3))
        .RuleFor(x => x.Guid, (_, x) => $"com.plexapp.agents.none://{x.PlexApiRatingKey}")
        .RuleFor(x => x.Guid_IMDB, _ => null)
        .RuleFor(x => x.Guid_TMDB, _ => null)
        .RuleFor(x => x.Guid_TVDB, _ => null)
        .RuleFor(x => x.ChildCount, _ => 0)
        .RuleFor(x => x.SortIndex, f => f.IndexFaker + 1)
        .Ignore(x => x.MediaDataList)
        .FinishWith(
            (_, video) =>
            {
                var original = video.MediaDataList.Single();
                original.PlexOtherVideo = video;
                original.PlexOtherVideoId = video.Id;
                original.UpdateInitProperty(nameof(original.PlexApiRatingKey), video.PlexApiRatingKey);
                video.FullTitle = video.Title;
                video.Duration = original.Duration / 1000;
                video.MediaSize = original.Size;
                video.Quality = original.Quality;
            }
        );

    public static Faker<PlexOtherVideo> GetPlexOtherVideos(Seed seed, Action<FakeDataConfig>? options = null)
    {
        var mediaDataFaker = GetPlexOtherVideoMediaData(seed, options);
        return _plexOtherVideo
            .Clone()
            .RuleFor(x => x.MediaDataList, _ => mediaDataFaker.Generate(1))
            .UseSeed(seed.Next());
    }

    private static readonly Faker<PlexOtherVideoMediaData> _plexOtherVideoMediaData =
        new Faker<PlexOtherVideoMediaData>()
            .ApplyBasePlexMediaData(new FakeDataConfig())
            .Ignore(x => x.PlexOtherVideoId)
            .Ignore(x => x.PlexOtherVideo)
            .RuleFor(x => x.PartIndex, _ => 0)
            .RuleFor(x => x.Width, _ => 1920)
            .RuleFor(x => x.Height, _ => 1080)
            .RuleFor(x => x.VideoCodec, _ => "h264")
            .RuleFor(x => x.AudioCodec, _ => "aac")
            .RuleFor(x => x.VideoProfile, _ => "high")
            .RuleFor(x => x.VideoFrameRate, _ => 24m)
            .RuleFor(x => x.VideoResolution, _ => VideoQuality.FullHD)
            .RuleFor(x => x.Quality, (_, x) => x.VideoResolution)
            .RuleFor(x => x.Container, _ => "mp4")
            .RuleFor(x => x.Source, _ => ReleaseSource.None)
            .RuleFor(x => x.OriginalFilename, (_, x) => $"video-{x.PlexApiPartId}.mp4")
            .RuleFor(x => x.OriginalFilePath, (_, x) => $"/other-videos/{x.OriginalFilename}")
            .RuleFor(x => x.SourceRelativePath, (_, x) => x.OriginalFilename)
            .RuleFor(x => x.Key, (_, x) => $"/library/parts/{x.PlexApiPartId}/file.mp4")
            .RuleFor(x => x.GeneratedNameSyncedAt, _ => null);

    public static Faker<PlexOtherVideoMediaData> GetPlexOtherVideoMediaData(
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        var faker = _plexOtherVideoMediaData.Clone();
        if (config.DownloadFileSizeInMb > 0)
            faker.RuleFor(x => x.Size, (long)ByteSize.FromMebiBytes(config.DownloadFileSizeInMb).Bytes);
        return faker.UseSeed(seed.Next());
    }
}
