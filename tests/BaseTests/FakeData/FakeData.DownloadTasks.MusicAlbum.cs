namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static Faker<DownloadTaskMusicAlbum> _downloadTaskMusicAlbumFaker = new Faker<DownloadTaskMusicAlbum>()
        .ApplyDownloadTaskParentBase(DownloadTaskType.MusicAlbum)
        .Ignore(x => x.ParentId)
        .Ignore(x => x.Parent)
        .Ignore(x => x.Children)
        .FinishWith(
            (_, album) =>
            {
                album.FullTitle = album.Title;

                var trackIndex = 1;
                foreach (var track in album.Children)
                {
                    track.Title = $"{track.Title} {trackIndex++}";
                    track.FullTitle = $"{album.FullTitle}/{track.Title}";

                    var fileIndex = 1;
                    foreach (var file in track.Children)
                    {
                        file.FullTitle = $"{track.FullTitle}/{fileIndex++}-{file.FileName}";
                        file.DirectoryMeta.MusicAlbumFolder = album.Title.SanitizeFolderName();
                    }
                }
            }
        );

    public static Faker<DownloadTaskMusicAlbum> GetDownloadTaskMusicAlbum(
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);

        return _downloadTaskMusicAlbumFaker
            .RuleFor(
                x => x.Children,
                _ =>
                {
                    var faker = GetDownloadTaskMusicTrack(seed, options);
                    return config.MusicTrackDownloadTasksCount > 0
                        ? faker.Generate(config.MusicTrackDownloadTasksCount)
                        : faker.GenerateBetween(5, 10);
                }
            )
            .RuleFor(x => x.DataTotal, (_, x) => x.Children.Sum(track => track.DataTotal))
            .UseSeed(seed.Next());
    }
}
