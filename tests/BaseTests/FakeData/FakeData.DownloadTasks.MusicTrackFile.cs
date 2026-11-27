namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<DownloadTaskMusicTrackFile> _downloadTaskMusicTrackFileFaker =
        new Faker<DownloadTaskMusicTrackFile>()
            .ApplyDownloadTaskFileBase(DownloadTaskType.MusicTrackData)
            .Ignore(x => x.Parent)
            .Ignore(x => x.ParentId)
            .Ignore(x => x.Logs)
            .RuleFor(x => x.VideoQuality, _ => VideoQuality.None)
            .RuleFor(x => x.AudioQuality, _ => AudioQuality.FLAC_16Bit_44_1kHz)
            .RuleFor(x => x.FileName, (_, x) => $"track-{x.PlexApiPartId}.flac");

    public static Faker<DownloadTaskMusicTrackFile> GetDownloadTaskMusicTrackFile(
        Seed seed,
        Action<FakeDataConfig>? options = null
    ) => GetDownloadTaskMusicTrackFile(seed, FakeDataConfig.FromOptions(options));

    private static Faker<DownloadTaskMusicTrackFile> GetDownloadTaskMusicTrackFile(Seed seed, FakeDataConfig config)
    {
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
