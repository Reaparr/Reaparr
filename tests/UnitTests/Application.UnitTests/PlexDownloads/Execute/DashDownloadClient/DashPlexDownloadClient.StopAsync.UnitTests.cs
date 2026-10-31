using System.Reactive.Subjects;
using Reaparr.External.Contracts;
using DomainDownloadStatus = Reaparr.Domain.DownloadStatus;

namespace Reaparr.Application.UnitTests;

public class DashPlexDownloadClientStopAsyncUnitTests : BaseUnitTest<DashPlexDownloadClient>
{
    private DashPlexDownloadClient CreateSut(Mock<IDashMpdCliWrapper> dashWrapperMock)
    {
        var directoryMock = new Mock<IDirectory>();
        directoryMock.Setup(x => x.CreateDirectory(It.IsAny<string>()));
        var outputFileInfoMock = new Mock<IFileInfo>();
        outputFileInfoMock.Setup(x => x.Exists).Returns(true);
        outputFileInfoMock.Setup(x => x.Length).Returns(0);
        Mock.Mock<IFileInfoFactory>().Setup(x => x.New(It.IsAny<string>())).Returns(outputFileInfoMock.Object);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.PersistProgressAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        return Mock.Create<DashPlexDownloadClient>(
            new NamedParameter("dashWrapper", dashWrapperMock.Object),
            new NamedParameter("directory", directoryMock.Object)
        );
    }

