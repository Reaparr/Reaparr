namespace Reaparr.Domain.UnitTests;

public class DownloadTaskPhotoImageFileUnitTests
{
    [Test]
    public void ShouldCountFoldersAndDirectFiles()
    {
        var root = CreateFolder(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var child = CreateFolder(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), root.Id, "Trips");
        root.Children.Add(child);
        root.Files.Add(CreateFile(root.Id, "root.jpg"));
        child.Files.Add(CreateFile(child.Id, "beach.jpg"));

        root.Count.ShouldBe(4);
        child.Count.ShouldBe(2);
    }

    [Test]
    public void ShouldUsePhotoFolderParentKeys()
    {
        var root = CreateFolder(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var child = CreateFolder(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), root.Id, "Trips");
        var file = CreateFile(child.Id, "beach.jpg");
        file.DownloadTaskType.IsDownloadable().ShouldBeTrue();
        child.DownloadTaskType.IsDownloadable().ShouldBeFalse();

        root.ToParentKey().ShouldBeNull();
        child
            .ToParentKey()
            .ShouldBe(
                new DownloadTaskKey
                {
                    Type = DownloadTaskType.PhotoFolder,
                    Id = root.Id,
                    PlexServerId = 1,
                    PlexLibraryId = 1,
                }
            );
        file.ToParentKey()
            .ShouldBe(
                new DownloadTaskKey
                {
                    Type = DownloadTaskType.PhotoFolder,
                    Id = child.Id,
                    PlexServerId = 1,
                    PlexLibraryId = 1,
                }
            );
    }

    [Test]
    public void ShouldResolveCapturedPhotoFolderPaths()
    {
        var file = CreateFile(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "beach.jpg", "root-7/Trips");

        file.DownloadDirectory.ShouldBe(Path.Combine("/downloads", "Photos", "root-7", "Trips"));
        file.DestinationDirectory.ShouldBe(Path.Combine("/photos", "root-7", "Trips"));
        file.DownloadFilePath.ShouldBe(
            Path.Combine(file.DownloadDirectory, "beach.jpg".AddReaparrTempSuffixToFileName())
        );
        file.DestinationFilePath.ShouldBe(Path.Combine(file.DestinationDirectory, "beach.jpg"));
    }

    private static DownloadTaskPhotoFolder CreateFolder(Guid id, Guid? parentId = null, string relativePath = "") =>
        new()
        {
            Id = id,
            ParentId = parentId,
            PlexApiRatingKey = 0,
            Title = relativePath.Length == 0 ? "Home Plex - Photos" : relativePath,
            DownloadStatus = DownloadStatus.Unknown,
            CreatedAt = DateTime.UtcNow,
            FullTitle = relativePath,
            PlexServerId = 1,
            PlexLibraryId = 1,
            Year = 0,
            DataReceived = 0,
            FileDataTransferred = 0,
            DataTotal = 0,
            DownloadSpeed = 0,
            FileTransferSpeed = 0,
            Children = [],
            Files = [],
            RelativePath = relativePath,
            DownloadRootPath = "/downloads",
            DestinationRootPath = "/photos",
        };

    private static DownloadTaskPhotoImageFile CreateFile(Guid parentId, string fileName, string relativeFolder = "") =>
        new()
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            PlexApiRatingKey = 1,
            PlexApiMediaId = 2,
            PlexApiPartId = 3,
            Title = fileName,
            DownloadStatus = DownloadStatus.Unknown,
            CreatedAt = DateTime.UtcNow,
            FullTitle = fileName,
            PlexServerId = 1,
            PlexLibraryId = 1,
            FileName = fileName,
            FileLocationUrl = "/library/parts/3/file.jpg",
            HashId = null,
            VideoQuality = VideoQuality.Unknown,
            DownloadClientType = PlexDownloadClientType.Direct,
            DirectoryMeta = new DownloadTaskDirectory
            {
                DownloadRootPath = "/downloads",
                DestinationRootPath = "/photos",
                MovieFolder = string.Empty,
                TvShowFolder = string.Empty,
                SeasonFolder = string.Empty,
                MusicArtistFolder = string.Empty,
                MusicAlbumFolder = string.Empty,
                PhotoFolder = relativeFolder,
                OtherVideoFolder = string.Empty,
                KeepCompletedInDownloadFolder = false,
            },
            DataReceived = 0,
            DataTotal = 0,
            DownloadSpeed = 0,
            DirectDownloadSnapshot = null,
            FileTransferSpeed = 0,
            FileDataTransferred = 0,
            TimeRemaining = 0,
            DestinationFolderPathId = null,
        };
}
