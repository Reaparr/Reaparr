namespace Reaparr.Data.UnitTests;

public class DbContextExtensionsDownloadTaskLogPhotoUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(DownloadTaskType.PhotoData)]
    [Arguments(DownloadTaskType.PhotoPart)]
    public async Task ShouldPersistPhotoLogWithParentId_WhenMovieLogUsesTheSameId(DownloadTaskType type)
    {
        // Arrange
        await SetupDatabase(
            62311,
            config =>
            {
                config.PlexPhotoLibraryCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var file = await AddPhotoTask(dbContext, 1);
        var movieFile = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        await dbContext.CreateDownloadClientLog(
            movieFile.ToKey(),
            NotificationLevel.Information,
            DownloadStatus.Downloading,
            "movie-control"
        );
        var movieLog = await dbContext.DownloadTaskMovieFileLogs.SingleAsync(CancellationToken);
        movieLog.Id.ShouldBe(1);

        // Act
        await dbContext.CreateDownloadClientLog(
            file.ToKey() with
            {
                Type = type,
            },
            NotificationLevel.Error,
            DownloadStatus.Error,
            "photo-log"
        );

        // Assert
        var log = await dbContext.DownloadTaskPhotoFileLogs.SingleAsync(CancellationToken);
        log.Id.ShouldBe(movieLog.Id);
        log.Message.ShouldBe("photo-log");
        log.LogLevel.ShouldBe(NotificationLevel.Error);
        log.Status.ShouldBe(DownloadStatus.Error);
        log.DownloadTaskFileId.ShouldBe(file.Id);
        log.DownloadTaskPhotoId.ShouldBe(file.ParentId);
        (await dbContext.DownloadTaskMovieFileLogs.Select(x => x.Message).ToListAsync(CancellationToken)).ShouldBe([
            "movie-control",
        ]);
    }

    [Test]
    [Arguments(DownloadTaskType.PhotoAlbum, false)]
    [Arguments(DownloadTaskType.PhotoAlbum, true)]
    [Arguments(DownloadTaskType.PhotoData, false)]
    [Arguments(DownloadTaskType.PhotoPart, false)]
    public async Task ShouldReturnOrderedPhotoLogsForRequestedScope_WhenSiblingLogsExist(
        DownloadTaskType type,
        bool incremental
    )
    {
        // Arrange
        await SetupDatabase(62312, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var file = await AddPhotoTask(dbContext, 1);
        var sibling = await AddPhotoTask(dbContext, 2);
        dbContext.DownloadTaskPhotoFileLogs.AddRange(
            PhotoLog(file, "photo-1"),
            PhotoLog(file, "photo-2"),
            PhotoLog(file, "photo-3"),
            PhotoLog(sibling, "sibling")
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var logs = await dbContext
            .DownloadTaskPhotoFileLogs.Where(x => x.DownloadTaskFileId == file.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        logs.Select(x => x.Message).ShouldBe(["photo-1", "photo-2", "photo-3"]);
        var key = file.ToKey() with { Type = type, Id = type == DownloadTaskType.PhotoAlbum ? file.ParentId : file.Id };

        // Act
        var result = await dbContext.GetDownloadTaskLogsAsync(
            key,
            incremental ? logs[0].Id : null,
            incremental ? 1 : null,
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var expected = incremental ? new[] { logs[1] } : logs.ToArray();
        result.Value.Select(x => (x.Id, x.Message)).ShouldBe(expected.Select(x => (x.Id, x.Message)));
    }

    [Test]
    [Arguments(DownloadTaskType.PhotoAlbum)]
    [Arguments(DownloadTaskType.PhotoData)]
    [Arguments(DownloadTaskType.PhotoPart)]
    public async Task ShouldDeleteOnlyRequestedPhotoLogs_WhenSiblingLogsExist(DownloadTaskType type)
    {
        // Arrange
        await SetupDatabase(62313, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var file = await AddPhotoTask(dbContext, 1);
        var sibling = await AddPhotoTask(dbContext, 2);
        dbContext.DownloadTaskPhotoFileLogs.AddRange(PhotoLog(file, "target"), PhotoLog(sibling, "sibling"));
        await dbContext.SaveChangesAsync(CancellationToken);
        (
            await dbContext
                .DownloadTaskPhotoFileLogs.OrderBy(x => x.Id)
                .Select(x => x.Message)
                .ToListAsync(CancellationToken)
        ).ShouldBe(["target", "sibling"]);
        var key = file.ToKey() with { Type = type, Id = type == DownloadTaskType.PhotoAlbum ? file.ParentId : file.Id };

        // Act
        var result = await dbContext.DeleteDownloadTaskLogsAsync(key, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(1);
        (
            await dbContext
                .DownloadTaskPhotoFileLogs.Select(x => new { x.DownloadTaskFileId, x.Message })
                .ToListAsync(CancellationToken)
        ).ShouldBe([new { DownloadTaskFileId = sibling.Id, Message = "sibling" }]);
    }

    private static async Task<DownloadTaskPhotoImageFile> AddPhotoTask(IReaparrDbContext dbContext, int index)
    {
        var library = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum);
        var createdAt = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc);
        var photo = new DownloadTaskPhotoImage
        {
            Id = Guid.NewGuid(),
            PlexApiRatingKey = 1000 + index,
            Title = $"photo-{index}",
            FullTitle = $"photo-{index}",
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = createdAt,
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Year = 2026,
            Children = [],
            DataReceived = 0,
            FileDataTransferred = 0,
            DataTotal = 0,
            DownloadSpeed = 0,
            FileTransferSpeed = 0,
        };
        var file = new DownloadTaskPhotoImageFile
        {
            Id = Guid.NewGuid(),
            ParentId = photo.Id,
            Parent = photo,
            PlexApiRatingKey = 3000 + index,
            Title = $"photo-{index}",
            FullTitle = $"photo-{index}",
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = createdAt,
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            PlexApiMediaId = 1,
            PlexApiPartId = 1,
            FileName = $"photo-{index}.jpg",
            FileLocationUrl = "/file",
            HashId = null,
            Quality = VideoQuality.HD,
            DirectoryMeta = new DownloadTaskDirectory
            {
                DownloadRootPath = "/downloads",
                DestinationRootPath = "/destination",
                MovieFolder = $"photo-{index}",
                TvShowFolder = string.Empty,
                SeasonFolder = string.Empty,
                KeepCompletedInDownloadFolder = false,
            },
            DataReceived = 0,
            DataTotal = 0,
            DownloadSpeed = 0,
            DirectDownloadSnapshot = null,
            DownloadClientType = PlexDownloadClientType.Direct,
            FileTransferSpeed = 0,
            FileDataTransferred = 0,
            TimeRemaining = 0,
            DestinationFolderPathId = null,
        };
        dbContext.DownloadTaskPhotoFiles.Add(file);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return file;
    }

    private static DownloadTaskPhotoImageFileLog PhotoLog(DownloadTaskPhotoImageFile imageFile, string message) =>
        new()
        {
            Message = message,
            LogLevel = NotificationLevel.Information,
            Status = DownloadStatus.Downloading,
            CreatedAt = new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc),
            DownloadTaskFileId = imageFile.Id,
            DownloadTaskPhotoId = imageFile.ParentId,
        };
}
