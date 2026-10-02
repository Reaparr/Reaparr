namespace Reaparr.Application.UnitTests;

public class QueueLibrarySyncJobCommandHandlerUnitTests : BaseUnitTest<QueueLibrarySyncJobCommandHandler>
{
    [Test]
    public async Task ShouldPersistForceMediaRefresh_WhenQueuingNewLibrary()
    {
        // Arrange
        await SetupDatabase(
            3000,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var library = IDbContext.PlexLibraries.First();
        await IDbContext
            .PlexLibraries.Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SyncedAt, (DateTime?)null), CancellationToken);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        var command = new QueueLibrarySyncJobCommand([library.Id], ForceMediaRefresh: true);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        IDbContext.LibrarySyncJobQueues.Single().ForceMediaRefresh.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldQueueNewLibraries_WhenLibrariesDoNotExistInQueue()
    {
        // Arrange
        await SetupDatabase(
            3001,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraryIds = dbContext.PlexLibraries.Select(x => x.Id).ToList();
        await dbContext
            .PlexLibraries.Where(x => libraryIds.Contains(x.Id))
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SyncedAt, (DateTime?)null), CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var command = new QueueLibrarySyncJobCommand(libraryIds);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var queueItems = await IDbContext.LibrarySyncJobQueues.IgnoreQueryFilters().ToListAsync(CancellationToken);
        queueItems.Count.ShouldBe(2);
        queueItems.All(x => x.Status == LibrarySyncJobStatus.Queued).ShouldBeTrue();
        queueItems.All(x => libraryIds.Contains(x.PlexLibraryId)).ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldResetCompletedLibraries_WhenCompletedLibrariesExist()
    {
        // Arrange
        await SetupDatabase(
            3002,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var library = dbContext.PlexLibraries.Select(x => new { x.Id, x.PlexServerId }).First();
        await dbContext
            .PlexLibraries.Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.SyncedAt, DateTime.UtcNow.AddHours(-2))
                        .SetProperty(y => y.UpdatedAt, DateTime.UtcNow.AddHours(-1)),
                CancellationToken
            );

        var completedItem = new LibrarySyncJobQueue
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow.AddHours(-4),
        };

        await dbContext.LibrarySyncJobQueues.AddAsync(completedItem, CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var command = new QueueLibrarySyncJobCommand([library.Id]);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

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
    public async Task ShouldResetFailedLibraries_WhenFailedLibrariesExist()
    {
        // Arrange
        await SetupDatabase(
            3003,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var library = IDbContext.PlexLibraries.First();

        var failedItem = new LibrarySyncJobQueue
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Failed,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow.AddHours(-4),
            ErrorMessage = "Test error",
        };

        await IDbContext.LibrarySyncJobQueues.AddAsync(failedItem, CancellationToken);
        await IDbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var command = new QueueLibrarySyncJobCommand([library.Id]);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var queueItem = await IDbContext.LibrarySyncJobQueues.IgnoreQueryFilters().FirstAsync(CancellationToken);
        queueItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        queueItem.PlexLibraryId.ShouldBe(library.Id);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldSkipRecentlySyncedNewLibraries_WhenSyncedWithinSyncBuffer()
    {
        // Arrange
        await SetupDatabase(
            3013,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var library = IDbContext.PlexLibraries.First();
        await IDbContext
            .PlexLibraries.Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SyncedAt, DateTime.UtcNow.AddHours(-2)), CancellationToken);

        var command = new QueueLibrarySyncJobCommand([library.Id]);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var queueItems = await IDbContext.LibrarySyncJobQueues.IgnoreQueryFilters().ToListAsync(CancellationToken);
        queueItems.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldQueueRecentlySyncedNewLibrary_WhenForceIsEnabled()
    {
        // Arrange
        await SetupDatabase(
            3033,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var library = IDbContext.PlexLibraries.First();
        await IDbContext
            .PlexLibraries.Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SyncedAt, DateTime.UtcNow.AddHours(-2)), CancellationToken);

        var command = new QueueLibrarySyncJobCommand([library.Id], ForceLibrarySync: true);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var queueItems = await IDbContext.LibrarySyncJobQueues.IgnoreQueryFilters().ToListAsync(CancellationToken);
        queueItems.Count.ShouldBe(1);
        queueItems[0].PlexLibraryId.ShouldBe(library.Id);
        queueItems[0].Status.ShouldBe(LibrarySyncJobStatus.Queued);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldSkipRecentlyCompletedLibraries_WhenCompletedWithinSyncBuffer()
    {
        // Arrange
        await SetupDatabase(
            3012,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var library = dbContext.PlexLibraries.Select(x => new { x.Id, x.PlexServerId }).First();
        await dbContext
            .PlexLibraries.Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.SyncedAt, DateTime.UtcNow.AddHours(-2))
                        .SetProperty(y => y.UpdatedAt, DateTime.UtcNow.AddHours(-1)),
                CancellationToken
            );

        var completedItem = new LibrarySyncJobQueue
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow.AddHours(-3),
            CompletedAt = DateTime.UtcNow.AddHours(-2),
        };

        await dbContext.LibrarySyncJobQueues.AddAsync(completedItem, CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        await dbContext
            .PlexLibraries.Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.SyncedAt, DateTime.UtcNow.AddHours(-2))
                        .SetProperty(y => y.UpdatedAt, DateTime.UtcNow.AddHours(-1)),
                CancellationToken
            );

