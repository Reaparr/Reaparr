using Autofac;
using Quartz;

namespace Reaparr.Application.UnitTests;

public class CheckAllConnectionsStatusByPlexServerJobUnitTests : BaseUnitTest<CheckAllConnectionsStatusByPlexServerJob>
{
    private static Mock<IJobExecutionContext> SetupJobContext()
    {
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.Key).Returns(CheckAllConnectionsStatusByPlexServerJob.GetJobKey());

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(x => x.MergedJobDataMap).Returns(new JobDataMap());
        context.SetupGet(x => x.FireInstanceId).Returns("connection-check-test");
        context.SetupProperty(x => x.Result);
        return context;
    }

    [Test]
    public async Task ShouldSetFailedStatus_WhenCommandThrows()
    {
        // Arrange
        await SetupDatabase(135045, config => config.PlexServerCount = 1);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckAllConnectionsStatusByPlexServerCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection check failed"));
        Mock.Mock<IProgressHubService>()
            .Setup(x => x.SendJobStatusUpdateAsync(It.IsAny<JobStatusUpdate<CheckAllConnectionStatusUpdateDTO>>()))
            .Returns(Task.CompletedTask);
        var context = SetupJobContext();

        // Act
        await Sut.Execute(context.Object);

        // Assert
        var update = context.Object.Result.ShouldBeOfType<JobStatusUpdate<CheckAllConnectionStatusUpdateDTO>>();
        update.Status.ShouldBe(JobStatus.Failed);
    }

    [Test]
    public async Task ShouldSetCancelledStatus_WhenCommandIsCancelled()
    {
        // Arrange
        await SetupDatabase(135046, config => config.PlexServerCount = 1);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckAllConnectionsStatusByPlexServerCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromCanceled<Result<List<PlexServerStatus>>>(new CancellationToken(true)));
        Mock.Mock<IProgressHubService>()
            .Setup(x => x.SendJobStatusUpdateAsync(It.IsAny<JobStatusUpdate<CheckAllConnectionStatusUpdateDTO>>()))
            .Returns(Task.CompletedTask);
        var context = SetupJobContext();

        // Act
        await Sut.Execute(context.Object);

        // Assert
        var update = context.Object.Result.ShouldBeOfType<JobStatusUpdate<CheckAllConnectionStatusUpdateDTO>>();
        update.Status.ShouldBe(JobStatus.Cancelled);
    }

    [Test]
    public async Task ShouldComplete_WhenOneOrMorePlexServersAreUnavailable()
    {
        // Arrange
        await SetupDatabase(
            135044,
            config =>
            {
                config.PlexServerCount = 2;
            }
        );

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

        // Act
        var action = () => Sut.Execute(context.Object);

        // Assert
        await action.ShouldNotThrowAsync();
        var update = context.Object.Result.ShouldBeOfType<JobStatusUpdate<CheckAllConnectionStatusUpdateDTO>>();
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
        dispatchedCommands.Count.ShouldBe(plexServerIds.Count);
        dispatchedCommands.ShouldAllBe(command => !command.EmitTelemetry);
    }

    [Test]
    public async Task ShouldCaptureNestedTerminalException_WhenScheduledConnectionCheckFails()
    {
        // Arrange
        await SetupDatabase(
            135045,
            config =>
            {
                config.PlexServerCount = 1;
            }
        );

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

        // Act
        await Sut.Execute(context.Object);

        // Assert
        Mock.Mock<ITelemetry>().Verify();
    }
    [Test]
    public async Task ShouldRethrowUnexpectedOuterException()
    {
        // Arrange
        var exception = new InvalidOperationException("Connection catalog unavailable");
        var database = new Mock<IReaparrDbContext>();
        database.SetupGet(x => x.PlexServers).Throws(exception);
        SetupDependencies(builder => builder.RegisterInstance(database.Object).As<IReaparrDbContext>());
        var context = SetupJobContext();

        // Act
        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => Sut.Execute(context.Object));

        // Assert
        thrown.ShouldBeSameAs(exception);
    }


}
