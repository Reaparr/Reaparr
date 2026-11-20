using Autofac.Features.Indexed;
using Quartz;

namespace Reaparr.Application.UnitTests;

public class DownloadJobUnitTests : BaseUnitTest<DownloadJob>
{
    private static IJobExecutionContext SetupJobContext(DownloadTaskKey key)
    {
        var payload = new DownloadJobPayload(key).ToJobDataMap();
        var jobDetail = JobBuilder
            .Create<DownloadJob>()
            .WithIdentity(DownloadJob.GetJobKey(key.Id))
            .UsingJobData(payload)
            .Build();

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail);
        context.SetupGet(x => x.MergedJobDataMap).Returns(payload);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        context.SetupGet(x => x.FireInstanceId).Returns("download-job-test");
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
    public async Task ShouldCaptureUnexpectedDownloadClientExceptionAtWorkerBoundary()
    {
        // Arrange
        await SetupDatabase(39391, config => config.MovieDownloadTasksCount = 1);
        var downloadTask = IDbContext.DownloadTaskMovieFile.First();
        var context = SetupJobContext(downloadTask.ToKey());
        var exception = new InvalidOperationException("download client failed unexpectedly");
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(PlexDownloadClientType.Direct));
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(It.Is<ResultBase>(r => r.IsFailed), TelemetryActivity.Download))
            .Verifiable(Times.Once());
        var downloadClientMock = Mock.Mock<IPlexDownloadClient>();
        downloadClientMock
            .Setup(x => x.Start(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
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
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureErrors(It.Is<ResultBase>(r => r.IsFailed), TelemetryActivity.Download), Times.Once());
    }

    [Test]
    public async Task ShouldCapturePostClientBoundaryException_WhenClientResultHandlingThrows()
    {
        // Arrange
        await SetupDatabase(39390, config => config.MovieDownloadTasksCount = 1);
        var downloadTask = IDbContext.DownloadTaskMovieFile.First();
        var context = SetupJobContext(downloadTask.ToKey());
        var notificationException = new InvalidOperationException("download notification failed unexpectedly");
        var startResult = Result.Fail("Download failed").Add503ServiceUnavailableError();
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
            .ThrowsAsync(notificationException);
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(It.Is<ResultBase>(r => r.IsFailed), TelemetryActivity.Download))
            .Verifiable(Times.Once());
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
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureErrors(It.Is<ResultBase>(r => r.IsFailed), TelemetryActivity.Download), Times.Once());
    }

    [Test]
    public async Task ShouldCaptureDisposeExceptionAfterSuccessfulClientStartAtWorkerBoundary()
    {
        // Arrange
        await SetupDatabase(39389, config => config.MovieDownloadTasksCount = 1);
        var downloadTask = IDbContext.DownloadTaskMovieFile.First();
        var context = SetupJobContext(downloadTask.ToKey());
        var disposeException = new InvalidOperationException("download client disposal failed unexpectedly");
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeterminePlexDownloadClientCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(PlexDownloadClientType.Direct));
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(It.Is<ResultBase>(r => r.IsFailed), TelemetryActivity.Download))
            .Verifiable(Times.Once());
        var downloadClientMock = Mock.Mock<IPlexDownloadClient>();
        downloadClientMock
            .Setup(x => x.Start(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        downloadClientMock
            .Setup(x => x.DisposeAsync())
            .Returns(() => new ValueTask(Task.FromException(disposeException)));
        var downloadClientIndexMock = new Mock<IIndex<PlexDownloadClientType, IPlexDownloadClient>>();
        downloadClientIndexMock.Setup(x => x[PlexDownloadClientType.Direct]).Returns(downloadClientMock.Object);
        var sut = Mock.Create<DownloadJob>(
            new NamedParameter("plexDownloadClientFactory", downloadClientIndexMock.Object)
        );

        // Act
        await sut.Execute(context);

        // Assert
        context.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Failed);
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureErrors(It.Is<ResultBase>(r => r.IsFailed), TelemetryActivity.Download), Times.Once());
    }

    [Test]
    public async Task ShouldCaptureDisposeExceptionAfterFailedClientStartAtWorkerBoundary()
    {
        // Arrange
        await SetupDatabase(39388, config => config.MovieDownloadTasksCount = 1);
        var downloadTask = IDbContext.DownloadTaskMovieFile.First();
        var context = SetupJobContext(downloadTask.ToKey());
        var disposeException = new InvalidOperationException("failed download client disposal failed unexpectedly");
        var startResult = Result.Fail("Download failed").Add503ServiceUnavailableError();
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
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(It.Is<ResultBase>(r => r.IsFailed), TelemetryActivity.Download))
            .Verifiable(Times.Once());
        var downloadClientMock = Mock.Mock<IPlexDownloadClient>();
        downloadClientMock
            .Setup(x => x.Start(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(startResult);
        downloadClientMock
            .Setup(x => x.DisposeAsync())
            .Returns(() => new ValueTask(Task.FromException(disposeException)));
        var downloadClientIndexMock = new Mock<IIndex<PlexDownloadClientType, IPlexDownloadClient>>();
        downloadClientIndexMock.Setup(x => x[PlexDownloadClientType.Direct]).Returns(downloadClientMock.Object);
        var sut = Mock.Create<DownloadJob>(
            new NamedParameter("plexDownloadClientFactory", downloadClientIndexMock.Object)
        );

        // Act
        await sut.Execute(context);

        // Assert
        context.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Failed);
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureErrors(It.Is<ResultBase>(r => r.IsFailed), TelemetryActivity.Download), Times.Once());
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
    public async Task ShouldRequestPausedAfterStoppingDashClient_WhenClientStartIsCancelled()
    {
        // Arrange
        await SetupDatabase(39394, config => config.MovieDownloadTasksCount = 1);
        var downloadTask = await IDbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        var key = downloadTask.ToKey();
        var context = SetupJobContext(key);
        var sequence = new MockSequence();
        Mock.Mock<IDownloadManagerSettings>().Setup(x => x.DownloadSegments).Returns(4);
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<DeterminePlexDownloadClientCommand>(command => command.DownloadTaskKey == key),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(Result.Ok(PlexDownloadClientType.Dash))
            .Verifiable(Times.Once());
        var client = Mock.Mock<IPlexDownloadClient>();
        client
            .Setup(x => x.Start(key, CancellationToken.None))
            .ReturnsAsync(ResultExtensions.TaskIsCancelled(nameof(DownloadJob)))
            .Verifiable(Times.Once());
        client.InSequence(sequence).Setup(x => x.StopAsync()).ReturnsAsync(Result.Ok()).Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .InSequence(sequence)
            .Setup(x => x.OnStatusChangedAsync(key, DownloadStatus.Paused, CancellationToken.None))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        client.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable(Times.Once());
        var clients = new Mock<IIndex<PlexDownloadClientType, IPlexDownloadClient>>();
        clients.Setup(x => x[PlexDownloadClientType.Dash]).Returns(client.Object).Verifiable(Times.Once());
        var sut = Mock.Create<DownloadJob>(new NamedParameter("plexDownloadClientFactory", clients.Object));

        // Act
        await sut.Execute(context);

        // Assert
        context.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Cancelled);
        Mock.Mock<ICommandExecutor>().Verify();
        client.Verify();
        clients.Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x => x.OnStatusChangedAsync(key, DownloadStatus.Stopped, It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<SendNotificationResult>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldSetDownloadAndDestinationPath_WhenDownloadTaskIsStarted()
    {
        // Arrange
        await SetupDatabase(39394, config =>
        {
            config.MovieDownloadTasksCount = 2;
        });
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
        await SetupDatabase(39395, config =>
        {
            config.MovieDownloadTasksCount = 1;
        });

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
        await SetupDatabase(39396, config =>
        {
            config.MovieDownloadTasksCount = 1;
        });

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
        await SetupDatabase(39397, config =>
        {
            config.MovieDownloadTasksCount = 1;
        });

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
    [Arguments("generic", DownloadStatus.DownloadClientError)]
    [Arguments("http", DownloadStatus.DownloadClientError)]
    [Arguments("disk", DownloadStatus.StorageError)]
    public async Task ShouldSetExpectedStatus_WhenClientStartFails(string failureKind, DownloadStatus expectedStatus)
    {
        // Arrange
        await SetupDatabase(39397, config =>
        {
            config.MovieDownloadTasksCount = 1;
        });

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

        var startResult = failureKind switch
        {
            "http" => Result.Fail(
                new ExceptionalError(
                    new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely.")
                )
            ),
            "disk" => Result.Fail(new ExceptionalError(new IOException("Disk write failed."))),
            _ => Result.Fail("Client failed to start"),
        };
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
                        It.Is<DownloadTaskKey>(key => key.Id == testDownloadTask.Id),
                        expectedStatus,
                        It.Is<Result>(result => ReferenceEquals(result, startResult)),
                        CancellationToken.None
                    ),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        context.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Failed);
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
        var clientResolutionStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
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
