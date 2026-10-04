using System.ComponentModel;
using Downloader;
using DomainDownloadStatus = Reaparr.Domain.DownloadStatus;

namespace Reaparr.Application.UnitTests;

public class DirectPlexDownloadClientStopAsyncUnitTests : BaseUnitTest<DirectPlexDownloadClient>
{
    // -------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------

    private DirectPlexDownloadClient CreateSut(Mock<IDownloadService> downloadServiceMock) =>
        Mock.Create<DirectPlexDownloadClient>(
            new NamedParameter(
                "downloadServiceFactory",
                (Func<DownloadConfiguration, IDownloadService>)(_ => downloadServiceMock.Object)
            )
        );

    private void SetupCommandExecutor()
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<GetDirectDownloadUrlCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok("https://plex.example/download.mp4"));

        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<ICommand<Result>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
    }

    private void SetupSpeedLimitMocks(string serverMachineIdentifier, int speedLimit = 1000)
    {
        // IDownloadManagerSettings.DownloadSegments is read in the SUT constructor
        Mock.Mock<IDownloadManagerSettings>().Setup(x => x.DownloadSegments).Returns(1);

        Mock.Mock<IDownloadSpeedLimitProvider>()
            .Setup(x => x.GetEffectiveDownloadSpeedLimitObservable(serverMachineIdentifier))
            .Returns(Observable.Return(checked((long)speedLimit * 1024)));
    }

    /// <summary>
    /// Builds a download service mock that simulates an in-progress download.
    /// Calling CancelTaskAsync() on the mock cancels the internal CTS, which unblocks
    /// the infinite Task.Delay in DownloadFileTaskAsync and raises DownloadFileCompleted(cancelled=true).
    /// </summary>
    private static Mock<IDownloadService> BuildInProgressDownloadServiceMock()
    {
        var cts = new CancellationTokenSource();
        var mock = new Mock<IDownloadService>();
        mock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        mock.Setup(x => x.CancelTaskAsync())
            .Returns(() =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            });

        mock.Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                async (_, _, _) =>
                {
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
                    }
                    catch (OperationCanceledException) { }

                    mock.Raise(
                        x => x.DownloadFileCompleted += null,
                        mock.Object,
                        new AsyncCompletedEventArgs(null, true, null)
                    );
                }
            );

        return mock;
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Test]
    public async Task ShouldDispatchPausedStatus_AndReturnSuccess_WhenDownloadClientIsStopped()
    {
        // Arrange
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

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                    DomainDownloadStatus.Downloading,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                    DomainDownloadStatus.Paused,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        var downloadServiceMock = BuildInProgressDownloadServiceMock();

        // Act
        var sut = CreateSut(downloadServiceMock);
        var startTask = sut.Start(downloadTask.ToKey(), CancellationToken);

        await Task.Delay(200, CancellationToken);
        var stopResult = await sut.StopAsync();
        var startResult = await startTask;

        // Assert
        startResult.IsSuccess.ShouldBeTrue();
        stopResult.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                        DomainDownloadStatus.Downloading,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                        DomainDownloadStatus.Paused,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                m =>
                    m.Send(
                        It.IsAny<GetDirectDownloadUrlCommand>(),
                        It.Is<CancellationToken>(token => token == CancellationToken)
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldDispatchPausedStatus_WhenDownloadIsCancelledByStop()
    {
        // Arrange
        await SetupDatabase(
            11110,
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

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                    DomainDownloadStatus.Downloading,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                    DomainDownloadStatus.Paused,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        var downloadServiceMock = BuildInProgressDownloadServiceMock();

        // Act
        var sut = CreateSut(downloadServiceMock);
        var startTask = sut.Start(downloadTask.ToKey(), CancellationToken);

        await Task.Delay(200, CancellationToken);
        await sut.StopAsync();
        await startTask;

        // Assert
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                        DomainDownloadStatus.Downloading,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                        DomainDownloadStatus.Paused,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                m =>
                    m.Send(
                        It.IsAny<GetDirectDownloadUrlCommand>(),
                        It.Is<CancellationToken>(token => token == CancellationToken)
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldReturnSuccessResult_WhenStopAsyncIsCalledTwice()
    {
        // Arrange
        await SetupDatabase(
            22220,
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

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                    DomainDownloadStatus.Downloading,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                    DomainDownloadStatus.Paused,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        var downloadServiceMock = BuildInProgressDownloadServiceMock();

        var sut = CreateSut(downloadServiceMock);
        var startTask = sut.Start(downloadTask.ToKey(), CancellationToken);
        await Task.Delay(200, CancellationToken);

        // Act — stop twice
        var firstStop = await sut.StopAsync();
        await startTask;
        var secondStop = await sut.StopAsync();

        // Assert — both calls must succeed without throwing
        firstStop.IsSuccess.ShouldBeTrue();
        secondStop.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                        DomainDownloadStatus.Downloading,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                        DomainDownloadStatus.Paused,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                m =>
                    m.Send(
                        It.IsAny<GetDirectDownloadUrlCommand>(),
                        It.Is<CancellationToken>(token => token == CancellationToken)
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldCallCancelTaskAsync_WhenStopAsyncIsCalled()
    {
        // Arrange
        await SetupDatabase(
            33330,
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

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                    DomainDownloadStatus.Downloading,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                    DomainDownloadStatus.Paused,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        var downloadServiceMock = BuildInProgressDownloadServiceMock();

        // Re-setup CancelTaskAsync with Verifiable after BuildInProgressDownloadServiceMock wires it
        var cancelCallCount = 0;
        downloadServiceMock
            .Setup(x => x.CancelTaskAsync())
            .Returns(() =>
            {
                cancelCallCount++;

                // Still need to unblock the infinite delay — grab the internal CTS via closure
                return Task.CompletedTask;
            });

        // Use a fresh, self-contained approach with a local CTS
        var localCts = new CancellationTokenSource();
        downloadServiceMock
            .Setup(x => x.CancelTaskAsync())
            .Returns(() =>
            {
                cancelCallCount++;
                localCts.Cancel();
                return Task.CompletedTask;
            });

        downloadServiceMock
            .Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                async (_, _, _) =>
                {
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, localCts.Token);
                    }
                    catch (OperationCanceledException) { }

                    downloadServiceMock.Raise(
                        x => x.DownloadFileCompleted += null,
                        downloadServiceMock.Object,
                        new AsyncCompletedEventArgs(null, true, null)
                    );
                }
            );

        var sut = CreateSut(downloadServiceMock);
        var startTask = sut.Start(downloadTask.ToKey(), CancellationToken);
        await Task.Delay(200, CancellationToken);

        // Act
        await sut.StopAsync();
        await startTask;

        // Assert — CancelTaskAsync must have been invoked exactly once on the underlying service
        cancelCallCount.ShouldBe(1);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                        DomainDownloadStatus.Downloading,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                        DomainDownloadStatus.Paused,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                m =>
                    m.Send(
                        It.IsAny<GetDirectDownloadUrlCommand>(),
                        It.Is<CancellationToken>(token => token == CancellationToken)
                    ),
                Times.Once()
            );
    }
}
