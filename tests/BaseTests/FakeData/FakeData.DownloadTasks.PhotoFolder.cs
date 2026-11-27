namespace Reaparr.BaseTests;

public static partial class FakeData
{
    public static Faker<DownloadTaskPhotoFolder> GetDownloadTaskPhotoFolder(
        Seed seed,
        Action<FakeDataConfig>? options = null
    ) => GetDownloadTaskPhotoFolder(seed, FakeDataConfig.FromOptions(options));

    internal static Faker<DownloadTaskPhotoFolder> GetDownloadTaskPhotoFolder(Seed seed, FakeDataConfig config)
    {
        return new Faker<DownloadTaskPhotoFolder>()
            .ApplyDownloadTaskParentBase(DownloadTaskType.PhotoFolder)
            .UseSeed(seed.Next())
            .RuleFor(x => x.ParentId, _ => null)
            .RuleFor(x => x.Parent, _ => null)
            .RuleFor(x => x.RelativePath, _ => string.Empty)
            .RuleFor(x => x.DownloadRootPath, _ => "/Downloads")
            .RuleFor(x => x.DestinationRootPath, _ => "/Photos")
            .RuleFor(x => x.Children, _ => [])
            .RuleFor(
                x => x.Files,
                _ =>
                {
                    var count = config.PhotoImageDownloadTasksCount > 0 ? config.PhotoImageDownloadTasksCount : 1;
                    return GetDownloadTaskPhotoImageFile(seed, config).Generate(count);
                }
            )
            .RuleFor(x => x.DataTotal, (_, x) => x.Files.Sum(file => file.DataTotal))
            .FinishWith(
                (_, folder) =>
                {
                    var rootName = folder.Title.SanitizeFolderName();
                    var relativePhotoFolder = string.IsNullOrEmpty(folder.RelativePath)
                        ? rootName
                        : $"{rootName}/{folder.RelativePath}";
                    foreach (var file in folder.Files)
                    {
                        file.UpdateInitProperty(nameof(file.ParentId), folder.Id);
                        file.UpdateInitProperty(nameof(file.Parent), folder);
                        file.DirectoryMeta.DownloadRootPath = folder.DownloadRootPath;
                        file.DirectoryMeta.DestinationRootPath = folder.DestinationRootPath;
                        file.DirectoryMeta.PhotoFolder = relativePhotoFolder;
                        file.FullTitle = $"{folder.FullTitle}/{file.FileName}";
                    }
                }
            );
    }
}
