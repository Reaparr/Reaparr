using Autofac.Features.Indexed;
using Quartz;

namespace Reaparr.Application.UnitTests;

public class DownloadJobUnitTests : BaseUnitTest<DownloadJob>
{
    private static IJobExecutionContext SetupJobContext(DownloadTaskKey key)
    {
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.JobDataMap).Returns(new DownloadJobPayload(key).ToJobDataMap());

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(x => x.MergedJobDataMap).Returns(new DownloadJobPayload(key).ToJobDataMap());
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        context.SetupProperty(x => x.Result);
        return context.Object;
    }

    [Test]
    public async Task ShouldSetFailedJobResult_WhenDownloadClientFails()
    {
        // Arrange
        await SetupDatabase(39393, config => config.MovieDownloadTasksCount = 1);
        var downloadTask = IDbContext.DownloadTaskMovieFile.First();
        var context = SetupJobContext(downloadTask.ToKey());
        var startResult = Result.Fail("Download failed").Add503ServiceUnavailableError();
        Mock.Mock<IDownloadManagerSettings>().Setup(x => x.DownloadSegments).Returns(4);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(PlexDownloadClientType.Direct));
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DownloadStatus.ServerUnreachable,
                    It.IsAny<Result>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);
        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(It.IsAny<SendNotificationResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var downloadClientMock = Mock.Mock<IPlexDownloadClient>();
        downloadClientMock
            .Setup(x => x.Start(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(startResult);
        downloadClientMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var downloadClientIndexMock = new Mock<IIndex<PlexDownloadClientType, IPlexDownloadClient>>();
        downloadClientIndexMock.Setup(x => x[PlexDownloadClientType.Direct]).Returns(downloadClientMock.Object);
        var sut = Mock.Create<DownloadJob>(
            new NamedParameter("plexDownloadClientFactory", downloadClientIndexMock.Object)
        );

        // Act
        await sut.Execute(context);

        // Assert
        var jobResult = context.Result.ShouldBeOfType<BackgroundJobResult>();
        jobResult.Status.ShouldBe(JobStatus.Failed);
        jobResult.ErrorSummary.ShouldBe(startResult.Errors[0].Message);
    }

    [Test]
    public async Task ShouldNotDispatchDuplicateFailureStatus_WhenDownloadClientAlreadyPersistedIt()
    {
        // Arrange
        await SetupDatabase(39392, config => config.MovieDownloadTasksCount = 1);
        var downloadTask = IDbContext.DownloadTaskMovieFile.First();
        await IDbContext
            .DownloadTaskMovieFile.Where(x => x.Id == downloadTask.Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(task => task.DownloadStatus, DownloadStatus.ServerUnreachable),
                CancellationToken
            );
        var context = SetupJobContext(downloadTask.ToKey());
        var startResult = Result.Fail("Download failed").Add503ServiceUnavailableError();
        Mock.Mock<IDownloadManagerSettings>().Setup(x => x.DownloadSegments).Returns(4);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(PlexDownloadClientType.Direct));
        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(It.IsAny<SendNotificationResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var downloadClientMock = Mock.Mock<IPlexDownloadClient>();
        downloadClientMock
            .Setup(x => x.Start(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(startResult);
        downloadClientMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var downloadClientIndexMock = new Mock<IIndex<PlexDownloadClientType, IPlexDownloadClient>>();
        downloadClientIndexMock.Setup(x => x[PlexDownloadClientType.Direct]).Returns(downloadClientMock.Object);
        var sut = Mock.Create<DownloadJob>(
            new NamedParameter("plexDownloadClientFactory", downloadClientIndexMock.Object)
        );

        // Act
        await sut.Execute(context);

        // Assert
        context.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Failed);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<Result>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldSetDownloadAndDestinationPath_WhenDownloadTaskIsStarted()
    {
        // Arrange
        await SetupDatabase(
            39394,
            config =>
            {
                config.MovieDownloadTasksCount = 2;
            }
        );
        var dbContext = IDbContext;
        var testDownloadTask = dbContext.DownloadTaskMovieFile.First();
        var expectedDestinationRootPath = testDownloadTask.DirectoryMeta.DestinationRootPath;
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == testDownloadTask.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(
                        task => task.DirectoryMeta,
                        testDownloadTask.DirectoryMeta with
                        {
                            DownloadRootPath = string.Empty,
                        }
                    ),
                CancellationToken
            );
        Mock.Mock<IDownloadManagerSettings>().Setup(x => x.DownloadSegments).Returns(4);
        var context = SetupJobContext(testDownloadTask.ToKey());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(PlexDownloadClientType.Direct))
            .Verifiable(Times.Once());
        var downloadClientMock = Mock.Mock<IPlexDownloadClient>();
        downloadClientMock
            .Setup(x => x.Start(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        downloadClientMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable(Times.Once());

        var downloadClientIndexMock = new Mock<IIndex<PlexDownloadClientType, IPlexDownloadClient>>();
        downloadClientIndexMock.Setup(x => x[PlexDownloadClientType.Direct]).Returns(downloadClientMock.Object);

        var sut = Mock.Create<DownloadJob>(
            new NamedParameter("plexDownloadClientFactory", downloadClientIndexMock.Object)
        );

        // Act
        await sut.Execute(context);

        // Assert
        var downloadTaskResult = await dbContext.DownloadTaskMovieFile.FirstOrDefaultAsync(
            x => x.Id == testDownloadTask.Id,
            CancellationToken
        );
        downloadTaskResult.ShouldNotBeNull();

        var downloadFolder = await dbContext.GetDownloadFolder();
        var expectedDownloadRootPath = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;
        await dbContext.GetDefaultDestinationFolderPath(PlexMediaType.Movie);

        downloadTaskResult.DirectoryMeta.DownloadRootPath.ShouldBe(downloadFolder.DirectoryPath);
        downloadTaskResult.DirectoryMeta.DestinationRootPath.ShouldBe(expectedDestinationRootPath);
        downloadTaskResult.DownloadDirectory.ShouldContain(expectedDownloadRootPath);
        downloadTaskResult.DestinationDirectory.ShouldContain(downloadTaskResult.DirectoryMeta.MovieFolder);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        downloadClientMock.Verify(
            x => x.Start(It.Is<DownloadTaskKey>(key => key.Id == testDownloadTask.Id), It.IsAny<CancellationToken>()),
            Times.Once()
        );
        downloadClientMock.Verify(x => x.DisposeAsync(), Times.Once());
    }

    [Test]
    public async Task ShouldDisposeDownloadClient_WhenJobExecutionCompletes()
    {
        // Arrange
        await SetupDatabase(
            39395,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
            }
        );

        var testDownloadTask = IDbContext.DownloadTaskMovieFile.First();
        var context = SetupJobContext(testDownloadTask.ToKey());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(PlexDownloadClientType.Direct))
            .Verifiable(Times.Once());

        var downloadClientMock = Mock.Mock<IPlexDownloadClient>();
        downloadClientMock
            .Setup(x => x.Start(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        downloadClientMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var downloadClientIndexMock = new Mock<IIndex<PlexDownloadClientType, IPlexDownloadClient>>();
        downloadClientIndexMock.Setup(x => x[It.IsAny<PlexDownloadClientType>()]).Returns(downloadClientMock.Object);

        var sut = Mock.Create<DownloadJob>(
            new NamedParameter("plexDownloadClientFactory", downloadClientIndexMock.Object)
        );

        // Act
        await sut.Execute(context);

        // Assert
        downloadClientMock.Verify();
        downloadClientMock.Verify(
            x => x.Start(It.Is<DownloadTaskKey>(key => key.Id == testDownloadTask.Id), It.IsAny<CancellationToken>()),
            Times.Once()
        );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldSetSourceUnavailableStatus_WhenClientStartFailsWithNotFound()
    {
        // Arrange
        await SetupDatabase(
            39396,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
            }
        );

        var testDownloadTask = IDbContext.DownloadTaskMovieFile.First();
        testDownloadTask.DownloadStatus = DownloadStatus.SourceUnavailable;
        await IDbContext.SaveChangesAsync(CancellationToken);

        var context = SetupJobContext(testDownloadTask.ToKey());

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(PlexDownloadClientType.Direct))
            .Verifiable(Times.Once());
        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(It.IsAny<SendNotificationResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        var startResult = Result.Fail("Source is unavailable").Add404NotFoundError();
        var downloadClientMock = new Mock<IPlexDownloadClient>();
        downloadClientMock
            .Setup(x => x.Start(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(startResult)
            .Verifiable(Times.Once());
        downloadClientMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable(Times.Once());

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

        var downloadClientIndexMock = new Mock<IIndex<PlexDownloadClientType, IPlexDownloadClient>>();
        downloadClientIndexMock.Setup(x => x[PlexDownloadClientType.Direct]).Returns(downloadClientMock.Object);

        var sut = Mock.Create<DownloadJob>(
            new NamedParameter("plexDownloadClientFactory", downloadClientIndexMock.Object)
        );

        // Act
        await sut.Execute(context);

        // Assert
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DownloadStatus.SourceUnavailable,
                        It.IsAny<Result>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<SendNotificationResult>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        downloadClientMock.Verify(
            x => x.Start(It.Is<DownloadTaskKey>(key => key.Id == testDownloadTask.Id), It.IsAny<CancellationToken>()),
            Times.Once()
        );
        downloadClientMock.Verify(x => x.DisposeAsync(), Times.Once());
    }

    [Test]
    public async Task ShouldSetAuthErrorStatus_WhenClientStartFailsWithoutAuthenticationToken()
    {
        // Arrange
        await SetupDatabase(
            39397,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
            }
        );

        var testDownloadTask = IDbContext.DownloadTaskMovieFile.First();
        var context = SetupJobContext(testDownloadTask.ToKey());

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(PlexDownloadClientType.Direct))
            .Verifiable(Times.Once());
        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(It.IsAny<SendNotificationResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        var startResult = Result
            .Fail("Could not find any authenticationToken for PlexServer with id: 15")
            .AddPlex401UnauthorizedError();
        var downloadClientMock = new Mock<IPlexDownloadClient>();
        downloadClientMock
            .Setup(x => x.Start(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(startResult)
            .Verifiable(Times.Once());
        downloadClientMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable(Times.Once());

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

        var downloadClientIndexMock = new Mock<IIndex<PlexDownloadClientType, IPlexDownloadClient>>();
        downloadClientIndexMock.Setup(x => x[PlexDownloadClientType.Direct]).Returns(downloadClientMock.Object);

        var sut = Mock.Create<DownloadJob>(
            new NamedParameter("plexDownloadClientFactory", downloadClientIndexMock.Object)
        );

        // Act
        await sut.Execute(context);

        // Assert
        startResult.IsFailed.ShouldBeTrue();
        startResult.Errors.Count.ShouldBeGreaterThan(0);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(key => key.Id == testDownloadTask.Id),
                        DownloadStatus.AuthError,
                        startResult,
                        CancellationToken.None
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<SendNotificationResult>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        downloadClientMock.Verify(
            x => x.Start(It.Is<DownloadTaskKey>(key => key.Id == testDownloadTask.Id), It.IsAny<CancellationToken>()),
            Times.Once()
        );
        downloadClientMock.Verify(x => x.DisposeAsync(), Times.Once());
    }

    [Test]
    public async Task ShouldSetDownloadClientErrorStatus_WhenClientStartFailsWithoutSpecificError()
    {
        // Arrange
        await SetupDatabase(
            39397,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
            }
        );

        var testDownloadTask = IDbContext.DownloadTaskMovieFile.First();
        var context = SetupJobContext(testDownloadTask.ToKey());

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(PlexDownloadClientType.Direct))
            .Verifiable(Times.Once());
        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(It.IsAny<SendNotificationResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        var startResult = Result.Fail("Client failed to start");
        var downloadClientMock = new Mock<IPlexDownloadClient>();
        downloadClientMock
            .Setup(x => x.Start(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(startResult)
            .Verifiable(Times.Once());
        downloadClientMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable(Times.Once());

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

        var downloadClientIndexMock = new Mock<IIndex<PlexDownloadClientType, IPlexDownloadClient>>();
        downloadClientIndexMock.Setup(x => x[PlexDownloadClientType.Direct]).Returns(downloadClientMock.Object);

        var sut = Mock.Create<DownloadJob>(
            new NamedParameter("plexDownloadClientFactory", downloadClientIndexMock.Object)
        );

        // Act
        await sut.Execute(context);

        // Assert
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DownloadStatus.DownloadClientError,
                        It.IsAny<Result>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<SendNotificationResult>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        downloadClientMock.Verify(
            x => x.Start(It.Is<DownloadTaskKey>(key => key.Id == testDownloadTask.Id), It.IsAny<CancellationToken>()),
            Times.Once()
        );
        downloadClientMock.Verify(x => x.DisposeAsync(), Times.Once());
    }

    [Test]
    public async Task ShouldNotStartClient_WhenQueuedJobReachesPausedTask()
    {
        // Arrange
        await SetupDatabase(39401, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        await dbContext.SetDownloadStatus(downloadTask.ToKey(), DownloadStatus.Paused);
        var context = SetupJobContext(downloadTask.ToKey());

        // Act
        await Sut.Execute(context);

        // Assert
        var result = context.Result.ShouldBeOfType<BackgroundJobResult>();
        result.Status.ShouldBe(JobStatus.Failed);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldNotStartClient_WhenQueuedJobReachesStoppedTask()
    {
        // Arrange
        await SetupDatabase(39402, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        await dbContext.SetDownloadStatus(downloadTask.ToKey(), DownloadStatus.Stopped);
        var context = SetupJobContext(downloadTask.ToKey());

        // Act
        await Sut.Execute(context);

        // Assert
        var result = context.Result.ShouldBeOfType<BackgroundJobResult>();
        result.Status.ShouldBe(JobStatus.Failed);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldNotStartClient_WhenTaskIsPausedDuringClientResolution()
    {
        // Arrange
        await SetupDatabase(39403, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var downloadTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var context = SetupJobContext(downloadTask.ToKey());
        var clientResolutionStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeClientResolution = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), CancellationToken.None))
            .Returns(async () =>
            {
                clientResolutionStarted.TrySetResult(true);
                await resumeClientResolution.Task;
                return Result.Ok(PlexDownloadClientType.Direct);
            })
            .Verifiable(Times.Once());
        var downloadClientMock = new Mock<IPlexDownloadClient>();
        downloadClientMock
            .Setup(x => x.Start(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never());
        var downloadClientIndexMock = new Mock<IIndex<PlexDownloadClientType, IPlexDownloadClient>>();
        downloadClientIndexMock.Setup(x => x[PlexDownloadClientType.Direct]).Returns(downloadClientMock.Object);
        var sut = Mock.Create<DownloadJob>(
            new NamedParameter("plexDownloadClientFactory", downloadClientIndexMock.Object)
        );

        // Act
        var executeTask = sut.Execute(context);
        await clientResolutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await dbContext.SetDownloadStatus(downloadTask.ToKey(), DownloadStatus.Paused);
        resumeClientResolution.TrySetResult(true);
        await executeTask;

        // Assert
        context.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Failed);
        Mock.Mock<ICommandExecutor>().Verify();
        downloadClientMock.Verify();
    }
}
