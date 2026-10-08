namespace Reaparr.BaseTests;

public static partial class FakeData
{
    public static Faker<DownloadTaskPhotoAlbum> GetDownloadTaskPhotoAlbum(
        Seed seed,
        Action<FakeDataConfig>? options = null
    ) =>
        new Faker<DownloadTaskPhotoAlbum>()
            .ApplyDownloadTaskParentBase(DownloadTaskType.PhotoAlbum)
            .UseSeed(seed.Next())
            .RuleFor(x => x.Children, _ => GetDownloadTaskPhotoImage(seed, options).Generate(1))
            .RuleFor(x => x.DataTotal, (_, x) => x.Children.Sum(image => image.DataTotal))
            .FinishWith(
                (_, album) =>
                {
                    foreach (var image in album.Children)
                    {
                        image.FullTitle = $"{album.FullTitle}/{image.Title}";
                        foreach (var file in image.Children)
                        {
                            file.FullTitle = $"{image.FullTitle}/{file.FileName}";
                            file.DirectoryMeta.PhotoAlbumFolder = album.Title.SanitizeFolderName();
                        }
                    }
                }
            );
}