    private void SetupCommandExecutor()
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<ICommand<Result<GetTranscodeUrlResult>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result.Ok(
                    new GetTranscodeUrlResult
                    {
                        DownloadUrl = "https://plex.example/start.mpd",
                        TranscodedQuality = VideoQuality.SD,
                    }
                )
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<ICommand<Result>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
    }

    private void SetupSpeedLimit(string serverMachineIdentifier)
    {
        Mock.Mock<IDownloadSpeedLimitProvider>()
            .Setup(x => x.GetEffectiveDownloadSpeedLimit(serverMachineIdentifier))
            .Returns(0);
    }

    [Test]
    public async Task ShouldPersistStoppedStatus_WhenStopAsyncIsCalled()
    {
        var runnerStopEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runnerStopRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stoppedWriteEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stoppedWriteRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<DownloadTaskKey, DownloadStatus, CancellationToken>(
                (_, status, _) =>
                {
                    if (status != DownloadStatus.Stopped)
                        return Task.CompletedTask;

                    stoppedWriteEntered.TrySetResult();
                    return stoppedWriteRelease.Task;
                }
            )
            .Verifiable(Times.Exactly(2));
        await SetupDatabase(
            13001,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimit(serverMachineIdentifier);
        SetupCommandExecutor();

        var dashWrapperMock = new Mock<IDashMpdCliWrapper>();
        dashWrapperMock.Setup(x => x.Progress).Returns(Observable.Empty<DashDownloadProgress>());
        dashWrapperMock.Setup(x => x.StandardOutput).Returns(Observable.Empty<string>());
        dashWrapperMock.Setup(x => x.DownloadCompleted).Returns(Observable.Empty<DashDownloadCompletedEventArgs>());
        dashWrapperMock.Setup(x => x.StartAsync(It.IsAny<DashMpdCliOptions>())).ReturnsAsync(Result.Ok());
        dashWrapperMock
            .Setup(x => x.StopAsync())
            .Returns(async () =>
            {
                runnerStopEntered.TrySetResult();
                await runnerStopRelease.Task;
                return Result.Ok();
            });
        dashWrapperMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(dashWrapperMock);
        var startTask = sut.Start(downloadTask.ToKey(), CancellationToken);
        Task<Result>? stopTask = null;
        try
        {
            await startTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
            stopTask = sut.StopAsync();
            await runnerStopEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
            stopTask!.IsCompleted.ShouldBeFalse();
            runnerStopRelease.TrySetResult();
            await stoppedWriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
            stopTask!.IsCompleted.ShouldBeFalse();
            stoppedWriteRelease.TrySetResult();
            var stopResult = await stopTask!.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
            var startResult = await startTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

            stopResult.IsSuccess.ShouldBeTrue();
            startResult.IsSuccess.ShouldBeTrue();

            Mock.Mock<IDownloadTaskUpdateDispatcher>()
                .Verify(
                    x =>
                        x.OnStatusChangedAsync(
                            It.IsAny<DownloadTaskKey>(),
                            DomainDownloadStatus.Stopped,
                            It.IsAny<CancellationToken>()
                        ),
                    Times.Once()
                );
            Mock.Mock<INotificationHubService>()
                .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()), Times.Once());
            dashWrapperMock.VerifyGet(x => x.DownloadCompleted, Times.Once());
            dashWrapperMock.Verify(x => x.StopAsync(), Times.Once());
            Mock.Mock<ICommandExecutor>()
                .Verify(
                    x => x.Send(It.IsAny<ICommand<Result<GetTranscodeUrlResult>>>(), It.IsAny<CancellationToken>()),
                    Times.Once()
                );
        }
        finally
        {
            runnerStopRelease.TrySetResult();
            stoppedWriteRelease.TrySetResult();
        }
    }

    [Test]
    public async Task ShouldAcceptCancelledRunnerStop_AndAwaitTerminalWorkBeforePersistingStopped()
    {
        // Arrange
        await SetupDatabase(
            13004,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);
        SetupSpeedLimit(serverMachineIdentifier);
        SetupCommandExecutor();

        var completionSubject = new Subject<DashDownloadCompletedEventArgs>();
        var terminalWorkEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminalWorkRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runnerStopEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var statusOrder = new List<DownloadStatus>();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<DownloadTaskKey, DownloadStatus, CancellationToken>(
                (_, status, _) =>
                {
                    statusOrder.Add(status);
                    if (status != DownloadStatus.DownloadFinished)
                        return Task.CompletedTask;

                    terminalWorkEntered.TrySetResult();
                    return terminalWorkRelease.Task;
                }
            )
            .Verifiable(Times.Exactly(3));

        var dashWrapperMock = new Mock<IDashMpdCliWrapper>();
        dashWrapperMock.Setup(x => x.Progress).Returns(Observable.Empty<DashDownloadProgress>());
        dashWrapperMock.Setup(x => x.StandardOutput).Returns(Observable.Empty<string>());
        dashWrapperMock.Setup(x => x.DownloadCompleted).Returns(completionSubject.AsObservable());
        dashWrapperMock.Setup(x => x.StartAsync(It.IsAny<DashMpdCliOptions>())).ReturnsAsync(Result.Ok());
        dashWrapperMock
            .Setup(x => x.StopAsync())
            .Callback(() => runnerStopEntered.TrySetResult())
            .ReturnsAsync(ResultExtensions.TaskIsCancelled(nameof(IDashMpdCliWrapper)));
        dashWrapperMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(dashWrapperMock);
        var startResult = await sut.Start(downloadTask.ToKey(), CancellationToken);
        startResult.IsSuccess.ShouldBeTrue();

        // Act
        completionSubject.OnNext(new DashDownloadCompletedEventArgs(false, 0, Result.Ok()));
        await terminalWorkEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        var stopTask = sut.StopAsync();

        // Assert
        await runnerStopEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        stopTask.IsCompleted.ShouldBeFalse();
        statusOrder.ShouldBe([DownloadStatus.Downloading, DownloadStatus.DownloadFinished]);

        terminalWorkRelease.TrySetResult();
        var stopResult = await stopTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        stopResult.IsSuccess.ShouldBeTrue();
        stopResult.Errors.Count.ShouldBe(0);
        statusOrder.ShouldBe([DownloadStatus.Downloading, DownloadStatus.DownloadFinished, DownloadStatus.Stopped]);
        dashWrapperMock.Verify(x => x.StopAsync(), Times.Once());
    }

    [Test]
    public async Task ShouldReturnSuccess_WhenStopAsyncIsCalledTwice()
    {
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(3));
        await SetupDatabase(
            13002,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimit(serverMachineIdentifier);
        SetupCommandExecutor();

        var dashWrapperMock = new Mock<IDashMpdCliWrapper>();
        dashWrapperMock.Setup(x => x.Progress).Returns(Observable.Empty<DashDownloadProgress>());
        dashWrapperMock.Setup(x => x.StandardOutput).Returns(Observable.Empty<string>());
        dashWrapperMock.Setup(x => x.DownloadCompleted).Returns(Observable.Empty<DashDownloadCompletedEventArgs>());
        dashWrapperMock.Setup(x => x.StartAsync(It.IsAny<DashMpdCliOptions>())).ReturnsAsync(Result.Ok());
        dashWrapperMock.Setup(x => x.StopAsync()).ReturnsAsync(Result.Ok());
        dashWrapperMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(dashWrapperMock);
        var startTask = sut.Start(downloadTask.ToKey(), CancellationToken);
        await startTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        var firstStop = await sut.StopAsync();
        var secondStop = await sut.StopAsync();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        firstStop.IsSuccess.ShouldBeTrue();
        secondStop.IsSuccess.ShouldBeTrue();

        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()), Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Exactly(3)
            );
        dashWrapperMock.VerifyGet(x => x.DownloadCompleted, Times.Once());
        dashWrapperMock.Verify(x => x.StopAsync(), Times.Exactly(2));
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ICommand<Result<GetTranscodeUrlResult>>>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldAwaitRunnerAndTerminalWorkBeforeDisposeAsync()
    {
        // Arrange
        await SetupDatabase(
            13003,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);
        SetupSpeedLimit(serverMachineIdentifier);
        SetupCommandExecutor();

        var completionSubject = new Subject<DashDownloadCompletedEventArgs>();
        var terminalWriteEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminalWriteRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runnerDisposeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runnerDisposeRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<DownloadTaskKey, DownloadStatus, CancellationToken>(
                (_, status, _) =>
                {
                    if (status != DownloadStatus.DownloadFinished)
                        return Task.CompletedTask;

                    terminalWriteEntered.TrySetResult();
                    return terminalWriteRelease.Task;
                }
            )
            .Verifiable(Times.Exactly(2));

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnProgressUpdated(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadTaskProgress>(),
                    It.IsAny<DirectDownloadSnapshot?>()
                )
            );

        var dashWrapperMock = new Mock<IDashMpdCliWrapper>();
        dashWrapperMock.Setup(x => x.Progress).Returns(Observable.Empty<DashDownloadProgress>());
        dashWrapperMock.Setup(x => x.StandardOutput).Returns(Observable.Empty<string>());
        dashWrapperMock.Setup(x => x.DownloadCompleted).Returns(completionSubject.AsObservable());
        dashWrapperMock.Setup(x => x.StartAsync(It.IsAny<DashMpdCliOptions>())).ReturnsAsync(Result.Ok());
        dashWrapperMock.Setup(x => x.StopAsync()).ReturnsAsync(Result.Ok());
        dashWrapperMock
            .Setup(x => x.DisposeAsync())
            .Returns(() =>
            {
                runnerDisposeEntered.TrySetResult();
                return new ValueTask(runnerDisposeRelease.Task);
            });

        var sut = CreateSut(dashWrapperMock);
        try
        {
            var startTask = sut.Start(downloadTask.ToKey(), CancellationToken);
            var startResult = await startTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
            startResult.IsSuccess.ShouldBeTrue();
            completionSubject.HasObservers.ShouldBeTrue();
            dashWrapperMock.VerifyGet(x => x.DownloadCompleted, Times.Once());
            completionSubject.OnNext(new DashDownloadCompletedEventArgs(false, 0, Result.Ok()));
            await terminalWriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

            // Act
            var disposeTask = sut.DisposeAsync().AsTask();
            await runnerDisposeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
            disposeTask.IsCompleted.ShouldBeFalse();
            runnerDisposeRelease.TrySetResult();
            disposeTask.IsCompleted.ShouldBeFalse();
            terminalWriteRelease.TrySetResult();
            await disposeTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

            // Assert
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
            dashWrapperMock.Verify(x => x.DisposeAsync(), Times.Once());
        }
        finally
        {
            completionSubject.OnCompleted();
            runnerDisposeRelease.TrySetResult();
            terminalWriteRelease.TrySetResult();
        }
    }
}
