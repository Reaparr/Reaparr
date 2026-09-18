namespace Reaparr.Application.UnitTests;

public class CleanupLibrarySyncJobQueueCommandHandlerUnitTests : BaseUnitTest<CleanupLibrarySyncJobQueueCommandHandler>
{
    private void SetupSignalRMock()
    {
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask);
    }

    [Test]
    public async Task ShouldDeleteCompletedItems_WhenCompletedItemsExist()
    {
        // Arrange
        await SetupDatabase(
            2001,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
            }
        );

        // Use a single context instance to ensure data is saved and visible
        var dbContext = IDbContext;
        var server = dbContext.PlexServers.First();
        var libraries = dbContext.PlexLibraries.ToList();

        var completedItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = libraries[0].Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
        };

        var queuedItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = libraries[1].Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Queued,
            CreatedAt = DateTime.UtcNow,
        };

        await dbContext.LibrarySyncJobQueues.AddRangeAsync([completedItem, queuedItem], CancellationToken);
        var saveResult = await dbContext.SaveChangesAsync(CancellationToken);
        saveResult.ShouldBeGreaterThan(0);

        SetupSignalRMock();
        var command = new CleanupLibrarySyncJobQueueCommand();

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Use AsNoTracking to see ExecuteDeleteAsync changes
        var remainingItems = await dbContext.LibrarySyncJobQueues.AsNoTracking().ToListAsync(CancellationToken);
        remainingItems.Count.ShouldBe(1);
        remainingItems[0].Status.ShouldBe(LibrarySyncJobStatus.Queued);
        remainingItems[0].PlexLibraryId.ShouldBe(libraries[1].Id);
    }

    [Test]
    public async Task ShouldRequeueFailedItems_WhenFailedItemsExist()
    {
        // Arrange
        await SetupDatabase(
            2002,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        // Use a single context instance to ensure data is saved and visible
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
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ErrorMessage = "Test error",
        };

        await dbContext.LibrarySyncJobQueues.AddAsync(failedItem, CancellationToken);
        var saveResult = await dbContext.SaveChangesAsync(CancellationToken);
        saveResult.ShouldBeGreaterThan(0);

        // Verify the item was saved
        var savedItems = await dbContext
            .LibrarySyncJobQueues.Where(x => x.PlexLibraryId == library.Id && x.Status == LibrarySyncJobStatus.Failed)
            .ToListAsync(CancellationToken);
        savedItems.Count.ShouldBe(1);

        SetupSignalRMock();
        var command = new CleanupLibrarySyncJobQueueCommand();

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Use AsNoTracking to see ExecuteUpdateAsync changes
        var updatedItems = await dbContext
            .LibrarySyncJobQueues.AsNoTracking()
            .Where(x => x.PlexLibraryId == library.Id)
            .ToListAsync(CancellationToken);
        updatedItems.Count.ShouldBe(1);
        var updatedItem = updatedItems[0];
        updatedItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        updatedItem.StartedAt.ShouldBeNull();
        updatedItem.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task ShouldCancelProcessingItems_WhenProcessingItemsExist()
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

        // Use a single context instance to ensure data is saved and visible
        var dbContext = IDbContext;
        var server = dbContext.PlexServers.First();
        var library = dbContext.PlexLibraries.First();
        var startedAt = DateTime.UtcNow.AddMinutes(-5);

        var processingItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = library.Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Processing,
            CreatedAt = DateTime.UtcNow,
            StartedAt = startedAt,
        };

        await dbContext.LibrarySyncJobQueues.AddAsync(processingItem, CancellationToken);
        var saveResult = await dbContext.SaveChangesAsync(CancellationToken);
        saveResult.ShouldBeGreaterThan(0);

        // Verify the item was saved
        var savedItems = await dbContext
            .LibrarySyncJobQueues.Where(x =>
                x.PlexLibraryId == library.Id && x.Status == LibrarySyncJobStatus.Processing
            )
            .ToListAsync(CancellationToken);
        savedItems.Count.ShouldBe(1);

        SetupSignalRMock();
        var command = new CleanupLibrarySyncJobQueueCommand();

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Use AsNoTracking to see ExecuteUpdateAsync changes
        var updatedItem = await dbContext
            .LibrarySyncJobQueues.AsNoTracking()
            .FirstAsync(x => x.PlexLibraryId == library.Id, CancellationToken);
        updatedItem.Status.ShouldBe(LibrarySyncJobStatus.Cancelled);
        updatedItem.StartedAt.ShouldBe(startedAt);
        updatedItem.CompletedAt.ShouldNotBeNull();
        updatedItem.ErrorMessage.ShouldBeNull();
        updatedItem.IsServerOffline.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldRequeueFailedAndCancelProcessingItems_WhenRecoveringQueue()
    {
        // Arrange
        await SetupDatabase(
            2004,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
            }
        );

        // Use a single context instance to ensure data is saved and visible
        var dbContext = IDbContext;
        var server = dbContext.PlexServers.First();
        var libraries = dbContext.PlexLibraries.ToList();
        var failedStartedAt = DateTime.UtcNow.AddHours(-1);
        var processingStartedAt = DateTime.UtcNow.AddHours(-2);

        var failedItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = libraries[0].Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Failed,
            CreatedAt = DateTime.UtcNow,
            StartedAt = failedStartedAt,
            CompletedAt = DateTime.UtcNow,
            ErrorMessage = "Failed error message",
        };

        var processingItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = libraries[1].Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Processing,
            CreatedAt = DateTime.UtcNow,
            StartedAt = processingStartedAt,
        };

        await dbContext.LibrarySyncJobQueues.AddRangeAsync([failedItem, processingItem], CancellationToken);
        var saveResult = await dbContext.SaveChangesAsync(CancellationToken);
        saveResult.ShouldBeGreaterThan(0);

        SetupSignalRMock();
        var command = new CleanupLibrarySyncJobQueueCommand();

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Use AsNoTracking to see ExecuteUpdateAsync changes
        var updatedItems = await dbContext.LibrarySyncJobQueues.AsNoTracking().ToListAsync(CancellationToken);
        updatedItems.Count.ShouldBe(2);

        var updatedFailedItem = updatedItems.Single(x => x.PlexLibraryId == libraries[0].Id);
        updatedFailedItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        updatedFailedItem.StartedAt.ShouldBeNull();
        updatedFailedItem.CompletedAt.ShouldBeNull();
        updatedFailedItem.ErrorMessage.ShouldBeNull();

        var updatedProcessingItem = updatedItems.Single(x => x.PlexLibraryId == libraries[1].Id);
        updatedProcessingItem.Status.ShouldBe(LibrarySyncJobStatus.Cancelled);
        updatedProcessingItem.StartedAt.ShouldBe(processingStartedAt);
        updatedProcessingItem.CompletedAt.ShouldNotBeNull();
        updatedProcessingItem.ErrorMessage.ShouldBeNull();
        updatedProcessingItem.IsServerOffline.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldReturnOk_WhenQueueIsEmpty()
    {
        // Arrange
        await SetupDatabase(
            2005,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
            }
        );

        SetupSignalRMock();
        var command = new CleanupLibrarySyncJobQueueCommand();

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var items = await IDbContext.LibrarySyncJobQueues.ToListAsync(CancellationToken);
        items.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ICommand<Result>>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldHandleMixedStatuses_WhenMultipleStatusesExist()
    {
        // Arrange
        await SetupDatabase(
            2006,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 5;
            }
        );

        // Use a single context instance to ensure data is saved and visible
        var dbContext = IDbContext;
        var server = dbContext.PlexServers.First();
        var libraries = dbContext.PlexLibraries.ToList();

        var completedItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = libraries[0].Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
        };

        var failedItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = libraries[1].Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Failed,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            ErrorMessage = "Error",
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

        var queuedItem = new LibrarySyncJobQueue
        {
            PlexServerId = server.Id,
            PlexLibraryId = libraries[3].Id,
            Priority = 1,
            Status = LibrarySyncJobStatus.Queued,
            CreatedAt = DateTime.UtcNow,
        };

        await dbContext.LibrarySyncJobQueues.AddRangeAsync(
            [completedItem, failedItem, processingItem, queuedItem],
            CancellationToken
        );
        var saveResult = await dbContext.SaveChangesAsync(CancellationToken);
        saveResult.ShouldBeGreaterThan(0);

        SetupSignalRMock();
        var command = new CleanupLibrarySyncJobQueueCommand();

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var remainingItems = await dbContext.LibrarySyncJobQueues.AsNoTracking().ToListAsync(CancellationToken);

        // Completed item should be deleted, failed should be requeued, processing should be cancelled, queued should remain
        remainingItems.Count.ShouldBe(3);
        remainingItems.Any(x => x.PlexLibraryId == libraries[0].Id).ShouldBeFalse();

        var updatedFailedItem = remainingItems.Single(x => x.PlexLibraryId == libraries[1].Id);
        updatedFailedItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
        updatedFailedItem.StartedAt.ShouldBeNull();
        updatedFailedItem.CompletedAt.ShouldBeNull();
        updatedFailedItem.ErrorMessage.ShouldBeNull();

        var updatedProcessingItem = remainingItems.Single(x => x.PlexLibraryId == libraries[2].Id);
        updatedProcessingItem.Status.ShouldBe(LibrarySyncJobStatus.Cancelled);
        updatedProcessingItem.CompletedAt.ShouldNotBeNull();
        updatedProcessingItem.ErrorMessage.ShouldBeNull();

        var remainingQueuedItem = remainingItems.Single(x => x.PlexLibraryId == libraries[3].Id);
        remainingQueuedItem.Status.ShouldBe(LibrarySyncJobStatus.Queued);
    }
}
