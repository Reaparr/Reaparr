using Reaparr.External.Contracts;
using DomainDownloadStatus = Reaparr.Domain.DownloadStatus;

namespace Reaparr.Application.UnitTests;

public class DashPlexDownloadClientStopAsyncUnitTests : BaseUnitTest<DashPlexDownloadClient>
{
    private DashPlexDownloadClient CreateSut(Mock<IDashMpdCliWrapper> dashWrapperMock)
    {
        var directoryMock = new Mock<IDirectory>();
        directoryMock.Setup(x => x.CreateDirectory(It.IsAny<string>()));
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
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
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
        dashWrapperMock.Setup(x => x.StopAsync()).ReturnsAsync(Result.Ok());
        dashWrapperMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(dashWrapperMock);
        var startTask = sut.Start(downloadTask.ToKey(), CancellationToken);

        await Task.Delay(100, CancellationToken);
        var stopResult = await sut.StopAsync();
        var startResult = await startTask;

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
        await Task.Delay(100, CancellationToken);

        var firstStop = await sut.StopAsync();
        var secondStop = await sut.StopAsync();
        await startTask;

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
}
