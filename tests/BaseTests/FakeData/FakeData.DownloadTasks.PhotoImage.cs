namespace Reaparr.BaseTests;

public static partial class FakeData
{
    public static Faker<DownloadTaskPhotoImage> GetDownloadTaskPhotoImage(
        Seed seed,
        Action<FakeDataConfig>? options = null
    ) =>
        new Faker<DownloadTaskPhotoImage>()
            .ApplyDownloadTaskParentBase(DownloadTaskType.PhotoImage)
            .UseSeed(seed.Next())
            .Ignore(x => x.ParentId)
            .Ignore(x => x.Parent)
            .RuleFor(x => x.Children, _ => GetDownloadTaskPhotoImageFile(seed, options).Generate(1))
            .RuleFor(x => x.DataTotal, (_, x) => x.Children.Sum(file => file.DataTotal));
}
