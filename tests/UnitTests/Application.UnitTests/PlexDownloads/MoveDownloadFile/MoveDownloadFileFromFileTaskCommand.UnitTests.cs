using System.Reactive.Subjects;

namespace Reaparr.Application.UnitTests;

public class MoveDownloadFileFromFileTaskCommandUnitTests : BaseCommandUnitTest<MoveDownloadFileFromFileTaskCommand>
{
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldRejectInvalidKeyBeforeExecutingHandler_WhenKeyIsNullOrEmpty(bool nullKey)
    {
        // Arrange
        var command = new MoveDownloadFileFromFileTaskCommand(nullKey ? null! : new DownloadTaskKey
        {
            Id = Guid.Empty, Type = DownloadTaskType.MovieData, PlexServerId = 1, PlexLibraryId = 1,
        });

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<MoveFileWithResumeCommand>(),
            It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IEventPublisher>().Verify(x => x.PublishAsync(It.IsAny<SendNotificationResult>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(PlexMediaType.Movie, "normal")]
    [Arguments(PlexMediaType.Episode, "normal")]
    [Arguments(PlexMediaType.MusicTrack, "normal")]
    [Arguments(PlexMediaType.PhotoImage, "normal")]
    [Arguments(PlexMediaType.OtherVideos, "normal")]
    [Arguments(PlexMediaType.Movie, "overwrite")]
    [Arguments(PlexMediaType.Episode, "overwrite")]
    [Arguments(PlexMediaType.MusicTrack, "overwrite")]
    [Arguments(PlexMediaType.PhotoImage, "overwrite")]
    [Arguments(PlexMediaType.OtherVideos, "overwrite")]
    [Arguments(PlexMediaType.Movie, "existing-offset")]
    [Arguments(PlexMediaType.Episode, "existing-offset")]
    [Arguments(PlexMediaType.MusicTrack, "existing-offset")]
    [Arguments(PlexMediaType.PhotoImage, "existing-offset")]
    [Arguments(PlexMediaType.OtherVideos, "existing-offset")]
    [Arguments(PlexMediaType.Movie, "keep-task")]
    [Arguments(PlexMediaType.Episode, "keep-task")]
    [Arguments(PlexMediaType.MusicTrack, "keep-task")]
    [Arguments(PlexMediaType.PhotoImage, "keep-task")]
    [Arguments(PlexMediaType.OtherVideos, "keep-task")]
    [Arguments(PlexMediaType.Movie, "keep-global")]
    [Arguments(PlexMediaType.Episode, "keep-global")]
    [Arguments(PlexMediaType.MusicTrack, "keep-global")]
    [Arguments(PlexMediaType.PhotoImage, "keep-global")]
    [Arguments(PlexMediaType.OtherVideos, "keep-global")]
    [Arguments(PlexMediaType.Movie, "in-place")]
    [Arguments(PlexMediaType.Episode, "in-place")]
    [Arguments(PlexMediaType.MusicTrack, "in-place")]
    [Arguments(PlexMediaType.PhotoImage, "in-place")]
    [Arguments(PlexMediaType.OtherVideos, "in-place")]
    [Arguments(PlexMediaType.Movie, "renamed")]
    [Arguments(PlexMediaType.Episode, "renamed")]
    [Arguments(PlexMediaType.MusicTrack, "renamed")]
    [Arguments(PlexMediaType.PhotoImage, "renamed")]
    [Arguments(PlexMediaType.OtherVideos, "renamed")]
    [Arguments(PlexMediaType.Movie, "renamed-kept")]
    [Arguments(PlexMediaType.Episode, "renamed-kept")]
    [Arguments(PlexMediaType.MusicTrack, "renamed-kept")]
    [Arguments(PlexMediaType.PhotoImage, "renamed-kept")]
    [Arguments(PlexMediaType.OtherVideos, "renamed-kept")]
    [Arguments(PlexMediaType.Movie, "existing-destination")]
    [Arguments(PlexMediaType.Episode, "existing-destination")]
    [Arguments(PlexMediaType.MusicTrack, "existing-destination")]
    [Arguments(PlexMediaType.PhotoImage, "existing-destination")]
    [Arguments(PlexMediaType.OtherVideos, "existing-destination")]
    public async Task ShouldMoveExactBytesAndPersistOnlyTargetCompletion_WhenSupportedStrategyRuns(
        PlexMediaType family, string strategy)
    {
        // Arrange
        await SetupDatabase(52223, c =>
        {
            c.MovieDownloadTasksCount = family == PlexMediaType.Movie ? 2 : 0;
            c.TvShowDownloadTasksCount = family == PlexMediaType.Episode ? 1 : 0;
            c.TvShowSeasonDownloadTasksCount = family == PlexMediaType.Episode ? 1 : 0;
            c.TvShowEpisodeDownloadTasksCount = family == PlexMediaType.Episode ? 2 : 0;
            c.PlexMusicLibraryCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.MusicArtistDownloadTasksCount = family == PlexMediaType.MusicTrack ? 2 : 0;
            c.MusicAlbumDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.MusicTrackDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.MusicTrackFileDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.PlexPhotoLibraryCount = family == PlexMediaType.PhotoImage ? 1 : 0;
            c.PlexOtherVideoLibraryCount = family == PlexMediaType.OtherVideos ? 1 : 0;
            c.OtherVideoDownloadTasksCount = family == PlexMediaType.OtherVideos ? 2 : 0;
            c.OtherVideoFileDownloadTasksCount = family == PlexMediaType.OtherVideos ? 1 : 0;
        });
        var dbContext = IDbContext;
        DownloadTaskFileBase target;
        DownloadTaskFileBase control;
        switch (family)
        {
            case PlexMediaType.MusicTrack:
                var musicFiles = await dbContext.DownloadTaskMusicTrackFiles
                    .OrderBy(x => x.PlexApiRatingKey)
                    .ToArrayAsync(CancellationToken);
                target = musicFiles[0];
                control = musicFiles[1];
                break;
            case PlexMediaType.OtherVideos:
                var otherVideoFiles = await dbContext.DownloadTaskOtherVideoFiles
                    .OrderBy(x => x.PlexApiRatingKey)
                    .ToArrayAsync(CancellationToken);
                target = otherVideoFiles[0];
                control = otherVideoFiles[1];
                break;
            case PlexMediaType.PhotoImage:
                var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
                var album = FakeData.GetDownloadTaskPhotoAlbum(new Seed(52223))
                    .RuleFor(x => x.Children, _ => FakeData.GetDownloadTaskPhotoImage(new Seed(52224)).Generate(2)).Generate();
                foreach (var node in new DownloadTaskBase[] { album }
                    .Concat(album.Children).Concat(album.Children.SelectMany(x => x.Children)))
                {
                    node.PlexServerId = library.PlexServerId;
                    node.PlexLibraryId = library.Id;
                }
                dbContext.DownloadTaskPhotoAlbums.Add(album);
                await dbContext.SaveChangesAsync(CancellationToken);
                var photos = album.Children.SelectMany(x => x.Children).ToArray();
                target = photos[0];
                control = photos[1];
                break;
            default:
                var files = family == PlexMediaType.Movie
                    ? (await dbContext.DownloadTaskMovieFile.OrderBy(x => x.Id).ToListAsync(CancellationToken)).Cast<DownloadTaskFileBase>().ToArray()
                    : (await dbContext.DownloadTaskTvShowEpisodeFile.OrderBy(x => x.Id).ToListAsync(CancellationToken)).Cast<DownloadTaskFileBase>().ToArray();
                target = files[0];
                control = files[1];
                break;
        }
        var paths = Mock.Container.Resolve<IPathProvider>();
        var category = family switch
        {
            PlexMediaType.Movie => "Movies", PlexMediaType.Episode => "TvShows",
            PlexMediaType.MusicTrack => "Music", PlexMediaType.PhotoImage => "Photos", _ => "OtherVideos",
        };
        var relative = family switch
        {
            PlexMediaType.Episode => Path.Combine(target.DirectoryMeta.TvShowFolder, target.DirectoryMeta.SeasonFolder),
            PlexMediaType.MusicTrack => Path.Combine(
                target.DirectoryMeta.MusicArtistFolder,
                target.DirectoryMeta.MusicAlbumFolder
            ),
            PlexMediaType.PhotoImage => target.DirectoryMeta.PhotoAlbumFolder,
            PlexMediaType.OtherVideos => target.DirectoryMeta.OtherVideoFolder,
            _ => target.DirectoryMeta.MovieFolder,
        };
        target.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DestinationRootPath = strategy == "in-place"
            ? Path.Combine(paths.DefaultDownloadsDestinationFolder, category)
            : Path.Combine(paths.DefaultDownloadsDestinationFolder, "move-destination");
        target.DirectoryMeta.KeepCompletedInDownloadFolder = strategy == "keep-task";
        target.DataTotal = 8;
        target.DataReceived = 0;
        target.DownloadStatus = DownloadStatus.DownloadFinished;
        target.Percentage = 75;
        target.CurrentFileTransferBytesOffset = strategy == "existing-offset" ? 4 : 0;
        target.FileDataTransferred = strategy == "existing-offset" ? 4 : 0;
        dbContext.Entry(target).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var key = target.ToKey();
        var sourcePath = Path.Combine(paths.DefaultDownloadsDestinationFolder, category, relative,
            target.FileName.AddReaparrTempSuffixToFileName());
        var destinationPath = Path.Combine(target.DirectoryMeta.DestinationRootPath, relative, target.FileName);
        target.DownloadFilePath.ShouldBe(sourcePath);
        target.DestinationFilePath.ShouldBe(destinationPath);
        var sourceIsRenamed = strategy is "renamed" or "renamed-kept";
        var keep = strategy is "keep-task" or "keep-global" or "renamed-kept";
        var finalPath = keep ? sourcePath.RemoveReapTempSuffix() : destinationPath;
        var sourceToMove = sourceIsRenamed ? sourcePath.RemoveReapTempSuffix() : sourcePath;
        byte[] content = [10, 20, 30, 40, 50, 60, 70, 80];
        byte[] controlBytes = [99, 100];
        var controlBefore = (control.DownloadStatus, control.CurrentFileTransferBytesOffset,
            control.FileDataTransferred, control.Percentage, control.DirectoryMeta);
        SetupFileSystem(fs =>
        {
            fs.AddFile(strategy == "existing-destination" ? destinationPath : sourceToMove, new MockFileData(content));
            fs.AddFile(control.DownloadFilePath, new MockFileData(controlBytes));
            if (strategy == "overwrite")
                fs.AddFile(destinationPath, new MockFileData(new byte[] { 1, 2, 3 }));
            if (strategy == "existing-offset")
                fs.AddFile(destinationPath, new MockFileData(content.Take(4).ToArray()));
        });
        var fs = Mock.Container.Resolve<IFileSystem>();
        fs.File.ReadAllBytes(strategy == "existing-destination" ? destinationPath : sourceToMove).ShouldBe(content);
        (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!.DownloadStatus.ShouldBe(DownloadStatus.DownloadFinished);
        using var progress = new Subject<IDownloadFileTransferProgress>();
        var updates = new List<IDownloadFileTransferProgress>();
        var subjectCompleted = false;
        using var subscription = progress.Subscribe(updates.Add, _ => { }, () => subjectCompleted = true);
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>().SingleInstance());
        var needsCopy = !keep && strategy is not ("in-place" or "existing-destination");
        Mock.Mock<IDownloadManagerSettings>().SetupGet(x => x.KeepCompletedInDownloadFolder)
            .Returns(strategy is "keep-global" or "renamed-kept");
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(
                It.Is<MoveFileWithResumeCommand>(c => c.SourcePath == sourceToMove && c.TargetPath == destinationPath
                    && c.CurrentOffset == 0 && c.DataTotal == content.Length), CancellationToken))
            .Returns<MoveFileWithResumeCommand, CancellationToken>((c, ct) =>
                Mock.Create<MoveFileWithResumeCommandHandler>().ExecuteAsync(c, ct))
            .Verifiable(needsCopy ? Times.Once() : Times.Never());
        Mock.Mock<IEventPublisher>().Setup(x => x.PublishAsync(It.IsAny<SendNotificationResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask).Verifiable(Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync(new MoveDownloadFileFromFileTaskCommand(key, progress));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        fs.File.ReadAllBytes(finalPath).ShouldBe(content);
        fs.Path.GetFileName(finalPath).ShouldBe(target.FileName);
        fs.File.Exists(sourcePath).ShouldBeFalse();
        if (sourceToMove != finalPath)
            fs.File.Exists(sourceToMove).ShouldBeFalse();
        if (keep)
            fs.File.Exists(destinationPath).ShouldBeFalse();
        var persisted = (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!;
        persisted.Id.ShouldBe(target.Id);
        persisted.ToParentKey().ShouldBe(target.ToParentKey());
        persisted.DownloadStatus.ShouldBe(DownloadStatus.MoveFinished);
        persisted.DataReceived.ShouldBe(0);
        persisted.FileDataTransferred.ShouldBe(content.Length);
        persisted.CurrentFileTransferBytesOffset.ShouldBe(content.Length);
        persisted.Percentage.ShouldBe(100);
        persisted.TimeRemaining.ShouldBe(0);
        persisted.DirectoryMeta.ShouldBe(target.DirectoryMeta);
        updates.ShouldHaveSingleItem().FileDataTransferred.ShouldBe(content.Length);
        subjectCompleted.ShouldBe(strategy is not ("existing-destination" or "renamed-kept"));
        var retained = (await dbContext.GetDownloadTaskFileAsync(control.ToKey(), CancellationToken))!;
        (retained.DownloadStatus, retained.CurrentFileTransferBytesOffset, retained.FileDataTransferred,
            retained.Percentage, retained.DirectoryMeta).ShouldBe(controlBefore);
        fs.File.ReadAllBytes(control.DownloadFilePath).ShouldBe(controlBytes);
        var logs = await dbContext.GetDownloadTaskLogsAsync(key, null, null, CancellationToken);
        logs.IsSuccess.ShouldBeTrue();
        logs.Errors.Count.ShouldBe(0);
        logs.Value.Select(x => x.Status).ShouldBe(needsCopy
            ? new[] { DownloadStatus.Moving, DownloadStatus.Moving, DownloadStatus.MoveFinished }
            : [DownloadStatus.MoveFinished]);
        logs.Value.Last().Message.ShouldBe($"Download {target.FileName} transitioned to status: {DownloadStatus.MoveFinished}");
        var controlLogs = await dbContext.GetDownloadTaskLogsAsync(control.ToKey(), null, null, CancellationToken);
        controlLogs.IsSuccess.ShouldBeTrue();
        controlLogs.Errors.Count.ShouldBe(0);
        controlLogs.Value.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
    }

    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.Episode)]
    [Arguments(PlexMediaType.MusicTrack)]
    [Arguments(PlexMediaType.PhotoImage)]
    [Arguments(PlexMediaType.OtherVideos)]
    public async Task ShouldPersistExactPartialOffsetAndKeepSource_WhenRealCopyIsCancelled(PlexMediaType family)
    {
        // Arrange
        await SetupDatabase(52224, c =>
        {
            c.MovieDownloadTasksCount = family == PlexMediaType.Movie ? 2 : 0;
            c.TvShowDownloadTasksCount = family == PlexMediaType.Episode ? 1 : 0;
            c.TvShowSeasonDownloadTasksCount = family == PlexMediaType.Episode ? 1 : 0;
            c.TvShowEpisodeDownloadTasksCount = family == PlexMediaType.Episode ? 2 : 0;
            c.PlexMusicLibraryCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.MusicArtistDownloadTasksCount = family == PlexMediaType.MusicTrack ? 2 : 0;
            c.MusicAlbumDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.MusicTrackDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.MusicTrackFileDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.PlexPhotoLibraryCount = family == PlexMediaType.PhotoImage ? 1 : 0;
            c.PlexOtherVideoLibraryCount = family == PlexMediaType.OtherVideos ? 1 : 0;
            c.OtherVideoDownloadTasksCount = family == PlexMediaType.OtherVideos ? 2 : 0;
            c.OtherVideoFileDownloadTasksCount = family == PlexMediaType.OtherVideos ? 1 : 0;
        });
        var dbContext = IDbContext;
        DownloadTaskFileBase target;
        DownloadTaskFileBase control;
        switch (family)
        {
            case PlexMediaType.MusicTrack:
                var musicFiles = await dbContext.DownloadTaskMusicTrackFiles
                    .OrderBy(x => x.PlexApiRatingKey)
                    .ToArrayAsync(CancellationToken);
                target = musicFiles[0];
                control = musicFiles[1];
                break;
            case PlexMediaType.OtherVideos:
                var otherVideoFiles = await dbContext.DownloadTaskOtherVideoFiles
                    .OrderBy(x => x.PlexApiRatingKey)
                    .ToArrayAsync(CancellationToken);
                target = otherVideoFiles[0];
                control = otherVideoFiles[1];
                break;
            case PlexMediaType.PhotoImage:
                var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
                var album = FakeData.GetDownloadTaskPhotoAlbum(new Seed(52224))
                    .RuleFor(x => x.Children, _ => FakeData.GetDownloadTaskPhotoImage(new Seed(52225)).Generate(2)).Generate();
                foreach (var node in new DownloadTaskBase[] { album }
                    .Concat(album.Children).Concat(album.Children.SelectMany(x => x.Children)))
                {
                    node.PlexServerId = library.PlexServerId;
                    node.PlexLibraryId = library.Id;
                }
                dbContext.DownloadTaskPhotoAlbums.Add(album);
                await dbContext.SaveChangesAsync(CancellationToken);
                var photos = album.Children.SelectMany(x => x.Children).ToArray();
                target = photos[0];
                control = photos[1];
                break;
            default:
                var files = family == PlexMediaType.Movie
                    ? (await dbContext.DownloadTaskMovieFile.OrderBy(x => x.Id).ToListAsync(CancellationToken)).Cast<DownloadTaskFileBase>().ToArray()
                    : (await dbContext.DownloadTaskTvShowEpisodeFile.OrderBy(x => x.Id).ToListAsync(CancellationToken)).Cast<DownloadTaskFileBase>().ToArray();
                target = files[0];
                control = files[1];
                break;
        }
        var paths = Mock.Container.Resolve<IPathProvider>();
        target.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DestinationRootPath = Path.Combine(paths.DefaultDownloadsDestinationFolder, "move-destination");
        target.DataTotal = 3 * 1024 * 1024;
        target.DownloadStatus = DownloadStatus.DownloadFinished;
        dbContext.Entry(target).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var key = target.ToKey();
        var content = new byte[target.DataTotal];
        new Random(13).NextBytes(content);
        byte[] controlBytes = [99, 100];
        SetupFileSystem(fs =>
        {
            fs.AddFile(target.DownloadFilePath, new MockFileData(content));
            fs.AddFile(control.DownloadFilePath, new MockFileData(controlBytes));
        });
        var fs = Mock.Container.Resolve<IFileSystem>();
        using var cancellation = new CancellationTokenSource();
        using var progress = new Subject<IDownloadFileTransferProgress>();
        var updates = new List<IDownloadFileTransferProgress>();
        var subjectCompleted = false;
        using var subscription = progress.Subscribe(updates.Add, _ => { }, () => subjectCompleted = true);
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>().SingleInstance());
        Mock.Mock<IDownloadManagerSettings>().SetupGet(x => x.KeepCompletedInDownloadFolder).Returns(false);
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(
                It.Is<MoveFileWithResumeCommand>(c => c.SourcePath == target.DownloadFilePath
                    && c.TargetPath == target.DestinationFilePath && c.CurrentOffset == 0 && c.DataTotal == content.Length),
                cancellation.Token))
            .Returns<MoveFileWithResumeCommand, CancellationToken>((c, ct) =>
                Mock.Create<MoveFileWithResumeCommandHandler>().ExecuteAsync(c with
                {
                    Progress = dto =>
                    {
                        c.Progress(dto);
                        cancellation.Cancel();
                    },
                }, ct)).Verifiable(Times.Once());
        Mock.Mock<IEventPublisher>().Setup(x => x.PublishAsync(It.IsAny<SendNotificationResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask).Verifiable(Times.Never());

        // Act
        var result = await Mock.Create<MoveDownloadFileFromFileTaskCommandHandler>()
            .ExecuteAsync(new MoveDownloadFileFromFileTaskCommand(key, progress), cancellation.Token);

        // Assert
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        cancellation.IsCancellationRequested.ShouldBeTrue();
        fs.File.ReadAllBytes(target.DownloadFilePath).ShouldBe(content);
        fs.File.ReadAllBytes(target.DestinationFilePath).ShouldBe(content.Take(1024 * 1024).ToArray());
        var persisted = (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!;
        persisted.Id.ShouldBe(target.Id);
        persisted.DownloadStatus.ShouldBe(DownloadStatus.MovePaused);
        persisted.FileDataTransferred.ShouldBe(1024 * 1024);
        persisted.CurrentFileTransferBytesOffset.ShouldBe(1024 * 1024);
        persisted.Percentage.ShouldBe(100m / 3);
        var update = updates.ShouldHaveSingleItem();
        update.FileDataTransferred.ShouldBe(1024 * 1024);
        update.CurrentFileTransferBytesOffset.ShouldBe(1024 * 1024);
        update.TimeRemaining.ShouldBe(persisted.TimeRemaining);
        subjectCompleted.ShouldBeTrue();
        var retained = (await dbContext.GetDownloadTaskFileAsync(control.ToKey(), CancellationToken))!;
        retained.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        retained.FileDataTransferred.ShouldBe(0);
        retained.CurrentFileTransferBytesOffset.ShouldBe(0);
        fs.File.ReadAllBytes(control.DownloadFilePath).ShouldBe(controlBytes);
        var logs = await dbContext.GetDownloadTaskLogsAsync(key, null, null, CancellationToken);
        logs.IsSuccess.ShouldBeTrue();
        logs.Errors.Count.ShouldBe(0);
        logs.Value.Select(x => x.Status).ShouldBe([
            DownloadStatus.Moving, DownloadStatus.Moving, DownloadStatus.MovePaused]);
        var controlLogs = await dbContext.GetDownloadTaskLogsAsync(control.ToKey(), null, null, CancellationToken);
        controlLogs.IsSuccess.ShouldBeTrue();
        controlLogs.Errors.Count.ShouldBe(0);
        controlLogs.Value.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
    }

    [Test]
    [Arguments(PlexMediaType.Movie, false)]
    [Arguments(PlexMediaType.Episode, false)]
    [Arguments(PlexMediaType.MusicTrack, false)]
    [Arguments(PlexMediaType.PhotoImage, false)]
    [Arguments(PlexMediaType.OtherVideos, false)]
    [Arguments(PlexMediaType.Movie, true)]
    [Arguments(PlexMediaType.Episode, true)]
    [Arguments(PlexMediaType.MusicTrack, true)]
    [Arguments(PlexMediaType.PhotoImage, true)]
    [Arguments(PlexMediaType.OtherVideos, true)]
    public async Task ShouldPersistMoveErrorAndPublishExactFailure_WhenSourceIsMissingOrIncomplete(PlexMediaType family, bool missing)
    {
        // Arrange
        await SetupDatabase(52225, c =>
        {
            c.MovieDownloadTasksCount = family == PlexMediaType.Movie ? 2 : 0;
            c.TvShowDownloadTasksCount = family == PlexMediaType.Episode ? 1 : 0;
            c.TvShowSeasonDownloadTasksCount = family == PlexMediaType.Episode ? 1 : 0;
            c.TvShowEpisodeDownloadTasksCount = family == PlexMediaType.Episode ? 2 : 0;
            c.PlexMusicLibraryCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.MusicArtistDownloadTasksCount = family == PlexMediaType.MusicTrack ? 2 : 0;
            c.MusicAlbumDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.MusicTrackDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.MusicTrackFileDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.PlexPhotoLibraryCount = family == PlexMediaType.PhotoImage ? 1 : 0;
            c.PlexOtherVideoLibraryCount = family == PlexMediaType.OtherVideos ? 1 : 0;
            c.OtherVideoDownloadTasksCount = family == PlexMediaType.OtherVideos ? 2 : 0;
            c.OtherVideoFileDownloadTasksCount = family == PlexMediaType.OtherVideos ? 1 : 0;
        });
        var dbContext = IDbContext;
        DownloadTaskFileBase target;
        DownloadTaskFileBase control;
        switch (family)
        {
            case PlexMediaType.MusicTrack:
                var musicFiles = await dbContext.DownloadTaskMusicTrackFiles
                    .OrderBy(x => x.PlexApiRatingKey)
                    .ToArrayAsync(CancellationToken);
                target = musicFiles[0];
                control = musicFiles[1];
                break;
            case PlexMediaType.OtherVideos:
                var otherVideoFiles = await dbContext.DownloadTaskOtherVideoFiles
                    .OrderBy(x => x.PlexApiRatingKey)
                    .ToArrayAsync(CancellationToken);
                target = otherVideoFiles[0];
                control = otherVideoFiles[1];
                break;
            case PlexMediaType.PhotoImage:
                var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
                var album = FakeData.GetDownloadTaskPhotoAlbum(new Seed(52225))
                    .RuleFor(x => x.Children, _ => FakeData.GetDownloadTaskPhotoImage(new Seed(52226)).Generate(2)).Generate();
                foreach (var node in new DownloadTaskBase[] { album }
                    .Concat(album.Children).Concat(album.Children.SelectMany(x => x.Children)))
                {
                    node.PlexServerId = library.PlexServerId;
                    node.PlexLibraryId = library.Id;
                }
                dbContext.DownloadTaskPhotoAlbums.Add(album);
                await dbContext.SaveChangesAsync(CancellationToken);
                var photos = album.Children.SelectMany(x => x.Children).ToArray();
                target = photos[0];
                control = photos[1];
                break;
            default:
                var files = family == PlexMediaType.Movie
                    ? (await dbContext.DownloadTaskMovieFile.OrderBy(x => x.Id).ToListAsync(CancellationToken)).Cast<DownloadTaskFileBase>().ToArray()
                    : (await dbContext.DownloadTaskTvShowEpisodeFile.OrderBy(x => x.Id).ToListAsync(CancellationToken)).Cast<DownloadTaskFileBase>().ToArray();
                target = files[0];
                control = files[1];
                break;
        }
        var paths = Mock.Container.Resolve<IPathProvider>();
        target.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DestinationRootPath = Path.Combine(paths.DefaultDownloadsDestinationFolder, "move-destination");
        target.DataTotal = 8;
        target.DownloadStatus = DownloadStatus.DownloadFinished;
        dbContext.Entry(target).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var key = target.ToKey();
        byte[] sourceBytes = [1, 2, 3, 4];
        byte[] controlBytes = [99, 100];
        SetupFileSystem(fs =>
        {
            if (!missing)
                fs.AddFile(target.DownloadFilePath, new MockFileData(sourceBytes));
            fs.AddFile(control.DownloadFilePath, new MockFileData(controlBytes));
        });
        var fs = Mock.Container.Resolve<IFileSystem>();
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>().SingleInstance());
        Mock.Mock<IDownloadManagerSettings>().SetupGet(x => x.KeepCompletedInDownloadFolder).Returns(false);
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(
                It.Is<MoveFileWithResumeCommand>(c => c.SourcePath == target.DownloadFilePath
                    && c.TargetPath == target.DestinationFilePath && c.CurrentOffset == 0 && c.DataTotal == 8), CancellationToken))
            .Returns<MoveFileWithResumeCommand, CancellationToken>((c, ct) =>
                Mock.Create<MoveFileWithResumeCommandHandler>().ExecuteAsync(c, ct))
            .Verifiable(missing ? Times.Never() : Times.Once());
        Result? notifiedResult = null;
        Mock.Mock<IEventPublisher>().Setup(x => x.PublishAsync(
                It.Is<SendNotificationResult>(n => n.Result.IsFailed && n.Result.Errors.Count == 1), CancellationToken.None))
            .Callback<IEvent, CancellationToken>((n, _) => notifiedResult = ((SendNotificationResult)n).Result)
            .Returns(Task.CompletedTask).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new MoveDownloadFileFromFileTaskCommand(key));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        notifiedResult.ShouldBeSameAs(result);
        var persisted = (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!;
        persisted.DownloadStatus.ShouldBe(DownloadStatus.MoveError);
        persisted.CurrentFileTransferBytesOffset.ShouldBe(0);
        persisted.FileDataTransferred.ShouldBe(0);
        if (missing)
        {
            fs.File.Exists(target.DownloadFilePath).ShouldBeFalse();
            fs.File.Exists(target.DestinationFilePath).ShouldBeFalse();
        }
        else
        {
            fs.File.ReadAllBytes(target.DownloadFilePath).ShouldBe(sourceBytes);
            fs.File.ReadAllBytes(target.DestinationFilePath).ShouldBe(sourceBytes);
        }
        var retained = (await dbContext.GetDownloadTaskFileAsync(control.ToKey(), CancellationToken))!;
        retained.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        retained.CurrentFileTransferBytesOffset.ShouldBe(0);
        retained.FileDataTransferred.ShouldBe(0);
        fs.File.ReadAllBytes(control.DownloadFilePath).ShouldBe(controlBytes);
        var logs = await dbContext.GetDownloadTaskLogsAsync(key, null, null, CancellationToken);
        logs.IsSuccess.ShouldBeTrue();
        logs.Errors.Count.ShouldBe(0);
        logs.Value.Select(x => x.Status).ShouldBe(missing
            ? new[] { DownloadStatus.MoveError, DownloadStatus.MoveError }
            : [DownloadStatus.Moving, DownloadStatus.Moving, DownloadStatus.MoveError, DownloadStatus.MoveError]);
        logs.Value.Last().Message.ShouldBe(result.ToString());
        var controlLogs = await dbContext.GetDownloadTaskLogsAsync(control.ToKey(), null, null, CancellationToken);
        controlLogs.IsSuccess.ShouldBeTrue();
        controlLogs.Errors.Count.ShouldBe(0);
        controlLogs.Value.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
    }

