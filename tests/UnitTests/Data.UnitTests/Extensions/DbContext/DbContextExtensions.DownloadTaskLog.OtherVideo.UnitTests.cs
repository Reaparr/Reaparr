namespace Reaparr.Data.UnitTests;

public class DbContextExtensionsDownloadTaskLogOtherVideoUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(DownloadTaskType.OtherVideoData)]
    [Arguments(DownloadTaskType.OtherVideoPart)]
    public async Task ShouldPersistOtherVideoLogWithParentId_WhenMovieLogUsesTheSameId(DownloadTaskType type)
    {
        // Arrange
        await SetupDatabase(62321, config =>
        {
            config.PlexOtherVideoLibraryCount = 1;
            config.MovieDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var file = await FakeData.AddOtherVideoTask(dbContext, 1);
        var movieFile = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        await dbContext.CreateDownloadClientLog(movieFile.ToKey(), NotificationLevel.Information, DownloadStatus.Downloading, "movie-control");
        var movieLog = await dbContext.DownloadTaskMovieFileLogs.SingleAsync(CancellationToken);
        movieLog.Id.ShouldBe(1);

        // Act
        await dbContext.CreateDownloadClientLog(file.ToKey() with { Type = type }, NotificationLevel.Information, DownloadStatus.DownloadFinished, "other-video-log");

        // Assert
        var log = await dbContext.DownloadTaskOtherVideoFileLogs.SingleAsync(CancellationToken);
        log.Id.ShouldBe(movieLog.Id);
        log.Message.ShouldBe("other-video-log");
        log.LogLevel.ShouldBe(NotificationLevel.Information);
        log.Status.ShouldBe(DownloadStatus.DownloadFinished);
        log.DownloadTaskFileId.ShouldBe(file.Id);
        log.DownloadTaskOtherVideoId.ShouldBe(file.ParentId);
        (await dbContext.DownloadTaskMovieFileLogs.Select(x => x.Message).ToListAsync(CancellationToken)).ShouldBe(["movie-control"]);
    }

    [Test]
    [Arguments(DownloadTaskType.OtherVideo, false)]
    [Arguments(DownloadTaskType.OtherVideo, true)]
    [Arguments(DownloadTaskType.OtherVideoData, false)]
    [Arguments(DownloadTaskType.OtherVideoPart, false)]
    public async Task ShouldReturnOrderedOtherVideoLogsForRequestedScope_WhenSiblingLogsExist(DownloadTaskType type, bool incremental)
    {
        // Arrange
        await SetupDatabase(62322, config => config.PlexOtherVideoLibraryCount = 1);
        var dbContext = IDbContext;
        var file = await FakeData.AddOtherVideoTask(dbContext, 1);
        var sibling = await FakeData.AddOtherVideoTask(dbContext, 2);
        dbContext.DownloadTaskOtherVideoFileLogs.AddRange(
            OtherVideoLog(file, "video-1"), OtherVideoLog(file, "video-2"), OtherVideoLog(file, "video-3"), OtherVideoLog(sibling, "sibling"));
        await dbContext.SaveChangesAsync(CancellationToken);
        var logs = await dbContext.DownloadTaskOtherVideoFileLogs.Where(x => x.DownloadTaskFileId == file.Id).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        logs.Select(x => x.Message).ShouldBe(["video-1", "video-2", "video-3"]);
        var key = file.ToKey() with { Type = type, Id = type == DownloadTaskType.OtherVideo ? file.ParentId : file.Id };

        // Act
        var result = await dbContext.GetDownloadTaskLogsAsync(key, incremental ? logs[0].Id : null, incremental ? 1 : null, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var expected = incremental ? new[] { logs[1] } : logs.ToArray();
        result.Value.Select(x => (x.Id, x.Message)).ShouldBe(expected.Select(x => (x.Id, x.Message)));
    }

    [Test]
    [Arguments(DownloadTaskType.OtherVideo)]
    [Arguments(DownloadTaskType.OtherVideoData)]
    [Arguments(DownloadTaskType.OtherVideoPart)]
    public async Task ShouldDeleteOnlyRequestedOtherVideoLogs_WhenSiblingLogsExist(DownloadTaskType type)
    {
        // Arrange
        await SetupDatabase(62323, config => config.PlexOtherVideoLibraryCount = 1);
        var dbContext = IDbContext;
        var file = await FakeData.AddOtherVideoTask(dbContext, 1);
        var sibling = await FakeData.AddOtherVideoTask(dbContext, 2);
        dbContext.DownloadTaskOtherVideoFileLogs.AddRange(OtherVideoLog(file, "target"), OtherVideoLog(sibling, "sibling"));
        await dbContext.SaveChangesAsync(CancellationToken);
        (await dbContext.DownloadTaskOtherVideoFileLogs.OrderBy(x => x.Id).Select(x => x.Message).ToListAsync(CancellationToken)).ShouldBe(["target", "sibling"]);
        var key = file.ToKey() with { Type = type, Id = type == DownloadTaskType.OtherVideo ? file.ParentId : file.Id };

        // Act
        var result = await dbContext.DeleteDownloadTaskLogsAsync(key, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(1);
        (await dbContext.DownloadTaskOtherVideoFileLogs.Select(x => new { x.DownloadTaskFileId, x.Message }).ToListAsync(CancellationToken))
            .ShouldBe([new { DownloadTaskFileId = sibling.Id, Message = "sibling" }]);
    }


    private static DownloadTaskOtherVideoFileLog OtherVideoLog(DownloadTaskOtherVideoFile file, string message) => new()
    {
        Message = message,
        LogLevel = NotificationLevel.Information,
        Status = DownloadStatus.Downloading,
        CreatedAt = new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc),
        DownloadTaskFileId = file.Id,
        DownloadTaskOtherVideoId = file.ParentId,
    };
}
