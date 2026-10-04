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
        await dbContext.CreateDownloadClientLog(files[0].ToKey(), NotificationLevel.Information, DownloadStatus.Downloading, "target");
        await dbContext.CreateDownloadClientLog(files[1].ToKey(), NotificationLevel.Warning, DownloadStatus.Error, "control");

        // Act
        var readResult = await dbContext.GetDownloadTaskLogsAsync(files[0].ToParentKey(), null, null, CancellationToken);
        var deleteResult = await dbContext.DeleteDownloadTaskLogsAsync(files[0].ToParentKey(), CancellationToken);

        // Assert
        readResult.IsSuccess.ShouldBeTrue();
        readResult.Errors.Count.ShouldBe(0);
        readResult.Value.Select(x => x.Message).ShouldBe(["target"]);
        deleteResult.IsSuccess.ShouldBeTrue();
        deleteResult.Errors.Count.ShouldBe(0);
        deleteResult.Value.ShouldBe(1);
        (await dbContext.DownloadTaskMovieFileLogs.Select(x => x.Message).ToListAsync(CancellationToken)).ShouldBe(["control"]);
    }
}
