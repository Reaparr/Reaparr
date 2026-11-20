using System.Collections.Concurrent;
using System.Diagnostics;
using Quartz;

namespace Reaparr.Application.UnitTests;

public class InspectPlexServerJobUnitTests : BaseUnitTest<InspectPlexServerJob>
{
    private static IJobExecutionContext SetupJobContext(
        List<int> plexServerIds,
        CancellationToken cancellationToken = default
    ) => SetupJobContext(new InspectPlexServerJobPayload(plexServerIds), cancellationToken);

    private static IJobExecutionContext SetupJobContext(
        InspectPlexServerJobPayload payload,
        CancellationToken cancellationToken = default
    )
    {
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.JobDataMap).Returns(payload.ToJobDataMap());
        jobDetail.SetupGet(x => x.Key).Returns(InspectPlexServerJob.GetJobKey(0));

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(x => x.MergedJobDataMap).Returns(payload.ToJobDataMap());
        context.SetupGet(x => x.CancellationToken).Returns(cancellationToken);
        context.SetupGet(x => x.FireInstanceId).Returns("server-inspection-test");
        context.SetupProperty(x => x.Result);
        return context.Object;
    }

    private void SetupBeginJob()
    {
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.BeginJob(
                    TelemetryActivity.ServerInspection,
                    It.IsAny<(ActivityContext Parent, string? SessionId, string JobKey, string FireInstanceId)>()
                )
            )
            .Returns((IDisposable?)null)
            .Verifiable(Times.Once());
    }

    [Test]
    public async Task ShouldInspectServersInParallel_WhenMultipleServersAreQueued()
    {
        // Arrange
        await SetupDatabase(90001, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.PlexAccountCount = 1;
        });

        var dbContext = IDbContext;
        var plexServerIds = await dbContext
            .PlexServers.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        var expectedLibraryIds = await dbContext
            .PlexLibraries.GroupBy(x => x.PlexServerId)
            .Select(x => x.OrderBy(y => y.Id).Select(y => y.Id).ToList())
            .ToListAsync(CancellationToken);
        var startedServerIds = new ConcurrentDictionary<int, byte>();
        var bothConnectionChecksStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseConnectionChecks = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = SetupJobContext(plexServerIds);

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<CheckAllConnectionsStatusByPlexServerCommand>(command => command.Timeout == 5),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<CheckAllConnectionsStatusByPlexServerCommand, CancellationToken>(
                async (command, cancellationToken) =>
                {
                    startedServerIds.TryAdd(command.PlexServerId, 0);
                    if (startedServerIds.Count == plexServerIds.Count)
                        bothConnectionChecksStarted.SetResult();

                    await releaseConnectionChecks.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                    return Result.Ok(new List<PlexServerStatus>());
                }
            )
            .Verifiable(Times.Exactly(plexServerIds.Count));

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<RefreshLibraryAccessCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new PlexLibraryAccessRefreshResponse { OfflineServers = [], Reports = [] }))
            .Verifiable(Times.Exactly(plexServerIds.Count));

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(plexServerIds.Count));

        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(types =>
                        types.SequenceEqual(new[] { RefreshDataType.PlexAccount, RefreshDataType.PlexLibrary })
                    )
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(plexServerIds.Count));

        SetupBeginJob();
        // Act
        var executeTask = Sut.Execute(context);
        await bothConnectionChecksStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        executeTask.IsCompleted.ShouldBeFalse();
        releaseConnectionChecks.SetResult();
        await executeTask;

        // Assert
        startedServerIds.Keys.OrderBy(x => x).ShouldBe(plexServerIds);
        expectedLibraryIds.ShouldNotBeEmpty();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<CheckAllConnectionsStatusByPlexServerCommand>(command => command.Timeout == 5),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Exactly(plexServerIds.Count)
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<RefreshLibraryAccessCommand>(), It.IsAny<CancellationToken>()),
                Times.Exactly(plexServerIds.Count)
            );
        foreach (var libraryIds in expectedLibraryIds)
        {
            Mock.Mock<ICommandExecutor>()
                .Verify(
                    x =>
                        x.Send(
                            It.Is<QueueLibrarySyncJobCommand>(command =>
                                command.PlexLibraryIds.SequenceEqual(libraryIds)
                            ),
                            It.IsAny<CancellationToken>()
                        ),
                    Times.Once
                );
        }

        Mock.Mock<INotificationHubService>()
            .Verify(
                x =>
                    x.SendRefreshNotificationAsync(
                        It.Is<List<RefreshDataType>>(types =>
                            types.SequenceEqual(new[] { RefreshDataType.PlexAccount, RefreshDataType.PlexLibrary })
                        )
                    ),
                Times.Exactly(plexServerIds.Count)
            );
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldContinueInspectingRemainingServers_WhenOneServerConnectionCheckFails()
    {
        // Arrange
        await SetupDatabase(90002, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.PlexAccountCount = 1;
        });

        var dbContext = IDbContext;
        var plexServerIds = await dbContext
            .PlexServers.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        var failedServerId = plexServerIds.First();
        var successfulServerId = plexServerIds.Last();
        var context = SetupJobContext(plexServerIds);

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<CheckAllConnectionsStatusByPlexServerCommand>(command => command.Timeout == 5),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<CheckAllConnectionsStatusByPlexServerCommand, CancellationToken>(
                (command, _) =>
                {
                    if (command.PlexServerId == failedServerId)
                        return Task.FromResult(Result.Fail<List<PlexServerStatus>>("failed"));

                    return Task.FromResult(Result.Ok(new List<PlexServerStatus>()));
                }
            )
            .Verifiable(Times.Exactly(plexServerIds.Count));

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshLibraryAccessCommand>(command => command.PlexServerId == successfulServerId),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Ok(new PlexLibraryAccessRefreshResponse { OfflineServers = [], Reports = [] }))
            .Verifiable(Times.Once);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once);

        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        SetupBeginJob();
        // Act
        await Sut.Execute(context);

        // Assert
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<CheckAllConnectionsStatusByPlexServerCommand>(command => command.Timeout == 5),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Exactly(plexServerIds.Count)
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<RefreshLibraryAccessCommand>(command => command.PlexServerId == successfulServerId),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<RefreshLibraryAccessCommand>(command => command.PlexServerId == failedServerId),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()), Times.Once);
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldContainUnexpectedConnectionCheckExceptionAndMarkJobFailed()
    {
        // Arrange
        const int plexServerId = 90003;
        var context = SetupJobContext(new List<int> { plexServerId });
        var exception = new InvalidOperationException("connection check failed");
        var capturedResult = (ResultBase?)null;
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<CheckAllConnectionsStatusByPlexServerCommand>(command =>
                        command.PlexServerId == plexServerId && command.Timeout == 5
                    ),
                    CancellationToken.None
                )
            )
            .ThrowsAsync(exception)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.CaptureErrors(
                    It.Is<ResultBase>(result =>
                        result.IsFailed
                        && result.Errors.Count == 1
                        && result.HasError<ExceptionalError>(error => ReferenceEquals(error.Exception, exception))
                    ),
                    TelemetryActivity.ServerInspection
                )
            )
            .Callback<ResultBase, TelemetryActivity>((result, _) => capturedResult = result)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>().Setup(x => x.ServerInspectionStarted(plexServerId)).Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.ServerInspectionFinished(
                    plexServerId,
                    It.Is<ResultBase>(result =>
                        result.IsFailed
                        && result.Errors.Count == 1
                        && result
                            .Errors.OfType<ExceptionalError>()
                            .Any(error => ReferenceEquals(error.Exception, exception))
                    )
                )
            )
            .Verifiable(Times.Once());

        SetupBeginJob();
        // Act
        await Sut.Execute(context);

        // Assert
        var terminalResult = context.Result.ShouldBeOfType<BackgroundJobResult>();
        terminalResult.Status.ShouldBe(JobStatus.Failed);
        terminalResult.ErrorSummary.ShouldBe(exception.Message);
        capturedResult.ShouldNotBeNull().IsCancelled.ShouldBeFalse();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldCancel_WhenConnectionCheckObservesCallerCancellation()
    {
        // Arrange
        const int plexServerId = 90004;
        var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        await cancellationSource.CancelAsync();
        var context = SetupJobContext(new List<int> { plexServerId }, cancellationToken);
        var cancellation = new OperationCanceledException(cancellationToken);
        var capturedResult = (ResultBase?)null;
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<CheckAllConnectionsStatusByPlexServerCommand>(command =>
                        command.PlexServerId == plexServerId && command.Timeout == 5
                    ),
                    cancellationToken
                )
            )
            .ThrowsAsync(cancellation)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>().Setup(x => x.ServerInspectionStarted(plexServerId)).Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.ServerInspectionFinished(
                    plexServerId,
                    It.Is<ResultBase>(result =>
                        result.Errors.Count == 1
                        && result
                            .Errors.OfType<ExceptionalError>()
                            .Any(error => ReferenceEquals(error.Exception, cancellation))
                    )
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.CaptureErrors(
                    It.Is<ResultBase>(result =>
                        result.Errors.Count == 1
                        && result.HasError<ExceptionalError>(error => ReferenceEquals(error.Exception, cancellation))
                    ),
                    TelemetryActivity.ServerInspection
                )
            )
            .Callback<ResultBase, TelemetryActivity>((result, _) => capturedResult = result)
            .Verifiable(Times.Once());

        SetupBeginJob();
        // Act
        await Sut.Execute(context, context.CancellationToken);

        // Assert
        context.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Cancelled);
        capturedResult.ShouldNotBeNull().IsCancelled.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(x => x.CaptureException(It.IsAny<Exception>(), It.IsAny<TelemetryActivity>()), Times.Never());
        cancellationSource.Dispose();
    }

    [Test]
    public async Task ShouldFinishInspectionAndCaptureOriginalError_WhenNotificationFails()
    {
        // Arrange
        await SetupDatabase(90005, config =>
        {
            config.PlexServerCount = 1;
            config.PlexAccountCount = 1;
            config.PlexMovieLibraryCount = 1;
        });
        var dbContext = IDbContext;
        var plexServerId = await dbContext.PlexServers.Select(server => server.Id).SingleAsync(CancellationToken);
        var plexAccountId = await dbContext.PlexAccounts.Select(account => account.Id).SingleAsync(CancellationToken);
        var context = SetupJobContext(new List<int> { plexServerId });
        var exception = new IOException("Inspection notification failed");
        var capturedResult = (ResultBase?)null;
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<CheckAllConnectionsStatusByPlexServerCommand>(command =>
                        command.PlexServerId == plexServerId && command.Timeout == 5
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(Result.Ok(new List<PlexServerStatus>()))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshLibraryAccessCommand>(command =>
                        command.PlexServerId == plexServerId && command.PlexAccountId == plexAccountId
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(Result.Ok(new PlexLibraryAccessRefreshResponse { Reports = [], OfflineServers = [] }))
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(types =>
                        types.SequenceEqual(new[] { RefreshDataType.PlexAccount, RefreshDataType.PlexLibrary })
                    )
                )
            )
            .ThrowsAsync(exception)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>().Setup(x => x.ServerInspectionStarted(plexServerId)).Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.ServerInspectionFinished(
                    plexServerId,
                    It.Is<ResultBase>(result =>
                        result.IsFailed
                        && result.Errors.Count == 1
                        && result
                            .Errors.OfType<ExceptionalError>()
                            .Any(error => ReferenceEquals(error.Exception, exception))
                    )
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.CaptureErrors(
                    It.Is<ResultBase>(result =>
                        result.IsFailed
                        && result.Errors.Count == 1
                        && result.HasError<ExceptionalError>(error => ReferenceEquals(error.Exception, exception))
                    ),
                    TelemetryActivity.ServerInspection
                )
            )
            .Callback<ResultBase, TelemetryActivity>((result, _) => capturedResult = result)
            .Verifiable(Times.Once());

        SetupBeginJob();
        // Act
        await Sut.Execute(context);

        // Assert
        var terminalResult = context.Result.ShouldBeOfType<BackgroundJobResult>();
        terminalResult.Status.ShouldBe(JobStatus.Failed);
        terminalResult.ErrorSummary.ShouldBe(exception.Message);
        capturedResult.ShouldNotBeNull().IsCancelled.ShouldBeFalse();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
