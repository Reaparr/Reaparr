using System.Reactive.Subjects;
using Reaparr.External.Contracts;
using DomainDownloadStatus = Reaparr.Domain.DownloadStatus;

namespace Reaparr.Application.UnitTests;

public class DashPlexDownloadClientUnitTests : BaseUnitTest<DashPlexDownloadClient>
{
    private DashPlexDownloadClient CreateSut(Mock<IDashMpdCliWrapper> dashWrapperMock)
    {
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()))
            .Returns(Task.CompletedTask);

        return Mock.Create<DashPlexDownloadClient>(new NamedParameter("dashWrapper", dashWrapperMock.Object));
    }

    private void SetupCommandExecutor(Result<GetTranscodeUrlResult>? getUrlResult = null)
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<ICommand<Result<GetTranscodeUrlResult>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                getUrlResult
                    ?? Result.Ok(
                        new GetTranscodeUrlResult
                        {
                            DownloadUrl = "https://plex.example/start.mpd",
                            TranscodedQuality = VideoQuality.SD,
                        }
                    )
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(m => m.Send(It.IsAny<EnsureDownloadDirectoryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
    }

    private void SetupSpeedLimit(string serverMachineIdentifier, long speedLimitBytes = 12345)
    {
        Mock.Mock<IDownloadSpeedLimitProvider>()
            .Setup(x => x.GetEffectiveDownloadSpeedLimit(serverMachineIdentifier))
            .Returns(speedLimitBytes);
    }

    [Test]
    [Arguments(12345L, "12345")]
    [Arguments(0L, null)]
    public async Task ShouldReturnSuccessAndPersistDownloadFinished_WhenProcessExitsZero(
        long speedLimitBytes,
        string? expectedLimitRate
    )
    {
        // Arrange
        await SetupDatabase(
            12001,
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

        SetupSpeedLimit(serverMachineIdentifier, speedLimitBytes);

        var progressSubject = new Subject<DashDownloadProgress>();
        var outputSubject = new Subject<string>();
        var completionSubject = new Subject<DashDownloadCompletedEventArgs>();
        DashMpdCliOptions? capturedOptions = null;

        var dashWrapperMock = new Mock<IDashMpdCliWrapper>();
        dashWrapperMock.Setup(x => x.Progress).Returns(progressSubject.AsObservable());
        dashWrapperMock.Setup(x => x.StandardOutput).Returns(outputSubject.AsObservable());
        dashWrapperMock.Setup(x => x.DownloadCompleted).Returns(completionSubject.AsObservable());
        dashWrapperMock
            .Setup(x => x.StartAsync(It.IsAny<DashMpdCliOptions>()))
            .Returns<DashMpdCliOptions>(options =>
            {
                capturedOptions = options;
                completionSubject.OnNext(new DashDownloadCompletedEventArgs(false, 0, Result.Ok()));
                return Task.FromResult(Result.Ok());
            });
        dashWrapperMock.Setup(x => x.StopAsync()).ReturnsAsync(Result.Ok());
        dashWrapperMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        SetupCommandExecutor();

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
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
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

        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(dashWrapperMock);
        // Act
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        capturedOptions.ShouldNotBeNull();
        capturedOptions.LimitRate.ShouldBe(expectedLimitRate);

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
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()), Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ICommand<Result<GetTranscodeUrlResult>>>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<EnsureDownloadDirectoryCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldReturnEntityNotFoundError_WhenDownloadTaskKeyDoesNotExist()
    {
        await SetupDatabase(
            12002,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        SetupCommandExecutor();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<Result>(),
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

        var key = new DownloadTaskKey
        {
            Type = DownloadTaskType.MovieData,
            Id = Guid.NewGuid(),
            PlexServerId = 1,
            PlexLibraryId = 1,
        };

        var dashWrapperMock = new Mock<IDashMpdCliWrapper>();
        dashWrapperMock.Setup(x => x.Progress).Returns(Observable.Empty<DashDownloadProgress>());
        dashWrapperMock.Setup(x => x.StandardOutput).Returns(Observable.Empty<string>());
        dashWrapperMock.Setup(x => x.DownloadCompleted).Returns(Observable.Empty<DashDownloadCompletedEventArgs>());

        var sut = CreateSut(dashWrapperMock);
        var result = await sut.Start(key, CancellationToken);

        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message.Contains(nameof(DownloadTaskGeneric)));
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ICommand<Result<GetTranscodeUrlResult>>>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()), Times.Never());
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenGetDashDownloadUrlFails()
    {
        await SetupDatabase(
            12003,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);

        SetupCommandExecutor(Result.Fail<GetTranscodeUrlResult>("Could not get DASH URL"));

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<Result>(),
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

        var dashWrapperMock = new Mock<IDashMpdCliWrapper>();
        dashWrapperMock.Setup(x => x.Progress).Returns(Observable.Empty<DashDownloadProgress>());
        dashWrapperMock.Setup(x => x.StandardOutput).Returns(Observable.Empty<string>());
        dashWrapperMock.Setup(x => x.DownloadCompleted).Returns(Observable.Empty<DashDownloadCompletedEventArgs>());

        var sut = CreateSut(dashWrapperMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        result.IsFailed.ShouldBeTrue();
        dashWrapperMock.Verify(x => x.StartAsync(It.IsAny<DashMpdCliOptions>()), Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ICommand<Result<GetTranscodeUrlResult>>>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()), Times.Never());
    }

    [Test]
    public async Task ShouldSetServerUnreachableStatus_WhenDashCompletesWithGatewayTimeoutError()
    {
        await SetupDatabase(
            12008,
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

        SetupSpeedLimit(serverMachineIdentifier, 0);
        SetupCommandExecutor();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<Result>(),
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

        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()))
            .Returns(Task.CompletedTask);

        var progressSubject = new Subject<DashDownloadProgress>();
        var outputSubject = new Subject<string>();
        var completionSubject = new Subject<DashDownloadCompletedEventArgs>();

        var networkErrorResult = Result.Fail("network error").Add504GatewayTimeoutError("network error");

        var dashWrapperMock = new Mock<IDashMpdCliWrapper>();
        dashWrapperMock.Setup(x => x.Progress).Returns(progressSubject.AsObservable());
        dashWrapperMock.Setup(x => x.StandardOutput).Returns(outputSubject.AsObservable());
        dashWrapperMock.Setup(x => x.DownloadCompleted).Returns(completionSubject.AsObservable());
        dashWrapperMock
            .Setup(x => x.StartAsync(It.IsAny<DashMpdCliOptions>()))
            .Returns(() =>
            {
                completionSubject.OnNext(new DashDownloadCompletedEventArgs(false, 1, networkErrorResult));
                return Task.FromResult(networkErrorResult);
            });
        dashWrapperMock.Setup(x => x.StopAsync()).ReturnsAsync(Result.Ok());
        dashWrapperMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(dashWrapperMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        result.IsFailed.ShouldBeTrue();

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
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()), Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ICommand<Result<GetTranscodeUrlResult>>>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        dashWrapperMock.Verify(x => x.StartAsync(It.IsAny<DashMpdCliOptions>()), Times.Once());
    }

    [Test]
    public async Task ShouldSetSourceUnavailableStatus_WhenDashCompletesWithNotFoundError()
    {
        await SetupDatabase(
            12010,
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

        SetupSpeedLimit(serverMachineIdentifier, 0);
        SetupCommandExecutor();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<Result>(),
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

        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()))
            .Returns(Task.CompletedTask);

        var progressSubject = new Subject<DashDownloadProgress>();
        var outputSubject = new Subject<string>();
        var completionSubject = new Subject<DashDownloadCompletedEventArgs>();

        var notFoundResult = Result.Fail("dash-mpd-cli failed with not found").Add404NotFoundError();

        var dashWrapperMock = new Mock<IDashMpdCliWrapper>();
        dashWrapperMock.Setup(x => x.Progress).Returns(progressSubject.AsObservable());
        dashWrapperMock.Setup(x => x.StandardOutput).Returns(outputSubject.AsObservable());
        dashWrapperMock.Setup(x => x.DownloadCompleted).Returns(completionSubject.AsObservable());
        dashWrapperMock
            .Setup(x => x.StartAsync(It.IsAny<DashMpdCliOptions>()))
            .Returns(() =>
            {
                completionSubject.OnNext(new DashDownloadCompletedEventArgs(false, 1, notFoundResult));
                return Task.FromResult(notFoundResult);
            });
        dashWrapperMock.Setup(x => x.StopAsync()).ReturnsAsync(Result.Ok());
        dashWrapperMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(dashWrapperMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        result.IsFailed.ShouldBeTrue();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DomainDownloadStatus.SourceUnavailable,
                        It.IsAny<Result>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()), Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ICommand<Result<GetTranscodeUrlResult>>>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldSetServerUnreachableStatus_WhenDashCompletesWithNetworkTimeoutError()
    {
        await SetupDatabase(
            12009,
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

        SetupSpeedLimit(serverMachineIdentifier, 0);
        SetupCommandExecutor();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<Result>(),
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

        var progressSubject = new Subject<DashDownloadProgress>();
        var outputSubject = new Subject<string>();
        var completionSubject = new Subject<DashDownloadCompletedEventArgs>();

        var networkTimeoutResult = Result.Fail("Download failed: network timeout: fetching DASH manifest timed out");

        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()))
            .Returns(Task.CompletedTask);

        var dashWrapperMock = new Mock<IDashMpdCliWrapper>();
        dashWrapperMock.Setup(x => x.Progress).Returns(progressSubject.AsObservable());
        dashWrapperMock.Setup(x => x.StandardOutput).Returns(outputSubject.AsObservable());
        dashWrapperMock.Setup(x => x.DownloadCompleted).Returns(completionSubject.AsObservable());
        dashWrapperMock
            .Setup(x => x.StartAsync(It.IsAny<DashMpdCliOptions>()))
            .Returns(() =>
            {
                completionSubject.OnNext(new DashDownloadCompletedEventArgs(false, 1, networkTimeoutResult));
                return Task.FromResult(networkTimeoutResult);
            });
        dashWrapperMock.Setup(x => x.StopAsync()).ReturnsAsync(Result.Ok());
        dashWrapperMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(dashWrapperMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        result.IsFailed.ShouldBeTrue();

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
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()), Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ICommand<Result<GetTranscodeUrlResult>>>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldDispatchProgressUpdate_WhenDashProgressEmits()
    {
        await SetupDatabase(
            12004,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimit(serverMachineIdentifier, 0);
        SetupCommandExecutor();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
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

        var progressSubject = new Subject<DashDownloadProgress>();
        var outputSubject = new Subject<string>();

        var dashWrapperMock = new Mock<IDashMpdCliWrapper>();
        dashWrapperMock.Setup(x => x.Progress).Returns(progressSubject.AsObservable());
        dashWrapperMock.Setup(x => x.StandardOutput).Returns(outputSubject.AsObservable());
        dashWrapperMock.Setup(x => x.DownloadCompleted).Returns(Observable.Empty<DashDownloadCompletedEventArgs>());
        dashWrapperMock
            .Setup(x => x.StartAsync(It.IsAny<DashMpdCliOptions>()))
            .Returns(async () =>
            {
                progressSubject.OnNext(
                    new DashDownloadProgress
                    {
                        Eta = 1,
                        Percent = 50,
                        DownloadedBytes = downloadTask.DataTotal / 2,
                        TotalBytes = downloadTask.DataTotal,
                        DownloadSpeedInBytes = 1024,
                        RawOutput = "{}",
                    }
                );

                // Allow the Rx Sample(500ms) window to elapse and the handler to persist progress
                await Task.Delay(700, CancellationToken);
                return Result.Ok();
            });
        dashWrapperMock.Setup(x => x.StopAsync()).ReturnsAsync(Result.Ok());
        dashWrapperMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(dashWrapperMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        result.IsSuccess.ShouldBeTrue();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnProgressUpdated(
                        It.Is<DownloadTaskKey>(k => k.Id == downloadTask.Id),
                        It.Is<DownloadTaskProgress>(p =>
                            p.DataReceived > 0 && p.DownloadSpeed >= 0 && p.Percentage == 50 && p.TimeRemaining == 1
                        ),
                        It.IsAny<DirectDownloadSnapshot?>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ICommand<Result<GetTranscodeUrlResult>>>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()), Times.Once());
    }

    [Test]
    public async Task ShouldKeepDataTotalAtZero_WhenDashDoesNotReportTotalBytes()
    {
        await SetupDatabase(
            12007,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimit(serverMachineIdentifier, 0);
        SetupCommandExecutor();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
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

        var progressSubject = new Subject<DashDownloadProgress>();
        var outputSubject = new Subject<string>();

        var dashWrapperMock = new Mock<IDashMpdCliWrapper>();
        dashWrapperMock.Setup(x => x.Progress).Returns(progressSubject.AsObservable());
        dashWrapperMock.Setup(x => x.StandardOutput).Returns(outputSubject.AsObservable());
        dashWrapperMock.Setup(x => x.DownloadCompleted).Returns(Observable.Empty<DashDownloadCompletedEventArgs>());
        dashWrapperMock
            .Setup(x => x.StartAsync(It.IsAny<DashMpdCliOptions>()))
            .Returns(async () =>
            {
                progressSubject.OnNext(
                    new DashDownloadProgress
                    {
                        Eta = 42,
                        Percent = 12,
                        DownloadedBytes = 3_000,
                        TotalBytes = 0,
                        DownloadSpeedInBytes = 256,
                        RawOutput = "{}",
                    }
                );

                await Task.Delay(700, CancellationToken);
                return Result.Ok();
            });
        dashWrapperMock.Setup(x => x.StopAsync()).ReturnsAsync(Result.Ok());
        dashWrapperMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(dashWrapperMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        result.IsSuccess.ShouldBeTrue();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnProgressUpdated(
                        It.IsAny<DownloadTaskKey>(),
                        It.Is<DownloadTaskProgress>(p =>
                            p.DataTotal == 0 && p.Percentage == 12 && p.TimeRemaining == 42
                        ),
                        It.IsAny<DirectDownloadSnapshot?>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ICommand<Result<GetTranscodeUrlResult>>>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()), Times.Once());
    }

    [Test]
    public async Task ShouldNotMarkDownloadFinished_WhenProgressReaches100WithoutCompletionEvent()
    {
        await SetupDatabase(
            12005,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimit(serverMachineIdentifier, 0);
        SetupCommandExecutor();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
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

        var progressSubject = new Subject<DashDownloadProgress>();
        var outputSubject = new Subject<string>();

        var dashWrapperMock = new Mock<IDashMpdCliWrapper>();
        dashWrapperMock.Setup(x => x.Progress).Returns(progressSubject.AsObservable());
        dashWrapperMock.Setup(x => x.StandardOutput).Returns(outputSubject.AsObservable());
        dashWrapperMock.Setup(x => x.DownloadCompleted).Returns(Observable.Empty<DashDownloadCompletedEventArgs>());
        dashWrapperMock
            .Setup(x => x.StartAsync(It.IsAny<DashMpdCliOptions>()))
            .Returns(async () =>
            {
                progressSubject.OnNext(
                    new DashDownloadProgress
                    {
                        Eta = 0,
                        Percent = 100,
                        DownloadedBytes = downloadTask.DataTotal,
                        TotalBytes = downloadTask.DataTotal,
                        DownloadSpeedInBytes = 0,
                        RawOutput = "{}",
                    }
                );

                await Task.Delay(700, CancellationToken);
                return Result.Ok();
            });
        dashWrapperMock.Setup(x => x.StopAsync()).ReturnsAsync(Result.Ok());
        dashWrapperMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(dashWrapperMock);
        var result = await sut.Start(downloadTask.ToKey(), CancellationToken);

        result.IsSuccess.ShouldBeTrue();

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
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ICommand<Result<GetTranscodeUrlResult>>>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()), Times.Once());
    }

    [Test]
    [Arguments(DownloadTaskType.MovieData, "mp4")]
    [Arguments(DownloadTaskType.EpisodeData, "mp4")]
    [Arguments(DownloadTaskType.MusicTrackData, "flac")]
    [Arguments(DownloadTaskType.PhotoData, "jpg")]
    [Arguments(DownloadTaskType.OtherVideoData, "avi")]
    public async Task ShouldCreateDashOutputUsingMkvFilePathAndNormalizedFileName(
        DownloadTaskType taskType,
        string originalExtension
    )
    {
        await SetupDatabase(
            12006,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 2;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 2;
                config.MusicArtistDownloadTasksCount = 2;
                config.MusicAlbumDownloadTasksCount = 1;
                config.MusicTrackDownloadTasksCount = 1;
                config.MusicTrackFileDownloadTasksCount = 1;
                config.PhotoAlbumDownloadTasksCount = 2;
                config.PhotoImageDownloadTasksCount = 1;
                config.PhotoImageFileDownloadTasksCount = 1;
                config.OtherVideoDownloadTasksCount = 2;
                config.OtherVideoFileDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        List<DownloadTaskFileBase> downloadTasks =
        [
            .. await dbContext.DownloadTaskMovieFile.ToListAsync(CancellationToken),
            .. await dbContext.DownloadTaskTvShowEpisodeFile.ToListAsync(CancellationToken),
            .. await dbContext.DownloadTaskMusicTrackFiles.ToListAsync(CancellationToken),
            .. await dbContext.DownloadTaskPhotoImageFiles.ToListAsync(CancellationToken),
            .. await dbContext.DownloadTaskOtherVideoFiles.ToListAsync(CancellationToken),
        ];
        var downloadTask = downloadTasks.First(x => x.DownloadTaskType == taskType);
        downloadTask.FileName = $"Sample.Release.1080p.BluRay.x264.{originalExtension}";
        downloadTask.Title = "Original title";
        dbContext.Entry(downloadTask).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var key = downloadTask.ToKey();
        var serverMachineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);

        SetupSpeedLimit(serverMachineIdentifier, 0);
        SetupCommandExecutor();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
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

        var progressSubject = new Subject<DashDownloadProgress>();
        var outputSubject = new Subject<string>();
        DashMpdCliOptions? capturedOptions = null;

        var dashWrapperMock = new Mock<IDashMpdCliWrapper>();
        dashWrapperMock.Setup(x => x.Progress).Returns(progressSubject.AsObservable());
        dashWrapperMock.Setup(x => x.StandardOutput).Returns(outputSubject.AsObservable());
        dashWrapperMock.Setup(x => x.DownloadCompleted).Returns(Observable.Empty<DashDownloadCompletedEventArgs>());
        dashWrapperMock
            .Setup(x => x.StartAsync(It.IsAny<DashMpdCliOptions>()))
            .Returns<DashMpdCliOptions>(options =>
            {
                capturedOptions = options;
                return Task.FromResult(Result.Ok());
            });
        dashWrapperMock.Setup(x => x.StopAsync()).ReturnsAsync(Result.Ok());
        dashWrapperMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var sut = CreateSut(dashWrapperMock);

        var result = await sut.Start(key, CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        capturedOptions.ShouldNotBeNull();

        const string expectedNormalizedName = "Sample.Release.WEB-DL.480p.mkv";
        var expectedFinalPath = Path.Combine(downloadTask.DownloadDirectory, expectedNormalizedName);

        capturedOptions!.Output.ShouldBe(expectedFinalPath);
        capturedOptions.Output.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        capturedOptions.Output.ShouldContain("480p");

        var persistedTask = await IDbContext.GetDownloadTaskFileAsync(key, CancellationToken);
        persistedTask.ShouldNotBeNull();
        persistedTask.FileName.ShouldBe(expectedNormalizedName);
        persistedTask.Title.ShouldBe(expectedNormalizedName);
        persistedTask.DownloadFilePath.RemoveReapTempSuffix().ShouldBe(expectedFinalPath);
        persistedTask.DestinationFilePath.ShouldBe(
            Path.Combine(downloadTask.DestinationDirectory, expectedNormalizedName)
        );

        foreach (var control in downloadTasks.Where(x => x.Id != key.Id || x.DownloadTaskType != taskType))
        {
            var persistedControl = await IDbContext.GetDownloadTaskFileAsync(control.ToKey(), CancellationToken);
            persistedControl.ShouldNotBeNull();
            persistedControl.FileName.ShouldBe(control.FileName);
            persistedControl.Title.ShouldBe(control.Title);
        }
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ICommand<Result<GetTranscodeUrlResult>>>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()), Times.Once());
    }

    [Test]
    public async Task ShouldStopDashProcessAndReturnCancelled_WhenCallerCancelsDuringStart()
    {
        // Arrange
        await SetupDatabase(
            12010,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var serverMachineIdentifier = await IDbContext.GetPlexServerMachineIdentifierById(downloadTask.PlexServerId);
        SetupSpeedLimit(serverMachineIdentifier, 0);
        SetupCommandExecutor();
        var dashStartEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
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

        var dashWrapperMock = new Mock<IDashMpdCliWrapper>();
        dashWrapperMock.Setup(x => x.Progress).Returns(Observable.Empty<DashDownloadProgress>());
        dashWrapperMock.Setup(x => x.StandardOutput).Returns(Observable.Empty<string>());
        dashWrapperMock.Setup(x => x.DownloadCompleted).Returns(Observable.Empty<DashDownloadCompletedEventArgs>());
        dashWrapperMock
            .Setup(x => x.StartAsync(It.IsAny<DashMpdCliOptions>()))
            .Returns(() =>
            {
                dashStartEntered.TrySetResult();
                return new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            });
        dashWrapperMock.Setup(x => x.StopAsync()).ReturnsAsync(Result.Ok()).Verifiable(Times.Once());
        dashWrapperMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        using var cancellationTokenSource = new CancellationTokenSource();
        var sut = CreateSut(dashWrapperMock);

        // Act
        var startTask = sut.Start(downloadTask.ToKey(), cancellationTokenSource.Token);
        await dashStartEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationTokenSource.Cancel();
        var result = await startTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        result.IsCancelled.ShouldBeTrue();
        dashWrapperMock.Verify();
    }
}
