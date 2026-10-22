namespace Reaparr.Application.UnitTests;

public class RecoverInterruptedDownloadsCommandRestartUnitTests : BaseUnitTest<RecoverInterruptedDownloadsCommandHandler>
{
    [Test]
    public async Task ShouldStopInterruptedRestartsAndExposeRestartWithoutChangingProgressOrPausedControls_WhenRecovering()
    {
        // Arrange
        await SetupDatabase(89090, ConfigureDownloadFamilies);
        var dbContext = IDbContext;
        List<DownloadTaskFileBase> files =
        [
            .. await dbContext.DownloadTaskMovieFile.ToListAsync(CancellationToken),
            .. await dbContext.DownloadTaskTvShowEpisodeFile.ToListAsync(CancellationToken),
            .. await dbContext.DownloadTaskMusicTrackFiles.ToListAsync(CancellationToken),
            .. await dbContext.DownloadTaskPhotoImageFiles.ToListAsync(CancellationToken),
            .. await dbContext.DownloadTaskOtherVideoFiles.ToListAsync(CancellationToken),
        ];
        files.GroupBy(x => x.DownloadTaskType).Select(x => (x.Key, x.Count())).OrderBy(x => x.Key).ShouldBe(
            new[] { DownloadTaskType.MovieData, DownloadTaskType.EpisodeData, DownloadTaskType.MusicTrackData,
                DownloadTaskType.PhotoImage, DownloadTaskType.OtherVideoData }.OrderBy(x => x).Select(x => (x, 2)));
        var targets = files.GroupBy(x => x.DownloadTaskType).Select(x => x.OrderBy(f => f.Id).First()).ToList();
        var targetIds = targets.Select(x => x.Id).ToHashSet();
        foreach (var file in files)
        {
            file.DownloadStatus = targetIds.Contains(file.Id) ? DownloadStatus.Restarting : DownloadStatus.Paused;
            file.DataReceived = 128;
            file.FileDataTransferred = 64;
            file.CurrentFileTransferBytesOffset = 32;
            dbContext.Entry(file).State = EntityState.Modified;
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        var parentKeys = targets.Select(x => x.ToParentKey()!).OrderBy(x => x.Id).ToList();
        foreach (var parentKey in parentKeys)
            await dbContext.SetDownloadStatus(parentKey, DownloadStatus.Restarting);
        var before = await dbContext.GetDownloadTaskFilesAsync(files.Select(x => x.ToKey()).ToList(), CancellationToken);
        before.OrderBy(x => x.Id).Select(x => (x.Id, x.DownloadStatus)).ShouldBe(
            files.OrderBy(x => x.Id).Select(x => (x.Id, x.DownloadStatus)));
        SetupDependencies(builder => builder.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());

        // Act
        var result = await Sut.ExecuteAsync(new RecoverInterruptedDownloadsCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = await dbContext.GetDownloadTaskFilesAsync(files.Select(x => x.ToKey()).ToList(), CancellationToken);
        after.OrderBy(x => x.Id).Select(x => (x.Id, x.DownloadTaskType, x.DownloadStatus, x.DataReceived,
            x.FileDataTransferred, x.CurrentFileTransferBytesOffset, x.DownloadFilePath, x.DestinationFilePath)).ShouldBe(
            files.OrderBy(x => x.Id).Select(x => (x.Id, x.DownloadTaskType,
                targetIds.Contains(x.Id) ? DownloadStatus.Stopped : DownloadStatus.Paused,
                128L, 64L, 32L, x.DownloadFilePath, x.DestinationFilePath)));
        var parents = new List<DownloadTaskGeneric>();
        foreach (var parentKey in parentKeys)
            parents.Add((await dbContext.GetDownloadTaskAsync(parentKey, CancellationToken))!);
        parents.Select(x => (x.Id, x.DownloadStatus)).ShouldBe(parentKeys.Select(x => (x.Id, DownloadStatus.Stopped)));
        parents.SelectMany(x => DownloadTaskActions.Convert(x.DownloadStatus).Select(action => (x.Id, action))).ShouldBe(
            parentKeys.SelectMany(x => new[] { DownloadActions.Details, DownloadActions.Restart, DownloadActions.Delete }
                .Select(action => (x.Id, action))));
        Mock.Mock<IDownloadTaskScheduler>().Verify(
            x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IMoveDownloadFileScheduler>().Verify(
            x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    private static void ConfigureDownloadFamilies(FakeDataConfig config)
    {
        config.PlexMovieLibraryCount = 1;
        config.PlexTvShowLibraryCount = 1;
        config.PlexMusicLibraryCount = 1;
        config.PlexPhotoLibraryCount = 1;
        config.PlexOtherVideoLibraryCount = 1;
        config.MovieDownloadTasksCount = 2;
        config.TvShowDownloadTasksCount = 2;
        config.TvShowSeasonDownloadTasksCount = 1;
        config.TvShowEpisodeDownloadTasksCount = 1;
        config.MusicArtistDownloadTasksCount = 2;
        config.MusicAlbumDownloadTasksCount = 1;
        config.MusicTrackDownloadTasksCount = 1;
        config.MusicTrackFileDownloadTasksCount = 1;
        config.PhotoFolderDownloadTasksCount = 2;
        config.PhotoImageDownloadTasksCount = 1;
        config.OtherVideoDownloadTasksCount = 2;
        config.OtherVideoFileDownloadTasksCount = 1;
    }
}
