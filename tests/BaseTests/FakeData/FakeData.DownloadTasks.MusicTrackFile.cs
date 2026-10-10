namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<DownloadTaskMusicTrackFile> _downloadTaskMusicTrackFileFaker =
        new Faker<DownloadTaskMusicTrackFile>()
            .ApplyDownloadTaskFileBase(DownloadTaskType.MusicTrackData)
            .Ignore(x => x.Parent)
            .Ignore(x => x.ParentId)
            .Ignore(x => x.Logs)
            .RuleFor(x => x.Quality, _ => VideoQuality.None)
            .RuleFor(x => x.FileName, (_, x) => $"track-{x.PlexApiPartId}.flac");

    public static Faker<DownloadTaskMusicTrackFile> GetDownloadTaskMusicTrackFile(
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);

        return _downloadTaskMusicTrackFileFaker
            .RuleFor(
                x => x.DataTotal,
                f =>
                    config.DownloadFileSizeInMb > 0
                        ? (long)ByteSize.FromMebiBytes(config.DownloadFileSizeInMb).Bytes
                        : f.Random.Long(1, 10000000)
            )
            .UseSeed(seed.Next());
    }
}
