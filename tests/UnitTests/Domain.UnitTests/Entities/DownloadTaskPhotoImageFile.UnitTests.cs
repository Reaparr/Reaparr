namespace Reaparr.Domain.UnitTests;

public class DownloadTaskPhotoImageFileUnitTests : BaseUnitTest
{
    [Test]
    public void ShouldUseAlbumFolderWithoutImageSubfolder_WhenResolvingPhotoPaths()
    {
        // Arrange
        var task = FakeData.GetDownloadTaskPhotoImageFile(new Seed(62308)).Generate();
        task.DirectoryMeta.DownloadRootPath = "/downloads";
        task.DirectoryMeta.DestinationRootPath = "/photos";
        task.DirectoryMeta.PhotoAlbumFolder = "Family Album";

        // Act
        var downloadDirectory = task.DownloadDirectory;
        var destinationDirectory = task.DestinationDirectory;

        // Assert
        downloadDirectory.ShouldBe(Path.Combine("/downloads", "Photos", "Family Album"));
        destinationDirectory.ShouldBe(Path.Combine("/photos", "Family Album"));
        task.DownloadFilePath.ShouldBe(
            Path.Combine(downloadDirectory, task.FileName.AddReaparrTempSuffixToFileName())
        );
        task.DestinationFilePath.ShouldBe(Path.Combine(destinationDirectory, task.FileName));
    }

    [Test]
    [Arguments(PlexMediaType.MusicTrack)]
    [Arguments(PlexMediaType.OtherVideos)]
    public async Task ShouldPreserveFamilyHierarchyAndBasename_WhenResolvingOriginalFilePaths(PlexMediaType type)
    {
        // Arrange
        await SetupDatabase(62380, config =>
        {
            config.PlexMusicLibraryCount = 1;
            config.PlexOtherVideoLibraryCount = 1;
        });
        var dbContext = IDbContext;
        DownloadTaskFileBase file = type == PlexMediaType.MusicTrack
            ? await FakeData.AddMusicTask(dbContext, 1)
            : await FakeData.AddOtherVideoTask(dbContext, 1);
        var downloadRoot = Path.Combine(Path.GetTempPath(), "downloads");
        var destinationRoot = Path.Combine(Path.GetTempPath(), "destination");
        file.DirectoryMeta.DownloadRootPath = downloadRoot;
        file.DirectoryMeta.DestinationRootPath = destinationRoot;
        var relativeDirectory = type == PlexMediaType.MusicTrack
            ? Path.Combine("artist-1", "album-1")
            : "video-1";
        var category = type == PlexMediaType.MusicTrack ? "Music" : "OtherVideos";
        var originalFilename = file.FileName;

        // Act
        var downloadDirectory = file.DownloadDirectory;
        var destinationDirectory = file.DestinationDirectory;

        // Assert
        downloadDirectory.ShouldBe(Path.Combine(downloadRoot, category, relativeDirectory));
        destinationDirectory.ShouldBe(Path.Combine(destinationRoot, relativeDirectory));
        file.DownloadFilePath.ShouldBe(Path.Combine(downloadDirectory, originalFilename.AddReaparrTempSuffixToFileName()));
        file.DestinationFilePath.ShouldBe(Path.Combine(destinationDirectory, originalFilename));
    }
}
