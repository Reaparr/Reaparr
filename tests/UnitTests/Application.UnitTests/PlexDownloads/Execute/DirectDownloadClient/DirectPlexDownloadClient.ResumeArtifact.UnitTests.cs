using System.ComponentModel;
using Downloader;
using DownloadStatus = Reaparr.Domain.DownloadStatus;

namespace Reaparr.Application.UnitTests;

public class DirectPlexDownloadClientResumeArtifactUnitTests : BaseUnitTest<DirectPlexDownloadClient>
{
    [Test]
    [Arguments(-1)]
    [Arguments(19)]
    public async Task ShouldReturnStorageErrorAndPreserveSnapshot_WhenPartialFileCannotContainSavedRanges(
        int partialLength
    )
    {
        // Arrange
        var (task, snapshot, fileSystem) = await CreateScenario(partialLength);
        var key = task.ToKey();
        Mock.Mock<IDownloadManagerSettings>().Setup(x => x.DownloadSegments).Returns(1);
        var partialPath = snapshot.FileName + snapshot.DownloadingFileExtension;
        var downloader = new Mock<IDownloadService>(MockBehavior.Strict);
        downloader.As<IAsyncDisposable>().Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GetDirectDownloadUrlCommand>(c => c.PlexServerId == key.PlexServerId), CancellationToken)
            )
            .ReturnsAsync(Result.Ok("http://plex/resume.mkv"))
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadSpeedLimitProvider>()
            .Setup(x => x.GetEffectiveDownloadSpeedLimitObservable(It.IsAny<string>()))
            .Returns(Observable.Return(0L))
            .Verifiable(Times.Never());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x => x.OnStatusChangedAsync(key, DownloadStatus.Downloading, CancellationToken.None))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    key,
                    DownloadStatus.StorageError,
                    It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1),
                    CancellationToken.None
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        await using var sut = Mock.Create<DirectPlexDownloadClient>(
            new NamedParameter(
                "downloadServiceFactory",
                (Func<DownloadConfiguration, IDownloadService>)(_ => downloader.Object)
            )
        );

        // Act
        var result = await sut.Start(key, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.HasStorageError().ShouldBeTrue();
        var persisted = await IDbContext.GetDownloadTaskFileAsync(key, CancellationToken);
        persisted.ShouldNotBeNull();
        persisted.DirectDownloadSnapshot.ShouldNotBeNull();
        persisted.DirectDownloadSnapshot.FileName.ShouldBe(snapshot.FileName);
        persisted
            .DirectDownloadSnapshot.Chunks.Select(x => (x.Start, x.End, x.Position))
            .ShouldBe(snapshot.Chunks.Select(x => (x.Start, x.End, x.Position)));
        fileSystem.File.Exists(partialPath).ShouldBe(partialLength >= 0);
        if (partialLength >= 0)
            fileSystem.File.ReadAllBytes(partialPath).ShouldBe(Enumerable.Repeat((byte)7, partialLength).ToArray());
        fileSystem.File.Exists(snapshot.FileName).ShouldBeFalse();
        downloader.Verify(
            x => x.DownloadFileTaskAsync(It.IsAny<DownloadPackage>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
        downloader.Verify(
            x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x => x.OnStatusChangedAsync(key, DownloadStatus.DownloadFinished, It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<EnsureDownloadDirectoryCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IDownloadSpeedLimitProvider>().Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
    }

    [Test]
    [Arguments(20, 21, 8, 4)]
    [Arguments(20, 20, 8, 4)]
    [Arguments(32, 0, 16, 16)]
    public async Task ShouldFinishResumedDownload_WhenValidatedProgressFitsAvailableSpace(
        int partialLength,
        long availableSpace,
        long firstPosition,
        long secondPosition
    )
    {
        // Arrange
        var (task, snapshot, fileSystem) = await CreateScenario(
            partialLength,
            availableSpace,
            firstPosition,
            secondPosition
        );
        var remainingBytes = 32 - firstPosition - secondPosition;
        var directoryHandler = new EnsureDownloadDirectoryCommandHandler(fileSystem.Path, fileSystem.Directory);
        Mock.Mock<IDownloadManagerSettings>().Setup(x => x.DownloadSegments).Returns(1);
        var key = task.ToKey();
        var downloader = new Mock<IDownloadService>(MockBehavior.Strict);
        downloader.As<IAsyncDisposable>().Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var output = Enumerable.Range(1, 32).Select(x => (byte)x).ToArray();
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GetDirectDownloadUrlCommand>(c => c.PlexServerId == key.PlexServerId), CancellationToken)
            )
            .ReturnsAsync(Result.Ok("http://plex/resume.mkv"))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<EnsureDownloadDirectoryCommand>(c =>
                        c.Directory == task.DownloadDirectory && c.FileSize == remainingBytes
                    ),
                    CancellationToken
                )
            )
            .Returns(
                (EnsureDownloadDirectoryCommand command, CancellationToken ct) =>
                    directoryHandler.ExecuteAsync(command, ct)
            )
            .Verifiable(remainingBytes == 0 ? Times.Never() : Times.Once());
        Mock.Mock<IDownloadSpeedLimitProvider>()
            .Setup(x => x.GetEffectiveDownloadSpeedLimitObservable(It.IsAny<string>()))
            .Returns(Observable.Return(0L))
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x => x.OnStatusChangedAsync(key, DownloadStatus.Downloading, CancellationToken.None))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x => x.OnStatusChangedAsync(key, DownloadStatus.DownloadFinished, CancellationToken.None))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    key,
                    It.Is<DownloadTaskProgress>(p => p.DataTotal == 32 && p.DataReceived == 32 && p.Percentage == 100),
                    It.IsAny<DirectDownloadSnapshot>()
                )
            )
            .Verifiable(Times.Once());
        downloader
            .Setup(x =>
                x.DownloadFileTaskAsync(
                    It.Is<DownloadPackage>(p =>
                        p.Chunks[0].Position == firstPosition
                        && p.Chunks[1].Position == secondPosition
                        && p.Urls.Single() == "http://plex/resume.mkv"
                    ),
                    CancellationToken
                )
            )
            .Returns<DownloadPackage, CancellationToken>(
                (package, _) =>
                {
                    fileSystem.File.WriteAllBytes(task.DownloadFilePath, output);
                    downloader.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloader.Object,
                        new AsyncCompletedEventArgs(null, false, package)
                    );
                    return Task.FromResult(Stream.Null);
                }
            )
            .Verifiable(Times.Once());
        await using var sut = Mock.Create<DirectPlexDownloadClient>(
            new NamedParameter(
                "downloadServiceFactory",
                (Func<DownloadConfiguration, IDownloadService>)(_ => downloader.Object)
            )
        );

        // Act
        var result = await sut.Start(key, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        fileSystem.File.ReadAllBytes(task.DownloadFilePath).ShouldBe(output);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        key,
                        DownloadStatus.StorageError,
                        It.IsAny<Result>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        downloader.Verify(
            x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
        downloader.Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadSpeedLimitProvider>().Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
    }

    [Test]
    [Arguments(ResumeSpaceScenario.Partial, 19, 20)]
    [Arguments(ResumeSpaceScenario.Fresh, 31, 32)]
    [Arguments(ResumeSpaceScenario.NoChunks, 31, 32)]
    [Arguments(ResumeSpaceScenario.NoRanges, 31, 32)]
    [Arguments(ResumeSpaceScenario.Overlapping, 31, 32)]
    [Arguments(ResumeSpaceScenario.SizeMismatch, 31, 32)]
    public async Task ShouldPreservePartialFileWithoutStarting_WhenFreeSpaceIsLessThanRequiredBytes(
        ResumeSpaceScenario scenario,
        long availableSpace,
        long expectedRequiredBytes
    )
    {
        // Arrange
        var (task, snapshot, fileSystem) = await CreateScenario(32, availableSpace);
        var key = task.ToKey();
        var dbContext = IDbContext;
        var savedSnapshot = CreateSpaceSnapshot(snapshot, scenario);
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == task.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.DirectDownloadSnapshot, savedSnapshot), CancellationToken);
        var before = fileSystem.File.ReadAllBytes(snapshot.FileName + snapshot.DownloadingFileExtension);
        var directoryHandler = new EnsureDownloadDirectoryCommandHandler(fileSystem.Path, fileSystem.Directory);
        var downloader = new Mock<IDownloadService>(MockBehavior.Strict);
        downloader.As<IAsyncDisposable>().Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        Mock.Mock<IDownloadManagerSettings>().Setup(x => x.DownloadSegments).Returns(1);
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GetDirectDownloadUrlCommand>(c => c.PlexServerId == task.PlexServerId), CancellationToken)
            )
            .ReturnsAsync(Result.Ok("http://plex/resume.mkv"))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<EnsureDownloadDirectoryCommand>(c =>
                        c.Directory == task.DownloadDirectory && c.FileSize == expectedRequiredBytes
                    ),
                    CancellationToken
                )
            )
            .Returns(
                (EnsureDownloadDirectoryCommand command, CancellationToken ct) =>
                    directoryHandler.ExecuteAsync(command, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    key,
                    DownloadStatus.StorageError,
                    It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1),
                    CancellationToken.None
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        await using var sut = Mock.Create<DirectPlexDownloadClient>(
            new NamedParameter(
                "downloadServiceFactory",
                (Func<DownloadConfiguration, IDownloadService>)(_ => downloader.Object)
            )
        );

        // Act
        var result = await sut.Start(key, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().Message.ShouldContain("not enough space");
        fileSystem.File.ReadAllBytes(snapshot.FileName + snapshot.DownloadingFileExtension).ShouldBe(before);
        fileSystem.File.Exists(snapshot.FileName).ShouldBeFalse();
        var persisted = await dbContext.GetDownloadTaskFileAsync(key, CancellationToken);
        persisted.ShouldNotBeNull();
        System
            .Text.Json.JsonSerializer.Serialize(persisted.DirectDownloadSnapshot)
            .ShouldBe(System.Text.Json.JsonSerializer.Serialize(savedSnapshot));
        downloader.Verify(
            x => x.DownloadFileTaskAsync(It.IsAny<DownloadPackage>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
        downloader.Verify(
            x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x => x.OnStatusChangedAsync(key, DownloadStatus.Downloading, It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
    }

    public enum ResumeSpaceScenario
    {
        Partial,
        Fresh,
        NoChunks,
        NoRanges,
        Overlapping,
        SizeMismatch,
    }

    private static DirectDownloadSnapshot? CreateSpaceSnapshot(
        DirectDownloadSnapshot snapshot,
        ResumeSpaceScenario scenario
    ) =>
        scenario switch
        {
            ResumeSpaceScenario.Fresh => null,
            ResumeSpaceScenario.NoChunks => snapshot with { Chunks = [] },
            ResumeSpaceScenario.NoRanges => snapshot with { IsSupportDownloadInRange = false },
            ResumeSpaceScenario.Overlapping => snapshot with { Chunks = [snapshot.Chunks[0], snapshot.Chunks[0]] },
            ResumeSpaceScenario.SizeMismatch => snapshot with { TotalFileSize = 64 },
            _ => snapshot,
        };

    private async Task<(
        DownloadTaskMovieFile Task,
        DirectDownloadSnapshot Snapshot,
        IFileSystem FileSystem
    )> CreateScenario(int partialLength, long availableSpace = 1024, long firstPosition = 8, long secondPosition = 4)
    {
        await SetupDatabase(
            51001,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );
        SetupFileSystem(fs =>
            fs.AddDrive(
                "/",
                new MockDriveData
                {
                    IsReady = true,
                    DriveType = DriveType.Fixed,
                    AvailableFreeSpace = availableSpace,
                }
            )
        );
        SetupDependencies(builder =>
            builder.Register(ctx => ctx.Resolve<IFileSystem>().FileInfo).As<IFileInfoFactory>()
        );
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var dbContext = IDbContext;
        var task = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        var snapshot = new DirectDownloadSnapshot
        {
            SaveProgress = (firstPosition + secondPosition) * 100.0 / 32,
            Status = (int)Downloader.DownloadStatus.Stopped,
            Urls = ["http://old-plex/resume.mkv"],
            TotalFileSize = 32,
            FileName = Path.Combine(task.DownloadDirectory, task.FileName),
            DownloadingFileExtension = FilePathExtensions.TempDownloadFileSuffix,
            IsSupportDownloadInRange = true,
            Chunks =
            [
                new()
                {
                    Id = "first",
                    Start = 0,
                    End = 15,
                    Position = firstPosition,
                    MaxTryAgainOnFailure = 3,
                    Timeout = 1000,
                },
                new()
                {
                    Id = "second",
                    Start = 16,
                    End = 31,
                    Position = secondPosition,
                    MaxTryAgainOnFailure = 3,
                    Timeout = 1000,
                },
            ],
        };
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == task.Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(p => p.DirectDownloadSnapshot, snapshot).SetProperty(p => p.DataTotal, 32),
                CancellationToken
            );
        task = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        fileSystem.Directory.CreateDirectory(task.DownloadDirectory);
        if (partialLength >= 0)
            fileSystem.File.WriteAllBytes(
                snapshot.FileName + snapshot.DownloadingFileExtension,
                Enumerable.Repeat((byte)7, partialLength).ToArray()
            );
        return (task, snapshot, fileSystem);
    }
}
