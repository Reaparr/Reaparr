using System.ComponentModel;
using Downloader;
using DomainDownloadStatus = Reaparr.Domain.DownloadStatus;

namespace Reaparr.Application.UnitTests;

public class DirectPlexDownloadClientStartUnitTests : BaseUnitTest<DirectPlexDownloadClient>
{
    public enum CompletionFailureKind
    {
        Timeout,
        NetworkTimeoutMessage,
        TimedOutMessage,
        Storage,
        UnauthorizedStorage,
        HttpRequest,
        Argument,
        NotSupported,
        Format,
        Unknown,
    }

    public enum DirectFailureKind
    {
        HttpRequest,
        Storage,
        Unknown,
    }

    // -------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------

    private static DownloadPackage MakeDownloadPackage(long totalBytes = 10 * 1024) =>
        new()
        {
            TotalFileSize = totalBytes,
            FileName = "test.mp4",
            Urls = ["http://plex/test.mp4"],
        };

    /// <summary>
    /// Creates a mock IDownloadService whose DownloadFileTaskAsync(url, path, ct) raises a single
    /// DownloadProgressChanged event and then a successful DownloadFileCompleted event.
    /// The Package property and UserState on the completed event are both populated so that
    /// the production DownloadFileCompleted handler does not throw a NullReferenceException.
    /// </summary>
    private static void SetupVerifiedFile(
        Mock<IFile> fileMock,
        Mock<IFileInfoFactory> fileInfoFactoryMock,
        string path,
        long size
    )
    {
        var fileInfoMock = new Mock<IFileInfo>();
        fileInfoMock.Setup(x => x.Length).Returns(size);

        fileMock.Setup(x => x.Exists(path)).Returns(true);
        fileMock
            .Setup(x => x.ReadAllBytes(path))
            .Throws(new InvalidOperationException("File length verification must not read the file contents."));
        fileInfoFactoryMock.Setup(x => x.New(It.IsAny<string>())).Returns(fileInfoMock.Object);
    }

    private Mock<IDownloadService> BuildSuccessDownloadServiceMock(
        long totalBytes = 10 * 1024,
        Mock<IFile>? fileMock = null,
        Mock<IFileInfoFactory>? fileInfoFactoryMock = null
    )
    {
        var package = MakeDownloadPackage(totalBytes);
        fileMock ??= new Mock<IFile>();
        fileInfoFactoryMock ??= new Mock<IFileInfoFactory>();
        var mock = new Mock<IDownloadService>();
        mock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        mock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        mock.Setup(x => x.Package).Returns(package);

        mock.Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                (_, targetPath, _) =>
                {
                    SetupVerifiedFile(fileMock, fileInfoFactoryMock, targetPath, totalBytes);

                    mock.Raise(
                        x => x.DownloadProgressChanged += null,
                        mock.Object,
                        new DownloadProgressChangedEventArgs("Main")
                        {
                            TotalBytesToReceive = totalBytes,
                            ReceivedBytesSize = totalBytes,
                            BytesPerSecondSpeed = 1024,
                        }
                    );
                    mock.Raise(
                        x => x.DownloadFileCompleted += null,
                        mock.Object,
                        // UserState must be a DownloadPackage — the production handler casts it
                        new AsyncCompletedEventArgs(null, false, package)
                    );
                    return Task.CompletedTask;
                }
            );

