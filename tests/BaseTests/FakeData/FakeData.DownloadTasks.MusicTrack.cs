namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<DownloadTaskMusicTrack> _downloadTaskMusicTrackFaker =
        new Faker<DownloadTaskMusicTrack>()
            .ApplyDownloadTaskParentBase(DownloadTaskType.MusicTrack)
            .Ignore(x => x.ParentId)
            .Ignore(x => x.Parent)
            .Ignore(x => x.Children)
            .FinishWith(
                (_, track) =>
                {
                    track.FullTitle = track.Title;

                    var fileIndex = 1;
                    foreach (var file in track.Children)
                        file.FullTitle = $"{track.FullTitle}/{fileIndex++}-{file.FileName}";
                }
            );

    public static Faker<DownloadTaskMusicTrack> GetDownloadTaskMusicTrack(
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);

        return _downloadTaskMusicTrackFaker
            .RuleFor(
                x => x.Children,
                _ =>
                {
                    var faker = GetDownloadTaskMusicTrackFile(seed, options);
                    return config.MusicTrackFileDownloadTasksCount > 0
                        ? faker.Generate(config.MusicTrackFileDownloadTasksCount)
                        : faker.Generate(1);
                }
            )
            .RuleFor(x => x.DataTotal, (_, x) => x.Children.Sum(file => file.DataTotal))
            .UseSeed(seed.Next());
    }
}
