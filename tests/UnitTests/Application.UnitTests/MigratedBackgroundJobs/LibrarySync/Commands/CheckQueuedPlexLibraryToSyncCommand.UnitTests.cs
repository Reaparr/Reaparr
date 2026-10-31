using Quartz;

namespace Reaparr.Application.UnitTests;

public class CheckQueuedPlexLibraryToSyncCommandUnitTests : BaseCommandUnitTest<CheckQueuedPlexLibraryToSyncCommand>
{
    [Test]
    public async Task ShouldKeepOneProcessingLibrary_WhenConcurrentChecksSelectDifferentLibraries()
    {
        // Arrange
        await SetupDatabase(
            55200,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var firstLibrary = libraries[0];
        var secondLibrary = libraries[1];
        var createdAt = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
        dbContext.LibrarySyncJobQueues.AddRange(
            libraries.Select(
                (library, index) =>
                    new LibrarySyncJobQueue
                    {
                        PlexServerId = library.PlexServerId,
                        PlexLibraryId = library.Id,
                        Priority = index + 1,
                        Status = LibrarySyncJobStatus.Queued,
                        CreatedAt = createdAt,
                    }
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.IsSuccessful, true),
            CancellationToken
        );
        var before = await dbContext
            .LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId)
            .Select(x => new { x.PlexLibraryId, x.Status })
            .ToListAsync(CancellationToken);
        before.ShouldBe(
            libraries.Select(x => new { PlexLibraryId = x.Id, Status = LibrarySyncJobStatus.Queued }).ToList()
        );

        var firstKey = LibrarySyncJob.GetJobKey(firstLibrary.PlexServerId, firstLibrary.Id);
        var secondKey = LibrarySyncJob.GetJobKey(secondLibrary.PlexServerId, secondLibrary.Id);
        var firstCheckReachedClaim = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstCheck = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstKeyChecks = 0;
        Mock.Mock<IScheduler>()
            .Setup(x => x.CheckExists(firstKey, CancellationToken))
            .Returns(async () =>
            {
                if (Interlocked.Increment(ref firstKeyChecks) == 1)
                {
                    firstCheckReachedClaim.TrySetResult();
                    await releaseFirstCheck.Task.WaitAsync(CancellationToken);
                }

                return false;
            })
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.CheckExists(secondKey, CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IScheduler>()
            .Setup(x =>
                x.ScheduleJob(
                    It.Is<IJobDetail>(job =>
                        job.Key.Equals(secondKey)
                        && job.JobDataMap.GetPayload<LibrarySyncJobPayload>()
                            == new LibrarySyncJobPayload(secondLibrary.PlexServerId, secondLibrary.Id)
                    ),
                    It.Is<ITrigger>(trigger => trigger.JobKey.Equals(secondKey)),
                    CancellationToken
                )
            )
            .ReturnsAsync(DateTimeOffset.UtcNow)
            .Verifiable(Times.Once());

        // Act
        var firstCheck = TestHandlerExecuteAsync(new CheckQueuedPlexLibraryToSyncCommand());
        Result secondResult;
        try
        {
            await firstCheckReachedClaim.Task.WaitAsync(CancellationToken);
            await dbContext
                .LibrarySyncJobQueues.Where(x => x.PlexLibraryId == secondLibrary.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Priority, 0), CancellationToken);
            secondResult = await TestHandlerExecuteAsync(new CheckQueuedPlexLibraryToSyncCommand());
        }
        finally
        {
            releaseFirstCheck.TrySetResult();
        }

        var firstResult = await firstCheck;

        // Assert
        firstResult.IsSuccess.ShouldBeTrue();
        firstResult.Errors.Count.ShouldBe(0);
        secondResult.IsSuccess.ShouldBeTrue();
        secondResult.Errors.Count.ShouldBe(0);
        var after = await dbContext
            .LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId)
            .Select(x => new
            {
                x.PlexLibraryId,
                x.Status,
                x.StartedAt,
            })
            .ToListAsync(CancellationToken);
        after
            .Select(x => new { x.PlexLibraryId, x.Status })
            .ShouldBe([
                new { PlexLibraryId = firstLibrary.Id, Status = LibrarySyncJobStatus.Queued },
                new { PlexLibraryId = secondLibrary.Id, Status = LibrarySyncJobStatus.Processing },
            ]);
        after[0].StartedAt.ShouldBeNull();
        after[1].StartedAt.ShouldNotBeNull();
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<IScheduler>()
            .Verify(
                x =>
                    x.ScheduleJob(
                        It.Is<IJobDetail>(job => job.Key.Equals(firstKey)),
                        It.IsAny<ITrigger>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldScheduleHighestPriorityLibraryForEachServer_WhenLibrariesAreQueued()
    {
        // Arrange
        await SetupDatabase(
            55201,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 2;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext
            .PlexLibraries.OrderBy(x => x.PlexServerId)
            .ThenBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var selectedIds = libraries.GroupBy(x => x.PlexServerId).Select(x => x.Last().Id).ToList();
        var createdAt = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
        dbContext.LibrarySyncJobQueues.AddRange(
            libraries.Select(library => new LibrarySyncJobQueue
            {
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Priority = selectedIds.Contains(library.Id) ? 1 : 2,
                Status = LibrarySyncJobStatus.Queued,
                CreatedAt = createdAt,
                IsServerOffline = true,
                ForceMediaRefresh = selectedIds.Contains(library.Id),
            })
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.IsSuccessful, true),
            CancellationToken
        );
        var before = await dbContext
            .LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId)
            .Select(x => new
            {
                x.PlexLibraryId,
                x.Status,
                x.IsServerOffline,
            })
            .ToListAsync(CancellationToken);
        before.ShouldBe(
            libraries
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    PlexLibraryId = x.Id,
                    Status = LibrarySyncJobStatus.Queued,
                    IsServerOffline = true,
                })
                .ToList()
        );