        return mock;
    }

    private DirectPlexDownloadClient CreateSut(
        Mock<IDownloadService> downloadServiceMock,
        Mock<IFile>? fileMock = null,
        Mock<IFileInfoFactory>? fileInfoFactoryMock = null
    )
    {
        fileMock ??= new Mock<IFile>();
        fileInfoFactoryMock ??= Mock.Mock<IFileInfoFactory>();
        return Mock.Create<DirectPlexDownloadClient>(
            new TypedParameter(typeof(IFile), fileMock.Object),
            new TypedParameter(typeof(IFileInfoFactory), fileInfoFactoryMock.Object),
            new NamedParameter(
                "downloadServiceFactory",
                (Func<DownloadConfiguration, IDownloadService>)(_ => downloadServiceMock.Object)
            )
        );
    }

    private void SetupCommandExecutor()
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<ICommand<Result>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok("http://plex/file.mkv"));
    }

    private void SetupSpeedLimitMocks(string serverMachineIdentifier, int speedLimit = 1000)
    {
        // IDownloadManagerSettings.DownloadSegments is read in the SUT constructor
        Mock.Mock<IDownloadManagerSettings>().Setup(x => x.DownloadSegments).Returns(1);

        Mock.Mock<IDownloadSpeedLimitProvider>()
            .Setup(x => x.GetEffectiveDownloadSpeedLimitObservable(serverMachineIdentifier))
            .Returns(Observable.Return(checked((long)speedLimit * 1024)));
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Test]
    public async Task ShouldReturnSuccessResult_WhenSetupAndStartedSuccessfully()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<Result?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            82345,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);

        SetupCommandExecutor();

        // Act
        var fileMock = new Mock<IFile>();
        var fileInfoFactoryMock = new Mock<IFileInfoFactory>();
        var sut = CreateSut(
            BuildSuccessDownloadServiceMock(downloadTask.DataTotal, fileMock, fileInfoFactoryMock),
            fileMock,
            fileInfoFactoryMock
        );
        var startResult = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        startResult.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DomainDownloadStatus.DownloadFinished,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<EnsureDownloadDirectoryCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldEnsureDownloadDirectoryExists_WhenStartingDownload()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<Result>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            82346,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);

        EnsureDownloadDirectoryCommand? ensureDirectoryCommand = null;

        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<ICommand<Result>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock.Mock<ICommandExecutor>()
            .Setup(m =>
                m.Send(
                    It.Is<ICommand<Result>>(cmd => cmd is EnsureDownloadDirectoryCommand),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<ICommand<Result>, CancellationToken>(
                (command, _) => ensureDirectoryCommand = (EnsureDownloadDirectoryCommand)command
            )
            .ReturnsAsync(Result.Ok());

        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok("http://plex/file.mkv"));

        // Act
        var fileMock = new Mock<IFile>();
        var fileInfoFactoryMock = new Mock<IFileInfoFactory>();
        var sut = CreateSut(
            BuildSuccessDownloadServiceMock(downloadTask.DataTotal, fileMock, fileInfoFactoryMock),
            fileMock,
            fileInfoFactoryMock
        );
        var startResult = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        startResult.IsSuccess.ShouldBeTrue();
        ensureDirectoryCommand.ShouldNotBeNull();
        ensureDirectoryCommand!.Directory.ShouldBe(downloadTask.DownloadDirectory);
        ensureDirectoryCommand.FileSize.ShouldBe(downloadTask.DataTotal);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<EnsureDownloadDirectoryCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldStartDownloaderWithFinalPath_WhenTempExtensionIsConfigured()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<Result?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            82347,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);
        SetupCommandExecutor();

        var fileMock = new Mock<IFile>();
        var package = MakeDownloadPackage(downloadTask.DataTotal);
        var downloadServiceMock = new Mock<IDownloadService>();
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Package).Returns(package);

        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                (_, targetPath, _) =>
                {
                    SetupVerifiedFile(fileMock, Mock.Mock<IFileInfoFactory>(), targetPath, downloadTask.DataTotal);

                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        new AsyncCompletedEventArgs(null, false, package)
                    );
                    return Task.CompletedTask;
                }
            );

        var sut = CreateSut(downloadServiceMock, fileMock);

        // Act
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var expectedFinalPath = Path.Combine(downloadTask.DownloadDirectory, downloadTask.FileName);
        downloadServiceMock.Verify(
            x => x.DownloadFileTaskAsync(It.IsAny<string>(), expectedFinalPath, It.IsAny<CancellationToken>()),
            Times.Once
        );
        downloadServiceMock.Verify(
            x =>
                x.DownloadFileTaskAsync(
                    It.IsAny<string>(),
                    downloadTask.DownloadFilePath,
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public void ShouldDisableDownloaderDiskSizePrecheck_WhenCreatingDownloadService()
    {
        // Arrange
        DownloadConfiguration? capturedConfig = null;
        var downloadServiceMock = new Mock<IDownloadService>();

        Mock.Mock<IDownloadManagerSettings>().Setup(x => x.DownloadSegments).Returns(1);

        // Act
        _ = Mock.Create<DirectPlexDownloadClient>(
            new NamedParameter(
                "downloadServiceFactory",
                (Func<DownloadConfiguration, IDownloadService>)(
                    config =>
                    {
                        capturedConfig = config;
                        return downloadServiceMock.Object;
                    }
                )
            )
        );

        // Assert
        capturedConfig.ShouldNotBeNull();
        capturedConfig!.CheckDiskSizeBeforeDownload.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldReturnEntityNotFoundError_WhenDownloadTaskKeyDoesNotExist()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            11111,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var nonExistentKey = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.NewGuid(),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };

        // IDownloadManagerSettings.DownloadSegments is read in the SUT constructor
        Mock.Mock<IDownloadManagerSettings>().Setup(x => x.DownloadSegments).Returns(1);

        SetupCommandExecutor();

        // Act
        var sut = CreateSut(BuildSuccessDownloadServiceMock());
        var result = await sut.Start(nonExistentKey, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Message.Contains(nameof(DownloadTaskGeneric)));
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenGetDownloadUrlFailsDueToNoServerConnections()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            22222,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);

        // Remove all server connections so GetDownloadUrl cannot choose a connection
        var connections = await dbContext.PlexServerConnections.ToListAsync(CancellationToken);
        dbContext.PlexServerConnections.RemoveRange(connections);
        await dbContext.SaveChangesAsync(CancellationToken);

        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);
        SetupCommandExecutor();
        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<string>("No available Plex server connection"));

        // Act
        var fileMock = new Mock<IFile>();
        var fileInfoFactoryMock = new Mock<IFileInfoFactory>();
        var sut = CreateSut(
            BuildSuccessDownloadServiceMock(downloadTask.DataTotal, fileMock, fileInfoFactoryMock),
            fileMock,
            fileInfoFactoryMock
        );
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldReturnFailedResultAndPersistStorageError_WhenCreateFileStreamFails()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<Result?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            33333,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);

        // All Result commands succeed by default
        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<ICommand<Result>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok("http://plex/file.mkv"));

        // Make EnsureDownloadDirectoryCommand fail (registered last to override the general ICommand<Result> setup)
        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<EnsureDownloadDirectoryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Disk full"));

        // Act
        var fileMock = new Mock<IFile>();
        var fileInfoFactoryMock = new Mock<IFileInfoFactory>();
        var sut = CreateSut(
            BuildSuccessDownloadServiceMock(downloadTask.DataTotal, fileMock, fileInfoFactoryMock),
            fileMock,
            fileInfoFactoryMock
        );
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DomainDownloadStatus.StorageError,
                        It.IsAny<Result>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldTransitionThroughDownloadingStatus_WhenDownloadStartedEventFires()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            44444,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);

        SetupCommandExecutor();

        var fileMock = new Mock<IFile>();
        var dlPackage = MakeDownloadPackage(downloadTask.DataTotal);
        var downloadServiceMock = new Mock<IDownloadService>();
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Package).Returns(dlPackage);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                async (_, targetPath, cancellationToken) =>
                {
                    SetupVerifiedFile(fileMock, Mock.Mock<IFileInfoFactory>(), targetPath, downloadTask.DataTotal);

                    // DownloadStarted fires before DownloadProgressChanged in a real download
                    downloadServiceMock.Raise(
                        x => x.DownloadStarted += null,
                        downloadServiceMock.Object,
                        new DownloadStartedEventArgs("test.mkv", 10 * 1024)
                    );

                    // Allow async subscription handlers a moment to process
                    await Task.Delay(100, cancellationToken);
                    downloadServiceMock.Raise(
                        x => x.DownloadProgressChanged += null,
                        downloadServiceMock.Object,
                        new DownloadProgressChangedEventArgs("Main")
                        {
                            TotalBytesToReceive = 10 * 1024,
                            ReceivedBytesSize = 10 * 1024,
                            BytesPerSecondSpeed = 1024,
                        }
                    );
                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        new AsyncCompletedEventArgs(null, false, dlPackage)
                    );
                }
            );

        // Act
        var sut = CreateSut(downloadServiceMock, fileMock);
        var startResult = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        startResult.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DomainDownloadStatus.DownloadFinished,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldSetDownloadingStatus_BeforeInvokingDownloadFileTaskAsync()
    {
        // Arrange
        var downloadingStatusWasSetBeforeDownloadStarted = false;

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<DownloadTaskKey, Domain.DownloadStatus, CancellationToken>(
                (_, status, _) =>
                {
                    if (status == DomainDownloadStatus.Downloading)
                        downloadingStatusWasSetBeforeDownloadStarted = true;
                }
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            84337,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);
        SetupCommandExecutor();

        var package = MakeDownloadPackage(downloadTask.DataTotal);
        var fileMock = new Mock<IFile>();
        var downloadServiceMock = new Mock<IDownloadService>();
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Package).Returns(package);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                (_, targetPath, _) =>
                {
                    downloadingStatusWasSetBeforeDownloadStarted.ShouldBeTrue();
                    SetupVerifiedFile(fileMock, Mock.Mock<IFileInfoFactory>(), targetPath, downloadTask.DataTotal);
                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        new AsyncCompletedEventArgs(null, false, package)
                    );
                    return Task.CompletedTask;
                }
            );

        // Act
        var sut = CreateSut(downloadServiceMock, fileMock);
        var startResult = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        startResult.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(key => key == downloadTask.ToKey()),
                        DomainDownloadStatus.Downloading,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldApplySpeedLimitFromObservable_WhenSpeedLimitObservableEmitsValue()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            55555,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        // Emit two speed limit values; the last one (2000 KB/s) must be reflected on the config
        var speedLimits = new[] { 1000, 2000 };

        // IDownloadManagerSettings.DownloadSegments is read in the SUT constructor
        Mock.Mock<IDownloadManagerSettings>().Setup(x => x.DownloadSegments).Returns(1);

        Mock.Mock<IServerSettingsModule>().Setup(x => x.GetDownloadSpeedLimit(serverMachineIdentifier)).Returns(2000);

        Mock.Mock<IServerSettingsModule>()
            .Setup(x => x.GetDownloadSpeedLimitObservable(serverMachineIdentifier))
            .Returns(speedLimits.ToObservable());

        DownloadConfiguration? capturedConfig = null;

        var fileMock = new Mock<IFile>();
        var speedPackage = MakeDownloadPackage(downloadTask.DataTotal);
        var downloadServiceMock = new Mock<IDownloadService>();
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Package).Returns(speedPackage);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                async (_, targetPath, cancellationToken) =>
                {
                    SetupVerifiedFile(fileMock, Mock.Mock<IFileInfoFactory>(), targetPath, downloadTask.DataTotal);

                    await Task.Delay(50, cancellationToken); // allow observable subscriptions to run
                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        new AsyncCompletedEventArgs(null, false, speedPackage)
                    );
                }
            );

        SetupCommandExecutor();

        var sut = Mock.Create<DirectPlexDownloadClient>(
            new TypedParameter(typeof(IFile), fileMock.Object),
            new NamedParameter(
                "downloadServiceFactory",
                (Func<DownloadConfiguration, IDownloadService>)(
                    config =>
                    {
                        capturedConfig = config;
                        return downloadServiceMock.Object;
                    }
                )
            )
        );

        // Act
        await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert — last emitted speed limit (2000 KB/s) is written to the configuration
        capturedConfig.ShouldNotBeNull();
        capturedConfig!.MaximumBytesPerSecond.ShouldBe(2000L * 1024);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    [Arguments(1, true)]
    [Arguments(0, false)]
    public async Task ShouldOnlyCallResumeOverload_WhenDirectDownloadSnapshotContainsChunks(
        int snapshotChunkCount,
        bool shouldResume
    )
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            66666,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);

        // Seed a resume snapshot — simulates a previously paused download
        var snapshot = new DirectDownloadSnapshot
        {
            SaveProgress = 0.5,
            Status = (int)Downloader.DownloadStatus.Running,
            Urls = ["http://old-plex-url/file.mkv"],
            TotalFileSize = downloadTask.DataTotal,
            FileName = downloadTask.FileName,
            DownloadingFileExtension = FilePathExtensions.TempDownloadFileSuffix,
            Chunks = Enumerable
                .Range(0, snapshotChunkCount)
                .Select(_ => new DirectDownloadSnapshotChunk
                {
                    Id = Guid.NewGuid().ToString(),
                    Start = 0,
                    End = downloadTask.DataTotal / 2,
                    Position = downloadTask.DataTotal / 2,
                    MaxTryAgainOnFailure = 3,
                    Timeout = 1000,
                })
                .ToList(),
            IsSupportDownloadInRange = true,
        };

        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == downloadTask.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.DirectDownloadSnapshot, snapshot), CancellationToken);

        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);

        DownloadPackage? capturedPackage = null;
        var fileMock = new Mock<IFile>();
        var resumePackage = MakeDownloadPackage(downloadTask.DataTotal);

        var downloadServiceMock = new Mock<IDownloadService>();
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Package).Returns(resumePackage);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                (_, targetPath, _) =>
                {
                    SetupVerifiedFile(fileMock, Mock.Mock<IFileInfoFactory>(), targetPath, downloadTask.DataTotal);
                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        new AsyncCompletedEventArgs(null, false, resumePackage)
                    );
                    return Task.CompletedTask;
                }
            );
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<DownloadPackage>(), It.IsAny<CancellationToken>()))
            .Returns<DownloadPackage, CancellationToken>(
                (pkg, _) =>
                {
                    capturedPackage = pkg;
                    SetupVerifiedFile(
                        fileMock,
                        Mock.Mock<IFileInfoFactory>(),
                        downloadTask.DownloadFilePath.RemoveReapTempSuffix(),
                        downloadTask.DataTotal
                    );
                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        // UserState must be a DownloadPackage — the production handler casts and dereferences it
                        new AsyncCompletedEventArgs(null, false, resumePackage)
                    );
                    return Task.FromResult(Stream.Null);
                }
            );

        SetupCommandExecutor();

        // Act
        var sut = CreateSut(downloadServiceMock, fileMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // A snapshot without chunks cannot be resumed by Downloader because it calculates zero concurrent tasks.
        downloadServiceMock.Verify(
            x => x.DownloadFileTaskAsync(It.IsAny<DownloadPackage>(), It.IsAny<CancellationToken>()),
            shouldResume ? Times.Once() : Times.Never()
        );
        downloadServiceMock.Verify(
            x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            shouldResume ? Times.Never() : Times.Once()
        );

        if (shouldResume)
        {
            // The stale URL from the snapshot must be replaced with the fresh authenticated URL
            capturedPackage.ShouldNotBeNull();
            capturedPackage!.Urls.ShouldNotContain("http://old-plex-url/file.mkv");
        }
        else
        {
            capturedPackage.ShouldBeNull();
        }

        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldPersistProgressSnapshot_WhenDownloadProgressChanges()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            77777,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);

        var fileMock = new Mock<IFile>();
        var downloadServiceMock = new Mock<IDownloadService>();
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);

        // Package must return a valid object for ToSnapshot() called during progress handling.
        var fakePackage = new DownloadPackage
        {
            TotalFileSize = downloadTask.DataTotal,
            FileName = downloadTask.FileName,
            Urls = ["http://plex/file.mkv"],
        };
        downloadServiceMock.Setup(x => x.Package).Returns(fakePackage);

        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                async (_, targetPath, cancellationToken) =>
                {
                    SetupVerifiedFile(fileMock, Mock.Mock<IFileInfoFactory>(), targetPath, downloadTask.DataTotal);

                    downloadServiceMock.Raise(
                        x => x.DownloadProgressChanged += null,
                        downloadServiceMock.Object,
                        new DownloadProgressChangedEventArgs("Main")
                        {
                            TotalBytesToReceive = downloadTask.DataTotal,
                            ReceivedBytesSize = downloadTask.DataTotal / 2,
                            BytesPerSecondSpeed = 1024,
                        }
                    );

                    // Wait beyond the 500 ms sample window so the Rx handler fires and persists
                    await Task.Delay(700, cancellationToken);
                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        // UserState must be a DownloadPackage — the production handler casts and dereferences it
                        new AsyncCompletedEventArgs(null, false, fakePackage)
                    );
                }
            );

        SetupCommandExecutor();

        // Act
        var sut = CreateSut(downloadServiceMock, fileMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // The DownloadProgressChanged handler should forward a non-null snapshot to the dispatcher.
        // Verifying the mock proves the Rx sample fired and the handler ran.
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnProgressUpdated(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadTaskProgress>(),
                        It.Is<DirectDownloadSnapshot?>(s => s != null)
                    ),
                Times.AtLeastOnce()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldPersistDownloadFinishedStatusInDatabase_WhenDownloadCompletesSuccessfully()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            88888,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);

        SetupCommandExecutor();

        // Act
        var fileMock = new Mock<IFile>();
        var fileInfoFactoryMock = new Mock<IFileInfoFactory>();
        var sut = CreateSut(
            BuildSuccessDownloadServiceMock(downloadTask.DataTotal, fileMock, fileInfoFactoryMock),
            fileMock,
            fileInfoFactoryMock
        );
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DomainDownloadStatus.DownloadFinished,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldWaitForCompletionStatusPersistence_WhenCompletionEventHandlerIsAsync()
    {
        // Arrange
        await SetupDatabase(
            88891,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);
        SetupSpeedLimitMocks(serverMachineIdentifier);
        SetupCommandExecutor();

        var completionHandlerStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var allowCompletionPersistence = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DomainDownloadStatus.Downloading,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DomainDownloadStatus.DownloadFinished,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(async () =>
            {
                completionHandlerStarted.TrySetResult(true);
                await allowCompletionPersistence.Task;
            });
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        var package = MakeDownloadPackage(downloadTask.DataTotal);
        var fileMock = new Mock<IFile>();
        var fileInfoFactoryMock = new Mock<IFileInfoFactory>();
        var downloadServiceMock = new Mock<IDownloadService>();
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Package).Returns(package);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                (_, targetPath, _) =>
                {
                    SetupVerifiedFile(fileMock, fileInfoFactoryMock, targetPath, downloadTask.DataTotal);
                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        new AsyncCompletedEventArgs(null, false, package)
                    );
                    return Task.CompletedTask;
                }
            );

        var sut = CreateSut(downloadServiceMock, fileMock, fileInfoFactoryMock);

        // Act
        var startTask = sut.Start(downloadTask.ToKey(), CancellationToken);
        await completionHandlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        startTask.IsCompleted.ShouldBeFalse();

        allowCompletionPersistence.TrySetResult(true);
        var result = await startTask;
        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldPersistVerifiedFileSize_WhenCompletionEventHasZeroReceivedBytes()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            88889,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);
        SetupCommandExecutor();

        var completionPackage = MakeDownloadPackage(downloadTask.DataTotal);
        var fileMock = new Mock<IFile>();

        var downloadServiceMock = new Mock<IDownloadService>();
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Package).Returns(completionPackage);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                (_, targetPath, _) =>
                {
                    SetupVerifiedFile(fileMock, Mock.Mock<IFileInfoFactory>(), targetPath, downloadTask.DataTotal);

                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        new AsyncCompletedEventArgs(null, false, completionPackage)
                    );
                    return Task.CompletedTask;
                }
            );

        // Act
        var sut = CreateSut(downloadServiceMock, fileMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // The completion handler verifies the actual file before forwarding complete progress.
        // Verify the dispatcher received the verified file length — not the raw zero from the event.
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnProgressUpdated(
                        It.IsAny<DownloadTaskKey>(),
                        It.Is<DownloadTaskProgress>(p =>
                            p.DataReceived == downloadTask.DataTotal && p.Percentage == 100 && p.TimeRemaining == 0
                        ),
                        It.IsAny<DirectDownloadSnapshot?>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldReportVerifiedFileSizeAsDataTotal_WhenCompletedFileIsLargerThanSeededMetadata()
    {
        // Arrange
        await SetupDatabase(
            88890,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var seededMetadataSize = downloadTask.DataTotal;
        var verifiedFileSize = seededMetadataSize + 1024;
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);
        var completionPackage = MakeDownloadPackage(verifiedFileSize);
        var fileMock = new Mock<IFile>();
        var fileInfoFactoryMock = new Mock<IFileInfoFactory>();
        var downloadServiceMock = new Mock<IDownloadService>();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        SetupSpeedLimitMocks(serverMachineIdentifier);
        SetupCommandExecutor();

        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Package).Returns(completionPackage);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                (_, targetPath, _) =>
                {
                    SetupVerifiedFile(fileMock, fileInfoFactoryMock, targetPath, verifiedFileSize);

                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        new AsyncCompletedEventArgs(null, false, completionPackage)
                    );
                    return Task.CompletedTask;
                }
            );

        // Act
        var sut = CreateSut(downloadServiceMock, fileMock, fileInfoFactoryMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnProgressUpdated(
                        It.Is<DownloadTaskKey>(key => key == downloadTask.ToKey()),
                        It.Is<DownloadTaskProgress>(p =>
                            p.DataTotal == verifiedFileSize
                            && p.DataReceived == verifiedFileSize
                            && p.Percentage == 100
                            && p.TimeRemaining == 0
                        ),
                        It.IsAny<DirectDownloadSnapshot?>()
                    ),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldReturnFailedResultAndPersistClientErrorLog_WhenGetDirectDownloadUrlFails()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            99992,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);

        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result
                    .Fail<string>("Plex download URL probe failed with status 500 (InternalServerError)")
                    .Add500InternalServerError()
            );

        // Act
        var fileMock = new Mock<IFile>();
        var fileInfoFactoryMock = new Mock<IFileInfoFactory>();
        var sut = CreateSut(
            BuildSuccessDownloadServiceMock(downloadTask.DataTotal, fileMock, fileInfoFactoryMock),
            fileMock,
            fileInfoFactoryMock
        );
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(key => key == downloadTask.ToKey()),
                        DomainDownloadStatus.ServerUnreachable,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<EnsureDownloadDirectoryCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );

        var logs = await dbContext
            .DownloadTaskMovieFileLogs.Where(x => x.DownloadTaskFileId == downloadTask.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

        logs.ShouldContain(x =>
            x.LogLevel == NotificationLevel.Error
            && x.Status == DomainDownloadStatus.ServerUnreachable
            && x.Message.Contains("status 500 (InternalServerError)", StringComparison.Ordinal)
        );
    }

    [Test]
    public async Task ShouldSetSourceUnavailableStatus_WhenGetDirectDownloadUrlFailsWithNotFound()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            99994,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);

        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result.Fail<string>("Plex download URL probe failed with status 404 (NotFound)").Add404NotFoundError()
            );

        // Act
        var fileMock = new Mock<IFile>();
        var fileInfoFactoryMock = new Mock<IFileInfoFactory>();
        var sut = CreateSut(
            BuildSuccessDownloadServiceMock(downloadTask.DataTotal, fileMock, fileInfoFactoryMock),
            fileMock,
            fileInfoFactoryMock
        );
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(key => key == downloadTask.ToKey()),
                        DomainDownloadStatus.SourceUnavailable,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<EnsureDownloadDirectoryCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );

        var logs = await dbContext
            .DownloadTaskMovieFileLogs.Where(x => x.DownloadTaskFileId == downloadTask.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

        logs.ShouldContain(x =>
            x.LogLevel == NotificationLevel.Error
            && x.Status == DomainDownloadStatus.SourceUnavailable
            && x.Message.Contains("status 404 (NotFound)", StringComparison.Ordinal)
        );
    }

    [Test]
    public async Task ShouldRejectCompletedEvent_WhenVerifiedFileIsIncomplete()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<Result?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            99995,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);
        SetupCommandExecutor();

        var fileMock = new Mock<IFile>();
        var fileInfoFactoryMock = new Mock<IFileInfoFactory>();
        var package = MakeDownloadPackage(downloadTask.DataTotal);
        var incompleteSize = downloadTask.DataTotal / 4;
        var downloadServiceMock = new Mock<IDownloadService>();
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Package).Returns(package);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                (_, targetPath, _) =>
                {
                    SetupVerifiedFile(fileMock, fileInfoFactoryMock, targetPath, incompleteSize);

                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        new AsyncCompletedEventArgs(null, false, package)
                    );

                    return Task.CompletedTask;
                }
            );

        // Act
        var sut = CreateSut(downloadServiceMock, fileMock, fileInfoFactoryMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.ToString().ShouldContain("file is incomplete");
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(key => key == downloadTask.ToKey()),
                        DomainDownloadStatus.Error,
                        It.Is<Result?>(error =>
                            error != null && error.ToString().Contains("file is incomplete", StringComparison.Ordinal)
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DomainDownloadStatus.DownloadFinished,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnProgressUpdated(
                        It.IsAny<DownloadTaskKey>(),
                        It.Is<DownloadTaskProgress>(progress => progress.Percentage == 100),
                        It.IsAny<DirectDownloadSnapshot?>()
                    ),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldReturnFailure_WhenCompletionEventIsMissingAndFileCannotBeVerified()
    {
        // Arrange
        await SetupDatabase(
            99985,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );
        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);
        var fileMock = new Mock<IFile>();
        var downloadServiceMock = new Mock<IDownloadService>();

        SetupSpeedLimitMocks(serverMachineIdentifier);
        SetupCommandExecutor();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DomainDownloadStatus.Downloading,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DomainDownloadStatus.Error,
                    It.IsAny<Result>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Package).Returns(MakeDownloadPackage(downloadTask.DataTotal));
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        fileMock.Setup(x => x.Exists(It.IsAny<string>())).Returns(false);

        // Act
        var result = await CreateSut(downloadServiceMock, fileMock).Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        fileMock.Verify(x => x.Exists(It.IsAny<string>()), Times.AtLeastOnce());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DomainDownloadStatus.DownloadFinished,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldProcessOnlyFirstCompletionEvent_WhenDownloaderRaisesDuplicateCompletion()
    {
        // Arrange
        await SetupDatabase(
            99984,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );
        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);
        var fileMock = new Mock<IFile>();
        var fileInfoFactoryMock = new Mock<IFileInfoFactory>();
        var downloadServiceMock = new Mock<IDownloadService>();
        var package = MakeDownloadPackage(downloadTask.DataTotal);

        SetupSpeedLimitMocks(serverMachineIdentifier);
        SetupCommandExecutor();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DomainDownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Package).Returns(package);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                (_, targetPath, _) =>
                {
                    SetupVerifiedFile(fileMock, fileInfoFactoryMock, targetPath, downloadTask.DataTotal);
                    var completion = new AsyncCompletedEventArgs(null, false, package);
                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        completion
                    );
                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        completion
                    );
                    return Task.CompletedTask;
                }
            );

        // Act
        var result = await CreateSut(downloadServiceMock, fileMock, fileInfoFactoryMock)
            .Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DomainDownloadStatus.DownloadFinished,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnProgressUpdated(
                        It.IsAny<DownloadTaskKey>(),
                        It.Is<DownloadTaskProgress>(p => p.Percentage == 100),
                        It.IsAny<DirectDownloadSnapshot?>()
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldSetDownloadFinished_WhenProgressReaches100AndCompletionEventIsMissingButFileIsVerified()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            99990,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);
        SetupCommandExecutor();

        var fileMock = new Mock<IFile>();
        var fileInfoFactoryMock = new Mock<IFileInfoFactory>();
        var package = MakeDownloadPackage(downloadTask.DataTotal);

        var downloadServiceMock = new Mock<IDownloadService>();
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Package).Returns(package);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                async (_, targetPath, _) =>
                {
                    SetupVerifiedFile(fileMock, fileInfoFactoryMock, targetPath, downloadTask.DataTotal);

                    downloadServiceMock.Raise(
                        x => x.DownloadProgressChanged += null,
                        downloadServiceMock.Object,
                        new DownloadProgressChangedEventArgs("Main")
                        {
                            TotalBytesToReceive = downloadTask.DataTotal,
                            ReceivedBytesSize = downloadTask.DataTotal,
                            BytesPerSecondSpeed = 1024,
                        }
                    );

                    await Task.Delay(2500, CancellationToken);
                }
            );

        // Act
        var sut = CreateSut(downloadServiceMock, fileMock, fileInfoFactoryMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(key => key == downloadTask.ToKey()),
                        DomainDownloadStatus.DownloadFinished,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnProgressUpdated(
                        It.IsAny<DownloadTaskKey>(),
                        It.Is<DownloadTaskProgress>(p =>
                            p.Percentage == 100 && p.DataReceived == downloadTask.DataTotal
                        ),
                        It.IsAny<DirectDownloadSnapshot?>()
                    ),
                Times.AtLeastOnce()
            );
    }

    [Test]
    [Arguments(CompletionFailureKind.Timeout, DomainDownloadStatus.ServerUnreachable)]
    [Arguments(CompletionFailureKind.NetworkTimeoutMessage, DomainDownloadStatus.ServerUnreachable)]
    [Arguments(CompletionFailureKind.TimedOutMessage, DomainDownloadStatus.ServerUnreachable)]
    [Arguments(CompletionFailureKind.Storage, DomainDownloadStatus.StorageError)]
    [Arguments(CompletionFailureKind.UnauthorizedStorage, DomainDownloadStatus.StorageError)]
    [Arguments(CompletionFailureKind.HttpRequest, DomainDownloadStatus.Error)]
    [Arguments(CompletionFailureKind.Argument, DomainDownloadStatus.Error)]
    [Arguments(CompletionFailureKind.NotSupported, DomainDownloadStatus.Error)]
    [Arguments(CompletionFailureKind.Format, DomainDownloadStatus.Error)]
    [Arguments(CompletionFailureKind.Unknown, DomainDownloadStatus.Error)]
    public async Task ShouldSetExpectedStatus_WhenCompletionCallbackReportsError(
        CompletionFailureKind failureKind,
        DomainDownloadStatus expectedStatus
    )
    {
        // Arrange
        await SetupDatabase(
            99988,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);
        var downloadServiceMock = new Mock<IDownloadService>();
        Exception exception = failureKind switch
        {
            CompletionFailureKind.Timeout => new TimeoutException("Plex request timed out."),
            CompletionFailureKind.NetworkTimeoutMessage => new InvalidOperationException(
                "Network timeout while reading."
            ),
            CompletionFailureKind.TimedOutMessage => new InvalidOperationException("The request timed out."),
            CompletionFailureKind.Storage => new IOException("Disk write failed."),
            CompletionFailureKind.UnauthorizedStorage => new UnauthorizedAccessException("Access denied."),
            CompletionFailureKind.HttpRequest => new HttpRequestException("Connection reset."),
            CompletionFailureKind.Argument => new ArgumentException("Invalid downloader argument."),
            CompletionFailureKind.NotSupported => new NotSupportedException("Unsupported operation."),
            CompletionFailureKind.Format => new FormatException("Malformed response."),
            _ => new InvalidOperationException("Unexpected downloader failure."),
        };

        SetupSpeedLimitMocks(serverMachineIdentifier);
        SetupCommandExecutor();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DomainDownloadStatus.Downloading,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                downloadServiceMock.Raise(
                    x => x.DownloadFileCompleted += null,
                    downloadServiceMock.Object,
                    new AsyncCompletedEventArgs(exception, false, null)
                );
                return Task.CompletedTask;
            });

        // Act
        var sut = CreateSut(downloadServiceMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        result.ToString().ShouldContain(exception.Message);
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DomainDownloadStatus.DownloadFinished,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        downloadServiceMock.Verify(
            x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once()
        );
    }

    [Test]
    [Arguments(DirectFailureKind.HttpRequest)]
    [Arguments(DirectFailureKind.Storage)]
    [Arguments(DirectFailureKind.Unknown)]
    public async Task ShouldFailWithoutRetry_WhenDownloaderThrowsNonTransientException(DirectFailureKind failureKind)
    {
        // Arrange
        await SetupDatabase(
            99986,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);
        var downloadServiceMock = new Mock<IDownloadService>();
        Exception exception = failureKind switch
        {
            DirectFailureKind.HttpRequest => new HttpRequestException("Connection reset."),
            DirectFailureKind.Storage => new IOException("Read failed."),
            _ => new InvalidOperationException("Unexpected direct failure."),
        };

        SetupSpeedLimitMocks(serverMachineIdentifier);
        SetupCommandExecutor();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DomainDownloadStatus.Downloading,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var sut = CreateSut(downloadServiceMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.OfType<ExceptionalError>().ShouldContain(x => ReferenceEquals(x.Exception, exception));
        downloadServiceMock.Verify(
            x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once()
        );
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DomainDownloadStatus.DownloadFinished,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DomainDownloadStatus.ServerUnreachable,
                        It.IsAny<Result>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldSetDownloadFinished_WhenTransportRetryRecovers()
    {
        // Arrange
        await SetupDatabase(
            99987,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);
        var fileMock = new Mock<IFile>();
        var fileInfoFactoryMock = new Mock<IFileInfoFactory>();
        var downloadServiceMock = new Mock<IDownloadService>();
        var package = MakeDownloadPackage(downloadTask.DataTotal);
        package.SaveProgress = 60;
        package.Chunks = [new Chunk(0, downloadTask.DataTotal) { Position = downloadTask.DataTotal * 3 / 5 }];
        var resumedPackages = new List<DownloadPackage>();
        var attempt = 0;

        SetupSpeedLimitMocks(serverMachineIdentifier);
        SetupCommandExecutor();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DomainDownloadStatus.Downloading,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DomainDownloadStatus.DownloadFinished,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DomainDownloadStatus.ServerUnreachable,
                    It.IsAny<Result>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Package).Returns(package);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<DownloadPackage>(), It.IsAny<CancellationToken>()))
            .Returns<DownloadPackage, CancellationToken>(
                (resumePackage, _) =>
                {
                    resumedPackages.Add(resumePackage);
                    SetupVerifiedFile(
                        fileMock,
                        fileInfoFactoryMock,
                        downloadTask.DownloadFilePath.RemoveReapTempSuffix(),
                        downloadTask.DataTotal
                    );
                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        new AsyncCompletedEventArgs(null, false, package)
                    );
                    return Task.FromResult(Stream.Null);
                }
            );
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                (_, targetPath, _) =>
                {
                    if (Interlocked.Increment(ref attempt) == 1)
                    {
                        downloadServiceMock.Raise(
                            x => x.DownloadFileCompleted += null,
                            downloadServiceMock.Object,
                            new AsyncCompletedEventArgs(
                                new TaskCanceledException("Temporary transport cancellation."),
                                false,
                                package
                            )
                        );
                        return Task.CompletedTask;
                    }

                    SetupVerifiedFile(fileMock, fileInfoFactoryMock, targetPath, downloadTask.DataTotal);
                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        new AsyncCompletedEventArgs(null, false, package)
                    );
                    return Task.CompletedTask;
                }
            );

        // Act
        var sut = CreateSut(downloadServiceMock, fileMock, fileInfoFactoryMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        downloadServiceMock.Verify(
            x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once()
        );
        downloadServiceMock.Verify(
            x => x.DownloadFileTaskAsync(It.IsAny<DownloadPackage>(), It.IsAny<CancellationToken>()),
            Times.Once()
        );
        downloadServiceMock.Verify(x => x.Clear(), Times.Once());
        resumedPackages.Count.ShouldBe(1);
        resumedPackages[0].SaveProgress.ShouldBe(package.SaveProgress);
        resumedPackages[0].Chunks.Single().Position.ShouldBe(package.Chunks.Single().Position);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2)
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<EnsureDownloadDirectoryCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DomainDownloadStatus.ServerUnreachable,
                        It.IsAny<Result>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldSetServerUnreachable_WhenTransportRetryIsExhausted()
    {
        // Arrange
        await SetupDatabase(
            99989,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);
        var downloadServiceMock = new Mock<IDownloadService>();

        SetupSpeedLimitMocks(serverMachineIdentifier);
        SetupCommandExecutor();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DomainDownloadStatus.ServerUnreachable,
                    It.IsAny<Result>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                downloadServiceMock.Raise(
                    x => x.DownloadFileCompleted += null,
                    downloadServiceMock.Object,
                    new AsyncCompletedEventArgs(
                        new TaskCanceledException("Transport connection cancelled."),
                        false,
                        null
                    )
                );
                return Task.CompletedTask;
            });

        // Act
        var sut = CreateSut(downloadServiceMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        downloadServiceMock.Verify(
            x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)
        );
        result.IsServerUnreachable().ShouldBeTrue();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DomainDownloadStatus.ServerUnreachable,
                        It.IsAny<Result>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldReturnCancelledResult_WhenDownloaderThrowsForCallerCancellation()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            99990,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);
        SetupSpeedLimitMocks(serverMachineIdentifier);
        SetupCommandExecutor();

        var cancellationTokenSource = new CancellationTokenSource();
        var downloadServiceMock = new Mock<IDownloadService>();
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), cancellationTokenSource.Token))
            .Returns<string, string, CancellationToken>(
                (_, _, token) =>
                {
                    cancellationTokenSource.Cancel();
                    return Task.FromCanceled(token);
                }
            );

        // Act
        var sut = CreateSut(downloadServiceMock);
        var result = await sut.Start(downloadTask.ToKey(), cancellationTokenSource.Token);

        // Assert
        result.IsCancelled.ShouldBeTrue();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DomainDownloadStatus.DownloadFinished,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldSetPausedStatus_WhenDownloadFileCompletedEventIsCancelled()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<Domain.DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        await SetupDatabase(
            99991,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimitMocks(serverMachineIdentifier);

        SetupCommandExecutor();

        // DownloadFileCompleted with Cancelled=true simulates an explicit cancellation/pause
        var fileMock = new Mock<IFile>();
        var downloadServiceMock = new Mock<IDownloadService>();
        downloadServiceMock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        downloadServiceMock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                (_, _, _) =>
                {
                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        new AsyncCompletedEventArgs(null, true, null) // Cancelled = true
                    );
                    return Task.CompletedTask;
                }
            );

        // Act
        var sut = CreateSut(downloadServiceMock, fileMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DomainDownloadStatus.Paused,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()), Times.Once());
    }
}