    [Test]
    [Arguments(DownloadTaskType.MovieData)]
    [Arguments(DownloadTaskType.EpisodeData)]
    [Arguments(DownloadTaskType.MusicTrackData)]
    [Arguments(DownloadTaskType.PhotoData)]
    [Arguments(DownloadTaskType.OtherVideoData)]
    public async Task ShouldReturnNotFoundWithoutSideEffects_WhenFileKeyDoesNotExist(DownloadTaskType type)
    {
        // Arrange
        await SetupDatabase(52226, c => c.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var control = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        var key = new DownloadTaskKey
        {
            Id = Guid.Parse("8419b7bd-25a4-4b2a-9571-54e9d6042793"), Type = type,
            PlexServerId = control.PlexServerId, PlexLibraryId = control.PlexLibraryId,
        };
        (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken)).ShouldBeNull();
        SetupFileSystem(fs => fs.AddFile(control.DownloadFilePath, new MockFileData(new byte[] { 99, 100 })));

        // Act
        var result = await TestHandlerExecuteAsync(new MoveDownloadFileFromFileTaskCommand(key));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        var retained = (await dbContext.GetDownloadTaskFileAsync(control.ToKey(), CancellationToken))!;
        retained.DownloadStatus.ShouldBe(control.DownloadStatus);
        retained.FileDataTransferred.ShouldBe(control.FileDataTransferred);
        Mock.Container.Resolve<IFileSystem>().File.ReadAllBytes(control.DownloadFilePath).ShouldBe(new byte[] { 99, 100 });
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<MoveFileWithResumeCommand>(),
            It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IEventPublisher>().Verify(x => x.PublishAsync(It.IsAny<SendNotificationResult>(),
            It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify(x => x.OnStatusChangedAsync(
            It.IsAny<DownloadTaskKey>(), It.IsAny<DownloadStatus>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.MusicTrack)]
    [Arguments(PlexMediaType.OtherVideos)]
    public async Task ShouldUseFreshProgressResourcesAndMoveBothFiles_WhenSameHandlerExecutesTwice(PlexMediaType family)
    {
        // Arrange
        await SetupDatabase(52227, c =>
        {
            c.MovieDownloadTasksCount = family == PlexMediaType.Movie ? 2 : 0;
            c.MusicArtistDownloadTasksCount = family == PlexMediaType.MusicTrack ? 2 : 0;
            c.MusicAlbumDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.MusicTrackDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.MusicTrackFileDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.PlexMusicLibraryCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.OtherVideoDownloadTasksCount = family == PlexMediaType.OtherVideos ? 2 : 0;
            c.OtherVideoFileDownloadTasksCount = family == PlexMediaType.OtherVideos ? 1 : 0;
            c.PlexOtherVideoLibraryCount = family == PlexMediaType.OtherVideos ? 1 : 0;
        });
        var dbContext = IDbContext;
        DownloadTaskFileBase[] tasks = family switch
        {
            PlexMediaType.MusicTrack => (
                await dbContext.DownloadTaskMusicTrackFiles.OrderBy(x => x.PlexApiRatingKey).ToArrayAsync(CancellationToken)
            ).Cast<DownloadTaskFileBase>().ToArray(),
            PlexMediaType.OtherVideos => (
                await dbContext.DownloadTaskOtherVideoFiles.OrderBy(x => x.PlexApiRatingKey).ToArrayAsync(CancellationToken)
            ).Cast<DownloadTaskFileBase>().ToArray(),
            _ => (await dbContext.DownloadTaskMovieFile.OrderBy(x => x.Id).ToListAsync(CancellationToken))
                .Cast<DownloadTaskFileBase>().ToArray(),
        };
        var paths = Mock.Container.Resolve<IPathProvider>();
        foreach (var task in tasks)
        {
            task.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
            task.DirectoryMeta.DestinationRootPath = Path.Combine(paths.DefaultDownloadsDestinationFolder, "move-destination");
            task.DataTotal = 4;
            task.DownloadStatus = DownloadStatus.DownloadFinished;
            dbContext.Entry(task).State = EntityState.Modified;
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        byte[][] contents = [[1, 2, 3, 4], [5, 6, 7, 8]];
        SetupFileSystem(fs =>
        {
            for (var i = 0; i < tasks.Length; i++)
                fs.AddFile(tasks[i].DownloadFilePath, new MockFileData(contents[i]));
        });
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>().SingleInstance());
        Mock.Mock<IDownloadManagerSettings>().SetupGet(x => x.KeepCompletedInDownloadFolder).Returns(false);
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.Is<MoveFileWithResumeCommand>(c =>
                tasks.Any(t => t.DownloadFilePath == c.SourcePath && t.DestinationFilePath == c.TargetPath)
                && c.CurrentOffset == 0 && c.DataTotal == 4), CancellationToken))
            .Returns<MoveFileWithResumeCommand, CancellationToken>((c, ct) =>
                Mock.Create<MoveFileWithResumeCommandHandler>().ExecuteAsync(c, ct)).Verifiable(Times.Exactly(2));
        var handler = Mock.Create<MoveDownloadFileFromFileTaskCommandHandler>();
        var fs = Mock.Container.Resolve<IFileSystem>();

        // Act and assert
        for (var i = 0; i < tasks.Length; i++)
        {
            using var progress = new Subject<IDownloadFileTransferProgress>();
            var updates = new List<IDownloadFileTransferProgress>();
            var subjectCompleted = false;
            using var subscription = progress.Subscribe(updates.Add, _ => { }, () => subjectCompleted = true);
            var result = await handler.ExecuteAsync(new MoveDownloadFileFromFileTaskCommand(tasks[i].ToKey(), progress),
                CancellationToken);
            result.IsSuccess.ShouldBeTrue();
            result.Errors.ShouldBeEmpty();
            fs.File.ReadAllBytes(tasks[i].DestinationFilePath).ShouldBe(contents[i]);
            fs.File.Exists(tasks[i].DownloadFilePath).ShouldBeFalse();
            var persisted = (await dbContext.GetDownloadTaskFileAsync(tasks[i].ToKey(), CancellationToken))!;
            persisted.DownloadStatus.ShouldBe(DownloadStatus.MoveFinished);
            persisted.FileDataTransferred.ShouldBe(4);
            persisted.CurrentFileTransferBytesOffset.ShouldBe(4);
            persisted.Percentage.ShouldBe(100);
            updates.ShouldHaveSingleItem().FileDataTransferred.ShouldBe(4);
            subjectCompleted.ShouldBeTrue();
            if (i == 0)
            {
                var next = (await dbContext.GetDownloadTaskFileAsync(tasks[1].ToKey(), CancellationToken))!;
                next.DownloadStatus.ShouldBe(DownloadStatus.DownloadFinished);
                next.FileDataTransferred.ShouldBe(0);
                fs.File.ReadAllBytes(tasks[1].DownloadFilePath).ShouldBe(contents[1]);
                fs.File.Exists(tasks[1].DestinationFilePath).ShouldBeFalse();
                var logs = await dbContext.GetDownloadTaskLogsAsync(tasks[1].ToKey(), null, null, CancellationToken);
                logs.IsSuccess.ShouldBeTrue();
                logs.Value.ShouldBeEmpty();
            }
        }
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>().Verify(x => x.PublishAsync(It.IsAny<SendNotificationResult>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(PlexMediaType.Movie, false)]
    [Arguments(PlexMediaType.MusicTrack, false)]
    [Arguments(PlexMediaType.OtherVideos, false)]
    [Arguments(PlexMediaType.Movie, true)]
    [Arguments(PlexMediaType.MusicTrack, true)]
    [Arguments(PlexMediaType.OtherVideos, true)]
    public async Task ShouldCompleteSubjectPersistTerminalStateAndPreserveBytes_WhenMovingStatusDispatchThrows(
        PlexMediaType family, bool cancelled)
    {
        // Arrange
        await SetupDatabase(52228, c =>
        {
            c.MovieDownloadTasksCount = family == PlexMediaType.Movie ? 1 : 0;
            c.MusicArtistDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.MusicAlbumDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.MusicTrackDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.MusicTrackFileDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.PlexMusicLibraryCount = family == PlexMediaType.MusicTrack ? 1 : 0;
            c.OtherVideoDownloadTasksCount = family == PlexMediaType.OtherVideos ? 1 : 0;
            c.OtherVideoFileDownloadTasksCount = family == PlexMediaType.OtherVideos ? 1 : 0;
            c.PlexOtherVideoLibraryCount = family == PlexMediaType.OtherVideos ? 1 : 0;
        });
        var dbContext = IDbContext;
        DownloadTaskFileBase target = family switch
        {
            PlexMediaType.MusicTrack => await dbContext.DownloadTaskMusicTrackFiles.SingleAsync(CancellationToken),
            PlexMediaType.OtherVideos => await dbContext.DownloadTaskOtherVideoFiles.SingleAsync(CancellationToken),
            _ => await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken),
        };
        var paths = Mock.Container.Resolve<IPathProvider>();
        target.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DestinationRootPath = Path.Combine(paths.DefaultDownloadsDestinationFolder, "move-destination");
        target.DataTotal = 4;
        target.DownloadStatus = DownloadStatus.DownloadFinished;
        target.TimeRemaining = 123;
        dbContext.Entry(target).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var key = target.ToKey();
        byte[] bytes = [1, 2, 3, 4];
        SetupFileSystem(fs => fs.AddFile(target.DownloadFilePath, new MockFileData(bytes)));
        Mock.Mock<IDownloadManagerSettings>().SetupGet(x => x.KeepCompletedInDownloadFolder).Returns(false);
        var realDispatcher = Mock.Create<DownloadTaskUpdateDispatcher>();
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Setup(x => x.OnStatusChangedAsync(key, DownloadStatus.Moving,
                CancellationToken)).ThrowsAsync(cancelled
                ? new OperationCanceledException("moving dispatch cancelled")
                : new InvalidOperationException("moving dispatch failed")).Verifiable(Times.Once());
        if (cancelled)
            Mock.Mock<IDownloadTaskUpdateDispatcher>().Setup(x => x.OnStatusChangedAsync(key, DownloadStatus.MovePaused,
                    CancellationToken.None))
                .Returns<DownloadTaskKey, DownloadStatus, CancellationToken>((k, status, ct) =>
                    realDispatcher.OnStatusChangedAsync(k, status, ct)).Verifiable(Times.Once());
        else
            Mock.Mock<IDownloadTaskUpdateDispatcher>().Setup(x => x.OnStatusChangedAsync(key, DownloadStatus.MoveError,
                    It.IsAny<Result>(), CancellationToken.None))
                .Returns<DownloadTaskKey, DownloadStatus, Result, CancellationToken>((k, status, result, ct) =>
                    realDispatcher.OnStatusChangedAsync(k, status, result, ct)).Verifiable(Times.Once());
        Result? notification = null;
        Mock.Mock<IEventPublisher>().Setup(x => x.PublishAsync(It.IsAny<SendNotificationResult>(), CancellationToken.None))
            .Callback<IEvent, CancellationToken>((message, _) => notification = ((SendNotificationResult)message).Result)
            .Returns(Task.CompletedTask).Verifiable(cancelled ? Times.Never() : Times.Once());
        using var progress = new Subject<IDownloadFileTransferProgress>();
        var updates = new List<IDownloadFileTransferProgress>();
        var subjectCompleted = false;
        using var subscription = progress.Subscribe(updates.Add, _ => { }, () => subjectCompleted = true);

        // Act
        var result = await TestHandlerExecuteAsync(new MoveDownloadFileFromFileTaskCommand(key, progress));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBe(cancelled);
        result.Errors.Count.ShouldBe(1);
        if (cancelled)
            notification.ShouldBeNull();
        else
            notification.ShouldBeSameAs(result);
        var persisted = (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!;
        persisted.DownloadStatus.ShouldBe(cancelled ? DownloadStatus.MovePaused : DownloadStatus.MoveError);
        if (cancelled)
            persisted.TimeRemaining.ShouldBe(0);
        persisted.FileDataTransferred.ShouldBe(0);
        persisted.CurrentFileTransferBytesOffset.ShouldBe(0);
        var fs = Mock.Container.Resolve<IFileSystem>();
        fs.File.ReadAllBytes(target.DownloadFilePath).ShouldBe(bytes);
        fs.File.Exists(target.DestinationFilePath).ShouldBeFalse();
        if (cancelled)
        {
            var update = updates.ShouldHaveSingleItem();
            update.FileDataTransferred.ShouldBe(0);
            update.CurrentFileTransferBytesOffset.ShouldBe(0);
            update.TimeRemaining.ShouldBe(0);
        }
        else
            updates.ShouldBeEmpty();
        subjectCompleted.ShouldBeTrue();
        var logs = await dbContext.GetDownloadTaskLogsAsync(key, null, null, CancellationToken);
        logs.IsSuccess.ShouldBeTrue();
        logs.Errors.ShouldBeEmpty();
        logs.Value.Select(x => x.Status).ShouldBe(cancelled
            ? [DownloadStatus.Moving, DownloadStatus.MovePaused]
            : [DownloadStatus.Moving, DownloadStatus.MoveError, DownloadStatus.MoveError]);
        logs.Value.Last().Message.ShouldBe(cancelled
            ? $"Download {target.FileName} transitioned to status: {DownloadStatus.MovePaused}"
            : result.ToString());
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(It.IsAny<MoveFileWithResumeCommand>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }
}
