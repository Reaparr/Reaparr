using System.Diagnostics;
using Quartz;

namespace Reaparr.Application.UnitTests;

public class CheckAllConnectionsStatusByPlexServerJobUnitTests : BaseUnitTest<CheckAllConnectionsStatusByPlexServerJob>
{
    private static Mock<IJobExecutionContext> SetupJobContext(CancellationToken cancellationToken = default)
    {
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.Key).Returns(CheckAllConnectionsStatusByPlexServerJob.GetJobKey());

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.CancellationToken).Returns(cancellationToken);
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(x => x.MergedJobDataMap).Returns(new JobDataMap());
        context.SetupGet(x => x.FireInstanceId).Returns("connection-check-test");
        context.SetupProperty(x => x.Result);
        return context;
    }

    private void SetupBeginJob()
    {
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.BeginJob(
                    TelemetryActivity.ScheduledConnectionCheck,
                    It.IsAny<(ActivityContext Parent, string? SessionId, string JobKey, string FireInstanceId)>()
                )
            )
            .Returns((IDisposable?)null)
            .Verifiable(Times.Once());
    }

    [Test]
    public async Task ShouldSetFailedStatus_WhenCommandThrows()
    {
        // Arrange
        await SetupDatabase(135045, config => config.PlexServerCount = 1);
        var server = await IDbContext.PlexServers.Include(x => x.PlexServerConnections).SingleAsync(CancellationToken);
        var expectedConnectionIds = server.PlexServerConnections.Select(x => x.Id).Order().ToArray();
        var exception = new InvalidOperationException("connection check failed");
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckAllConnectionsStatusByPlexServerCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception)
            .Verifiable(Times.Once());
        Mock.Mock<IProgressHubService>()
            .Setup(x => x.SendJobStatusUpdateAsync(It.IsAny<JobStatusUpdate<CheckAllConnectionStatusUpdateDTO>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        ResultBase? capturedResult = null;
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(It.IsAny<ResultBase>(), TelemetryActivity.ScheduledConnectionCheck))
            .Callback<ResultBase, TelemetryActivity>((captured, _) => capturedResult = captured)
            .Verifiable(Times.Once());
        var context = SetupJobContext();

        SetupBeginJob();
        // Act
        await Sut.Execute(context.Object, context.Object.CancellationToken);

        // Assert
        var report = context.Object.Result.ShouldBeOfType<BackgroundJobResult>();
        report.Status.ShouldBe(JobStatus.Failed);
        var update = report.CronUpdate.ShouldBeOfType<JobStatusUpdate<CheckAllConnectionStatusUpdateDTO>>();
        update.Status.ShouldBe(JobStatus.Failed);
        update.Data.PlexServersWithConnectionIds.Keys.ShouldBe([server.Id]);
        update.Data.PlexServersWithConnectionIds[server.Id].Order().ShouldBe(expectedConnectionIds);
        capturedResult.ShouldNotBeNull();
        capturedResult!.HasException<InvalidOperationException>(out var capturedErrors).ShouldBeTrue();
        capturedErrors.OfType<ExceptionalError>().Single().Exception.ShouldBeSameAs(exception);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IProgressHubService>().Verify();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldSetCancelledStatus_WhenCommandIsCancelled()
    {
        // Arrange
        await SetupDatabase(135046, config => config.PlexServerCount = 1);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckAllConnectionsStatusByPlexServerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                ResultExtensions
                    .TaskIsCancelled(nameof(CheckAllConnectionsStatusByPlexServerJob))
                    .ToResult<List<PlexServerStatus>>()
            )
            .Verifiable(Times.Once());
        Mock.Mock<IProgressHubService>()
            .Setup(x => x.SendJobStatusUpdateAsync(It.IsAny<JobStatusUpdate<CheckAllConnectionStatusUpdateDTO>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(It.IsAny<ResultBase>(), TelemetryActivity.ScheduledConnectionCheck))
            .Verifiable(Times.Once());
        var context = SetupJobContext();

        SetupBeginJob();

        // Act
        await Sut.Execute(context.Object, context.Object.CancellationToken);

        var report = context.Object.Result.ShouldBeOfType<BackgroundJobResult>();
        report.Status.ShouldBe(JobStatus.Cancelled);
        var update = report.CronUpdate.ShouldBeOfType<JobStatusUpdate<CheckAllConnectionStatusUpdateDTO>>();
        update.Status.ShouldBe(JobStatus.Cancelled);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IProgressHubService>().Verify();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldFail_WhenOneOrMorePlexServersAreUnavailable()
    {
        // Arrange
        await SetupDatabase(135044, config =>
        {
            config.PlexServerCount = 2;
        });

        var plexServers = await IDbContext
            .PlexServers.Include(x => x.PlexServerConnections)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var plexServerIds = plexServers.Select(x => x.Id).ToList();
        var expectedPayload = plexServers.ToDictionary(
            x => x.Id,
            x => x.PlexServerConnections.Select(y => y.Id).ToList()
        );

        Mock.Mock<IProgressHubService>()
            .Setup(x => x.SendJobStatusUpdateAsync(It.IsAny<JobStatusUpdate<CheckAllConnectionStatusUpdateDTO>>()))
            .Returns(Task.CompletedTask);
        var dispatchedCommands = new List<CheckAllConnectionsStatusByPlexServerCommand>();
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckAllConnectionsStatusByPlexServerCommand>(), It.IsAny<CancellationToken>()))
            .Returns<ICommand<Result<List<PlexServerStatus>>>, CancellationToken>(
                (request, _) =>
                {
                    var command = (CheckAllConnectionsStatusByPlexServerCommand)request;
                    dispatchedCommands.Add(command);
                    return Task.FromResult(
                        command.PlexServerId == plexServerIds[0]
                            ? Result.Fail<List<PlexServerStatus>>("Plex server is unavailable")
                            : Result.Ok(new List<PlexServerStatus>())
                    );
                }
            );
        var context = SetupJobContext();
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(It.IsAny<ResultBase>(), TelemetryActivity.ScheduledConnectionCheck))
            .Verifiable(Times.Once());
        SetupBeginJob();

        // Act
        Func<Task> action = async () => await Sut.Execute(context.Object, context.Object.CancellationToken);

        // Assert
        await action.ShouldNotThrowAsync();
        var report = context.Object.Result.ShouldBeOfType<BackgroundJobResult>();
        ((IJobRunReport)report).Result.ShouldBe(JobRunResult.Failed);
        var update = report.CronUpdate.ShouldBeOfType<JobStatusUpdate<CheckAllConnectionStatusUpdateDTO>>();
        update.Status.ShouldBe(JobStatus.Failed);
        update.Data.PlexServersWithConnectionIds.Count.ShouldBe(expectedPayload.Count);
        update.Data.PlexServersWithConnectionIds.ShouldAllBe(pair =>
            expectedPayload.ContainsKey(pair.Key) && pair.Value.SequenceEqual(expectedPayload[pair.Key])
        );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckAllConnectionsStatusByPlexServerCommand>(), It.IsAny<CancellationToken>()),
                Times.Exactly(plexServerIds.Count)
            );
        dispatchedCommands.Select(command => command.PlexServerId).Order().ShouldBe(plexServerIds);
    }

    [Test]
    public async Task ShouldCaptureNestedTerminalException_WhenScheduledConnectionCheckFails()
    {
        // Arrange
        await SetupDatabase(135045, config =>
        {
            config.PlexServerCount = 1;
        });

        Mock.Mock<IProgressHubService>()
            .Setup(x => x.SendJobStatusUpdateAsync(It.IsAny<JobStatusUpdate<CheckAllConnectionStatusUpdateDTO>>()))
            .Returns(Task.CompletedTask);
        var exception = new InvalidOperationException("connection transport failed");
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckAllConnectionsStatusByPlexServerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<List<PlexServerStatus>>(new ExceptionalError(exception)));
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(It.IsAny<ResultBase>(), TelemetryActivity.ScheduledConnectionCheck))
            .Verifiable(Times.Once());
        var context = SetupJobContext();
        SetupBeginJob();

        // Act
        await Sut.Execute(context.Object, context.Object.CancellationToken);

        // Assert
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldSetFailedStatus_WhenDatabaseAccessFails()
    {
        // Arrange
        var exception = new InvalidOperationException("Connection catalog unavailable");
        var database = new Mock<IReaparrDbContext>();
        database.SetupGet(x => x.PlexServers).Throws(exception);
        SetupDependencies(builder => builder.RegisterInstance(database.Object).As<IReaparrDbContext>());
        Mock.Mock<ITelemetry>()
            .Setup(x => x.CaptureErrors(It.IsAny<ResultBase>(), TelemetryActivity.ScheduledConnectionCheck))
            .Verifiable(Times.Once());
        var context = SetupJobContext();

        SetupBeginJob();
        // Act
        await Sut.Execute(context.Object, context.Object.CancellationToken);

        // Assert
        var report = context.Object.Result.ShouldBeOfType<BackgroundJobResult>();
        ((IJobRunReport)report).Result.ShouldBe(JobRunResult.Failed);
        var update = report.CronUpdate.ShouldBeOfType<JobStatusUpdate<CheckAllConnectionStatusUpdateDTO>>();
        update.Status.ShouldBe(JobStatus.Failed);
        update.Data.PlexServersWithConnectionIds.ShouldBeEmpty();
        Mock.Mock<ITelemetry>().Verify();
    }
}
