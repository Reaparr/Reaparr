using Quartz;
using Quartz.Impl.Matchers;

namespace Reaparr.Application.UnitTests;

public class CleanupLibrarySyncJobQueueCommandHandlerUnitTests : BaseCommandUnitTest<CleanupLibrarySyncJobQueueCommand>
{
    [Test]
    public async Task ShouldDeleteStaleJobsAndRequeueInterruptedRows_WhenRecoveringAtStartup()
    {
        // Arrange
        await SetupDatabase(
            2001,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 5;
            }
        );

        // Use a single context instance to ensure data is saved and visible
        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.SingleAsync(CancellationToken);
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var startedAt = createdAt.AddHours(1);
        var completedAt = createdAt.AddHours(2);

        await dbContext.LibrarySyncJobQueues.AddRangeAsync(
            [
                new LibrarySyncJobQueue
                {
                    PlexServerId = server.Id,
                    PlexLibraryId = libraries[0].Id,
                    Priority = 1,
                    Status = LibrarySyncJobStatus.Completed,
                    CreatedAt = createdAt,
                    CompletedAt = completedAt,
                    ForceMediaRefresh = true,
                },
                new LibrarySyncJobQueue
                {
                    PlexServerId = server.Id,
                    PlexLibraryId = libraries[1].Id,
                    Priority = 2,
                    Status = LibrarySyncJobStatus.Failed,
                    CreatedAt = createdAt,
                    StartedAt = startedAt,
                    CompletedAt = completedAt,
                    IsServerOffline = true,
                    ErrorMessage = "failed sync",
                    ForceMediaRefresh = true,
                },
                new LibrarySyncJobQueue
                {
                    PlexServerId = server.Id,
                    PlexLibraryId = libraries[2].Id,
                    Priority = 3,
                    Status = LibrarySyncJobStatus.Processing,
                    CreatedAt = createdAt,
                    StartedAt = startedAt,
                    IsServerOffline = true,
                },
                new LibrarySyncJobQueue
                {
                    PlexServerId = server.Id,
                    PlexLibraryId = libraries[3].Id,
                    Priority = 4,
                    Status = LibrarySyncJobStatus.Queued,
                    CreatedAt = createdAt,
                    ForceMediaRefresh = true,
                },
                new LibrarySyncJobQueue
                {
                    PlexServerId = server.Id,
                    PlexLibraryId = libraries[4].Id,
                    Priority = 5,
                    Status = LibrarySyncJobStatus.Cancelled,
                    CreatedAt = createdAt,
                    StartedAt = startedAt,
                    CompletedAt = completedAt,
                    IsServerOffline = true,
                    ErrorMessage = "user cancelled",
                    ForceMediaRefresh = true,
                },
            ],
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        // Verify the items were saved
        var before = await dbContext
            .LibrarySyncJobQueues.AsNoTracking()
            .OrderBy(x => x.PlexLibraryId)
            .Select(x => new
            {
                x.PlexLibraryId,
                x.Priority,
                x.Status,
                x.CreatedAt,
                x.StartedAt,
                x.CompletedAt,
                x.IsServerOffline,
                x.ErrorMessage,
                x.ForceMediaRefresh,
            })
            .ToListAsync(CancellationToken);
        before.Count.ShouldBe(5);
        before.Select(x => x.PlexLibraryId).ShouldBe(libraries.Select(x => x.Id).ToList());
        before
            .Select(x => x.Status)
            .ShouldBe([
                LibrarySyncJobStatus.Completed,
                LibrarySyncJobStatus.Failed,
                LibrarySyncJobStatus.Processing,
                LibrarySyncJobStatus.Queued,
                LibrarySyncJobStatus.Cancelled,
            ]);

        var staleJobKeys = new[]
        {
            LibrarySyncJob.GetJobKey(server.Id, libraries[1].Id),
            LibrarySyncJob.GetJobKey(server.Id, libraries[2].Id),
            new JobKey("orphaned-library-sync", nameof(JobTypes.LibrarySyncJob)),
        };
        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .Setup(x =>
                x.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(JobTypes.LibrarySyncJob)), CancellationToken)
            )
            .ReturnsAsync(staleJobKeys)
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.DeleteJobs(
                    It.Is<IReadOnlyCollection<JobKey>>(keys => keys.SequenceEqual(staleJobKeys)),
                    CancellationToken
                )
            )
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(new List<RefreshDataType> { RefreshDataType.PlexLibrarySyncStatus })
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CleanupLibrarySyncJobQueueCommand());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);

        // Use AsNoTracking to see ExecuteDeleteAsync and ExecuteUpdateAsync changes
        var after = await dbContext
            .LibrarySyncJobQueues.AsNoTracking()
            .OrderBy(x => x.PlexLibraryId)
            .Select(x => new
            {
                x.PlexLibraryId,
                x.Priority,
                x.Status,
                x.CreatedAt,
                x.StartedAt,
                x.CompletedAt,
                x.IsServerOffline,
                x.ErrorMessage,
                x.ForceMediaRefresh,
            })
            .ToListAsync(CancellationToken);
        // Completed items should be deleted, failed and processing items requeued, and queued/cancelled items retained.
        after.Count.ShouldBe(4);
        after
            .Select(x => x.PlexLibraryId)
            .ShouldBe([libraries[1].Id, libraries[2].Id, libraries[3].Id, libraries[4].Id]);

        var requeuedFailed = after.Single(x => x.PlexLibraryId == libraries[1].Id);
        requeuedFailed.Priority.ShouldBe(2);
        requeuedFailed.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        requeuedFailed.CreatedAt.ShouldBeGreaterThan(createdAt);
        requeuedFailed.StartedAt.ShouldBeNull();
        requeuedFailed.CompletedAt.ShouldBeNull();
        requeuedFailed.IsServerOffline.ShouldBeFalse();
        requeuedFailed.ErrorMessage.ShouldBeNull();
        requeuedFailed.ForceMediaRefresh.ShouldBeTrue();

        var requeuedProcessing = after.Single(x => x.PlexLibraryId == libraries[2].Id);
        requeuedProcessing.Priority.ShouldBe(3);
        requeuedProcessing.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        requeuedProcessing.CreatedAt.ShouldBeGreaterThan(createdAt);
        requeuedProcessing.StartedAt.ShouldBeNull();
        requeuedProcessing.CompletedAt.ShouldBeNull();
        requeuedProcessing.IsServerOffline.ShouldBeFalse();
        requeuedProcessing.ErrorMessage.ShouldBeNull();
        requeuedProcessing.ForceMediaRefresh.ShouldBeFalse();

        var remainingQueued = after.Single(x => x.PlexLibraryId == libraries[3].Id);
        remainingQueued.ShouldBe(
            new
            {
                PlexLibraryId = libraries[3].Id,
                Priority = 4,
                Status = LibrarySyncJobStatus.Queued,
                CreatedAt = createdAt,
                StartedAt = (DateTime?)null,
                CompletedAt = (DateTime?)null,
                IsServerOffline = false,
                ErrorMessage = (string?)null,
                ForceMediaRefresh = true,
            }
        );

        var remainingCancelled = after.Single(x => x.PlexLibraryId == libraries[4].Id);
        remainingCancelled.ShouldBe(
            new
            {
                PlexLibraryId = libraries[4].Id,
                Priority = 5,
                Status = LibrarySyncJobStatus.Cancelled,
                CreatedAt = createdAt,
                StartedAt = (DateTime?)startedAt,
                CompletedAt = (DateTime?)completedAt,
                IsServerOffline = true,
                ErrorMessage = (string?)"user cancelled",
                ForceMediaRefresh = true,
            }
        );

        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldReturnFailureAndLeaveQueueUnchanged_WhenDeletingStaleJobsReturnsFalse()
    {
        // Arrange
        await SetupDatabase(
            2002,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
            }
        );

        // Use a single context instance to ensure data is saved and visible
        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.SingleAsync(CancellationToken);
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var createdAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var startedAt = createdAt.AddHours(1);
        await dbContext.LibrarySyncJobQueues.AddRangeAsync(
            [
                new LibrarySyncJobQueue
                {
                    PlexServerId = server.Id,
                    PlexLibraryId = libraries[0].Id,
                    Priority = 1,
                    Status = LibrarySyncJobStatus.Failed,
                    CreatedAt = createdAt,
                    StartedAt = startedAt,
                    ErrorMessage = "failed sync",
                    ForceMediaRefresh = true,
                },
                new LibrarySyncJobQueue
                {
                    PlexServerId = server.Id,
                    PlexLibraryId = libraries[1].Id,
                    Priority = 2,
                    Status = LibrarySyncJobStatus.Processing,
                    CreatedAt = createdAt,
                    StartedAt = startedAt,
                    ForceMediaRefresh = false,
                },
            ],
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var staleJobKey = LibrarySyncJob.GetJobKey(server.Id, libraries[0].Id);
        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .Setup(x =>
                x.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(JobTypes.LibrarySyncJob)), CancellationToken)
            )
            .ReturnsAsync([staleJobKey])
            .Verifiable(Times.Once());
        scheduler
            .Setup(x =>
                x.DeleteJobs(
                    It.Is<IReadOnlyCollection<JobKey>>(keys => keys.SequenceEqual(new[] { staleJobKey })),
                    CancellationToken
                )
            )
            .ReturnsAsync(false)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CleanupLibrarySyncJobQueueCommand());

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);

        // Use AsNoTracking to inspect the persisted queue state after cleanup
        var after = await dbContext
            .LibrarySyncJobQueues.AsNoTracking()
            .OrderBy(x => x.PlexLibraryId)
            .Select(x => new
            {
                x.PlexLibraryId,
                x.Priority,
                x.Status,
                x.CreatedAt,
                x.StartedAt,
                x.CompletedAt,
                x.IsServerOffline,
                x.ErrorMessage,
                x.ForceMediaRefresh,
            })
            .ToListAsync(CancellationToken);
        after.ShouldBe([
            new
            {
                PlexLibraryId = libraries[0].Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Failed,
                CreatedAt = createdAt,
                StartedAt = (DateTime?)startedAt,
                CompletedAt = (DateTime?)null,
                IsServerOffline = false,
                ErrorMessage = (string?)"failed sync",
                ForceMediaRefresh = true,
            },
            new
            {
                PlexLibraryId = libraries[1].Id,
                Priority = 2,
                Status = LibrarySyncJobStatus.Processing,
                CreatedAt = createdAt,
                StartedAt = (DateTime?)startedAt,
                CompletedAt = (DateTime?)null,
                IsServerOffline = false,
                ErrorMessage = (string?)null,
                ForceMediaRefresh = false,
            },
        ]);

        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()), Times.Never());
    }

    [Test]
    public async Task ShouldNotifyAndLeaveQueueEmpty_WhenNoRowsOrStaleJobsExist()
    {
        // Arrange
        await SetupDatabase(
            2003,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var scheduler = Mock.Mock<IScheduler>();
        scheduler
            .Setup(x =>
                x.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(JobTypes.LibrarySyncJob)), CancellationToken)
            )
            .ReturnsAsync([])
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(new List<RefreshDataType> { RefreshDataType.PlexLibrarySyncStatus })
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CleanupLibrarySyncJobQueueCommand());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (await IDbContext.LibrarySyncJobQueues.AsNoTracking().ToListAsync(CancellationToken)).ShouldBeEmpty();

        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<IScheduler>()
            .Verify(
                x => x.DeleteJobs(It.IsAny<IReadOnlyCollection<JobKey>>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<INotificationHubService>().Verify();
    }
}
