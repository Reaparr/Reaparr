using System.ComponentModel;
using System.Reactive.Linq;
using Autofac.Features.Indexed;
using Downloader;
using Quartz;
using DownloadStatus = Reaparr.Domain.DownloadStatus;

namespace Reaparr.Application.UnitTests;

public class DownloadJobPhotoUnitTests : BaseUnitTest<DownloadJob>
{
    [Test]
    public async Task ShouldDownloadMoveAndCompletePhotoAncestors_WhenDirectTransportCompletes()
    {
        // Arrange
        await SetupDatabase(88080, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var album = FakeData.GetDownloadTaskPhotoAlbum(new Seed(88080)).Generate();
        var image = album.Children.Single();
        var file = image.Children.Single();
        foreach (var node in new DownloadTaskBase[] { album, image, file })
        {
            node.PlexServerId = library.PlexServerId;
            node.PlexLibraryId = library.Id;
        }
        var paths = Mock.Container.Resolve<IPathProvider>();
        file.DataTotal = 4;
        file.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
        file.DirectoryMeta.DestinationRootPath = paths.DefaultPhotosDestinationFolder;
        dbContext.DownloadTaskPhotoAlbums.Add(album);
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext
            .DownloadTaskPhotoImageFiles.Where(x => x.Id == file.Id)
            .ExecuteUpdateAsync(
                p => p.SetProperty(x => x.DirectoryMeta, file.DirectoryMeta with { DownloadRootPath = string.Empty }),
                CancellationToken
            );
        (
            await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken)
        ).DirectoryMeta.DownloadRootPath.ShouldBeEmpty();
        var key = file.ToKey();
        var machineId = await dbContext.GetPlexServerMachineIdentifierById(library.PlexServerId);
        SetupFileSystem(fs => fs.AddDirectory(file.DownloadDirectory));
        SetupDependencies(b =>
        {
            b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>().SingleInstance();
            b.Register(c => c.Resolve<IFileSystem>().FileInfo).As<IFileInfoFactory>();
        });
        var fs = Mock.Container.Resolve<IFileSystem>();
        var package = new DownloadPackage
        {
            TotalFileSize = 4,
            FileName = file.FileName,
            Urls = ["https://plex/photo.jpg"],
        };
        var transport = new Mock<IDownloadService>();
        transport
            .Setup(x =>
                x.DownloadFileTaskAsync(
                    "https://plex/photo.jpg",
                    file.DownloadFilePath.RemoveReapTempSuffix(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<string, string, CancellationToken>(
                (_, _, _) =>
                {
                    fs.File.WriteAllBytes(file.DownloadFilePath, [1, 2, 3, 4]);
                    transport.Raise(
                        x => x.DownloadFileCompleted += null,
                        transport.Object,
                        new AsyncCompletedEventArgs(null, false, package)
                    );
                    return Task.CompletedTask;
                }
            )
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadManagerSettings>().SetupGet(x => x.DownloadSegments).Returns(1);
        Mock.Mock<IDownloadManagerSettings>().SetupGet(x => x.KeepCompletedInDownloadFolder).Returns(false);
        Mock.Mock<IServerSettingsModule>()
            .Setup(x => x.GetDownloadSpeedLimitObservable(machineId))
            .Returns(Observable.Return(0))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<DeterminePlexDownloadClientCommand>(c => c.DownloadTaskKey == key),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<DeterminePlexDownloadClientCommand, CancellationToken>(
                (c, ct) => Mock.Create<DeterminePlexDownloadClientCommandHandler>().ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetDirectDownloadUrlCommand>(c =>
                        c.PlexServerId == library.PlexServerId && c.FileLocationUrl == file.FileLocationUrl
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok("https://plex/photo.jpg"))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<EnsureDownloadDirectoryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<MoveDownloadFileFromFileTaskCommand>(c => c.Key == key), It.IsAny<CancellationToken>())
            )
            .Returns<MoveDownloadFileFromFileTaskCommand, CancellationToken>(
                (c, ct) => Mock.Create<MoveDownloadFileFromFileTaskCommandHandler>().ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<MoveFileWithResumeCommand>(c =>
                        c.SourcePath == file.DownloadFilePath
                        && c.TargetPath == file.DestinationFilePath
                        && c.DataTotal == 4
                        && c.CurrentOffset == 0
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<MoveFileWithResumeCommand, CancellationToken>(
                (c, ct) => Mock.Create<MoveFileWithResumeCommandHandler>().ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<CleanUpDownloadTaskFoldersCommand>(c => c.DownloadTaskKey == key), CancellationToken.None)
            )
            .Returns<CleanUpDownloadTaskFoldersCommand, CancellationToken>(
                (c, ct) => Mock.Create<CleanUpDownloadTaskFoldersHandler>().ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StartMoveDownloadFileJob(key, CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileQueue>()
            .Setup(x => x.CheckMoveDownloadFileJobQueue(CancellationToken.None))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        var client = Mock.Create<DirectPlexDownloadClient>(
            new NamedParameter(
                "downloadServiceFactory",
                (Func<DownloadConfiguration, IDownloadService>)(_ => transport.Object)
            )
        );
        var clients = new Mock<IIndex<PlexDownloadClientType, IPlexDownloadClient>>(MockBehavior.Strict);
        clients.Setup(x => x[PlexDownloadClientType.Direct]).Returns(client).Verifiable(Times.Once());
        var download = Mock.Create<DownloadJob>(new NamedParameter("plexDownloadClientFactory", clients.Object));
        var downloadContext = new Mock<IJobExecutionContext>();
        downloadContext.SetupGet(x => x.MergedJobDataMap).Returns(new DownloadJobPayload(key).ToJobDataMap());
        downloadContext.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        downloadContext.SetupProperty(x => x.Result);
        var moveContext = new Mock<IJobExecutionContext>();
        moveContext.SetupGet(x => x.MergedJobDataMap).Returns(new MoveDownloadFileJobPayload(key).ToJobDataMap());
        moveContext.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        moveContext.SetupProperty(x => x.Result);

        // Act
        await download.Execute(downloadContext.Object);
        (await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken)).DownloadStatus.ShouldBe(
            DownloadStatus.DownloadFinished
        );
        var queueResult = await Mock.Create<MoveDownloadFileJobQueue>()
            .CheckMoveDownloadFileJobQueue(CancellationToken);
        await Mock.Create<MoveDownloadFileJob>().Execute(moveContext.Object);

        // Assert
        queueResult.IsSuccess.ShouldBeTrue();
        queueResult.Errors.Count.ShouldBe(0);
        downloadContext.Object.Result.ShouldBeNull();
        moveContext.Object.Result.ShouldBeNull();
        var completed = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        completed.Id.ShouldBe(file.Id);
        completed.ParentId.ShouldBe(image.Id);
        completed.DownloadStatus.ShouldBe(DownloadStatus.Completed);
        completed.FileDataTransferred.ShouldBe(4);
        completed.DirectoryMeta.ShouldBe(file.DirectoryMeta);
        (await dbContext.DownloadTaskPhotoImages.SingleAsync(CancellationToken)).DownloadStatus.ShouldBe(
            DownloadStatus.Completed
        );
        (await dbContext.DownloadTaskPhotoAlbums.SingleAsync(CancellationToken)).DownloadStatus.ShouldBe(
            DownloadStatus.Completed
        );
        fs.File.ReadAllBytes(file.DestinationFilePath).ShouldBe(new byte[] { 1, 2, 3, 4 });
        fs.File.Exists(file.DownloadFilePath).ShouldBeFalse();
        fs.Directory.Exists(file.DownloadDirectory).ShouldBeFalse();
        fs.Directory.Exists(fs.Path.Combine(paths.DefaultDownloadsDestinationFolder, "Photos")).ShouldBeTrue();
        transport.Verify();
        clients.Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IServerSettingsModule>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileQueue>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GetDashTranscodeDecisionCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    [Arguments(PlexMediaType.MusicArtist, false, false, false)]
    [Arguments(PlexMediaType.OtherVideos, false, false, false)]
    [Arguments(PlexMediaType.MusicArtist, true, false, false)]
    [Arguments(PlexMediaType.OtherVideos, true, false, false)]
    [Arguments(PlexMediaType.MusicArtist, false, true, false)]
    [Arguments(PlexMediaType.OtherVideos, false, true, false)]
    [Arguments(PlexMediaType.MusicArtist, false, false, true)]
    [Arguments(PlexMediaType.OtherVideos, false, false, true)]
    public async Task ShouldPersistOnlyTargetFileAndAncestorState_WhenOriginalDownloadEnds(
        PlexMediaType family,
        bool incomplete,
        bool cancelled,
        bool resume
    )
    {
        // Arrange
        await SetupDatabase(
            88081,
            c =>
            {
                c.PlexMusicLibraryCount = family == PlexMediaType.MusicArtist ? 1 : 0;
                c.PlexOtherVideoLibraryCount = family == PlexMediaType.OtherVideos ? 1 : 0;
            }
        );
        var dbContext = IDbContext;
        DownloadTaskFileBase file =
            family == PlexMediaType.MusicArtist
                ? await FakeData.AddMusicTask(dbContext, 1)
                : await FakeData.AddOtherVideoTask(dbContext, 1);
        DownloadTaskFileBase sibling =
            family == PlexMediaType.MusicArtist
                ? await FakeData.AddMusicTask(dbContext, 2)
                : await FakeData.AddOtherVideoTask(dbContext, 2);
        var paths = Mock.Container.Resolve<IPathProvider>();
        file.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
        file.DirectoryMeta.DestinationRootPath = (
            await dbContext.GetDestinationFolder(file.PlexLibraryId)
        )!.DirectoryPath;
        file.DataTotal = 4;
        DirectDownloadSnapshot? snapshot = resume
            ? new DirectDownloadSnapshot
            {
                SaveProgress = 50,
                Status = 2,
                TotalFileSize = 4,
                FileName = file.FileName,
                Urls = ["https://stale-plex/file"],
                DownloadingFileExtension = ".reaptemp",
                IsSupportDownloadInRange = true,
                Chunks =
                [
                    new DirectDownloadSnapshotChunk
                    {
                        Id = "resume-chunk",
                        Start = 0,
                        End = 3,
                        Position = 2,
                        MaxTryAgainOnFailure = 3,
                        Timeout = 1000,
                    },
                ],
            }
            : null;
        if (family == PlexMediaType.MusicArtist)
            await dbContext
                .DownloadTaskMusicTrackFiles.Where(x => x.Id == file.Id)
                .ExecuteUpdateAsync(
                    p =>
                        p.SetProperty(x => x.DataTotal, 4)
                            .SetProperty(
                                x => x.DirectoryMeta,
                                file.DirectoryMeta with
                                {
                                    DownloadRootPath = string.Empty,
                                }
                            )
                            .SetProperty(x => x.DirectDownloadSnapshot, snapshot),
                    CancellationToken
                );
        else
            await dbContext
                .DownloadTaskOtherVideoFiles.Where(x => x.Id == file.Id)
                .ExecuteUpdateAsync(
                    p =>
                        p.SetProperty(x => x.DataTotal, 4)
                            .SetProperty(
                                x => x.DirectoryMeta,
                                file.DirectoryMeta with
                                {
                                    DownloadRootPath = string.Empty,
                                }
                            )
                            .SetProperty(x => x.DirectDownloadSnapshot, snapshot),
                    CancellationToken
                );
        var key = file.ToKey();
        DownloadTaskKey[] ancestorKeys = file switch
        {
            DownloadTaskMusicTrackFile music =>
            [
                music.Parent!.ToKey(),
                music.Parent.Parent!.ToKey(),
                music.Parent.Parent.Parent!.ToKey(),
            ],
            DownloadTaskOtherVideoFile video => [video.Parent!.ToKey()],
            _ => throw new InvalidOperationException(),
        };
        (
            await dbContext.GetDownloadTaskFileAsync(key, CancellationToken)
        )!.DirectoryMeta.DownloadRootPath.ShouldBeEmpty();
        (await dbContext.GetDownloadTaskFileAsync(sibling.ToKey(), CancellationToken))!.DownloadStatus.ShouldBe(
            DownloadStatus.Queued
        );
        using var cancellation = new CancellationTokenSource();
        var token = cancelled ? cancellation.Token : CancellationToken.None;
        var machineId = await dbContext.GetPlexServerMachineIdentifierById(file.PlexServerId);
        SetupFileSystem(fs => fs.AddDirectory(file.DownloadDirectory));
        SetupDependencies(b =>
        {
            b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>().SingleInstance();
            b.Register(c => c.Resolve<IFileSystem>().FileInfo).As<IFileInfoFactory>();
        });
        var fs = Mock.Container.Resolve<IFileSystem>();
        var downloadUrl = $"https://plex/{file.FileName}";
        var package = new DownloadPackage
        {
            TotalFileSize = 4,
            FileName = file.FileName,
            Urls = [downloadUrl],
        };
        var transport = new Mock<IDownloadService>();
        transport
            .Setup(x => x.CancelTaskAsync())
            .Returns(Task.CompletedTask)
            .Verifiable(cancelled ? Times.Once() : Times.Never());
        transport
            .Setup(x => x.DownloadFileTaskAsync(downloadUrl, file.DownloadFilePath.RemoveReapTempSuffix(), token))
            .Returns<string, string, CancellationToken>(
                (_, _, _) =>
                {
                    if (cancelled)
                    {
                        cancellation.Cancel();
                        return Task.FromCanceled(token);
                    }
                    fs.Directory.CreateDirectory(file.DownloadDirectory);
                    fs.File.WriteAllBytes(file.DownloadFilePath, incomplete ? [1, 2] : [1, 2, 3, 4]);
                    transport.Raise(
                        x => x.DownloadFileCompleted += null,
                        transport.Object,
                        new AsyncCompletedEventArgs(null, false, package)
                    );
                    return Task.CompletedTask;
                }
            )
            .Verifiable(resume ? Times.Never() : Times.Once());
        transport
            .Setup(x =>
                x.DownloadFileTaskAsync(
                    It.Is<DownloadPackage>(p =>
                        p.FileName == file.FileName
                        && p.TotalFileSize == 4
                        && p.SaveProgress == 50
                        && p.Urls.SequenceEqual(new[] { downloadUrl })
                        && p.Chunks.Length == 1
                        && p.Chunks[0].Id == "resume-chunk"
                        && p.Chunks[0].Position == 2
                    ),
                    token
                )
            )
            .Returns<DownloadPackage, CancellationToken>(
                (resumedPackage, _) =>
                {
                    fs.Directory.CreateDirectory(file.DownloadDirectory);
                    fs.File.WriteAllBytes(file.DownloadFilePath, [1, 2, 3, 4]);
                    transport.Raise(
                        x => x.DownloadFileCompleted += null,
                        transport.Object,
                        new AsyncCompletedEventArgs(null, false, resumedPackage)
                    );
                    return Task.FromResult<Stream>(Stream.Null);
                }
            )
            .Verifiable(resume ? Times.Once() : Times.Never());
        Mock.Mock<IDownloadManagerSettings>().SetupGet(x => x.DownloadSegments).Returns(1);
        Mock.Mock<IServerSettingsModule>()
            .Setup(x => x.GetDownloadSpeedLimitObservable(machineId))
            .Returns(Observable.Return(0))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<DeterminePlexDownloadClientCommand>(c =>
                        c.DownloadTaskKey == key
                        && c.PlexServerId == file.PlexServerId
                        && c.MetaDataPath == $"/library/metadata/{file.PlexApiRatingKey}"
                    ),
                    token
                )
            )
            .Returns<DeterminePlexDownloadClientCommand, CancellationToken>(
                (c, ct) => Mock.Create<DeterminePlexDownloadClientCommandHandler>().ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetDirectDownloadUrlCommand>(c =>
                        c.PlexServerId == file.PlexServerId && c.FileLocationUrl == file.FileLocationUrl
                    ),
                    token
                )
            )
            .ReturnsAsync(Result.Ok(downloadUrl))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<EnsureDownloadDirectoryCommand>(c =>
                        c.Directory == file.DownloadDirectory && c.FileSize == 4
                    ),
                    token
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(It.Is<SendNotificationResult>(n => n.Result.IsFailed), CancellationToken.None))
            .Returns(Task.CompletedTask)
            .Verifiable(incomplete ? Times.Once() : Times.Never());
        var client = Mock.Create<DirectPlexDownloadClient>(
            new NamedParameter(
                "downloadServiceFactory",
                (Func<DownloadConfiguration, IDownloadService>)(_ => transport.Object)
            )
        );
        var clients = new Mock<IIndex<PlexDownloadClientType, IPlexDownloadClient>>(MockBehavior.Strict);
        clients.Setup(x => x[PlexDownloadClientType.Direct]).Returns(client).Verifiable(Times.Once());
        var download = Mock.Create<DownloadJob>(new NamedParameter("plexDownloadClientFactory", clients.Object));
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.MergedJobDataMap).Returns(new DownloadJobPayload(key).ToJobDataMap());
        context.SetupGet(x => x.CancellationToken).Returns(token);
        context.SetupProperty(x => x.Result);

        // Act
        await download.Execute(context.Object);

        // Assert
        if (incomplete || cancelled)
            context
                .Object.Result.ShouldBeOfType<BackgroundJobResult>()
                .Status.ShouldBe(cancelled ? JobStatus.Cancelled : JobStatus.Failed);
        else
            context.Object.Result.ShouldBeNull();
        var persisted = (await dbContext.GetDownloadTaskFileAsync(key, CancellationToken))!;
        persisted.Id.ShouldBe(file.Id);
        persisted.ToParentKey()!.ShouldBe(file.ToParentKey());
        persisted.DirectoryMeta.ShouldBe(file.DirectoryMeta);
        var expectedStatus =
            cancelled ? DownloadStatus.Paused
            : incomplete ? DownloadStatus.DownloadClientError
            : DownloadStatus.DownloadFinished;
        persisted.DownloadStatus.ShouldBe(expectedStatus);
        foreach (var ancestorKey in ancestorKeys)
            (await dbContext.GetDownloadTaskStatusAsync(ancestorKey, CancellationToken)).ShouldBe(expectedStatus);
        var retained = (await dbContext.GetDownloadTaskFileAsync(sibling.ToKey(), CancellationToken))!;
        retained.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        retained.DataReceived.ShouldBe(0);
        retained.DirectDownloadSnapshot.ShouldBeNull();
        if (cancelled)
            fs.File.Exists(file.DownloadFilePath).ShouldBeFalse();
        else
            fs.File.ReadAllBytes(file.DownloadFilePath).ShouldBe(incomplete ? new byte[] { 1, 2 } : [1, 2, 3, 4]);
        fs.File.Exists(file.DestinationFilePath).ShouldBeFalse();
        transport.Verify();
        clients.Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IServerSettingsModule>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GetDashTranscodeDecisionCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetTranscodeUrlCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(PlexMediaType.MusicArtist, DownloadStatus.Paused, false)]
    [Arguments(PlexMediaType.MusicArtist, DownloadStatus.Stopped, false)]
    [Arguments(PlexMediaType.MusicArtist, DownloadStatus.Completed, false)]
    [Arguments(PlexMediaType.MusicArtist, DownloadStatus.Downloading, false)]
    [Arguments(PlexMediaType.MusicArtist, DownloadStatus.Queued, true)]
    [Arguments(PlexMediaType.OtherVideos, DownloadStatus.Paused, false)]
    [Arguments(PlexMediaType.OtherVideos, DownloadStatus.Stopped, false)]
    [Arguments(PlexMediaType.OtherVideos, DownloadStatus.Completed, false)]
    [Arguments(PlexMediaType.OtherVideos, DownloadStatus.Downloading, false)]
    [Arguments(PlexMediaType.OtherVideos, DownloadStatus.Queued, true)]
    public async Task ShouldNotStartNewFamilyClient_WhenExecutionIsNotAuthorized(
        PlexMediaType family,
        DownloadStatus initialStatus,
        bool pauseDuringResolution
    )
    {
        // Arrange
        await SetupDatabase(
            88082,
            c =>
            {
                c.PlexMusicLibraryCount = family == PlexMediaType.MusicArtist ? 1 : 0;
                c.PlexOtherVideoLibraryCount = family == PlexMediaType.OtherVideos ? 1 : 0;
            }
        );
        var dbContext = IDbContext;
        DownloadTaskFileBase file =
            family == PlexMediaType.MusicArtist
                ? await FakeData.AddMusicTask(dbContext, 1)
                : await FakeData.AddOtherVideoTask(dbContext, 1);
        DownloadTaskFileBase sibling =
            family == PlexMediaType.MusicArtist
                ? await FakeData.AddMusicTask(dbContext, 2)
                : await FakeData.AddOtherVideoTask(dbContext, 2);
        var key = file.ToKey();
        await dbContext.SetDownloadStatus(key, initialStatus);
        (await dbContext.GetDownloadTaskStatusAsync(key, CancellationToken)).ShouldBe(initialStatus);
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.MergedJobDataMap).Returns(new DownloadJobPayload(key).ToJobDataMap());
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        context.SetupProperty(x => x.Result);
        var resolutionStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolutionReleased = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (pauseDuringResolution)
            Mock.Mock<ICommandExecutor>()
                .Setup(x =>
                    x.Send(
                        It.Is<DeterminePlexDownloadClientCommand>(c => c.DownloadTaskKey == key),
                        CancellationToken.None
                    )
                )
                .Returns(async () =>
                {
                    resolutionStarted.TrySetResult(true);
                    await resolutionReleased.Task;
                    return Result.Ok(PlexDownloadClientType.Direct);
                })
                .Verifiable(Times.Once());
        var clients = new Mock<IIndex<PlexDownloadClientType, IPlexDownloadClient>>(MockBehavior.Strict);
        var download = Mock.Create<DownloadJob>(new NamedParameter("plexDownloadClientFactory", clients.Object));

        // Act
        var execution = download.Execute(context.Object);
        if (pauseDuringResolution)
        {
            try
            {
                await resolutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
                await dbContext.SetDownloadStatus(key, DownloadStatus.Paused);
            }
            finally
            {
                resolutionReleased.TrySetResult(true);
            }
        }
        await execution;

        // Assert
        context.Object.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Failed);
        (await dbContext.GetDownloadTaskStatusAsync(key, CancellationToken)).ShouldBe(
            pauseDuringResolution ? DownloadStatus.Paused : initialStatus
        );
        (await dbContext.GetDownloadTaskStatusAsync(sibling.ToKey(), CancellationToken)).ShouldBe(
            DownloadStatus.Queued
        );
        (await dbContext.GetDownloadTaskFileAsync(sibling.ToKey(), CancellationToken))!.DirectoryMeta.ShouldBe(
            sibling.DirectoryMeta
        );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeterminePlexDownloadClientCommand>(c => c.DownloadTaskKey == key),
                        CancellationToken.None
                    ),
                pauseDuringResolution ? Times.Once() : Times.Never()
            );
        clients.Verify(x => x[It.IsAny<PlexDownloadClientType>()], Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<SendNotificationResult>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }
}
