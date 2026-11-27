namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<DownloadTaskTvShowSeason> _downloadTaskTvShowSeasonFaker =
        new Faker<DownloadTaskTvShowSeason>()
            .StrictMode(true)
            .ApplyDownloadTaskParentBase(DownloadTaskType.Season)
            .Ignore(x => x.ParentId)
            .RuleFor(x => x.Title, _ => "Season")
            .RuleFor(x => x.FullTitle, _ => "Season")
            .Ignore(x => x.Parent)
            .Ignore(x => x.Children)
            .FinishWith(
                (_, season) =>
                {
                    season.FullTitle = season.Title;

                    var episodeIndex = 1;
                    foreach (var episode in season.Children)
                    {
                        episode.Title = $"{episode.Title} {episodeIndex++}";
                        episode.FullTitle = $"{season.FullTitle}/{episode.Title}";

                        var fileIndex = 1;
                        foreach (var file in episode.Children)
                        {
                            file.FullTitle = $"{episode.FullTitle}/{fileIndex}-{file.FileName}";
                            file.DirectoryMeta.SeasonFolder = season.Title;
                        }
                    }
                }
            );

    public static Faker<DownloadTaskTvShowSeason> GetDownloadTaskTvShowSeason(
        Seed seed,
        Action<FakeDataConfig>? options = null
    ) => GetDownloadTaskTvShowSeason(seed, FakeDataConfig.FromOptions(options));

    private static Faker<DownloadTaskTvShowSeason> GetDownloadTaskTvShowSeason(Seed seed, FakeDataConfig config)
    {
        return _downloadTaskTvShowSeasonFaker
            .RuleFor(
                x => x.DataTotal,
                (_, x) =>
                    config.DownloadFileSizeInMb > 0
                        ? (long)ByteSize.FromMebiBytes(config.DownloadFileSizeInMb).Bytes
                        : x.DataTotal
            )
            .RuleFor(
                x => x.Children,
                _ =>
                {
                    var f = GetDownloadTaskTvShowEpisode(seed, config);
                    if (config.TvShowEpisodeDownloadTasksCount > 0)
                        return f.Generate(config.TvShowEpisodeDownloadTasksCount);

                    return f.GenerateBetween(5, 10);
                }
            )
            .UseSeed(seed.Next());
    }
}