        foreach (var library in libraries.Where(x => selectedIds.Contains(x.Id)))
        {
            var jobKey = LibrarySyncJob.GetJobKey(library.PlexServerId, library.Id);
            Mock.Mock<IScheduler>()
                .Setup(x => x.CheckExists(jobKey, CancellationToken))
                .ReturnsAsync(false)
                .Verifiable(Times.Exactly(2));
            Mock.Mock<IScheduler>()
                .Setup(x =>
                    x.ScheduleJob(
                        It.Is<IJobDetail>(job =>
                            job.Key.Equals(jobKey)
                            && job.JobDataMap.GetPayload<LibrarySyncJobPayload>()
                                == new LibrarySyncJobPayload(library.PlexServerId, library.Id, true)
                        ),
                        It.Is<ITrigger>(trigger => trigger.JobKey.Equals(jobKey)),
                        CancellationToken
                    )
                )
                .ReturnsAsync(DateTimeOffset.UtcNow)
                .Verifiable(Times.Once());
        }

        // Act
        var result = await TestHandlerExecuteAsync(new CheckQueuedPlexLibraryToSyncCommand());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = await dbContext
            .LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId)
            .Select(x => new
            {
                x.PlexLibraryId,
                x.Status,
                x.IsServerOffline,
                x.StartedAt,
            })
            .ToListAsync(CancellationToken);
        after
            .Select(x => new
            {
                x.PlexLibraryId,
                x.Status,
                x.IsServerOffline,
            })
            .ShouldBe(
                libraries
                    .OrderBy(x => x.Id)
                    .Select(x => new
                    {
                        PlexLibraryId = x.Id,
                        Status = selectedIds.Contains(x.Id)
                            ? LibrarySyncJobStatus.Processing
                            : LibrarySyncJobStatus.Queued,
                        IsServerOffline = false,
                    })
                    .ToList()
            );
        foreach (var item in after)
        {
            if (selectedIds.Contains(item.PlexLibraryId))
                item.StartedAt.ShouldNotBeNull();
            else
                item.StartedAt.ShouldBeNull();
        }

        Mock.Mock<IScheduler>().Verify();
        foreach (var library in libraries.Where(x => !selectedIds.Contains(x.Id)))
        {
            var jobKey = LibrarySyncJob.GetJobKey(library.PlexServerId, library.Id);
            Mock.Mock<IScheduler>().Verify(x => x.CheckExists(jobKey, It.IsAny<CancellationToken>()), Times.Never());
        }
    }

    [Test]
    public async Task ShouldKeepQueuedLibrariesWaiting_WhenServerIsOffline()
    {
        // Arrange
        await SetupDatabase(
            55202,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        dbContext.LibrarySyncJobQueues.AddRange(
            libraries.Select(library => new LibrarySyncJobQueue
            {
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Queued,
                CreatedAt = DateTime.UtcNow,
            })
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.IsSuccessful, false),
            CancellationToken
        );
        (
            await dbContext
                .LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId)
                .Select(x => x.PlexLibraryId)
                .ToListAsync(CancellationToken)
        ).ShouldBe(libraries.Select(x => x.Id).ToList());

        // Act
        var result = await TestHandlerExecuteAsync(new CheckQueuedPlexLibraryToSyncCommand());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = await dbContext
            .LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId)
            .Select(x => new
            {
                x.PlexLibraryId,
                x.Status,
                x.IsServerOffline,
                x.StartedAt,
            })
            .ToListAsync(CancellationToken);
        after.ShouldBe(
            libraries
                .Select(x => new
                {
                    PlexLibraryId = x.Id,
                    Status = LibrarySyncJobStatus.Queued,
                    IsServerOffline = true,
                    StartedAt = (DateTime?)null,
                })
                .ToList()
        );
        Mock.Mock<IScheduler>()
            .Verify(
                x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldPreserveProcessingAndQueuedRows_WhenServerAlreadyHasAnActiveLibrary()
    {
        // Arrange
        await SetupDatabase(
            55203,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var startedAt = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
        dbContext.LibrarySyncJobQueues.AddRange(
            libraries.Select(
                (library, index) =>
                    new LibrarySyncJobQueue
                    {
                        PlexServerId = library.PlexServerId,
                        PlexLibraryId = library.Id,
                        Priority = 1,
                        Status = index == 0 ? LibrarySyncJobStatus.Processing : LibrarySyncJobStatus.Queued,
                        CreatedAt = startedAt.AddMinutes(-1),
                        StartedAt = index == 0 ? startedAt : null,
                    }
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.IsSuccessful, true),
            CancellationToken
        );
        var before = await dbContext
            .LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId)
            .Select(x => new
            {
                x.PlexLibraryId,
                x.Status,
                x.StartedAt,
            })
            .ToListAsync(CancellationToken);
        before.ShouldBe([
            new
            {
                PlexLibraryId = libraries[0].Id,
                Status = LibrarySyncJobStatus.Processing,
                StartedAt = (DateTime?)startedAt,
            },
            new
            {
                PlexLibraryId = libraries[1].Id,
                Status = LibrarySyncJobStatus.Queued,
                StartedAt = (DateTime?)null,
            },
        ]);

        // Act
        var result = await TestHandlerExecuteAsync(new CheckQueuedPlexLibraryToSyncCommand());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (
            await dbContext
                .LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId)
                .Select(x => new
                {
                    x.PlexLibraryId,
                    x.Status,
                    x.StartedAt,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(before);
        Mock.Mock<IScheduler>()
            .Verify(x => x.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<IScheduler>()
            .Verify(
                x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ShouldReleaseClaim_WhenSchedulingFails(bool failsDuringLookup, bool cancelled)
    {
        // Arrange
        await SetupDatabase(
            55204,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        dbContext.LibrarySyncJobQueues.Add(
            new LibrarySyncJobQueue
            {
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Queued,
                CreatedAt = DateTime.UtcNow,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.IsSuccessful, true),
            CancellationToken
        );
        var before = await dbContext
            .LibrarySyncJobQueues.Select(x => new
            {
                x.PlexLibraryId,
                x.Status,
                x.StartedAt,
            })
            .SingleAsync(CancellationToken);
        before.ShouldBe(
            new
            {
                PlexLibraryId = library.Id,
                Status = LibrarySyncJobStatus.Queued,
                StartedAt = (DateTime?)null,
            }
        );
        var jobKey = LibrarySyncJob.GetJobKey(library.PlexServerId, library.Id);
        var lookupCount = 0;
        Mock.Mock<IScheduler>()
            .Setup(x => x.CheckExists(jobKey, CancellationToken))
            .Returns(() =>
            {
                if (++lookupCount == 2 && failsDuringLookup)
                    return Task.FromException<bool>(
                        cancelled
                            ? new OperationCanceledException()
                            : new InvalidOperationException("Scheduler unavailable")
                    );
                return Task.FromResult(false);
            })
            .Verifiable(Times.Exactly(2));
        if (!failsDuringLookup)
            Mock.Mock<IScheduler>()
                .Setup(x =>
                    x.ScheduleJob(
                        It.Is<IJobDetail>(job => job.Key.Equals(jobKey)),
                        It.Is<ITrigger>(trigger => trigger.JobKey.Equals(jobKey)),
                        CancellationToken
                    )
                )
                .ThrowsAsync(new InvalidOperationException("Scheduler unavailable"))
                .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CheckQueuedPlexLibraryToSyncCommand());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (
            await dbContext
                .LibrarySyncJobQueues.Select(x => new
                {
                    x.PlexLibraryId,
                    x.Status,
                    x.StartedAt,
                })
                .SingleAsync(CancellationToken)
        ).ShouldBe(before);
        Mock.Mock<IScheduler>().Verify();
        if (failsDuringLookup)
            Mock.Mock<IScheduler>()
                .Verify(
                    x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()),
                    Times.Never()
                );
    }

    [Test]
    public async Task ShouldLeaveQueueUnclaimed_WhenQuartzAlreadyHasARunnableJob()
    {
        // Arrange
        await SetupDatabase(
            55205,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        dbContext.LibrarySyncJobQueues.Add(
            new LibrarySyncJobQueue
            {
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Queued,
                CreatedAt = DateTime.UtcNow,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.IsSuccessful, true),
            CancellationToken
        );
        var before = await dbContext
            .LibrarySyncJobQueues.Select(x => new
            {
                x.PlexLibraryId,
                x.Status,
                x.StartedAt,
            })
            .SingleAsync(CancellationToken);
        before.ShouldBe(
            new
            {
                PlexLibraryId = library.Id,
                Status = LibrarySyncJobStatus.Queued,
                StartedAt = (DateTime?)null,
            }
        );
        var jobKey = LibrarySyncJob.GetJobKey(library.PlexServerId, library.Id);
        var trigger = TriggerBuilder.Create().WithIdentity(jobKey.Name, jobKey.Group).ForJob(jobKey).Build();
        Mock.Mock<IScheduler>()
            .Setup(x => x.CheckExists(jobKey, CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetTriggersOfJob(jobKey, CancellationToken))
            .ReturnsAsync([trigger])
            .Verifiable(Times.Once());
        Mock.Mock<IScheduler>()
            .Setup(x => x.GetTriggerState(trigger.Key, CancellationToken))
            .ReturnsAsync(TriggerState.Normal)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CheckQueuedPlexLibraryToSyncCommand());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (
            await dbContext
                .LibrarySyncJobQueues.Select(x => new
                {
                    x.PlexLibraryId,
                    x.Status,
                    x.StartedAt,
                })
                .SingleAsync(CancellationToken)
        ).ShouldBe(before);
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<IScheduler>()
            .Verify(
                x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldAvoidScheduling_WhenThereAreNoQueuedLibraries()
    {
        // Arrange
        await SetupDatabase(55206);
        var dbContext = IDbContext;
        (await dbContext.LibrarySyncJobQueues.ToListAsync(CancellationToken)).ShouldBeEmpty();

        // Act
        var result = await TestHandlerExecuteAsync(new CheckQueuedPlexLibraryToSyncCommand());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (await dbContext.LibrarySyncJobQueues.ToListAsync(CancellationToken)).ShouldBeEmpty();
        Mock.Mock<IScheduler>()
            .Verify(
                x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldReleaseClaim_WhenExistingJobPreventsScheduling()
    {
        // Arrange
        await SetupDatabase(
            55207,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var createdAt = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        dbContext.LibrarySyncJobQueues.Add(
            new LibrarySyncJobQueue
            {
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Queued,
                CreatedAt = createdAt,
                ForceMediaRefresh = true,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.IsSuccessful, true),
            CancellationToken
        );
        var jobKey = LibrarySyncJob.GetJobKey(library.PlexServerId, library.Id);
        var lookupCount = 0;
        Mock.Mock<IScheduler>()
            .Setup(x => x.CheckExists(jobKey, CancellationToken))
            .ReturnsAsync(() => ++lookupCount == 2)
            .Verifiable(Times.Exactly(2));

        // Act
        var result = await TestHandlerExecuteAsync(new CheckQueuedPlexLibraryToSyncCommand());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = await dbContext.LibrarySyncJobQueues.SingleAsync(CancellationToken);
        after.PlexLibraryId.ShouldBe(library.Id);
        after.PlexServerId.ShouldBe(library.PlexServerId);
        after.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        after.StartedAt.ShouldBeNull();
        after.CompletedAt.ShouldBeNull();
        after.CreatedAt.ShouldBe(createdAt);
        after.ForceMediaRefresh.ShouldBeTrue();
        Mock.Mock<IScheduler>().Verify();
        Mock.Mock<IScheduler>()
            .Verify(
                x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }
}
