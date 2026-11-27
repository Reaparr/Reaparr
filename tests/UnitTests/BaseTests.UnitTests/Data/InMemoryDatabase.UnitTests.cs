namespace Reaparr.BaseTests.UnitTests;

public class InMemoryDatabaseUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldAddNotificationToInMemoryDatabase_WhenNotificationIsAdded()
    {
        // Arrange
        await SetupDatabase(5001);

        var notification = new Notification
        {
            Hidden = false,
            Level = NotificationLevel.Information,
            Message = "Test Notification",
            CreatedAt = DateTime.UtcNow,
        };

        // Act
        var dbContext = IDbContext;
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(CancellationToken);
        var notifications = await dbContext.Notifications.ToListAsync(CancellationToken);

        // Assert
        notifications.Count.ShouldBe(1);
    }

    [Test]
    public async Task ShouldAddAndRemoveNotificationToInMemoryDatabase_WhenNotificationIsAddedAndRemoved()
    {
        // Arrange
        await SetupDatabase(5002);

        var notification = new Notification
        {
            Hidden = false,
            Level = NotificationLevel.Information,
            Message = "Test Notification",
            CreatedAt = DateTime.UtcNow,
        };

        // Act
        var dbContext = IDbContext;
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.Notifications.Remove(notification);
        await dbContext.SaveChangesAsync(CancellationToken);
        var notifications = await dbContext.Notifications.ToListAsync(CancellationToken);

        // Assert
        notifications.Count.ShouldBe(0);
    }

    [Test]
    public async Task ShouldApplyOneSeedConfiguration_ToPersistedParentsAndFiles()
    {
        // Arrange
        var calls = 0;

        // Act
        await SetupDatabase(5003, config =>
        {
            calls++;
            config.MovieDownloadTasksCount = 1;
            config.IncludeMultiPartMovies = true;
            config.DownloadFileSizeInMb = 3;
        });

        // Assert
        calls.ShouldBe(1);
        var db = IDbContext;
        var parent = await db.DownloadTaskMovie.SingleAsync(CancellationToken);
        var files = await db.DownloadTaskMovieFile.ToListAsync(CancellationToken);
        files.Count.ShouldBe(2);
        files.ShouldAllBe(file => file.ParentId == parent.Id && file.DataTotal == 3L * 1024 * 1024);
    }
}
