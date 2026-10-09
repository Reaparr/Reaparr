using Quartz;

namespace Reaparr.Application.UnitTests;

public class MoveDownloadFileJobUnitTests : BaseUnitTest<MoveDownloadFileJob>
{
    private static IJobExecutionContext SetupJobContext(
        DownloadTaskKey key,
        CancellationToken cancellationToken = default
    )
    {
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.JobDataMap).Returns(new MoveDownloadFileJobPayload(key).ToJobDataMap());
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(x => x.MergedJobDataMap).Returns(jobDetail.Object.JobDataMap);
        context.SetupGet(x => x.CancellationToken).Returns(cancellationToken);
        context.SetupProperty(x => x.Result);
        return context.Object;
    }

    [Test]
    [Arguments(PlexMediaType.Movie, "normal")]
    [Arguments(PlexMediaType.Episode, "normal")]
    [Arguments(PlexMediaType.MusicTrack, "normal")]
    [Arguments(PlexMediaType.PhotoImage, "normal")]
    [Arguments(PlexMediaType.OtherVideos, "normal")]
    [Arguments(PlexMediaType.Movie, "keep")]
    [Arguments(PlexMediaType.Episode, "keep")]
    [Arguments(PlexMediaType.MusicTrack, "keep")]
    [Arguments(PlexMediaType.PhotoImage, "keep")]
    [Arguments(PlexMediaType.OtherVideos, "keep")]
    [Arguments(PlexMediaType.Movie, "late-cancellation")]
    [Arguments(PlexMediaType.Episode, "late-cancellation")]
    [Arguments(PlexMediaType.MusicTrack, "late-cancellation")]
    [Arguments(PlexMediaType.PhotoImage, "late-cancellation")]
    [Arguments(PlexMediaType.OtherVideos, "late-cancellation")]
    [Arguments(PlexMediaType.Movie, "cleanup-failure")]
    [Arguments(PlexMediaType.Episode, "cleanup-failure")]
    [Arguments(PlexMediaType.MusicTrack, "cleanup-failure")]
    [Arguments(PlexMediaType.PhotoImage, "cleanup-failure")]
    [Arguments(PlexMediaType.OtherVideos, "cleanup-failure")]
    [Arguments(PlexMediaType.Movie, "retry")]
    [Arguments(PlexMediaType.Episode, "retry")]
    [Arguments(PlexMediaType.MusicTrack, "retry")]
    [Arguments(PlexMediaType.PhotoImage, "retry")]
    [Arguments(PlexMediaType.OtherVideos, "retry")]
    [Arguments(PlexMediaType.Movie, "already-moved")]
    [Arguments(PlexMediaType.Episode, "already-moved")]
    [Arguments(PlexMediaType.MusicTrack, "already-moved")]
    [Arguments(PlexMediaType.PhotoImage, "already-moved")]
    [Arguments(PlexMediaType.OtherVideos, "already-moved")]
    public async Task ShouldMoveOriginalBytesCompleteAndCleanOnlyUnusedFolders_WhenJobFinishes(
        PlexMediaType family,
        string scenario
    )
    {
        // Arrange
        await SetupDatabase(
            11001,
            c =>
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
            }
        );
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
                var album = FakeData
                    .GetDownloadTaskPhotoAlbum(new Seed(11001))
                    .RuleFor(x => x.Children, _ => FakeData.GetDownloadTaskPhotoImage(new Seed(11002)).Generate(2))
                    .Generate();
                foreach (
                    var node in new DownloadTaskBase[] { album }
                        .Concat(album.Children)
                        .Concat(album.Children.SelectMany(x => x.Children))
                )
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
                var files =
                    family == PlexMediaType.Movie
                        ? (await dbContext.DownloadTaskMovieFile.OrderBy(x => x.Id).ToListAsync(CancellationToken))
                            .Cast<DownloadTaskFileBase>()
                            .ToArray()
                        : (
                            await dbContext
                                .DownloadTaskTvShowEpisodeFile.OrderBy(x => x.Id)
                                .ToListAsync(CancellationToken)
                        )
                            .Cast<DownloadTaskFileBase>()
                            .ToArray();
                target = files[0];
                control = files[1];
                break;
        }
        var paths = Mock.Container.Resolve<IPathProvider>();
        target.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DestinationRootPath = Path.Combine(
            paths.DefaultDownloadsDestinationFolder,
            "move-destination"
        );
        control.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
        target.DataReceived = 0;
        target.DataTotal = 4;
        target.DownloadStatus = scenario switch
        {
            "retry" => DownloadStatus.MoveError,
            "already-moved" => DownloadStatus.MoveFinished,
            _ => DownloadStatus.DownloadFinished,
        };
        target.Percentage = 75;
        dbContext.Entry(target).State = EntityState.Modified;
        dbContext.Entry(control).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var key = target.ToKey();
        var ancestorKeys = new List<DownloadTaskKey>();
        var parentKey = target.ToParentKey();
        while (parentKey is not null)
        {
            ancestorKeys.Add(parentKey);
            var parent = await dbContext.GetDownloadTaskAsync(parentKey.Id, parentKey.Type, CancellationToken);
            parent.ShouldNotBeNull();
            parentKey =
                parent.ParentId == Guid.Empty
                    ? null
                    : await dbContext.GetDownloadTaskKeyAsync(parent.ParentId, CancellationToken);
        }
        var controlBefore = (
            control.DownloadStatus,
            control.FileDataTransferred,
            control.CurrentFileTransferBytesOffset,
            control.Percentage,
            control.DirectoryMeta
        );
        var category = family switch
        {
            PlexMediaType.Movie => "Movies",
            PlexMediaType.Episode => "TvShows",
            PlexMediaType.MusicTrack => "Music",
            PlexMediaType.PhotoImage => "Photos",
            _ => "OtherVideos",
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
        var expectedDestination = Path.Combine(target.DirectoryMeta.DestinationRootPath, relative, target.FileName);
        target.DestinationFilePath.ShouldBe(expectedDestination);
        target.DownloadFilePath.ShouldBe(
            Path.Combine(
                paths.DefaultDownloadsDestinationFolder,
                category,
                relative,
                target.FileName.AddReaparrTempSuffixToFileName()
            )
        );
        byte[] bytes = [1, 2, 3, 4];
        byte[] controlBytes = [99, 100];
        SetupFileSystem(fs =>
        {
            fs.AddFile(
                scenario == "already-moved" ? expectedDestination : target.DownloadFilePath,
                new MockFileData(bytes)
            );
            fs.AddFile(control.DownloadFilePath, new MockFileData(controlBytes));
        });
        var fs = Mock.Container.Resolve<IFileSystem>();
        fs.File.ReadAllBytes(scenario == "already-moved" ? expectedDestination : target.DownloadFilePath)
            .ShouldBe(bytes);
        (await dbContext.GetDownloadTaskStatusAsync(key, CancellationToken)).ShouldBe(target.DownloadStatus);
        using var cancellation = new CancellationTokenSource();
        var token = scenario == "late-cancellation" ? cancellation.Token : CancellationToken.None;
        var context = SetupJobContext(key, token);
        SetupDependencies(b =>
            b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>().SingleInstance()
        );
        Mock.Mock<IDownloadManagerSettings>()
            .SetupGet(x => x.KeepCompletedInDownloadFolder)
            .Returns(scenario == "keep");
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<MoveDownloadFileFromFileTaskCommand>(c => c.Key == key && c.MoveDownloadFileProgress == null),
                    token
                )
            )
            .Returns<MoveDownloadFileFromFileTaskCommand, CancellationToken>(
                (c, ct) => Mock.Create<MoveDownloadFileFromFileTaskCommandHandler>().ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<MoveFileWithResumeCommand>(c =>
                        c.SourcePath == target.DownloadFilePath
                        && c.TargetPath == expectedDestination
                        && c.CurrentOffset == 0
                        && c.DataTotal == 4
                    ),
                    token
                )
            )
            .Returns<MoveFileWithResumeCommand, CancellationToken>(
                async (c, ct) =>
                {
                    var result = await Mock.Create<MoveFileWithResumeCommandHandler>().ExecuteAsync(c, ct);
                    if (scenario == "late-cancellation")
                        cancellation.Cancel();
                    return result;
                }
            )
            .Verifiable(scenario is "keep" or "already-moved" ? Times.Never() : Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<CleanUpDownloadTaskFoldersCommand>(c => c.DownloadTaskKey == key), CancellationToken.None)
            )
            .Returns<CleanUpDownloadTaskFoldersCommand, CancellationToken>(
                (c, ct) =>
                    scenario == "cleanup-failure"
                        ? Task.FromResult(Result.Fail("cleanup unavailable"))
                        : Mock.Create<CleanUpDownloadTaskFoldersCommandHandler>().ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(token))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(It.IsAny<SendNotificationResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());

        // Act
        await Sut.Execute(context);

        // Assert
        context.Result.ShouldBeNull();
        var persisted = (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!;
        persisted.Id.ShouldBe(target.Id);
        persisted.ToParentKey().ShouldBe(target.ToParentKey());
        persisted.DownloadStatus.ShouldBe(DownloadStatus.Completed);
        persisted.DataReceived.ShouldBe(0);
        persisted.FileDataTransferred.ShouldBe(4);
        persisted.CurrentFileTransferBytesOffset.ShouldBe(4);
        persisted.Percentage.ShouldBe(100);
        persisted.TimeRemaining.ShouldBe(0);
        persisted.DirectoryMeta.ShouldBe(target.DirectoryMeta);
        var finalPath = scenario == "keep" ? target.DownloadFilePath.RemoveReapTempSuffix() : expectedDestination;
        fs.File.ReadAllBytes(finalPath).ShouldBe(bytes);
        fs.Path.GetFileName(finalPath).ShouldBe(target.FileName);
        fs.File.Exists(target.DownloadFilePath).ShouldBeFalse();
        fs.Directory.Exists(target.DownloadDirectory)
            .ShouldBe(
                scenario is "keep" or "cleanup-failure" || family is PlexMediaType.Episode or PlexMediaType.PhotoImage
            );
        fs.Directory.Exists(Path.Combine(paths.DefaultDownloadsDestinationFolder, category)).ShouldBeTrue();
        if (scenario == "keep")
            fs.File.Exists(expectedDestination).ShouldBeFalse();
        var retained = (await dbContext.GetDownloadTaskFileAsync(control.ToKey(), CancellationToken))!;
        (
            retained.DownloadStatus,
            retained.FileDataTransferred,
            retained.CurrentFileTransferBytesOffset,
            retained.Percentage,
            retained.DirectoryMeta
        ).ShouldBe(controlBefore);
        fs.File.ReadAllBytes(control.DownloadFilePath).ShouldBe(controlBytes);
        foreach (var ancestor in ancestorKeys)
        {
            var expected =
                family is PlexMediaType.Episode or PlexMediaType.PhotoImage && ancestor.Id != target.ToParentKey()!.Id
                    ? DownloadStatus.Queued
                    : DownloadStatus.Completed;
            (await dbContext.GetDownloadTaskStatusAsync(ancestor, CancellationToken)).ShouldBe(expected);
        }
        var logs = await dbContext.GetDownloadTaskLogsAsync(key, null, null, CancellationToken);
        logs.IsSuccess.ShouldBeTrue();
        logs.Errors.Count.ShouldBe(0);
        logs.Value.Last().Status.ShouldBe(DownloadStatus.Completed);
        logs.Value.Last()
            .Message.ShouldBe($"Download {target.FileName} transitioned to status: {DownloadStatus.Completed}");
        var controlLogs = await dbContext.GetDownloadTaskLogsAsync(control.ToKey(), null, null, CancellationToken);
        controlLogs.IsSuccess.ShouldBeTrue();
        controlLogs.Errors.Count.ShouldBe(0);
        controlLogs.Value.ShouldBeEmpty();
        cancellation.IsCancellationRequested.ShouldBe(scenario == "late-cancellation");
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IMoveDownloadFileQueue>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
    }

    [Test]
    [Arguments(PlexMediaType.Movie, "missing")]
    [Arguments(PlexMediaType.Episode, "missing")]
    [Arguments(PlexMediaType.MusicTrack, "missing")]
    [Arguments(PlexMediaType.PhotoImage, "missing")]
    [Arguments(PlexMediaType.OtherVideos, "missing")]
    [Arguments(PlexMediaType.Movie, "incomplete")]
    [Arguments(PlexMediaType.Episode, "incomplete")]
    [Arguments(PlexMediaType.MusicTrack, "incomplete")]
    [Arguments(PlexMediaType.PhotoImage, "incomplete")]
    [Arguments(PlexMediaType.OtherVideos, "incomplete")]
    [Arguments(PlexMediaType.Movie, "cancelled")]
    [Arguments(PlexMediaType.Episode, "cancelled")]
    [Arguments(PlexMediaType.MusicTrack, "cancelled")]
    [Arguments(PlexMediaType.PhotoImage, "cancelled")]
    [Arguments(PlexMediaType.OtherVideos, "cancelled")]
    public async Task ShouldPreserveSourceAndSkipCompletionAndCleanup_WhenRealMovementDoesNotFinish(
        PlexMediaType family,
        string scenario
    )
    {
        // Arrange
        await SetupDatabase(
            11002,
            c =>
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
            }
        );
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
                var album = FakeData
                    .GetDownloadTaskPhotoAlbum(new Seed(11002))
                    .RuleFor(x => x.Children, _ => FakeData.GetDownloadTaskPhotoImage(new Seed(11003)).Generate(2))
                    .Generate();
                foreach (
                    var node in new DownloadTaskBase[] { album }
                        .Concat(album.Children)
                        .Concat(album.Children.SelectMany(x => x.Children))
                )
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
                var files =
                    family == PlexMediaType.Movie
                        ? (await dbContext.DownloadTaskMovieFile.OrderBy(x => x.Id).ToListAsync(CancellationToken))
                            .Cast<DownloadTaskFileBase>()
                            .ToArray()
                        : (
                            await dbContext
                                .DownloadTaskTvShowEpisodeFile.OrderBy(x => x.Id)
                                .ToListAsync(CancellationToken)
                        )
                            .Cast<DownloadTaskFileBase>()
                            .ToArray();
                target = files[0];
                control = files[1];
                break;
        }
        var paths = Mock.Container.Resolve<IPathProvider>();
        target.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DestinationRootPath = Path.Combine(
            paths.DefaultDownloadsDestinationFolder,
            "move-destination"
        );
        var bytes = scenario == "cancelled" ? new byte[3 * 1024 * 1024] : new byte[] { 1, 2, 3, 4 };
        new Random(17).NextBytes(bytes);
        target.DataTotal = scenario == "cancelled" ? bytes.Length : 8;
        target.DownloadStatus = DownloadStatus.DownloadFinished;
        dbContext.Entry(target).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var key = target.ToKey();
        byte[] controlBytes = [99, 100];
        SetupFileSystem(fs =>
        {
            if (scenario != "missing")
                fs.AddFile(target.DownloadFilePath, new MockFileData(bytes));
            fs.AddFile(control.DownloadFilePath, new MockFileData(controlBytes));
        });
        var fs = Mock.Container.Resolve<IFileSystem>();
        using var cancellation = new CancellationTokenSource();
        var token = scenario == "cancelled" ? cancellation.Token : CancellationToken.None;
        var context = SetupJobContext(key, token);
        SetupDependencies(b =>
            b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>().SingleInstance()
        );
        Mock.Mock<IDownloadManagerSettings>().SetupGet(x => x.KeepCompletedInDownloadFolder).Returns(false);
        Result? moveOutcome = null;
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<MoveDownloadFileFromFileTaskCommand>(c => c.Key == key), token))
            .Returns<MoveDownloadFileFromFileTaskCommand, CancellationToken>(
                async (c, ct) =>
                {
                    moveOutcome = await Mock.Create<MoveDownloadFileFromFileTaskCommandHandler>().ExecuteAsync(c, ct);
                    return moveOutcome;
                }
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<MoveFileWithResumeCommand>(c =>
                        c.SourcePath == target.DownloadFilePath
                        && c.TargetPath == target.DestinationFilePath
                        && c.CurrentOffset == 0
                        && c.DataTotal == target.DataTotal
                    ),
                    token
                )
            )
            .Returns<MoveFileWithResumeCommand, CancellationToken>(
                (c, ct) =>
                    Mock.Create<MoveFileWithResumeCommandHandler>()
                        .ExecuteAsync(
                            scenario == "cancelled"
                                ? c with
                                {
                                    Progress = dto =>
                                    {
                                        c.Progress(dto);
                                        cancellation.Cancel();
                                    },
                                }
                                : c,
                            ct
                        )
            )
            .Verifiable(scenario == "missing" ? Times.Never() : Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CleanUpDownloadTaskFoldersCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never());
        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(token))
            .ReturnsAsync(Result.Ok())
            .Verifiable(scenario == "cancelled" ? Times.Never() : Times.Once());
        Result? notified = null;
        Mock.Mock<IEventPublisher>()
            .Setup(x =>
                x.PublishAsync(
                    It.Is<SendNotificationResult>(n => n.Result.IsFailed && n.Result.Errors.Count == 1),
                    CancellationToken.None
                )
            )
            .Callback<IEvent, CancellationToken>((n, _) => notified = ((SendNotificationResult)n).Result)
            .Returns(Task.CompletedTask)
            .Verifiable(scenario == "cancelled" ? Times.Never() : Times.Once());

        // Act
        await Sut.Execute(context);

        // Assert
        moveOutcome.ShouldNotBeNull();
        moveOutcome!.Errors.Count.ShouldBe(1);
        moveOutcome.IsCancelled.ShouldBe(scenario == "cancelled");
        context.Result.ShouldBeNull();
        var persisted = (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!;
        persisted.DownloadStatus.ShouldBe(
            scenario == "cancelled" ? DownloadStatus.MovePaused : DownloadStatus.MoveError
        );
        persisted.CurrentFileTransferBytesOffset.ShouldBe(scenario == "cancelled" ? 1024 * 1024 : 0);
        persisted.FileDataTransferred.ShouldBe(scenario == "cancelled" ? 1024 * 1024 : 0);
        if (scenario == "missing")
        {
            fs.File.Exists(target.DownloadFilePath).ShouldBeFalse();
            fs.File.Exists(target.DestinationFilePath).ShouldBeFalse();
        }
        else
        {
            fs.File.ReadAllBytes(target.DownloadFilePath).ShouldBe(bytes);
            fs.File.ReadAllBytes(target.DestinationFilePath)
                .ShouldBe(scenario == "cancelled" ? bytes.Take(1024 * 1024).ToArray() : bytes);
        }
        if (scenario != "cancelled")
            notified.ShouldBeSameAs(moveOutcome);
        var retained = (await dbContext.GetDownloadTaskFileAsync(control.ToKey(), CancellationToken))!;
        retained.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        retained.FileDataTransferred.ShouldBe(0);
        retained.CurrentFileTransferBytesOffset.ShouldBe(0);
        fs.File.ReadAllBytes(control.DownloadFilePath).ShouldBe(controlBytes);
        var logs = await dbContext.GetDownloadTaskLogsAsync(key, null, null, CancellationToken);
        logs.IsSuccess.ShouldBeTrue();
        logs.Errors.ShouldBeEmpty();
        logs.Value.Select(x => x.Status)
            .ShouldBe(
                scenario switch
                {
                    "cancelled" => [DownloadStatus.Moving, DownloadStatus.Moving, DownloadStatus.MovePaused],
                    "missing" => [DownloadStatus.MoveError, DownloadStatus.MoveError],
                    _ =>
                    [
                        DownloadStatus.Moving,
                        DownloadStatus.Moving,
                        DownloadStatus.MoveError,
                        DownloadStatus.MoveError,
                    ],
                }
            );
        logs.Value.Last()
            .Message.ShouldBe(
                scenario == "cancelled"
                    ? $"Download {target.FileName} transitioned to status: {DownloadStatus.MovePaused}"
                    : moveOutcome.ToString()
            );
        var controlLogs = await dbContext.GetDownloadTaskLogsAsync(control.ToKey(), null, null, CancellationToken);
        controlLogs.IsSuccess.ShouldBeTrue();
        controlLogs.Errors.ShouldBeEmpty();
        controlLogs.Value.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IMoveDownloadFileQueue>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
    }

    [Test]
    [Arguments(PlexMediaType.Movie, DownloadStatus.Stopped)]
    [Arguments(PlexMediaType.Episode, DownloadStatus.Stopped)]
    [Arguments(PlexMediaType.MusicTrack, DownloadStatus.Stopped)]
    [Arguments(PlexMediaType.PhotoImage, DownloadStatus.Stopped)]
    [Arguments(PlexMediaType.OtherVideos, DownloadStatus.Stopped)]
    [Arguments(PlexMediaType.MusicTrack, DownloadStatus.Paused)]
    [Arguments(PlexMediaType.OtherVideos, DownloadStatus.Paused)]
    [Arguments(PlexMediaType.MusicTrack, DownloadStatus.Completed)]
    [Arguments(PlexMediaType.OtherVideos, DownloadStatus.Completed)]
    public async Task ShouldLeaveFilesAndStatusUnchanged_WhenQueuedJobIsNotAuthorizedToMove(
        PlexMediaType family,
        DownloadStatus status
    )
    {
        // Arrange
        await SetupDatabase(
            11003,
            c =>
            {
                c.MovieDownloadTasksCount = family == PlexMediaType.Movie ? 1 : 0;
                c.TvShowDownloadTasksCount = family == PlexMediaType.Episode ? 1 : 0;
                c.TvShowSeasonDownloadTasksCount = family == PlexMediaType.Episode ? 1 : 0;
                c.TvShowEpisodeDownloadTasksCount = family == PlexMediaType.Episode ? 1 : 0;
                c.PlexMusicLibraryCount = family == PlexMediaType.MusicTrack ? 1 : 0;
                c.MusicArtistDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
                c.MusicAlbumDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
                c.MusicTrackDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
                c.MusicTrackFileDownloadTasksCount = family == PlexMediaType.MusicTrack ? 1 : 0;
                c.PlexPhotoLibraryCount = family == PlexMediaType.PhotoImage ? 1 : 0;
                c.PlexOtherVideoLibraryCount = family == PlexMediaType.OtherVideos ? 1 : 0;
                c.OtherVideoDownloadTasksCount = family == PlexMediaType.OtherVideos ? 1 : 0;
                c.OtherVideoFileDownloadTasksCount = family == PlexMediaType.OtherVideos ? 1 : 0;
            }
        );
        var dbContext = IDbContext;
        DownloadTaskFileBase target;
        switch (family)
        {
            case PlexMediaType.MusicTrack:
                target = await dbContext.DownloadTaskMusicTrackFiles.SingleAsync(CancellationToken);
                break;
            case PlexMediaType.OtherVideos:
                target = await dbContext.DownloadTaskOtherVideoFiles.SingleAsync(CancellationToken);
                break;
            case PlexMediaType.PhotoImage:
                var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
                var album = FakeData.GetDownloadTaskPhotoAlbum(new Seed(11003)).Generate();
                var image = album.Children.Single();
                var photo = image.Children.Single();
                foreach (var node in new DownloadTaskBase[] { album, image, photo })
                {
                    node.PlexServerId = library.PlexServerId;
                    node.PlexLibraryId = library.Id;
                }
                dbContext.DownloadTaskPhotoAlbums.Add(album);
                await dbContext.SaveChangesAsync(CancellationToken);
                target = photo;
                break;
            default:
                target =
                    family == PlexMediaType.Movie
                        ? await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken)
                        : await dbContext.DownloadTaskTvShowEpisodeFile.SingleAsync(CancellationToken);
                break;
        }
        var key = target.ToKey();
        await dbContext.SetDownloadStatus(key, status);
        (await dbContext.GetDownloadTaskStatusAsync(key, CancellationToken)).ShouldBe(status);
        SetupFileSystem(fs => fs.AddFile(target.DownloadFilePath, new MockFileData(new byte[] { 1, 2, 3, 4 })));
        var context = SetupJobContext(key);
        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(CancellationToken.None))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context);

        // Assert
        (await dbContext.GetDownloadTaskStatusAsync(key, CancellationToken)).ShouldBe(status);
        var persisted = (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!;
        persisted.FileDataTransferred.ShouldBe(target.FileDataTransferred);
        persisted.CurrentFileTransferBytesOffset.ShouldBe(target.CurrentFileTransferBytesOffset);
        var fs = Mock.Container.Resolve<IFileSystem>();
        fs.File.ReadAllBytes(target.DownloadFilePath).ShouldBe(new byte[] { 1, 2, 3, 4 });
        fs.File.Exists(target.DestinationFilePath).ShouldBeFalse();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<MoveDownloadFileFromFileTaskCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CleanUpDownloadTaskFoldersCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileQueue>().Verify();
    }

    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.MusicTrack)]
    [Arguments(PlexMediaType.OtherVideos)]
    public async Task ShouldContainCompletionExceptionAndQueueNext_WhenCompletedStatusDispatchThrows(
        PlexMediaType family
    )
    {
        // Arrange
        await SetupDatabase(
            11004,
            c =>
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
            }
        );
        var dbContext = IDbContext;
        DownloadTaskFileBase target = family switch
        {
            PlexMediaType.MusicTrack => await dbContext.DownloadTaskMusicTrackFiles.SingleAsync(CancellationToken),
            PlexMediaType.OtherVideos => await dbContext.DownloadTaskOtherVideoFiles.SingleAsync(CancellationToken),
            _ => await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken),
        };
        target.DataTotal = 4;
        target.FileDataTransferred = 4;
        target.CurrentFileTransferBytesOffset = 4;
        target.Percentage = 100;
        target.DownloadStatus = DownloadStatus.MoveFinished;
        dbContext.Entry(target).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var key = target.ToKey();
        SetupFileSystem(fs => fs.AddFile(target.DestinationFilePath, new MockFileData(new byte[] { 1, 2, 3, 4 })));
        var context = SetupJobContext(key);
        var realDispatcher = Mock.Create<DownloadTaskUpdateDispatcher>();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x => x.OnStatusChangedAsync(key, DownloadStatus.MoveFinished, CancellationToken.None))
            .Returns<DownloadTaskKey, DownloadStatus, CancellationToken>(
                (k, status, ct) => realDispatcher.OnStatusChangedAsync(k, status, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x => x.OnStatusChangedAsync(key, DownloadStatus.Completed, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("completion dispatch failed"))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<MoveDownloadFileFromFileTaskCommand>(c => c.Key == key), CancellationToken.None))
            .Returns<MoveDownloadFileFromFileTaskCommand, CancellationToken>(
                (c, ct) => Mock.Create<MoveDownloadFileFromFileTaskCommandHandler>().ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CleanUpDownloadTaskFoldersCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never());
        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(CancellationToken.None))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        await Sut.Execute(context);

        // Assert
        context.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Failed);
        var persisted = (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!;
        persisted.DownloadStatus.ShouldBe(DownloadStatus.MoveFinished);
        persisted.FileDataTransferred.ShouldBe(4);
        persisted.CurrentFileTransferBytesOffset.ShouldBe(4);
        persisted.Percentage.ShouldBe(100);
        var fs = Mock.Container.Resolve<IFileSystem>();
        fs.File.ReadAllBytes(target.DestinationFilePath).ShouldBe(new byte[] { 1, 2, 3, 4 });
        fs.File.Exists(target.DownloadFilePath).ShouldBeFalse();
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IMoveDownloadFileQueue>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<MoveFileWithResumeCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(DownloadTaskType.MovieData, false)]
    [Arguments(DownloadTaskType.EpisodeData, false)]
    [Arguments(DownloadTaskType.MusicTrackData, false)]
    [Arguments(DownloadTaskType.PhotoData, false)]
    [Arguments(DownloadTaskType.OtherVideoData, false)]
    [Arguments(DownloadTaskType.MovieData, true)]
    public async Task ShouldLeaveControlTaskAndBytesUntouched_WhenTaskOrPayloadDoesNotExist(
        DownloadTaskType type,
        bool missingPayload
    )
    {
        // Arrange
        await SetupDatabase(11005, c => c.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var control = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        var before = (
            control.DownloadStatus,
            control.FileDataTransferred,
            control.CurrentFileTransferBytesOffset,
            control.Percentage,
            control.DirectoryMeta
        );
        var key = new DownloadTaskKey
        {
            Id = Guid.Parse("24ea01c9-5755-435c-a0cd-a17a11704045"),
            Type = type,
            PlexServerId = control.PlexServerId,
            PlexLibraryId = control.PlexLibraryId,
        };
        (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken)).ShouldBeNull();
        byte[] bytes = [99, 100];
        SetupFileSystem(fs => fs.AddFile(control.DownloadFilePath, new MockFileData(bytes)));
        var context = SetupJobContext(key);
        if (missingPayload)
            context.MergedJobDataMap.Clear();
        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(CancellationToken.None))
            .ReturnsAsync(Result.Ok())
            .Verifiable(missingPayload ? Times.Once() : Times.Never());

        // Act
        await Sut.Execute(context);

        // Assert
        if (missingPayload)
            context.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Failed);
        else
            context.Result.ShouldBeNull();
        var retained = (await dbContext.GetDownloadTaskFileAsync(control.ToKey(), CancellationToken))!;
        (
            retained.DownloadStatus,
            retained.FileDataTransferred,
            retained.CurrentFileTransferBytesOffset,
            retained.Percentage,
            retained.DirectoryMeta
        ).ShouldBe(before);
        var fs = Mock.Container.Resolve<IFileSystem>();
        fs.File.ReadAllBytes(control.DownloadFilePath).ShouldBe(bytes);
        fs.File.Exists(control.DestinationFilePath).ShouldBeFalse();
        var logs = await dbContext.GetDownloadTaskLogsAsync(control.ToKey(), null, null, CancellationToken);
        logs.IsSuccess.ShouldBeTrue();
        logs.Errors.ShouldBeEmpty();
        logs.Value.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<MoveDownloadFileFromFileTaskCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CleanUpDownloadTaskFoldersCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileQueue>()
            .Verify(
                x => x.CheckMoveDownloadFileJobQueue(It.IsAny<CancellationToken>()),
                missingPayload ? Times.Once() : Times.Never()
            );
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<SendNotificationResult>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }
}
