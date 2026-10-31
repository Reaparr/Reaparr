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
        var persistedBeforeCancellation = await dbContext
            .LibrarySyncJobQueues.AsNoTracking()
            .Select(x => new
            {
                x.PlexLibraryId,
                x.Status,
                x.CompletedAt,
                x.ErrorMessage,
                x.IsServerOffline,
            })
            .SingleAsync(CancellationToken);
        persistedBeforeCancellation.ShouldBe(
            new
            {
                PlexLibraryId = library.Id,
                Status = status,
                CompletedAt = (DateTime?)completedAt,
                ErrorMessage = (string?)"Retained sync history",
                IsServerOffline = true,
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync(new CancelLibrarySyncJobCommand(library.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var retainedHistory = await dbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        retainedHistory.Status.ShouldBe(status);
        retainedHistory.CompletedAt.ShouldBe(completedAt);
        retainedHistory.ErrorMessage.ShouldBe("Retained sync history");
        retainedHistory.IsServerOffline.ShouldBeTrue();
        Mock.Mock<IScheduler>().VerifyNoOtherCalls();
        Mock.Mock<INotificationHubService>().VerifyNoOtherCalls();
    }

    [Test]
    public async Task ShouldCancelProcessingJob_WhenQuartzInterruptCompletesExecution()
    {
        // Arrange
        await SetupDatabase(
            3104,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = dbContext.PlexLibraries.Single();
        var createdAt = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        var startedAt = createdAt.AddMinutes(1);
        await dbContext.LibrarySyncJobQueues.AddAsync(
            new LibrarySyncJobQueue
            {
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Processing,
                CreatedAt = createdAt,
                StartedAt = startedAt,
                ErrorMessage = "Stale processing details",
                IsServerOffline = true,
            },
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var jobKey = LibrarySyncJob.GetJobKey(library.PlexServerId, library.Id);

        Mock.Mock<IScheduler>()
            .Setup(x => x.Interrupt(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(types =>
                        types.Count == 1 && types[0] == RefreshDataType.PlexLibrarySyncStatus
                    )
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CancelLibrarySyncJobCommand(library.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var updatedQueueItem = await dbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        updatedQueueItem.PlexLibraryId.ShouldBe(library.Id);
        updatedQueueItem.Status.ShouldBe(LibrarySyncJobStatus.Cancelled);
        updatedQueueItem.CreatedAt.ShouldBe(createdAt);
        updatedQueueItem.StartedAt.ShouldBe(startedAt);
        updatedQueueItem.CompletedAt.ShouldNotBeNull();
        updatedQueueItem.ErrorMessage.ShouldBeNull();
        updatedQueueItem.IsServerOffline.ShouldBeFalse();
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldReturnCancellation_WhenCompletionWaitIsCancelled()
    {
        // Arrange
        await SetupDatabase(
            3106,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = dbContext.PlexLibraries.Single();
        var createdAt = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        var startedAt = createdAt.AddMinutes(1);
        await dbContext.LibrarySyncJobQueues.AddAsync(
            new LibrarySyncJobQueue
            {
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Processing,
                CreatedAt = createdAt,
                StartedAt = startedAt,
            },
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var jobKey = LibrarySyncJob.GetJobKey(library.PlexServerId, library.Id);

        Mock.Mock<IScheduler>()
            .Setup(x => x.Interrupt(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => throw new OperationCanceledException())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CancelLibrarySyncJobCommand(library.Id));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        var retainedQueueItem = await dbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        retainedQueueItem.Status.ShouldBe(LibrarySyncJobStatus.Processing);
        retainedQueueItem.CreatedAt.ShouldBe(createdAt);
        retainedQueueItem.StartedAt.ShouldBe(startedAt);
        retainedQueueItem.CompletedAt.ShouldBeNull();
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<INotificationHubService>().VerifyNoOtherCalls();
        Mock.Mock<ICommandExecutor>().VerifyNoOtherCalls();
    }

    [Test]
    public async Task ShouldNotCancelNewerProcessingExecution_WhenSuccessorClaimsSameKey()
    {
        // Arrange
        await SetupDatabase(
            3105,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = dbContext.PlexLibraries.Single();
        var createdAt = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        var startedAt = createdAt.AddMinutes(1);
        var successorCreatedAt = createdAt.AddMinutes(2);
        var successorStartedAt = successorCreatedAt.AddMinutes(1);
        await dbContext.LibrarySyncJobQueues.AddAsync(
            new LibrarySyncJobQueue
            {
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Processing,
                CreatedAt = createdAt,
                StartedAt = startedAt,
            },
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var jobKey = LibrarySyncJob.GetJobKey(library.PlexServerId, library.Id);

        Mock.Mock<IScheduler>()
            .Setup(x => x.Interrupt(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                dbContext
                    .LibrarySyncJobQueues.Where(x =>
                        x.PlexServerId == library.PlexServerId && x.PlexLibraryId == library.Id
                    )
                    .ExecuteUpdateAsync(
                        s =>
                            s.SetProperty(x => x.CreatedAt, successorCreatedAt)
                                .SetProperty(x => x.StartedAt, successorStartedAt),
                        CancellationToken.None
                    )
                    .GetAwaiter()
                    .GetResult();
                return new List<IJobExecutionContext>();
            })
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CancelLibrarySyncJobCommand(library.Id));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        var retainedExecution = await dbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        retainedExecution.Status.ShouldBe(LibrarySyncJobStatus.Processing);
        retainedExecution.CreatedAt.ShouldBe(successorCreatedAt);
        retainedExecution.StartedAt.ShouldBe(successorStartedAt);
        retainedExecution.CompletedAt.ShouldBeNull();
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<INotificationHubService>().VerifyNoOtherCalls();
        Mock.Mock<ICommandExecutor>().VerifyNoOtherCalls();
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
            CreatedAt = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc),
            ErrorMessage = "Stale queued details",
            IsServerOffline = true,
        };
        await dbContext.LibrarySyncJobQueues.AddAsync(queueItem, CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);
        var persistedBeforeCancellation = await dbContext
            .LibrarySyncJobQueues.AsNoTracking()
            .Select(x => new
            {
                x.PlexLibraryId,
                x.Status,
                x.CreatedAt,
                x.ErrorMessage,
                x.IsServerOffline,
            })
            .SingleAsync(CancellationToken);
        persistedBeforeCancellation.ShouldBe(
            new
            {
                PlexLibraryId = library.Id,
                Status = LibrarySyncJobStatus.Queued,
                CreatedAt = queueItem.CreatedAt,
                ErrorMessage = (string?)"Stale queued details",
                IsServerOffline = true,
            }
        );
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
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(types =>
                        types.Count == 1 && types[0] == RefreshDataType.PlexLibrarySyncStatus
                    )
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var updatedQueueItem = await dbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        updatedQueueItem.PlexLibraryId.ShouldBe(library.Id);
        updatedQueueItem.Status.ShouldBe(LibrarySyncJobStatus.Cancelled);
        updatedQueueItem.CreatedAt.ShouldBe(queueItem.CreatedAt);
        updatedQueueItem.StartedAt.ShouldBeNull();
        updatedQueueItem.CompletedAt.ShouldNotBeNull();
        updatedQueueItem.ErrorMessage.ShouldBeNull();
        updatedQueueItem.IsServerOffline.ShouldBeFalse();
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldNotCancelNewerProcessingExecution_WhenQueuedJobIsClaimedAfterDeletion()
    {
        // Arrange
        await SetupDatabase(
            3107,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = dbContext.PlexLibraries.Single();
        var createdAt = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        var startedAt = createdAt.AddMinutes(1);
        await dbContext.LibrarySyncJobQueues.AddAsync(
            new LibrarySyncJobQueue
            {
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Queued,
                CreatedAt = createdAt,
            },
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var jobKey = LibrarySyncJob.GetJobKey(library.PlexServerId, library.Id);

        Mock.Mock<IScheduler>()
            .SetupSequence(x => x.Interrupt(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(false);
        Mock.Mock<IScheduler>()
            .Setup(x => x.DeleteJob(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                dbContext
                    .LibrarySyncJobQueues.Where(x =>
                        x.PlexServerId == library.PlexServerId && x.PlexLibraryId == library.Id
                    )
                    .ExecuteUpdateAsync(
                        s =>
                            s.SetProperty(x => x.Status, LibrarySyncJobStatus.Processing)
                                .SetProperty(x => x.StartedAt, startedAt),
                        CancellationToken.None
                    )
                    .GetAwaiter()
                    .GetResult();
                return new List<IJobExecutionContext>();
            })
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CancelLibrarySyncJobCommand(library.Id));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        var retainedExecution = await dbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        retainedExecution.Status.ShouldBe(LibrarySyncJobStatus.Processing);
        retainedExecution.CreatedAt.ShouldBe(createdAt);
        retainedExecution.StartedAt.ShouldBe(startedAt);
        retainedExecution.CompletedAt.ShouldBeNull();
        Mock.Mock<IScheduler>().Verify(x => x.Interrupt(jobKey, It.IsAny<CancellationToken>()), Times.Exactly(2));
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<INotificationHubService>().VerifyNoOtherCalls();
        Mock.Mock<ICommandExecutor>().VerifyNoOtherCalls();
    }

    [Test]
    public async Task ShouldCancelProcessingJob_WhenDeletionRaceInterruptsExecution()
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
        var createdAt = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        var startedAt = createdAt.AddMinutes(1);
        var queueItem = new LibrarySyncJobQueue
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Processing,
            CreatedAt = createdAt,
            StartedAt = startedAt,
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
            .ReturnsAsync([])
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(types =>
                        types.Count == 1 && types[0] == RefreshDataType.PlexLibrarySyncStatus
                    )
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var updatedQueueItem = await dbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        updatedQueueItem.PlexLibraryId.ShouldBe(library.Id);
        updatedQueueItem.Status.ShouldBe(LibrarySyncJobStatus.Cancelled);
        updatedQueueItem.CreatedAt.ShouldBe(createdAt);
        updatedQueueItem.StartedAt.ShouldBe(startedAt);
        updatedQueueItem.CompletedAt.ShouldNotBeNull();
        updatedQueueItem.ErrorMessage.ShouldBeNull();
        updatedQueueItem.IsServerOffline.ShouldBeFalse();
        Mock.Mock<IScheduler>().Verify(x => x.Interrupt(jobKey, It.IsAny<CancellationToken>()), Times.Exactly(2));
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldCancelOnlyTargetedLibrary_AndAdvanceQueue()
    {
        // Arrange
        await SetupDatabase(
            3103,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = dbContext.PlexLibraries.OrderBy(x => x.Id).ToList();
        await dbContext.LibrarySyncJobQueues.AddRangeAsync(
            libraries
                .Select(
                    (library, index) =>
                        new LibrarySyncJobQueue
                        {
                            PlexServerId = library.PlexServerId,
                            PlexLibraryId = library.Id,
                            Priority = index + 1,
                            Status = LibrarySyncJobStatus.Queued,
                            CreatedAt = DateTime.UtcNow,
                        }
                )
                .ToList(),
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var targetLibrary = libraries[0];
        var retainedLibrary = libraries[1];
        var queueItemsBeforeCancellation = await dbContext
            .LibrarySyncJobQueues.AsNoTracking()
            .OrderBy(x => x.PlexLibraryId)
            .Select(x => new { x.PlexLibraryId, x.Status })
            .ToListAsync(CancellationToken);
        queueItemsBeforeCancellation.ShouldBe([
            new { PlexLibraryId = targetLibrary.Id, Status = LibrarySyncJobStatus.Queued },
            new { PlexLibraryId = retainedLibrary.Id, Status = LibrarySyncJobStatus.Queued },
        ]);
        var jobKey = LibrarySyncJob.GetJobKey(targetLibrary.PlexServerId, targetLibrary.Id);
        Mock.Mock<IScheduler>()
            .Setup(x => x.Interrupt(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IScheduler>()
            .Setup(x => x.DeleteJob(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(types =>
                        types.Count == 1 && types[0] == RefreshDataType.PlexLibrarySyncStatus
                    )
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CancelLibrarySyncJobCommand(targetLibrary.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var queueItems = await dbContext
            .LibrarySyncJobQueues.AsNoTracking()
            .OrderBy(x => x.PlexLibraryId)
            .Select(x => new { x.PlexLibraryId, x.Status })
            .ToListAsync(CancellationToken);
        queueItems.ShouldBe([
            new { PlexLibraryId = targetLibrary.Id, Status = LibrarySyncJobStatus.Cancelled },
            new { PlexLibraryId = retainedLibrary.Id, Status = LibrarySyncJobStatus.Queued },
        ]);
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
    }
}
