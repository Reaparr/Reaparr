namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static Faker<DownloadTaskMusicArtist> _downloadTaskMusicArtistFaker = new Faker<DownloadTaskMusicArtist>()
        .ApplyDownloadTaskParentBase(DownloadTaskType.MusicArtist)
        .Ignore(x => x.Children)
        .FinishWith(
            (_, artist) =>
            {
                artist.FullTitle = artist.Title;

                var albumIndex = 1;
                foreach (var album in artist.Children)
                {
                    album.Title = $"{album.Title} {albumIndex++}";
                    album.FullTitle = $"{artist.FullTitle}/{album.Title}";

                    foreach (var track in album.Children)
                    {
                        track.FullTitle = $"{album.FullTitle}/{track.Title}";
                        var fileIndex = 1;
                        foreach (var file in track.Children)
                        {
                            file.FullTitle = $"{track.FullTitle}/{fileIndex++}-{file.FileName}";
                            file.DirectoryMeta.MusicArtistFolder = artist.Title.SanitizeFolderName();
                            file.DirectoryMeta.MusicAlbumFolder = album.Title.SanitizeFolderName();
                        }
                    }
                }
            }
        );

    public static Faker<DownloadTaskMusicArtist> GetDownloadTaskMusicArtist(
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);

        return _downloadTaskMusicArtistFaker
            .RuleFor(
                x => x.Children,
                _ =>
                {
                    var faker = GetDownloadTaskMusicAlbum(seed, options);
                    return config.MusicAlbumDownloadTasksCount > 0
                        ? faker.Generate(config.MusicAlbumDownloadTasksCount)
                        : faker.GenerateBetween(1, 5);
                }
            )
            .RuleFor(x => x.DataTotal, (_, x) => x.Children.Sum(album => album.DataTotal))
            .UseSeed(seed.Next());
    }
}
