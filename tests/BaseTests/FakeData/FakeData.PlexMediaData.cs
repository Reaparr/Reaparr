namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static Faker<T> ApplyBasePlexMediaData<T>(this Faker<T> faker, FakeDataConfig config)
        where T : BasePlexMediaData =>
        faker
            .StrictMode(true)
            .Ignore(x => x.Id)
            .RuleFor(x => x.PlexApiMediaId, _ => GetUniqueNumber())
            .RuleFor(x => x.PlexApiPartId, _ => GetUniqueNumber())
            .RuleFor(x => x.VideoCodec, f => f.System.FileType())
            .RuleFor(x => x.AudioCodec, _ => "dca")
            .RuleFor(x => x.VideoQuality, f => f.PickRandom(VideoQuality.SD, VideoQuality.HD, VideoQuality.FullHD))
            .RuleFor(x => x.Duration, f => f.Random.Int(50000, 55124400))
            .RuleFor(x => x.Container, f => f.System.FileType())
            .RuleFor(x => x.Source, _ => ReleaseSource.WebDl)
            .RuleFor(x => x.OriginalFilename, f => f.System.FileName("video"))
            .RuleFor(x => x.PlexApiRatingKey, _ => GetUniqueNumber())
            .RuleFor(x => x.Key, _ => DownloadFileUrl)
            .RuleFor(
                x => x.Size,
                _ =>
                    config.DownloadFileSizeInMb > 0
                        ? (long)ByteSize.FromMebiBytes(config.DownloadFileSizeInMb).Bytes
                        : 50 * 1024
            )
            .Ignore(x => x.PlexServerId)
            .Ignore(x => x.PlexServer)
            .Ignore(x => x.PlexLibraryId)
            .Ignore(x => x.PlexLibrary);

    private static readonly Faker<PlexMovieMediaData> _plexMovieMediaDataFaker = CreatePlexMovieMediaDataFaker();

    private static Faker<PlexMovieMediaData> CreatePlexMovieMediaDataFaker()
    {
        return new Faker<PlexMovieMediaData>()
            .ApplyBasePlexMediaData(new FakeDataConfig())
            .RuleFor(x => x.NeedsGeneratedName, _ => false)
            .RuleFor(x => x.GeneratedNameSyncedAt, _ => DateTime.UtcNow)
            .RuleFor(x => x.GeneratedFilename, _ => string.Empty)
            .Ignore(x => x.PlexMovieId)
            .Ignore(x => x.PlexMovie)
            .Ignore(x => x.Streams);
    }

    private static readonly Faker<PlexTvShowEpisodeMediaData> _plexTvShowEpisodeMediaDataFaker =
        CreatePlexTvShowEpisodeMediaDataFaker();

    private static Faker<PlexTvShowEpisodeMediaData> CreatePlexTvShowEpisodeMediaDataFaker()
    {
        return new Faker<PlexTvShowEpisodeMediaData>()
            .ApplyBasePlexMediaData(new FakeDataConfig())
            .RuleFor(x => x.NeedsGeneratedName, _ => false)
            .RuleFor(x => x.GeneratedNameSyncedAt, _ => DateTime.UtcNow)
            .RuleFor(x => x.GeneratedFilename, _ => string.Empty)
            .Ignore(x => x.PlexTvShowEpisodeId)
            .Ignore(x => x.PlexTvShowEpisode)
            .Ignore(x => x.Streams);
    }

    public static Faker<PlexMovieMediaData> GetPlexMovieMediaData(Seed seed, Action<FakeDataConfig>? options = null)
    {
        var config = FakeDataConfig.FromOptions(options);

        return _plexMovieMediaDataFaker
            .RuleFor(
                x => x.Size,
                _ =>
                    config.DownloadFileSizeInMb > 0
                        ? (long)ByteSize.FromMebiBytes(config.DownloadFileSizeInMb).Bytes
                        : 50 * 1024L
            )
            .UseSeed(seed.Next());
    }

    public static Faker<PlexTvShowEpisodeMediaData> GetPlexTvShowEpisodeMediaData(
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);

        return _plexTvShowEpisodeMediaDataFaker
            .RuleFor(
                x => x.Size,
                _ =>
                    config.DownloadFileSizeInMb > 0
                        ? (long)ByteSize.FromMebiBytes(config.DownloadFileSizeInMb).Bytes
                        : 50 * 1024L
            )
            .UseSeed(seed.Next());
    }
}
