namespace Reaparr.BaseTests;

public static partial class FakeData
{
    public static Faker<DownloadTaskPhotoImageFile> GetDownloadTaskPhotoImageFile(
        Seed seed,
        Action<FakeDataConfig>? options = null,
        bool isClip = false
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        return new Faker<DownloadTaskPhotoImageFile>()
            .ApplyDownloadTaskFileBase(DownloadTaskType.PhotoImage)
            .UseSeed(seed.Next())
            .Ignore(x => x.Parent)
            .Ignore(x => x.ParentId)
            .Ignore(x => x.Logs)
            .RuleFor(x => x.AssetKind, _ => isClip ? PhotoAssetKind.VideoClip : PhotoAssetKind.Image)
            .RuleFor(x => x.VideoQuality, _ => VideoQuality.None)
            .RuleFor(
                x => x.FileName,
                (_, x) => $"photo-{x.PlexApiPartId}.{(x.AssetKind == PhotoAssetKind.VideoClip ? "mp4" : "jpg")}"
            )
            .RuleFor(x => x.FileLocationUrl, (_, x) => $"/library/parts/{x.PlexApiPartId}/file")
            .RuleFor(
                x => x.DataTotal,
                f =>
                    config.DownloadFileSizeInMb > 0
                        ? (long)ByteSize.FromMebiBytes(config.DownloadFileSizeInMb).Bytes
                        : f.Random.Long(1, 10_000_000)
            )
            .RuleFor(
                x => x.DirectoryMeta,
                (_, x) =>
                    new DownloadTaskDirectory
                    {
                        DownloadRootPath = "/Downloads",
                        DestinationRootPath = "/Photos",
                        MovieFolder = string.Empty,
                        TvShowFolder = string.Empty,
                        SeasonFolder = string.Empty,
                        MusicArtistFolder = string.Empty,
                        MusicAlbumFolder = string.Empty,
                        PhotoFolder = string.Empty,
                        OtherVideoFolder = string.Empty,
                        KeepCompletedInDownloadFolder = false,
                    }
            );
    }
}
