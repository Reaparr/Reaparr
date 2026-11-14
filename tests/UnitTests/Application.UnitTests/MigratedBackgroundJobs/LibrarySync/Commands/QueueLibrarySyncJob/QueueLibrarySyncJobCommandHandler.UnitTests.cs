using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Reaparr.Data;

namespace Reaparr.Application.UnitTests;

public class QueueLibrarySyncJobCommandHandlerUnitTests : BaseCommandUnitTest<QueueLibrarySyncJobCommand>
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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
            .ExecuteUpdateAsync(
                x => x.SetProperty(y => y.SyncedAt, DateTime.UtcNow.AddHours(-2)).SetProperty(y => y.Outdated, false),
                CancellationToken
            );

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
        (await dbContext.PlexLibraries.SingleAsync(CancellationToken)).Outdated.ShouldBeFalse();

        var command = new QueueLibrarySyncJobCommand([library.Id], ForceLibrarySync: true);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var queueItem = await IDbContext.LibrarySyncJobQueues.IgnoreQueryFilters().SingleAsync(CancellationToken);
        queueItem.PlexLibraryId.ShouldBe(library.Id);
        queueItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        queueItem.StartedAt.ShouldBeNull();
        queueItem.CompletedAt.ShouldBeNull();
        (await dbContext.PlexLibraries.SingleAsync(CancellationToken)).Outdated.ShouldBeTrue();
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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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
        var result = await TestHandlerExecuteAsync(command);

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

    [Test]
    public async Task ShouldKeepDurableQueueAdmission_WhenCheckerFails()
    {
        // Arrange
        await SetupDatabase(
            3040,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var library = IDbContext.PlexLibraries.Single();
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
        var result = await TestHandlerExecuteAsync(
            new QueueLibrarySyncJobCommand([library.Id], ForceLibrarySync: true)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var queueItem = await IDbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        queueItem.PlexLibraryId.ShouldBe(library.Id);
        queueItem.PlexServerId.ShouldBe(library.PlexServerId);
        queueItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldKeepDurableQueueAdmission_WhenNotificationFails()
    {
        // Arrange
        await SetupDatabase(
            3041,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        var library = IDbContext.PlexLibraries.Single();
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(types =>
                        types.Count == 1 && types[0] == RefreshDataType.PlexLibrarySyncStatus
                    )
                )
            )
            .ThrowsAsync(new InvalidOperationException("notification failed"))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(
            new QueueLibrarySyncJobCommand([library.Id], ForceLibrarySync: true)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var queueItem = await IDbContext.LibrarySyncJobQueues.AsNoTracking().SingleAsync(CancellationToken);
        queueItem.PlexLibraryId.ShouldBe(library.Id);
        queueItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldEmitQueueAdmission_WhenAQueueRowIsCreatedOrReset()
    {
        // Arrange
        await SetupDatabase(
            3042,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var library = IDbContext.PlexLibraries.Single();
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(2));

        var command = new QueueLibrarySyncJobCommand([library.Id], ForceLibrarySync: true);
        // Act
        var first = await TestHandlerExecuteAsync(command);
        var firstCreatedAt = IDbContext.LibrarySyncJobQueues.Single().CreatedAt;
        var second = await TestHandlerExecuteAsync(command);

        // Assert
        first.IsSuccess.ShouldBeTrue();
        first.Errors.Count.ShouldBe(0);
        second.IsSuccess.ShouldBeTrue();
        second.Errors.Count.ShouldBe(0);
        IDbContext.LibrarySyncJobQueues.Single().CreatedAt.ShouldBe(firstCreatedAt);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.LibrarySyncQueued(
                        It.Is<(int PlexLibraryId, int PlexServerId, DateTime CreatedAt, bool ForceMediaRefresh)>(
                            admission =>
                                admission.PlexServerId == library.PlexServerId
                                && admission.PlexLibraryId == library.Id
                                && admission.CreatedAt == firstCreatedAt
                                && !admission.ForceMediaRefresh
                        ),
                        library.Type,
                        true,
                        TelemetryTrigger.Manual
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldResetBatchWithOneQueueUpdate_WhenMultipleExistingRowsAreEligible()
    {
        // Arrange
        await SetupDatabase(
            3045,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 3;
            }
        );
        using var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var originalCreatedAt = DateTime.UtcNow.AddHours(-6);
        var resetLibraryIds = libraries.Take(2).Select(x => x.Id).ToList();
        await dbContext
            .PlexLibraries.Where(x => resetLibraryIds.Contains(x.Id))
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SyncedAt, DateTime.UtcNow.AddHours(-4)), CancellationToken);
        await dbContext.LibrarySyncJobQueues.AddRangeAsync(
            [
                new LibrarySyncJobQueue
                {
                    PlexServerId = libraries[0].PlexServerId,
                    PlexLibraryId = libraries[0].Id,
                    Priority = 1,
                    Status = LibrarySyncJobStatus.Completed,
                    CreatedAt = originalCreatedAt,
                    StartedAt = originalCreatedAt.AddMinutes(1),
                    CompletedAt = originalCreatedAt.AddMinutes(2),
                    ErrorMessage = "completed error",
                    IsServerOffline = true,
                },
                new LibrarySyncJobQueue
                {
                    PlexServerId = libraries[1].PlexServerId,
                    PlexLibraryId = libraries[1].Id,
                    Priority = 1,
                    Status = LibrarySyncJobStatus.Failed,
                    CreatedAt = originalCreatedAt,
                    StartedAt = originalCreatedAt.AddMinutes(1),
                    CompletedAt = originalCreatedAt.AddMinutes(2),
                    ErrorMessage = "failed error",
                    IsServerOffline = true,
                },
                new LibrarySyncJobQueue
                {
                    PlexServerId = libraries[2].PlexServerId,
                    PlexLibraryId = libraries[2].Id,
                    Priority = 1,
                    Status = LibrarySyncJobStatus.Processing,
                    CreatedAt = originalCreatedAt,
                    StartedAt = originalCreatedAt.AddMinutes(1),
                },
            ],
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var queueTableName = ((DbContext)dbContext).Model.FindEntityType(typeof(LibrarySyncJobQueue))!.GetTableName()!;
        var writeCounter = new QueueResetWriteCounter(queueTableName);
        await using var commandContext = CreateContext(dbContext.DatabaseName, writeCounter);
        SetupDependencies(builder =>
        {
            builder
                .RegisterInstance(commandContext)
                .As<IReaparrDbContext>()
                .ExternallyOwned();
            builder.RegisterType<QueueLibrarySyncJobCommandHandler>().InstancePerDependency();
        });
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        var command = new QueueLibrarySyncJobCommand(resetLibraryIds);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        writeCounter.ResetUpdateCount.ShouldBe(1);
        var queueItems = await dbContext
            .LibrarySyncJobQueues.OrderBy(x => x.PlexLibraryId)
            .Select(x =>
                new
                {
                    x.PlexServerId,
                    x.PlexLibraryId,
                    x.Status,
                    x.CreatedAt,
                    x.StartedAt,
                    x.CompletedAt,
                    x.ErrorMessage,
                    x.IsServerOffline,
                }
            )
            .ToListAsync(CancellationToken);
        queueItems
            .Select(x => (x.PlexServerId, x.PlexLibraryId, x.Status))
            .ShouldBe(
                [
                    (libraries[0].PlexServerId, libraries[0].Id, LibrarySyncJobStatus.Queued),
                    (libraries[1].PlexServerId, libraries[1].Id, LibrarySyncJobStatus.Queued),
                    (libraries[2].PlexServerId, libraries[2].Id, LibrarySyncJobStatus.Processing),
                ]
            );
        var resetRows = queueItems.Where(x => resetLibraryIds.Contains(x.PlexLibraryId)).ToList();
        resetRows.ShouldAllBe(x =>
            x.CreatedAt != originalCreatedAt
            && x.StartedAt == null
            && x.CompletedAt == null
            && x.ErrorMessage == null
            && !x.IsServerOffline
        );
        resetRows.Select(x => x.CreatedAt).Distinct().Count().ShouldBe(1);
        var processingRow = queueItems.Single(x => x.PlexLibraryId == libraries[2].Id);
        processingRow.CreatedAt.ShouldBe(originalCreatedAt);
        processingRow.StartedAt.ShouldNotBeNull();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldEmitOnlyRowsActuallyReset_WhenAnotherHandlerWinsPartOfBatch()
    {
        // Arrange
        await SetupDatabase(
            3043,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
            }
        );
        using var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var firstLibrary = libraries[0];
        var secondLibrary = libraries[1];
        var originalCreatedAt = DateTime.UtcNow.AddHours(-6);
        await dbContext.LibrarySyncJobQueues.AddRangeAsync(
            [
                new LibrarySyncJobQueue
                {
                    PlexServerId = firstLibrary.PlexServerId,
                    PlexLibraryId = firstLibrary.Id,
                    Priority = 1,
                    Status = LibrarySyncJobStatus.Completed,
                    CreatedAt = originalCreatedAt,
                    CompletedAt = originalCreatedAt,
                },
                new LibrarySyncJobQueue
                {
                    PlexServerId = secondLibrary.PlexServerId,
                    PlexLibraryId = secondLibrary.Id,
                    Priority = 1,
                    Status = LibrarySyncJobStatus.Completed,
                    CreatedAt = originalCreatedAt,
                    CompletedAt = originalCreatedAt,
                },
            ],
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var updateGate = new QueueResetUpdateGate();
        await using var firstContext = CreateContext(dbContext.DatabaseName, updateGate);
        await using var secondContext = CreateContext(dbContext.DatabaseName);
        var contextNumber = 0;
        SetupDependencies(builder =>
        {
            builder
                .Register((_, _) =>
                    Interlocked.Increment(ref contextNumber) == 1 ? firstContext : secondContext
                )
                .As<IReaparrDbContext>()
                .ExternallyOwned();
            builder.RegisterType<QueueLibrarySyncJobCommandHandler>().InstancePerDependency();
        });

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(2));

        var firstCommand = new QueueLibrarySyncJobCommand([firstLibrary.Id, secondLibrary.Id]);
        var secondCommand = new QueueLibrarySyncJobCommand([firstLibrary.Id]);

        // Act
        Task<Result>? firstTask = null;
        Task<Result>? secondTask = null;
        try
        {
            firstTask = TestHandlerExecuteAsync(firstCommand);
            await updateGate.UpdateEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            secondTask = TestHandlerExecuteAsync(secondCommand);
            var secondResult = await secondTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            updateGate.ReleaseUpdate.TrySetResult();
            var firstResult = await firstTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

        // Assert
        firstResult.IsSuccess.ShouldBeTrue();
        firstResult.Errors.Count.ShouldBe(0);
        secondResult.IsSuccess.ShouldBeTrue();
        secondResult.Errors.Count.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();

        var queueItems = await dbContext
            .LibrarySyncJobQueues
            .Where(x => x.PlexLibraryId == firstLibrary.Id || x.PlexLibraryId == secondLibrary.Id)
            .OrderBy(x => x.PlexLibraryId)
            .Select(x => new
            {
                x.PlexLibraryId,
                x.PlexServerId,
                x.Status,
                x.CreatedAt,
                x.CompletedAt,
            })
            .ToListAsync(CancellationToken);
        queueItems.Count.ShouldBe(2);
        queueItems
            .Select(x => (x.PlexLibraryId, x.PlexServerId, x.Status))
            .ShouldBe(
                [
                    (firstLibrary.Id, firstLibrary.PlexServerId, LibrarySyncJobStatus.Queued),
                    (secondLibrary.Id, secondLibrary.PlexServerId, LibrarySyncJobStatus.Queued),
                ]
            );
        queueItems.ShouldAllBe(x => x.CreatedAt != originalCreatedAt && x.CompletedAt == null);

        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.LibrarySyncQueued(
                        It.Is<(int PlexLibraryId, int PlexServerId, DateTime CreatedAt, bool ForceMediaRefresh)>(
                            admission =>
                                admission.PlexLibraryId == firstLibrary.Id
                                && admission.PlexServerId == firstLibrary.PlexServerId
                                && admission.CreatedAt == queueItems.Single(y => y.PlexLibraryId == firstLibrary.Id).CreatedAt
                                && !admission.ForceMediaRefresh
                        ),
                        firstLibrary.Type,
                        false,
                        TelemetryTrigger.Manual
                    ),
                Times.Once()
            );
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.LibrarySyncQueued(
                        It.Is<(int PlexLibraryId, int PlexServerId, DateTime CreatedAt, bool ForceMediaRefresh)>(
                            admission =>
                                admission.PlexLibraryId == secondLibrary.Id
                                && admission.PlexServerId == secondLibrary.PlexServerId
                                && admission.CreatedAt == queueItems.Single(y => y.PlexLibraryId == secondLibrary.Id).CreatedAt
                                && !admission.ForceMediaRefresh
                        ),
                        secondLibrary.Type,
                        false,
                        TelemetryTrigger.Manual
                    ),
                Times.Once()
            );
        }
        finally
        {
            updateGate.ReleaseUpdate.TrySetResult();
            if (firstTask is not null)
                await firstTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            if (secondTask is not null)
                await secondTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
        }
    }

    [Test]
    public async Task ShouldNotEmitAdmission_WhenAnotherHandlerWinsEveryReset()
    {
        // Arrange
        await SetupDatabase(
            3044,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );
        using var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var originalCreatedAt = DateTime.UtcNow.AddHours(-6);
        await dbContext.LibrarySyncJobQueues.AddAsync(
            new LibrarySyncJobQueue
            {
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Completed,
                CreatedAt = originalCreatedAt,
                CompletedAt = originalCreatedAt,
            },
            CancellationToken
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var updateGate = new QueueResetUpdateGate();
        await using var firstContext = CreateContext(dbContext.DatabaseName, updateGate);
        await using var secondContext = CreateContext(dbContext.DatabaseName);
        var contextNumber = 0;
        SetupDependencies(builder =>
        {
            builder
                .Register((_, _) =>
                    Interlocked.Increment(ref contextNumber) == 1 ? firstContext : secondContext
                )
                .As<IReaparrDbContext>()
                .ExternallyOwned();
            builder.RegisterType<QueueLibrarySyncJobCommandHandler>().InstancePerDependency();
        });

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CheckQueuedPlexLibraryToSyncCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(2));

        var command = new QueueLibrarySyncJobCommand([library.Id]);

        // Act
        Task<Result>? firstTask = null;
        Task<Result>? secondTask = null;
        try
        {
            firstTask = TestHandlerExecuteAsync(command);
            await updateGate.UpdateEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            secondTask = TestHandlerExecuteAsync(command);
            var secondResult = await secondTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            updateGate.ReleaseUpdate.TrySetResult();
            var firstResult = await firstTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

        // Assert
        firstResult.IsSuccess.ShouldBeTrue();
        firstResult.Errors.Count.ShouldBe(0);
        secondResult.IsSuccess.ShouldBeTrue();
        secondResult.Errors.Count.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();

        var queueItem = await dbContext
            .LibrarySyncJobQueues
            .SingleAsync(x => x.PlexLibraryId == library.Id, CancellationToken);
        queueItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        queueItem.CreatedAt.ShouldNotBe(originalCreatedAt);
        queueItem.CompletedAt.ShouldBeNull();
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.LibrarySyncQueued(
                        It.Is<(int PlexLibraryId, int PlexServerId, DateTime CreatedAt, bool ForceMediaRefresh)>(
                            admission =>
                                admission.PlexLibraryId == library.Id
                                && admission.PlexServerId == library.PlexServerId
                                && admission.CreatedAt == queueItem.CreatedAt
                                && !admission.ForceMediaRefresh
                        ),
                        library.Type,
                        false,
                        TelemetryTrigger.Manual
                    ),
                Times.Once()
            );
        }
        finally
        {
            updateGate.ReleaseUpdate.TrySetResult();
            if (firstTask is not null)
                await firstTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            if (secondTask is not null)
                await secondTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
        }
    }

    private ReaparrDbContext CreateContext(string databaseName, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<ReaparrDbContext>();
        options.ConfigureSqlite(databaseName, SqliteOpenMode.Memory);
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        if (interceptor is not null)
            options.AddInterceptors(interceptor);
        return new ReaparrDbContext(
            options.Options,
            Mock.Container.Resolve<ILogger>(),
            Mock.Container.Resolve<IPathProvider>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            databaseName
        );
    }

    private sealed class QueueResetWriteCounter : DbCommandInterceptor
    {
        private readonly string _queueTableName;
        private int _resetUpdateCount;

        public QueueResetWriteCounter(string queueTableName)
        {
            _queueTableName = queueTableName;
        }

        public int ResetUpdateCount => Volatile.Read(ref _resetUpdateCount);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            if (
                command.CommandText.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains(_queueTableName, StringComparison.Ordinal)
            )
                Interlocked.Increment(ref _resetUpdateCount);

            return ValueTask.FromResult(result);
        }
    }

    private sealed class QueueResetUpdateGate : DbTransactionInterceptor
    {
        private int _transactionPauseClaimed;

        public TaskCompletionSource UpdateEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseUpdate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection,
            TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default
        )
        {
            if (Interlocked.Exchange(ref _transactionPauseClaimed, 1) == 0)
            {
                UpdateEntered.TrySetResult();
                await ReleaseUpdate.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }
}
