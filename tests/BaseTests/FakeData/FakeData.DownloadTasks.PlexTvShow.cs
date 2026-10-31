namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<DownloadTaskTvShow> _downloadTaskTvShowFaker = new Faker<DownloadTaskTvShow>()
        .ApplyDownloadTaskParentBase(DownloadTaskType.TvShow)
        .Ignore(x => x.Children)
        .FinishWith(
            (_, tvShow) =>
            {
                var seasonIndex = 1;

                foreach (var season in tvShow.Children)
                {
                    season.Title = $"{season.Title} {seasonIndex++}";
                    season.FullTitle = $"{tvShow.FullTitle}/{season.Title}";

                    foreach (var episode in season.Children)
                    {
                        episode.FullTitle = $"{season.FullTitle}/{episode.Title}";

                        var fileIndex = 1;
                        foreach (var file in episode.Children)
                        {
                            file.FullTitle = $"{episode.FullTitle}/{fileIndex}-{file.FileName}";
                            file.DirectoryMeta.TvShowFolder = tvShow.Title;
                            file.DirectoryMeta.SeasonFolder = season.Title;
                            fileIndex++;
                        }
                    }
                }
            }
        );

    public static Faker<DownloadTaskTvShow> GetDownloadTaskTvShow(Seed seed, Action<FakeDataConfig>? options = null)
    {
        var config = FakeDataConfig.FromOptions(options);

        return _downloadTaskTvShowFaker
            .UseSeed(seed.Next())
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
                    if (config.TvShowSeasonDownloadTasksCount > 0)
                    {
                        return GetDownloadTaskTvShowSeason(seed, options)
                            .Generate(config.TvShowSeasonDownloadTasksCount);
                    }

                    return GetDownloadTaskTvShowSeason(seed, options).GenerateBetween(1, 5);
                }
            );
    }
}
