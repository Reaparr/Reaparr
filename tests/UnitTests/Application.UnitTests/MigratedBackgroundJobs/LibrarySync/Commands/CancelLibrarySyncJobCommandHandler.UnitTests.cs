using Quartz;

namespace Reaparr.Application.UnitTests;

public class CancelLibrarySyncJobCommandHandlerUnitTests : BaseCommandUnitTest<CancelLibrarySyncJobCommand>
{
    [Test]
    [Arguments(LibrarySyncJobStatus.Completed)]
    [Arguments(LibrarySyncJobStatus.Cancelled)]
    [Arguments(LibrarySyncJobStatus.Failed)]
    public async Task ShouldPreserveTerminalHistory_WhenCancelling(LibrarySyncJobStatus status)
    {
        // Arrange
        await SetupDatabase(
            3102,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = dbContext.PlexLibraries.Single();
        var completedAt = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        dbContext.LibrarySyncJobQueues.Add(
            new LibrarySyncJobQueue
            {
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = status,
                CreatedAt = completedAt.AddMinutes(-1),
                CompletedAt = completedAt,
                ErrorMessage = "Retained sync history",
                IsServerOffline = true,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CancelLibrarySyncJobCommand(library.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var retainedHistory = await dbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        retainedHistory.Status.ShouldBe(status);
        retainedHistory.CompletedAt.ShouldBe(completedAt);
        retainedHistory.ErrorMessage.ShouldBe("Retained sync history");
        retainedHistory.IsServerOffline.ShouldBeTrue();
        Mock.Mock<IScheduler>().VerifyNoOtherCalls();
        Mock.Mock<INotificationHubService>().VerifyNoOtherCalls();
    }

    [Test]
    public async Task ShouldCancelQueuedJob_WhenQuartzJobChangedStateBeforeDeletion()
    {
        // Arrange
        await SetupDatabase(
            3100,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = dbContext.PlexLibraries.Single();
        var queueItem = new LibrarySyncJobQueue
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Queued,
            CreatedAt = DateTime.UtcNow,
        };
        await dbContext.LibrarySyncJobQueues.AddAsync(queueItem, CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);
        var command = new CancelLibrarySyncJobCommand(library.Id);
        var jobKey = LibrarySyncJob.GetJobKey(library.PlexServerId, library.Id);

        Mock.Mock<IScheduler>()
            .Setup(x => x.Interrupt(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IScheduler>()
            .Setup(x => x.DeleteJob(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>().Setup(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var updatedQueueItem = await dbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        updatedQueueItem.Status.ShouldBe(LibrarySyncJobStatus.Cancelled);
        updatedQueueItem.CompletedAt.ShouldNotBeNull();
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldWaitForJobCompletion_WhenDeleteJobFailsButSecondInterruptSucceeds()
    {
        // Arrange
        await SetupDatabase(
            3101,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = dbContext.PlexLibraries.Single();
        var queueItem = new LibrarySyncJobQueue
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Processing,
            CreatedAt = DateTime.UtcNow,
        };
        await dbContext.LibrarySyncJobQueues.AddAsync(queueItem, CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);
        var command = new CancelLibrarySyncJobCommand(library.Id);
        var jobKey = LibrarySyncJob.GetJobKey(library.PlexServerId, library.Id);

        Mock.Mock<IScheduler>()
            .SetupSequence(x => x.Interrupt(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        Mock.Mock<IScheduler>()
            .Setup(x => x.DeleteJob(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var updatedQueueItem = await dbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        updatedQueueItem.Status.ShouldBe(LibrarySyncJobStatus.Processing);
        updatedQueueItem.CompletedAt.ShouldBeNull();
        Mock.Mock<IScheduler>().Verify(x => x.Interrupt(jobKey, It.IsAny<CancellationToken>()), Times.Exactly(2));
        Mock.Mock<IScheduler>().Verify(x => x.DeleteJob(jobKey, It.IsAny<CancellationToken>()), Times.Once());
        Mock.Mock<IScheduler>().Verify();
    }
}
