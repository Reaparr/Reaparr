namespace Reaparr.Application.UnitTests;

public class ResetFailedLibrarySyncJobsCommandHandlerUnitTests : BaseUnitTest<ResetFailedLibrarySyncJobsCommandHandler>
{
    [Test]
    public async Task ShouldResetFailedJobsToQueued_WhenFailedJobsExistForServer()
    {
        // Arrange
        await SetupDatabase(
            4001,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
            }
        );

        var dbContext = IDbContext;
        var server = dbContext.PlexServers.First();
        var libraries = dbContext.PlexLibraries.ToList();

        var failedItem1 = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = libraries[0].Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Failed,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow,
            ErrorMessage = "Connection failed",
            IsServerOffline = true,
        };

        var failedItem2 = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = libraries[1].Id,
            Priority = 2,
            Status = LibrarySyncJobStatus.Failed,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow,
            ErrorMessage = "Timeout error",
            IsServerOffline = true,
        };

        await dbContext.LibrarySyncJobQueues.AddRangeAsync([failedItem1, failedItem2], CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
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

        var command = new ResetFailedLibrarySyncJobsCommand(server.Id);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var updatedItems = await dbContext.LibrarySyncJobQueues.AsNoTracking().ToListAsync(CancellationToken);
        updatedItems.Count.ShouldBe(2);

        foreach (var item in updatedItems)
        {
            item.Status.ShouldBe(LibrarySyncJobStatus.Queued);
            item.StartedAt.ShouldBeNull();
            item.CompletedAt.ShouldBeNull();
            item.ErrorMessage.ShouldBeNull();
            item.IsServerOffline.ShouldBeFalse();
        }
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldKeepResetAdmission_WhenCheckerFails()
    {
        // Arrange
        await SetupDatabase(
            4008,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var server = dbContext.PlexServers.Single();
        var library = dbContext.PlexLibraries.Single();
        await dbContext.LibrarySyncJobQueues.AddAsync(
            new LibrarySyncJobQueue
            {
                PlexServerId = server.Id,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Failed,
                CreatedAt = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
                CompletedAt = DateTime.UtcNow.AddMinutes(-1),
                ErrorMessage = "failed sync",
                IsServerOffline = true,
            },
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("checker failed"))
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

        // Act
        var result = await Sut.ExecuteAsync(new ResetFailedLibrarySyncJobsCommand(server.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var queueItem = await dbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        queueItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        queueItem.StartedAt.ShouldBeNull();
        queueItem.CompletedAt.ShouldBeNull();
        queueItem.ErrorMessage.ShouldBeNull();
        queueItem.IsServerOffline.ShouldBeFalse();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldOnlyResetFailedJobsForSpecificServer_WhenMultipleServersExist()
    {
        // Arrange
        await SetupDatabase(
            4002,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var servers = dbContext.PlexServers.ToList();
        var libraries = dbContext.PlexLibraries.ToList();

        var server1Library = libraries.First(x => x.PlexServerId == servers[0].Id);
        var server2Library = libraries.First(x => x.PlexServerId == servers[1].Id);

        var failedItemServer1 = new LibrarySyncJobQueue
        {
            PlexServerId = servers[0].Id,
            PlexLibraryId = server1Library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Failed,
            CreatedAt = DateTime.UtcNow,
            ErrorMessage = "Error",
            IsServerOffline = true,
        };

        var failedItemServer2 = new LibrarySyncJobQueue
        {
            PlexServerId = servers[1].Id,
            PlexLibraryId = server2Library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Failed,
            CreatedAt = DateTime.UtcNow,
            ErrorMessage = "Error",
            IsServerOffline = true,
        };

        await dbContext.LibrarySyncJobQueues.AddRangeAsync([failedItemServer1, failedItemServer2], CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Only reset failed jobs for server 1
        var command = new ResetFailedLibrarySyncJobsCommand(servers[0].Id);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var updatedItems = await dbContext.LibrarySyncJobQueues.AsNoTracking().ToListAsync(CancellationToken);

        var server1Item = updatedItems.First(x => x.PlexServerId == servers[0].Id);
        server1Item.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        server1Item.IsServerOffline.ShouldBeFalse();

        var server2Item = updatedItems.First(x => x.PlexServerId == servers[1].Id);
        server2Item.Status.ShouldBe(LibrarySyncJobStatus.Failed); // Should remain failed
        server2Item.IsServerOffline.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldNotResetQueuedOrProcessingJobs_WhenOnlyFailedShouldBeReset()
    {
        // Arrange
        await SetupDatabase(
            4003,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 3;
            }
        );

        var dbContext = IDbContext;
        var server = dbContext.PlexServers.First();
        var libraries = dbContext.PlexLibraries.ToList();

        var failedItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = libraries[0].Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Failed,
            CreatedAt = DateTime.UtcNow,
            ErrorMessage = "Error",
        };

        var queuedItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = libraries[1].Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Queued,
            CreatedAt = DateTime.UtcNow,
        };

        var processingItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = libraries[2].Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Processing,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
        };

        await dbContext.LibrarySyncJobQueues.AddRangeAsync([failedItem, queuedItem, processingItem], CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var command = new ResetFailedLibrarySyncJobsCommand(server.Id);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var updatedItems = await dbContext.LibrarySyncJobQueues.AsNoTracking().ToListAsync(CancellationToken);

        var resetItem = updatedItems.First(x => x.PlexLibraryId == libraries[0].Id);
        resetItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);

        var stillQueued = updatedItems.First(x => x.PlexLibraryId == libraries[1].Id);
        stillQueued.Status.ShouldBe(LibrarySyncJobStatus.Queued);

        var stillProcessing = updatedItems.First(x => x.PlexLibraryId == libraries[2].Id);
        stillProcessing.Status.ShouldBe(LibrarySyncJobStatus.Processing);
    }

    [Test]
    public async Task ShouldCallCheckQueuedCommand_AfterResettingFailedJobs()
    {
        // Arrange
        await SetupDatabase(
            4004,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var server = dbContext.PlexServers.First();
        var library = dbContext.PlexLibraries.First();

        var failedItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Failed,
            CreatedAt = DateTime.UtcNow,
            ErrorMessage = "Error",
        };

        await dbContext.LibrarySyncJobQueues.AddAsync(failedItem, CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var command = new ResetFailedLibrarySyncJobsCommand(server.Id);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldNotCallCheckQueuedCommand_WhenNoFailedJobsExist()
    {
        // Arrange
        await SetupDatabase(
            4005,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var server = IDbContext.PlexServers.First();

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var command = new ResetFailedLibrarySyncJobsCommand(server.Id);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // CheckQueuedCommand should not be called when no jobs were reset
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldCheckQueuedJobs_WhenNoFailedJobsNeedReset()
    {
        // Arrange
        await SetupDatabase(
            4007,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var server = dbContext.PlexServers.Single();
        var library = dbContext.PlexLibraries.Single();
        await dbContext.LibrarySyncJobQueues.AddAsync(
            new LibrarySyncJobQueue
            {
                PlexServerId = server.Id,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Queued,
                CreatedAt = DateTime.UtcNow,
                IsServerOffline = true,
            },
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new ResetFailedLibrarySyncJobsCommand(server.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var queueItem = await dbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        queueItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        queueItem.IsServerOffline.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldClearErrorMessageAndTimestamps_WhenResettingFailedJobs()
    {
        // Arrange
        await SetupDatabase(
            4006,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var server = dbContext.PlexServers.First();
        var library = dbContext.PlexLibraries.First();

        var failedItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Failed,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow,
            ErrorMessage = "Server connection timed out",
            IsServerOffline = true,
        };

        await dbContext.LibrarySyncJobQueues.AddAsync(failedItem, CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var command = new ResetFailedLibrarySyncJobsCommand(server.Id);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var updatedItem = await dbContext.LibrarySyncJobQueues.AsNoTracking().FirstAsync(CancellationToken);

        updatedItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        updatedItem.StartedAt.ShouldBeNull();
        updatedItem.CompletedAt.ShouldBeNull();
        updatedItem.ErrorMessage.ShouldBeNull();
        updatedItem.IsServerOffline.ShouldBeFalse();
    }
}
