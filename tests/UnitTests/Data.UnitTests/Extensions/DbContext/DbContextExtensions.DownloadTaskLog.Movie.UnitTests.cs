namespace Reaparr.Data.UnitTests;

public class DbContextExtensionsDownloadTaskLogMovieUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldReadAndDeleteOnlyRequestedMovieLogs_WhenSiblingLogsExist()
    {
        // Arrange
        await SetupDatabase(62304, config => config.MovieDownloadTasksCount = 2);
        var dbContext = IDbContext;
        var files = await dbContext.DownloadTaskMovieFile.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await dbContext.CreateDownloadClientLog(
            files[0].ToKey(),
            NotificationLevel.Information,
            DownloadStatus.Downloading,
            "target"
        );
        await dbContext.CreateDownloadClientLog(
            files[1].ToKey(),
            NotificationLevel.Warning,
            DownloadStatus.Error,
            "control"
        );

        // Act
        var readResult = await dbContext.GetDownloadTaskLogsAsync(
            files[0].ToParentKey(),
            null,
            null,
            CancellationToken
        );
        var deleteResult = await dbContext.DeleteDownloadTaskLogsAsync(files[0].ToParentKey(), CancellationToken);

        // Assert
        readResult.IsSuccess.ShouldBeTrue();
        readResult.Errors.Count.ShouldBe(0);
        readResult.Value.Select(x => x.Message).ShouldBe(["target"]);
        deleteResult.IsSuccess.ShouldBeTrue();
        deleteResult.Errors.Count.ShouldBe(0);
        deleteResult.Value.ShouldBe(1);
        (await dbContext.DownloadTaskMovieFileLogs.Select(x => x.Message).ToListAsync(CancellationToken)).ShouldBe([
            "control",
        ]);
    }

    [Test]
    public async Task ShouldPersistTypedMovieLogBatch_WhenProvidedAsHomogeneousList()
    {
        // Arrange
        await SetupDatabase(62305, config => config.MovieDownloadTasksCount = 2);
        var dbContext = IDbContext;
        var files = await dbContext.DownloadTaskMovieFile.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        files.Count.ShouldBe(2);
        (await dbContext.DownloadTaskMovieFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
        var firstCreatedAt = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);
        var secondCreatedAt = firstCreatedAt.AddMinutes(1);
        List<DownloadTaskMovieFileLog> logs =
        [
            new()
            {
                DownloadTaskFileId = files[0].Id,
                DownloadTaskMovieId = files[0].ParentId,
                Message = "batch-target",
                Status = DownloadStatus.Downloading,
                LogLevel = NotificationLevel.Information,
                CreatedAt = firstCreatedAt,
            },
            new()
            {
                DownloadTaskFileId = files[1].Id,
                DownloadTaskMovieId = files[1].ParentId,
                Message = "batch-control",
                Status = DownloadStatus.Error,
                LogLevel = NotificationLevel.Warning,
                CreatedAt = secondCreatedAt,
            },
        ];

        // Act
        await dbContext.CreateDownloadClientLogs(logs, CancellationToken);

        // Assert
        var persisted = await dbContext
            .DownloadTaskMovieFileLogs.OrderBy(x => x.DownloadTaskFileId)
            .Select(x => new
            {
                x.DownloadTaskFileId,
                x.DownloadTaskMovieId,
                x.Message,
                x.Status,
                x.LogLevel,
                x.CreatedAt,
            })
            .ToListAsync(CancellationToken);
        persisted.ShouldBe([
            new
            {
                DownloadTaskFileId = files[0].Id,
                DownloadTaskMovieId = files[0].ParentId,
                Message = "batch-target",
                Status = DownloadStatus.Downloading,
                LogLevel = NotificationLevel.Information,
                CreatedAt = firstCreatedAt,
            },
            new
            {
                DownloadTaskFileId = files[1].Id,
                DownloadTaskMovieId = files[1].ParentId,
                Message = "batch-control",
                Status = DownloadStatus.Error,
                LogLevel = NotificationLevel.Warning,
                CreatedAt = secondCreatedAt,
            },
        ]);
    }
}
