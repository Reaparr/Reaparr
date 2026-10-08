namespace Reaparr.Application.UnitTests;

public class RecoverInterruptedDownloadsCommandMusicUnitTests : BaseUnitTest<RecoverInterruptedDownloadsCommandHandler>
{
    [Test]
    public async Task ShouldRecoverInterruptedMusicFilesWithoutResettingProgressOrIdleControls_WhenRecovering()
    {
        // Arrange
        await SetupDatabase(
            88270,
            c =>
            {
                c.PlexMusicLibraryCount = 1;
                c.MusicArtistDownloadTasksCount = 4;
                c.MusicAlbumDownloadTasksCount = 1;
                c.MusicTrackDownloadTasksCount = 1;
                c.MusicTrackFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var files = (await dbContext.DownloadTaskMusicTrackFiles.OrderBy(x => x.PlexApiRatingKey).ToListAsync(CancellationToken))
            .Cast<DownloadTaskFileBase>()
            .ToList();
        DownloadStatus[] statuses =
        [
            DownloadStatus.Downloading,
            DownloadStatus.Moving,
            DownloadStatus.MoveFinished,
            DownloadStatus.Paused,
        ];
        for (var index = 0; index < statuses.Length; index++)
        {
            var file = files[index];
            file.DownloadStatus = statuses[index];
            file.DataReceived = 128;
            file.FileDataTransferred = 64;
            file.CurrentFileTransferBytesOffset = 32;
            dbContext.Entry(file).State = EntityState.Modified;
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        foreach (var file in files)
            (await dbContext.GetDownloadTaskFileAsync(file.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(
                file.DownloadStatus
            );
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());

        // Act
        var result = await Sut.ExecuteAsync(new RecoverInterruptedDownloadsCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var expectedStatuses = new[]
        {
            DownloadStatus.AutoPaused,
            DownloadStatus.AutoMovePaused,
            DownloadStatus.Completed,
            DownloadStatus.Paused,
        };
        var after = await dbContext
            .DownloadTaskMusicTrackFiles.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.DownloadStatus,
                x.DataReceived,
                x.FileDataTransferred,
                x.CurrentFileTransferBytesOffset,
            })
            .ToListAsync(CancellationToken);
        after.ShouldBe(
            files
                .Select(
                    (x, index) =>
                        new
                        {
                            x.Id,
                            ParentId = ((DownloadTaskMusicTrackFile)x).ParentId,
                            DownloadStatus = expectedStatuses[index],
                            DataReceived = 128L,
                            FileDataTransferred = 64L,
                            CurrentFileTransferBytesOffset = 32L,
                        }
                )
                .OrderBy(x => x.Id)
        );
        for (var index = 0; index < 3; index++)
        {
            var key = files[index].ToParentKey()!;
            (await dbContext.GetDownloadTaskAsync(key, CancellationToken))!.DownloadStatus.ShouldBe(expectedStatuses[index]);
        }
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }
}
