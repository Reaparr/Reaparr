using Quartz;

namespace Reaparr.Application.UnitTests;

[NotInParallel]
public class LibrarySyncJobUnitTests : BaseUnitTest<LibrarySyncJob>
{
    public LibrarySyncJobUnitTests() =>
        SetupDependencies(builder =>
            builder
                .RegisterType<MediaOverviewRebuildCoordinator>()
                .As<IMediaOverviewRebuildCoordinator>()
                .SingleInstance()
        );

    private static IJobExecutionContext SetupJobContext(int serverId, int libraryId)
    {
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.JobDataMap).Returns(new LibrarySyncJobPayload(serverId, libraryId).ToJobDataMap());

        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context
            .SetupGet(x => x.MergedJobDataMap)
            .Returns(new LibrarySyncJobPayload(serverId, libraryId).ToJobDataMap());
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }

    [Test]
    public async Task ShouldResumeDelivery_WhenQueueItemIsAlreadyProcessing()
    {
        // Arrange
        await SetupDatabase(
            55100,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.FirstAsync(CancellationToken);
        var library = await dbContext.PlexLibraries.FirstAsync(CancellationToken);
        await dbContext.LibrarySyncJobQueues.AddAsync(
            new LibrarySyncJobQueue
            {
                PlexServerId = server.Id,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Processing,
                CreatedAt = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow,
            },
            CancellationToken
        );
        var savedCount = await dbContext.SaveChangesAsync(CancellationToken);
        savedCount.ShouldBeGreaterThan(0);

        var context = SetupJobContext(server.Id, library.Id);
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<RefreshLibraryMediaCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(library));
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        await Sut.Execute(context);

        // Assert
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<RefreshLibraryMediaCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldSkipRecoveredJob_WhenQueueItemWasCancelledDuringStartup()
    {
        // Arrange
        await SetupDatabase(
            55102,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.FirstAsync(CancellationToken);
        var library = await dbContext.PlexLibraries.FirstAsync(CancellationToken);
        var startedAt = DateTime.UtcNow.AddMinutes(-5);
        var completedAt = DateTime.UtcNow.AddMinutes(-1);
        await dbContext.LibrarySyncJobQueues.AddAsync(
            new LibrarySyncJobQueue
            {
                PlexServerId = server.Id,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Cancelled,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                StartedAt = startedAt,
                CompletedAt = completedAt,
            },
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var context = SetupJobContext(server.Id, library.Id);

        // Act
        await Sut.Execute(context);

        // Assert
        var queueItem = await dbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        queueItem.Status.ShouldBe(LibrarySyncJobStatus.Cancelled);
        queueItem.StartedAt.ShouldBe(startedAt);
        queueItem.CompletedAt.ShouldBe(completedAt);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<RefreshLibraryMediaCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()), Times.Never);
    }

    [Test]
    public async Task ShouldDequeueItself_WhenLibrarySyncFailsWithPlexUnauthorized()
    {
        // Arrange
        await SetupDatabase(
            55101,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.FirstAsync(CancellationToken);
        var library = await dbContext.PlexLibraries.FirstAsync(CancellationToken);
        var queueItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Processing,
            CreatedAt = DateTime.UtcNow,
        };

        await dbContext.LibrarySyncJobQueues.AddAsync(queueItem, CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<InvalidateLibraryComparisonJobsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<RefreshLibraryMediaCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<PlexLibrary>("Unauthorized").Add401UnauthorizedError());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask);
        Mock.Mock<IProgressHubService>()
            .Setup(x => x.SendJobStatusUpdateAsync(It.IsAny<JobStatusUpdate<LibrarySyncJobQueueDTO>>()))
            .Returns(Task.CompletedTask);

        // Act
        await Sut.Execute(SetupJobContext(server.Id, library.Id));

        // Assert
        var updatedQueueItem = await IDbContext.LibrarySyncJobQueues.FirstAsync(CancellationToken);
        updatedQueueItem.Status.ShouldBe(LibrarySyncJobStatus.Failed);
        updatedQueueItem.ErrorMessage.ShouldBe("Unauthorized");
        updatedQueueItem.CompletedAt.ShouldNotBeNull();
    }
}
