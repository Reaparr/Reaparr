namespace Reaparr.BaseTests;

public static partial class FakeData
{
    // TODO Do an audit of ensureing as much faker generation methods are readonly fields
    public static Faker<DownloadTaskPhotoImageFile> GetDownloadTaskPhotoImageFile(
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        return new Faker<DownloadTaskPhotoImageFile>()
            .ApplyDownloadTaskFileBase(DownloadTaskType.PhotoData)
            .UseSeed(seed.Next())
            .Ignore(x => x.Parent)
            .Ignore(x => x.ParentId)
            .Ignore(x => x.Logs)
            .RuleFor(x => x.Quality, _ => VideoQuality.None)
            .RuleFor(x => x.FileName, (_, x) => $"photo-{x.PlexApiPartId}.jpg")
            .RuleFor(
                x => x.DataTotal,
                f =>
                    config.DownloadFileSizeInMb > 0
                        ? (long)ByteSize.FromMebiBytes(config.DownloadFileSizeInMb).Bytes
                        : f.Random.Long(1, 10000000)
            );
    }
}