        var command = new QueueLibrarySyncJobCommand([library.Id]);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldResetRecentlyCompletedLibrary_WhenForceIsEnabled()
    {
        // Arrange
        await SetupDatabase(
            3034,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var library = dbContext.PlexLibraries.Select(x => new { x.Id, x.PlexServerId }).First();
        await dbContext
            .PlexLibraries.Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SyncedAt, DateTime.UtcNow.AddHours(-2)), CancellationToken);

        var completedItem = new LibrarySyncJobQueue
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow.AddHours(-2),
        };

        await dbContext.LibrarySyncJobQueues.AddAsync(completedItem, CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        var command = new QueueLibrarySyncJobCommand([library.Id], ForceLibrarySync: true);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var queueItem = await IDbContext.LibrarySyncJobQueues.IgnoreQueryFilters().SingleAsync(CancellationToken);
        queueItem.PlexLibraryId.ShouldBe(library.Id);
        queueItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        queueItem.StartedAt.ShouldBeNull();
        queueItem.CompletedAt.ShouldBeNull();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldResetRecentlyCompletedLibrary_WhenLibraryIsUnsynced()
    {
        // Arrange
        await SetupDatabase(
            3024,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var library = dbContext.PlexLibraries.Select(x => new { x.Id, x.PlexServerId }).First();
        await dbContext
            .PlexLibraries.Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SyncedAt, (DateTime?)null), CancellationToken);

        var completedItem = new LibrarySyncJobQueue
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            CompletedAt = DateTime.UtcNow.AddMinutes(-5),
        };

        await dbContext.LibrarySyncJobQueues.AddAsync(completedItem, CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        var command = new QueueLibrarySyncJobCommand([library.Id]);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var queueItem = await dbContext
            .LibrarySyncJobQueues.IgnoreQueryFilters()
            .SingleAsync(x => x.PlexLibraryId == library.Id, CancellationToken);
        queueItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        queueItem.StartedAt.ShouldBeNull();
        queueItem.CompletedAt.ShouldBeNull();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldSkipOldSyncedNewLibrary_WhenLibraryWasNotUpdatedAfterLastSync()
    {
        // Arrange
        await SetupDatabase(
            3022,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var library = dbContext.PlexLibraries.First();
        var syncedAt = DateTime.UtcNow.AddHours(-4);
        await dbContext
            .PlexLibraries.Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(y => y.SyncedAt, syncedAt).SetProperty(y => y.UpdatedAt, syncedAt.AddMinutes(-5)),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var command = new QueueLibrarySyncJobCommand([library.Id]);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var queueItems = await dbContext.LibrarySyncJobQueues.IgnoreQueryFilters().ToListAsync(CancellationToken);
        queueItems.Count.ShouldBe(1);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldNotResetOldCompletedLibrary_WhenLibraryWasNotUpdatedAfterLastSync()
    {
        // Arrange
        await SetupDatabase(
            3023,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var library = dbContext.PlexLibraries.Select(x => new { x.Id, x.PlexServerId }).First();
        var syncedAt = DateTime.UtcNow.AddHours(-2);
        await dbContext
            .PlexLibraries.Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(y => y.SyncedAt, syncedAt).SetProperty(y => y.UpdatedAt, syncedAt.AddMinutes(-5)),
                CancellationToken
            );

        await dbContext.LibrarySyncJobQueues.AddAsync(
            new LibrarySyncJobQueue
            {
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Completed,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow.AddHours(-4),
            },
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var command = new QueueLibrarySyncJobCommand([library.Id]);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var queueItem = await dbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        queueItem.PlexLibraryId.ShouldBe(library.Id);
        queueItem.PlexServerId.ShouldBe(library.PlexServerId);
        queueItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        queueItem.CompletedAt.ShouldBeNull();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldQueueOldSyncedNewLibraries_WhenSyncedOutsideSyncBuffer()
    {
        // Arrange
        await SetupDatabase(
            3014,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var library = IDbContext.PlexLibraries.First();
        await IDbContext
            .PlexLibraries.Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.SyncedAt, DateTime.UtcNow.AddHours(-4))
                        .SetProperty(y => y.UpdatedAt, DateTime.UtcNow.AddHours(-1)),
                CancellationToken
            );

        var command = new QueueLibrarySyncJobCommand([library.Id]);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var queueItem = await IDbContext.LibrarySyncJobQueues.IgnoreQueryFilters().FirstAsync(CancellationToken);
        queueItem.PlexLibraryId.ShouldBe(library.Id);
        queueItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldResetCancelledLibraries_WhenCancelledWithinSyncBuffer()
    {
        // Arrange
        await SetupDatabase(
            3015,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var library = IDbContext.PlexLibraries.First();

        var cancelledItem = new LibrarySyncJobQueue
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Cancelled,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow.AddHours(-1),
        };

        await IDbContext.LibrarySyncJobQueues.AddAsync(cancelledItem, CancellationToken);
        await IDbContext.SaveChangesAsync(CancellationToken);

        var command = new QueueLibrarySyncJobCommand([library.Id]);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var queueItem = await IDbContext.LibrarySyncJobQueues.IgnoreQueryFilters().FirstAsync(CancellationToken);
        queueItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        queueItem.StartedAt.ShouldBeNull();
        queueItem.CompletedAt.ShouldBeNull();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldSkipCompletedLibraries_WhenCompletedAtIsMissing()
    {
        // Arrange
        await SetupDatabase(
            3016,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var library = dbContext.PlexLibraries.Select(x => new { x.Id, x.PlexServerId }).First();

        var completedItem = new LibrarySyncJobQueue
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = null,
        };

        await dbContext.LibrarySyncJobQueues.AddAsync(completedItem, CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        await dbContext
            .PlexLibraries.Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.SyncedAt, DateTime.UtcNow.AddHours(-2))
                        .SetProperty(y => y.UpdatedAt, DateTime.UtcNow.AddHours(-1)),
                CancellationToken
            );

        var command = new QueueLibrarySyncJobCommand([library.Id]);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldQueueAllSupportedFamilies_AndSkipUnsupportedTypes()
    {
        // Arrange
        await SetupDatabase(
            3017,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var movieLibrary = dbContext.PlexLibraries.Single();
        var supportedTypes = new[]
        {
            PlexMediaType.Music,
            PlexMediaType.Photos,
            PlexMediaType.OtherVideos,
        };
        var addedLibraries = supportedTypes
            .Append(PlexMediaType.Games)
            .Select((type, index) => FakeData.GetPlexLibrary(new Seed(3017 + index), type).Generate())
            .ToList();
        foreach (var library in addedLibraries)
        {
            library.PlexServerId = movieLibrary.PlexServerId;
            library.SyncedAt = null;
        }

        movieLibrary.SyncedAt = null;
        await dbContext.PlexLibraries.AddRangeAsync(addedLibraries, CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexLibraries.Where(x => x.Id == movieLibrary.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SyncedAt, (DateTime?)null), CancellationToken);
        var expectedLibraryIds = new[] { movieLibrary.Id }
            .Concat(addedLibraries.Where(x => supportedTypes.Contains(x.Type)).Select(x => x.Id))
            .OrderBy(x => x)
            .ToList();

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(
            new QueueLibrarySyncJobCommand([movieLibrary.Id, .. addedLibraries.Select(x => x.Id)]),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var queueItems = await dbContext
            .LibrarySyncJobQueues.AsNoTracking()
            .OrderBy(x => x.PlexLibraryId)
            .Select(x => new { x.PlexLibraryId, x.Priority, x.Status })
            .ToListAsync(CancellationToken);
        queueItems.ShouldBe(
            expectedLibraryIds
                .Select(id => new
                {
                    PlexLibraryId = id,
                    Priority = id == movieLibrary.Id ? 1 : 3,
                    Status = LibrarySyncJobStatus.Queued,
                })
                .ToList()
        );
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldSkipRecentlySyncedLibraryButStillDispatch_WhenAnotherLibraryIsAlreadyQueued()
    {
        // Arrange
        await SetupDatabase(
            3018,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
            }
        );

        var libraries = IDbContext.PlexLibraries.ToList();
        var queuedLibrary = libraries[0];
        var recentlySyncedLibrary = libraries[1];
        var server = IDbContext.PlexServers.First();
        await IDbContext
            .PlexLibraries.Where(x => x.Id == recentlySyncedLibrary.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SyncedAt, DateTime.UtcNow.AddHours(-1)), CancellationToken);

        await IDbContext.LibrarySyncJobQueues.AddAsync(
            new LibrarySyncJobQueue
            {
                PlexServerId = server.Id,
                PlexLibraryId = queuedLibrary.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Queued,
                CreatedAt = DateTime.UtcNow,
            },
            CancellationToken
        );
        await IDbContext.SaveChangesAsync(CancellationToken);

        var command = new QueueLibrarySyncJobCommand([queuedLibrary.Id, recentlySyncedLibrary.Id]);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var queueItems = await IDbContext.LibrarySyncJobQueues.IgnoreQueryFilters().ToListAsync(CancellationToken);
        queueItems.Count.ShouldBe(1);
        queueItems[0].PlexLibraryId.ShouldBe(queuedLibrary.Id);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldSkipQueuedLibraries_WhenLibrariesAlreadyQueued()
    {
        // Arrange
        await SetupDatabase(
            3004,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var library = IDbContext.PlexLibraries.First();

        var queuedItem = new LibrarySyncJobQueue
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Queued,
            CreatedAt = DateTime.UtcNow,
        };

        await IDbContext.LibrarySyncJobQueues.AddAsync(queuedItem, CancellationToken);
        await IDbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var command = new QueueLibrarySyncJobCommand([library.Id]);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var queueItems = await IDbContext.LibrarySyncJobQueues.IgnoreQueryFilters().ToListAsync(CancellationToken);
        queueItems.Count.ShouldBe(1);
        queueItems[0].Status.ShouldBe(LibrarySyncJobStatus.Queued);
        queueItems[0].PlexLibraryId.ShouldBe(library.Id);

        // Should still call CheckQueuedPlexLibraryToSyncCommand even when skipping
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldSkipProcessingLibraries_WhenLibrariesAlreadyProcessing()
    {
        // Arrange
        await SetupDatabase(
            3005,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var library = IDbContext.PlexLibraries.First();

        // Use a single context instance to ensure the item is saved and can be queried
        var dbContext = IDbContext;
        var processingItem = new LibrarySyncJobQueue
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Processing,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
        };

        await dbContext.LibrarySyncJobQueues.AddAsync(processingItem, CancellationToken);
        var saveResult = await dbContext.SaveChangesAsync(CancellationToken);
        saveResult.ShouldBeGreaterThan(0); // Ensure save actually happened

        // Verify the Processing item was saved using the same context instance
        var savedItems = await dbContext
            .LibrarySyncJobQueues.Where(x =>
                x.PlexLibraryId == library.Id && x.Status == LibrarySyncJobStatus.Processing
            )
            .ToListAsync(CancellationToken);
        savedItems.Count.ShouldBe(1);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var command = new QueueLibrarySyncJobCommand([library.Id]);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Use AsNoTracking to see the actual database state
        var queueItems = await IDbContext.LibrarySyncJobQueues.AsNoTracking().ToListAsync(CancellationToken);
        queueItems.Count.ShouldBe(1);
        queueItems[0].Status.ShouldBe(LibrarySyncJobStatus.Processing); // Should remain processing (not reset)
        queueItems[0].PlexLibraryId.ShouldBe(library.Id);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldSetCorrectPriority_WhenQueuingMovies()
    {
        // Arrange
        await SetupDatabase(
            3006,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        // Use a library already created as a Movie type from SetupDatabase
        var library = IDbContext.PlexLibraries.First(x => x.Type == PlexMediaType.Movie);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var command = new QueueLibrarySyncJobCommand([library.Id]);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var queueItem = await IDbContext.LibrarySyncJobQueues.IgnoreQueryFilters().FirstAsync(CancellationToken);
        queueItem.Priority.ShouldBe(1); // Movies should have priority 1
    }

    [Test]
    public async Task ShouldSetCorrectPriority_WhenQueuingTvShows()
    {
        // Arrange
        await SetupDatabase(
            3007,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
            }
        );

        // Library is already created as a TvShow type from SetupDatabase config
        var library = IDbContext.PlexLibraries.First(x => x.Type == PlexMediaType.TvShow);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var command = new QueueLibrarySyncJobCommand([library.Id]);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var queueItem = await IDbContext.LibrarySyncJobQueues.IgnoreQueryFilters().FirstAsync(CancellationToken);
        queueItem.Priority.ShouldBe(2); // TV shows should have priority 2
    }

    [Test]
    public async Task ShouldCallCheckQueuedCommand_WhenItemsAreQueued()
    {
        // Arrange
        await SetupDatabase(
            3009,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var library = IDbContext.PlexLibraries.First();

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var command = new QueueLibrarySyncJobCommand([library.Id]);

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
    public async Task ShouldReturnOk_WhenNoLibrariesFound()
    {
        // Arrange
        await SetupDatabase(
            3010,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var command = new QueueLibrarySyncJobCommand([99999]); // Non-existent library ID

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var queueItems = await IDbContext.LibrarySyncJobQueues.IgnoreQueryFilters().ToListAsync(CancellationToken);
        queueItems.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldResetOldCompletedLibraryAndSkipRecentCompletedLibrary_WhenBothAreRequested()
    {
        // Arrange
        await SetupDatabase(
            3019,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
            }
        );

        var dbContext = IDbContext;
        var libraries = dbContext.PlexLibraries.Select(x => new { x.Id, x.PlexServerId }).ToList();
        var oldCompletedLibrary = libraries[0];
        var recentCompletedLibrary = libraries[1];

        await dbContext
            .PlexLibraries.Where(x => x.Id == oldCompletedLibrary.Id || x.Id == recentCompletedLibrary.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.SyncedAt, DateTime.UtcNow.AddHours(-2))
                        .SetProperty(y => y.UpdatedAt, DateTime.UtcNow.AddHours(-1)),
                CancellationToken
            );

        await dbContext.LibrarySyncJobQueues.AddRangeAsync(
            [
                new LibrarySyncJobQueue
                {
                    PlexServerId = oldCompletedLibrary.PlexServerId,
                    PlexLibraryId = oldCompletedLibrary.Id,
                    Priority = 1,
                    Status = LibrarySyncJobStatus.Completed,
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow.AddHours(-4),
                },
                new LibrarySyncJobQueue
                {
                    PlexServerId = recentCompletedLibrary.PlexServerId,
                    PlexLibraryId = recentCompletedLibrary.Id,
                    Priority = 1,
                    Status = LibrarySyncJobStatus.Completed,
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow.AddHours(-2),
                },
            ],
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var command = new QueueLibrarySyncJobCommand([oldCompletedLibrary.Id, recentCompletedLibrary.Id]);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var queueItems = await dbContext.LibrarySyncJobQueues.AsNoTracking().ToListAsync(CancellationToken);
        queueItems.Count.ShouldBe(2);

        var oldCompletedItem = queueItems.First(x => x.PlexLibraryId == oldCompletedLibrary.Id);
        oldCompletedItem.PlexServerId.ShouldBe(oldCompletedLibrary.PlexServerId);
        oldCompletedItem.Priority.ShouldBe(1);
        oldCompletedItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        oldCompletedItem.StartedAt.ShouldBeNull();
        oldCompletedItem.CompletedAt.ShouldBeNull();
        oldCompletedItem.ErrorMessage.ShouldBeNull();
        oldCompletedItem.IsServerOffline.ShouldBeFalse();

        var recentCompletedItem = queueItems.First(x => x.PlexLibraryId == recentCompletedLibrary.Id);
        recentCompletedItem.PlexServerId.ShouldBe(recentCompletedLibrary.PlexServerId);
        recentCompletedItem.Priority.ShouldBe(1);
        recentCompletedItem.Status.ShouldBe(LibrarySyncJobStatus.Completed);
        recentCompletedItem.CompletedAt.ShouldNotBeNull();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldQueueLibraryOnce_WhenCommandContainsDuplicateLibraryIds()
    {
        // Arrange
        await SetupDatabase(
            3020,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var dbContext = IDbContext;
        var library = dbContext.PlexLibraries.First();
        await dbContext
            .PlexLibraries.Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SyncedAt, (DateTime?)null), CancellationToken);

        var command = new QueueLibrarySyncJobCommand([library.Id, library.Id, library.Id]);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var queueItems = await IDbContext.LibrarySyncJobQueues.IgnoreQueryFilters().ToListAsync(CancellationToken);
        queueItems.Count.ShouldBe(1);
        queueItems[0].PlexLibraryId.ShouldBe(library.Id);
        queueItems[0].PlexServerId.ShouldBe(library.PlexServerId);
        queueItems[0].Priority.ShouldBe(1);
        queueItems[0].Status.ShouldBe(LibrarySyncJobStatus.Queued);
        queueItems[0].StartedAt.ShouldBeNull();
        queueItems[0].CompletedAt.ShouldBeNull();
        queueItems[0].ErrorMessage.ShouldBeNull();
        queueItems[0].IsServerOffline.ShouldBeFalse();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldReturnOkWithoutDispatch_WhenCommandContainsOnlyUnsupportedLibraries()
    {
        // Arrange
        await SetupDatabase(
            3021,
            config =>
            {
                config.PlexServerCount = 1;
            }
        );

        var dbContext = IDbContext;
        var serverId = dbContext.PlexServers.Select(x => x.Id).First();
        var unsupportedLibraries = new[] { PlexMediaType.Music, PlexMediaType.Unknown, PlexMediaType.Games }
            .Select((type, index) => FakeData.GetPlexLibrary(new Seed(3021 + index), type).Generate())
            .ToList();
        unsupportedLibraries[0].UpdateInitProperty(nameof(PlexLibrary.Type), PlexMediaType.None);
        foreach (var library in unsupportedLibraries)
            library.PlexServerId = serverId;

        await dbContext.PlexLibraries.AddRangeAsync(unsupportedLibraries, CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await Sut.ExecuteAsync(
            new QueueLibrarySyncJobCommand(unsupportedLibraries.Select(x => x.Id).ToList()),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var queueItems = await dbContext.LibrarySyncJobQueues.AsNoTracking().ToListAsync(CancellationToken);
        queueItems.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldHandleMixedScenarios_WhenMultipleConditionsExist()
    {
        // Arrange
        await SetupDatabase(
            3011,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 3;
                config.PlexTvShowLibraryCount = 2;
            }
        );

        var server = IDbContext.PlexServers.First();
        var libraries = IDbContext.PlexLibraries.ToList();
        var allLibraryIds = libraries.Select(x => x.Id).ToList();
        var movieLibrary1 = libraries.First(x => x.Type == PlexMediaType.Movie);
        var movieLibrary2 = libraries.Skip(1).First(x => x.Type == PlexMediaType.Movie);
        var movieLibrary3 = libraries.Skip(2).First(x => x.Type == PlexMediaType.Movie);
        var tvShowLibrary1 = libraries.First(x => x.Type == PlexMediaType.TvShow);
        var tvShowLibrary2 = libraries.Skip(1).First(x => x.Type == PlexMediaType.TvShow);

        await IDbContext
            .PlexLibraries.Where(x => allLibraryIds.Contains(x.Id))
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.SyncedAt, DateTime.UtcNow.AddHours(-4))
                        .SetProperty(y => y.UpdatedAt, DateTime.UtcNow.AddHours(-1)),
                CancellationToken
            );

        // Setup existing queue items with different statuses
        var completedItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = movieLibrary1.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow.AddHours(-4),
        };

        var failedItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = movieLibrary2.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Failed,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow.AddHours(-4),
            ErrorMessage = "Error",
        };

        var queuedItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = movieLibrary3.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Queued,
            CreatedAt = DateTime.UtcNow,
        };

        await IDbContext.LibrarySyncJobQueues.AddRangeAsync([completedItem, failedItem, queuedItem], CancellationToken);
        await IDbContext.SaveChangesAsync(CancellationToken);

        // Queue all libraries (some new, some existing with different statuses)

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var command = new QueueLibrarySyncJobCommand(allLibraryIds);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var queueItems = await IDbContext.LibrarySyncJobQueues.IgnoreQueryFilters().ToListAsync(CancellationToken);

        // Should have: 2 new libraries (tvShowLibrary1, tvShowLibrary2), 1 reset (completedItem), 1 reset (failedItem), 1 skipped (queuedItem)
        queueItems.Count.ShouldBe(5);

        // Verify the completed item was reset
        var resetCompleted = queueItems.First(x => x.PlexLibraryId == movieLibrary1.Id);
        resetCompleted.Status.ShouldBe(LibrarySyncJobStatus.Queued);

        // Verify the failed item was reset
        var resetFailed = queueItems.First(x => x.PlexLibraryId == movieLibrary2.Id);
        resetFailed.Status.ShouldBe(LibrarySyncJobStatus.Queued);

        // Verify queued item remains queued
        var skippedQueued = queueItems.First(x => x.PlexLibraryId == movieLibrary3.Id);
        skippedQueued.Status.ShouldBe(LibrarySyncJobStatus.Queued);

        // Verify new items were added
        var newTvShow1 = queueItems.First(x => x.PlexLibraryId == tvShowLibrary1.Id);
        newTvShow1.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        newTvShow1.Priority.ShouldBe(2); // TV shows have priority 2

        var newTvShow2 = queueItems.First(x => x.PlexLibraryId == tvShowLibrary2.Id);
        newTvShow2.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        newTvShow2.Priority.ShouldBe(2);

        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }
}
