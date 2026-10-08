namespace Reaparr.Application.UnitTests;

public class RecoverInterruptedDownloadsCommandPhotoUnitTests : BaseUnitTest<RecoverInterruptedDownloadsCommandHandler>
{
    [Test]
    public async Task ShouldRecoverInterruptedPhotoFilesWithoutChangingIdleSiblings_WhenRecovering()
    {
        // Arrange
        await SetupDatabase(88060, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var album = FakeData
            .GetDownloadTaskPhotoAlbum(new Seed(88060))
            .RuleFor(x => x.Children, _ => FakeData.GetDownloadTaskPhotoImage(new Seed(88061)).Generate(4))
            .Generate();
        var files = album.Children.SelectMany(x => x.Children).ToList();
        foreach (
            var node in new DownloadTaskBase[] { album }
                .Concat(album.Children)
                .Concat(files)
        )
        {
            node.PlexServerId = library.PlexServerId;
            node.PlexLibraryId = library.Id;
        }
        files[0].DownloadStatus = DownloadStatus.Downloading;
        files[1].DownloadStatus = DownloadStatus.Moving;
        files[2].DownloadStatus = DownloadStatus.MoveFinished;
        files[3].DownloadStatus = DownloadStatus.Paused;
        foreach (var file in files)
            file.DataReceived = 512;
        dbContext.DownloadTaskPhotoAlbums.Add(album);
        await dbContext.SaveChangesAsync(CancellationToken);
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());

        // Act
        var result = await Sut.ExecuteAsync(new RecoverInterruptedDownloadsCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var expected = new[]
        {
            DownloadStatus.AutoPaused,
            DownloadStatus.AutoMovePaused,
            DownloadStatus.Completed,
            DownloadStatus.Paused,
        };
        var after = await dbContext
            .DownloadTaskPhotoImageFiles.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.DownloadStatus,
                x.DataReceived,
            })
            .ToListAsync(CancellationToken);
        after.ShouldBe(
            files
                .Select(
                    (x, i) =>
                        new
                        {
                            x.Id,
                            x.ParentId,
                            DownloadStatus = expected[i],
                            DataReceived = 512L,
                        }
                )
                .OrderBy(x => x.Id)
        );
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

}
