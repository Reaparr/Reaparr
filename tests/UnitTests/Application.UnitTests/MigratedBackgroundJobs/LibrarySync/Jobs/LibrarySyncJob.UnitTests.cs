using Quartz;

namespace Reaparr.Application.UnitTests;

[NotInParallel]
public class LibrarySyncJobUnitTests : BaseUnitTest<LibrarySyncJob>
{
    private static Mock<IJobExecutionContext> SetupJobContext(
        int serverId,
        int libraryId,
        CancellationToken cancellationToken = default,
        bool forceMediaRefresh = false
    )
    {
        var payload = new LibrarySyncJobPayload(serverId, libraryId, forceMediaRefresh).ToJobDataMap();
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.JobDataMap).Returns(payload);
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(x => x.MergedJobDataMap).Returns(payload);
        context.SetupGet(x => x.CancellationToken).Returns(cancellationToken);
        context.SetupProperty(x => x.Result);
        return context;
    }

    [Test]
    [Arguments(LibrarySyncJobStatus.Completed, false)]
    [Arguments(LibrarySyncJobStatus.Failed, false)]
    [Arguments(LibrarySyncJobStatus.Cancelled, false)]
    [Arguments(LibrarySyncJobStatus.Failed, true)]
    public async Task ShouldPersistOutcomeAndContinueQueue_WhenRefreshEnds(
        LibrarySyncJobStatus expectedStatus,
        bool throws
    )
    {
        // Arrange
        SetupDependencies(builder =>
            builder
                .RegisterType<MediaOverviewRebuildCoordinator>()
                .As<IMediaOverviewRebuildCoordinator>()
                .SingleInstance()
        );
        await SetupDatabase(
            55100,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var library = libraries[0];
        var nextLibrary = libraries[1];
        var createdAt = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        dbContext.LibrarySyncJobQueues.AddRange(
            libraries.Select(
                (item, index) =>
                    new LibrarySyncJobQueue
                    {
                        PlexServerId = item.PlexServerId,
                        PlexLibraryId = item.Id,
                        Priority = index + 1,
                        Status = index == 0 ? LibrarySyncJobStatus.Processing : LibrarySyncJobStatus.Queued,
                        CreatedAt = createdAt,
                        ForceMediaRefresh = index == 0,
                    }
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.IsSuccessful, true),
            CancellationToken
        );
        var before = await dbContext
            .LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId)
            .Select(x => new { x.PlexLibraryId, x.Status })
            .ToListAsync(CancellationToken);
        before.ShouldBe([
            new { PlexLibraryId = library.Id, Status = LibrarySyncJobStatus.Processing },
            new { PlexLibraryId = nextLibrary.Id, Status = LibrarySyncJobStatus.Queued },
        ]);
        var context = SetupJobContext(library.PlexServerId, library.Id, forceMediaRefresh: true);
        var refreshResult = expectedStatus switch
        {
            LibrarySyncJobStatus.Completed => Result.Ok(library),
            LibrarySyncJobStatus.Cancelled => Result.Fail<PlexLibrary>(
                new ExceptionalError(new OperationCanceledException())
            ),
            _ => Result.Fail<PlexLibrary>("Unauthorized").Add401UnauthorizedError(),
        };
        var refreshSetup = Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshLibraryMediaCommand>(c => c.PlexLibraryId == library.Id && c.ForceMediaRefresh),
                    It.IsAny<CancellationToken>()
                )
            );
        if (throws)
            refreshSetup.ThrowsAsync(new InvalidOperationException("Refresh failed")).Verifiable(Times.Once());
        else
            refreshSetup.ReturnsAsync(refreshResult).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(),
                    It.Is<CancellationToken>(token => token.CanBeCanceled && !token.IsCancellationRequested)
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(types =>
                        types.SequenceEqual(
                            new[] { RefreshDataType.PlexLibrary, RefreshDataType.PlexLibrarySyncStatus }
                        )
                    )
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        await ExecuteWithListenerAsync(context.Object);

        // Assert
        var after = await dbContext.LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        after
            .Select(x => new { x.PlexLibraryId, x.Status })
            .ShouldBe([
                new { PlexLibraryId = library.Id, Status = expectedStatus },
                new { PlexLibraryId = nextLibrary.Id, Status = LibrarySyncJobStatus.Queued },
            ]);
        after[0].PlexServerId.ShouldBe(library.PlexServerId);
        after[0].StartedAt.ShouldNotBeNull();
        after[0].CompletedAt.ShouldNotBeNull();
        after[0]
            .ErrorMessage.ShouldBe(
                expectedStatus == LibrarySyncJobStatus.Failed
                    ? throws
                        ? "Refresh failed"
                        : "Unauthorized"
                    : null
            );
        after[0].IsServerOffline.ShouldBeFalse();
        after[0].ForceMediaRefresh.ShouldBeTrue();
        after[1].StartedAt.ShouldBeNull();
        after[1].CompletedAt.ShouldBeNull();
        after[1].CreatedAt.ShouldBe(createdAt);
        var outcome = context.Object.Result.ShouldBeOfType<BackgroundJobResult>();
        outcome.Status.ShouldBe(
            expectedStatus switch
            {
                LibrarySyncJobStatus.Completed => JobStatus.Completed,
                LibrarySyncJobStatus.Cancelled => JobStatus.Cancelled,
                _ => JobStatus.Failed,
            }
        );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldCompleteIndependentLibrary_WhenOverviewRebuildWaitsForAnotherServer()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        SetupDependencies(builder =>
            builder.RegisterInstance(coordinator).As<IMediaOverviewRebuildCoordinator>().SingleInstance()
        );
        await SetupDatabase(
            55108,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var library = libraries[0];
        var otherLibrary = libraries[1];
        library.PlexServerId.ShouldNotBe(otherLibrary.PlexServerId);
        var startedAt = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        dbContext.LibrarySyncJobQueues.AddRange(
            libraries.Select(item => new LibrarySyncJobQueue
            {
                PlexServerId = item.PlexServerId,
                PlexLibraryId = item.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Processing,
                CreatedAt = startedAt,
                StartedAt = startedAt,
            })
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.IsSuccessful, true),
            CancellationToken
        );
        var before = await dbContext
            .LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId)
            .Select(x => new { x.PlexLibraryId, x.Status })
            .ToListAsync(CancellationToken);
        before.ShouldBe([
            new { PlexLibraryId = library.Id, Status = LibrarySyncJobStatus.Processing },
            new { PlexLibraryId = otherLibrary.Id, Status = LibrarySyncJobStatus.Processing },
        ]);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var otherSyncLease = await coordinator.AcquireLibrarySyncLeaseAsync(cancellation.Token);
        var rebuildTask = coordinator.AcquireRebuildLeaseAsync(cancellation.Token);
        rebuildTask.IsCompleted.ShouldBeFalse();
        var context = SetupJobContext(library.PlexServerId, library.Id, cancellation.Token);
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshLibraryMediaCommand>(c => c.PlexLibraryId == library.Id && !c.ForceMediaRefresh),
                    cancellation.Token
                )
            )
            .ReturnsAsync(Result.Ok(library))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(),
                    It.Is<CancellationToken>(token => token != cancellation.Token && !token.IsCancellationRequested)
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(types =>
                        types.SequenceEqual(
                            new[] { RefreshDataType.PlexLibrary, RefreshDataType.PlexLibrarySyncStatus }
                        )
                    )
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        await ExecuteWithListenerAsync(context.Object).WaitAsync(cancellation.Token);

        // Assert
        var after = await dbContext.LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        after
            .Select(x => new { x.PlexLibraryId, x.Status })
            .ShouldBe([
                new { PlexLibraryId = library.Id, Status = LibrarySyncJobStatus.Completed },
                new { PlexLibraryId = otherLibrary.Id, Status = LibrarySyncJobStatus.Processing },
            ]);
        after[0].StartedAt.ShouldNotBe(startedAt);
        after[0].CompletedAt.ShouldNotBeNull();
        after[0].ErrorMessage.ShouldBeNull();
        after[1].StartedAt.ShouldBe(startedAt);
        after[1].CompletedAt.ShouldBeNull();
        context.Object.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Completed);
        rebuildTask.IsCompleted.ShouldBeFalse();
        // ReSharper disable once DisposeOnUsingVariable
        otherSyncLease.Dispose();
        using var rebuildLease = await rebuildTask.WaitAsync(cancellation.Token);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<RefreshLibraryMediaCommand>(c => c.PlexLibraryId == otherLibrary.Id),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldCancelAndContinueQueue_WhenInterruptedWhileWaitingForOverviewLease()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        using var rebuildLease = await coordinator.AcquireRebuildLeaseAsync(CancellationToken);
        var leaseRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await SetupDatabase(
            55103,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var library = libraries[0];
        var nextLibrary = libraries[1];
        var startedAt = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        dbContext.LibrarySyncJobQueues.AddRange(
            libraries.Select(
                (item, index) =>
                    new LibrarySyncJobQueue
                    {
                        PlexServerId = item.PlexServerId,
                        PlexLibraryId = item.Id,
                        Priority = index + 1,
                        Status = index == 0 ? LibrarySyncJobStatus.Processing : LibrarySyncJobStatus.Queued,
                        CreatedAt = startedAt,
                        StartedAt = index == 0 ? startedAt : null,
                    }
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.IsSuccessful, true),
            CancellationToken
        );
        using var cancellation = new CancellationTokenSource();
        var context = SetupJobContext(library.PlexServerId, library.Id, cancellation.Token);
        Mock.Mock<IMediaOverviewRebuildCoordinator>()
            .Setup(x => x.AcquireLibrarySyncLeaseAsync(cancellation.Token))
            .Returns(
                (CancellationToken token) =>
                {
                    var wait = coordinator.AcquireLibrarySyncLeaseAsync(token);
                    leaseRequested.SetResult();
                    return wait;
                }
            )
            .Verifiable(Times.Once());
        List<LibrarySyncJobStatus>? statusesAtDispatch = null;
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(),
                    It.Is<CancellationToken>(token => token != cancellation.Token && !token.IsCancellationRequested)
                )
            )
            .Returns(async () =>
            {
                statusesAtDispatch = await dbContext
                    .LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId)
                    .Select(x => x.Status)
                    .ToListAsync(CancellationToken);
                return Result.Ok();
            })
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(types =>
                        types.SequenceEqual(
                            new[] { RefreshDataType.PlexLibrary, RefreshDataType.PlexLibrarySyncStatus }
                        )
                    )
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var execution = ExecuteWithListenerAsync(context.Object);
        var firstCompleted = await Task.WhenAny(execution, leaseRequested.Task).WaitAsync(CancellationToken);
        firstCompleted.ShouldBe(leaseRequested.Task);
        execution.IsCompleted.ShouldBeFalse();
        cancellation.Cancel();
        await execution.WaitAsync(CancellationToken);

        // Assert
        statusesAtDispatch.ShouldBe([LibrarySyncJobStatus.Cancelled, LibrarySyncJobStatus.Queued]);
        var after = await dbContext.LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        after
            .Select(x => new { x.PlexLibraryId, x.Status })
            .ShouldBe([
                new { PlexLibraryId = library.Id, Status = LibrarySyncJobStatus.Cancelled },
                new { PlexLibraryId = nextLibrary.Id, Status = LibrarySyncJobStatus.Queued },
            ]);
        after[0].StartedAt.ShouldBe(startedAt);
        after[0].CompletedAt.ShouldNotBeNull();
        after[0].ErrorMessage.ShouldBeNull();
        after[0].IsServerOffline.ShouldBeFalse();
        after[1].StartedAt.ShouldBeNull();
        after[1].CompletedAt.ShouldBeNull();
        context.Object.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Cancelled);
        Mock.Mock<IMediaOverviewRebuildCoordinator>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<RefreshLibraryMediaCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldContinueQueue_WhenNotificationFails(bool cancelled)
    {
        // Arrange
        SetupDependencies(builder =>
            builder
                .RegisterType<MediaOverviewRebuildCoordinator>()
                .As<IMediaOverviewRebuildCoordinator>()
                .SingleInstance()
        );
        await SetupDatabase(
            55104,
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
                Status = LibrarySyncJobStatus.Processing,
                CreatedAt = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc),
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.IsSuccessful, true),
            CancellationToken
        );
        var context = SetupJobContext(library.PlexServerId, library.Id);
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<RefreshLibraryMediaCommand>(c => c.PlexLibraryId == library.Id && !c.ForceMediaRefresh),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                cancelled
                    ? Result.Fail<PlexLibrary>(new ExceptionalError(new OperationCanceledException()))
                    : Result.Ok(library)
            )
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .ThrowsAsync(new InvalidOperationException("Notification failed"))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(),
                    It.Is<CancellationToken>(token => token.CanBeCanceled && !token.IsCancellationRequested)
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        await ExecuteWithListenerAsync(context.Object);

        // Assert
        var after = await dbContext.LibrarySyncJobQueues.SingleAsync(CancellationToken);
        after.PlexLibraryId.ShouldBe(library.Id);
        after.Status.ShouldBe(cancelled ? LibrarySyncJobStatus.Cancelled : LibrarySyncJobStatus.Completed);
        after.CompletedAt.ShouldNotBeNull();
        after.ErrorMessage.ShouldBeNull();
        context
            .Object.Result.ShouldBeOfType<BackgroundJobResult>()
            .Status.ShouldBe(cancelled ? JobStatus.Cancelled : JobStatus.Completed);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    [Arguments(LibrarySyncJobStatus.Cancelled)]
    [Arguments(LibrarySyncJobStatus.Queued)]
    [Arguments(LibrarySyncJobStatus.Completed)]
    public async Task ShouldPreserveQueueState_WhenDeliveryIsNoLongerProcessing(LibrarySyncJobStatus status)
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
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var startedAt = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        var completedAt = startedAt.AddMinutes(5);
        dbContext.LibrarySyncJobQueues.Add(
            new LibrarySyncJobQueue
            {
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = status,
                CreatedAt = startedAt,
                StartedAt = startedAt,
                CompletedAt = completedAt,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var context = SetupJobContext(library.PlexServerId, library.Id);

        // Act
        await Sut.Execute(context.Object);

        // Assert
        var after = await dbContext.LibrarySyncJobQueues.SingleAsync(CancellationToken);
        after.PlexLibraryId.ShouldBe(library.Id);
        after.Status.ShouldBe(status);
        after.StartedAt.ShouldBe(startedAt);
        after.CompletedAt.ShouldBe(completedAt);
        context.Object.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Cancelled);
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
    public async Task ShouldPersistFailureAndContinueQueue_WhenLeaseAcquisitionThrows()
    {
        // Arrange
        await SetupDatabase(
            55105,
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
                Status = LibrarySyncJobStatus.Processing,
                CreatedAt = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc),
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.IsSuccessful, true),
            CancellationToken
        );
        var context = SetupJobContext(library.PlexServerId, library.Id);
        Mock.Mock<IMediaOverviewRebuildCoordinator>()
            .Setup(x => x.AcquireLibrarySyncLeaseAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Lease failed"))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(),
                    It.Is<CancellationToken>(token => !token.IsCancellationRequested)
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(types =>
                        types.SequenceEqual(
                            new[] { RefreshDataType.PlexLibrary, RefreshDataType.PlexLibrarySyncStatus }
                        )
                    )
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        await ExecuteWithListenerAsync(context.Object);

        // Assert
        var after = await dbContext.LibrarySyncJobQueues.SingleAsync(CancellationToken);
        after.PlexLibraryId.ShouldBe(library.Id);
        after.Status.ShouldBe(LibrarySyncJobStatus.Failed);
        after.CompletedAt.ShouldNotBeNull();
        after.ErrorMessage.ShouldBe("Lease failed");
        context
            .Object.Result.ShouldBeOfType<BackgroundJobResult>()
            .ShouldBe(new BackgroundJobResult(JobStatus.Failed, "Lease failed"));
        Mock.Mock<IMediaOverviewRebuildCoordinator>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<RefreshLibraryMediaCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldRequeueWithoutStartingMediaSync_WhenServerIsOffline()
    {
        // Arrange
        await SetupDatabase(
            55106,
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
                Status = LibrarySyncJobStatus.Processing,
                CreatedAt = createdAt,
                StartedAt = createdAt,
                ForceMediaRefresh = true,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.IsSuccessful, false),
            CancellationToken
        );
        var context = SetupJobContext(library.PlexServerId, library.Id);
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(),
                    It.Is<CancellationToken>(token => !token.IsCancellationRequested)
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(types =>
                        types.SequenceEqual(
                            new[] { RefreshDataType.PlexLibrary, RefreshDataType.PlexLibrarySyncStatus }
                        )
                    )
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        await ExecuteWithListenerAsync(context.Object);

        // Assert
        var after = await dbContext.LibrarySyncJobQueues.SingleAsync(CancellationToken);
        after.PlexLibraryId.ShouldBe(library.Id);
        after.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        after.StartedAt.ShouldBeNull();
        after.CompletedAt.ShouldBeNull();
        after.IsServerOffline.ShouldBeTrue();
        after.ForceMediaRefresh.ShouldBeTrue();
        after.CreatedAt.ShouldBe(createdAt);
        context.Object.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Completed);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<RefreshLibraryMediaCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        Mock.Mock<IMediaOverviewRebuildCoordinator>()
            .Verify(x => x.AcquireLibrarySyncLeaseAsync(It.IsAny<CancellationToken>()), Times.Never);
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldCancelClaim_WhenTokenIsAlreadyCancelledBeforeExecution()
    {
        // Arrange
        await SetupDatabase(
            55107,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var startedAt = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        dbContext.LibrarySyncJobQueues.Add(
            new LibrarySyncJobQueue
            {
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Processing,
                CreatedAt = startedAt,
                StartedAt = startedAt,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        (await dbContext.LibrarySyncJobQueues.SingleAsync(CancellationToken)).Status.ShouldBe(
            LibrarySyncJobStatus.Processing
        );
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = SetupJobContext(library.PlexServerId, library.Id, cancellation.Token);
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(),
                    It.Is<CancellationToken>(token => token != cancellation.Token && !token.IsCancellationRequested)
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(types =>
                        types.SequenceEqual(
                            new[] { RefreshDataType.PlexLibrary, RefreshDataType.PlexLibrarySyncStatus }
                        )
                    )
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        await ExecuteWithListenerAsync(context.Object);

        // Assert
        var after = await dbContext.LibrarySyncJobQueues.SingleAsync(CancellationToken);
        after.PlexLibraryId.ShouldBe(library.Id);
        after.PlexServerId.ShouldBe(library.PlexServerId);
        after.Status.ShouldBe(LibrarySyncJobStatus.Cancelled);
        after.StartedAt.ShouldBe(startedAt);
        after.CompletedAt.ShouldNotBeNull();
        after.ErrorMessage.ShouldBeNull();
        context.Object.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Cancelled);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<RefreshLibraryMediaCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        Mock.Mock<IMediaOverviewRebuildCoordinator>()
            .Verify(x => x.AcquireLibrarySyncLeaseAsync(It.IsAny<CancellationToken>()), Times.Never);
        Mock.Mock<INotificationHubService>().Verify();
    }

    private async Task ExecuteWithListenerAsync(IJobExecutionContext context)
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ScheduleAffectedLibraryComparisonJobsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        await Sut.Execute(context);
        await Mock.Create<LibrarySyncJobListener>().JobWasExecuted(context, null, CancellationToken);
    }
}
