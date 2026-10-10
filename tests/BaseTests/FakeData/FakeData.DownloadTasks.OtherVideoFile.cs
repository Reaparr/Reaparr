namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<DownloadTaskOtherVideoFile> _downloadTaskOtherVideoFileFaker =
        new Faker<DownloadTaskOtherVideoFile>()
            .ApplyDownloadTaskFileBase(DownloadTaskType.OtherVideoData)
            .Ignore(x => x.DestinationFolderPath)
            .Ignore(x => x.Parent)
            .Ignore(x => x.ParentId)
            .Ignore(x => x.Logs)
            .RuleFor(x => x.Quality, _ => VideoQuality.FullHD)
            .RuleFor(x => x.FileName, (_, x) => $"video-{x.PlexApiPartId}.mp4");

    public static Faker<DownloadTaskOtherVideoFile> GetDownloadTaskOtherVideoFile(
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);

        return _downloadTaskOtherVideoFileFaker
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
