namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<DownloadTaskOtherVideo> _downloadTaskOtherVideoFaker =
        new Faker<DownloadTaskOtherVideo>()
            .ApplyDownloadTaskParentBase(DownloadTaskType.OtherVideo)
            .Ignore(x => x.Children)
            .FinishWith(
                (_, video) =>
                {
                    video.FullTitle = video.Title;

                    var fileIndex = 1;
                    foreach (var file in video.Children)
                    {
                        var currentFileIndex = fileIndex++;
                        file.Title = $"{file.Title} {currentFileIndex}";
                        file.FullTitle = $"{video.FullTitle}/{currentFileIndex}-{file.FileName}";
                        file.DirectoryMeta.OtherVideoFolder = video.Title.SanitizeFolderName();
                    }
                }
            );

    public static Faker<DownloadTaskOtherVideo> GetDownloadTaskOtherVideo(
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);

        return _downloadTaskOtherVideoFaker
            .RuleFor(
                x => x.Children,
                _ =>
                {
                    var faker = GetDownloadTaskOtherVideoFile(seed, options);
                    return config.OtherVideoFileDownloadTasksCount > 0
                        ? faker.Generate(config.OtherVideoFileDownloadTasksCount)
                        : faker.Generate(1);
                }
            )
            .RuleFor(x => x.DataTotal, (_, x) => x.Children.Sum(file => file.DataTotal))
            .UseSeed(seed.Next());
    }
}
