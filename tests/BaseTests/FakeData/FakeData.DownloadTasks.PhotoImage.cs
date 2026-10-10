namespace Reaparr.BaseTests;

public static partial class FakeData
{
    public static Faker<DownloadTaskPhotoImage> GetDownloadTaskPhotoImage(
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);

        return new Faker<DownloadTaskPhotoImage>()
            .ApplyDownloadTaskParentBase(DownloadTaskType.PhotoImage)
            .UseSeed(seed.Next())
            .Ignore(x => x.ParentId)
            .Ignore(x => x.Parent)
            .RuleFor(
                x => x.Children,
                _ =>
                {
                    var faker = GetDownloadTaskPhotoImageFile(seed, options);
                    return config.PhotoImageFileDownloadTasksCount > 0
                        ? faker.Generate(config.PhotoImageFileDownloadTasksCount)
                        : faker.Generate(1);
                }
            )
            .RuleFor(x => x.DataTotal, (_, x) => x.Children.Sum(file => file.DataTotal));
    }
}
