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
        ((DbContext)dbContext).ChangeTracker.Clear();

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
        var log = await dbContext.DownloadTaskPhotoImageFileLogs.SingleAsync(CancellationToken);
        log.Id.ShouldBe(movieLog.Id);
        log.Message.ShouldBe("photo-log");
        log.LogLevel.ShouldBe(NotificationLevel.Error);
        log.Status.ShouldBe(DownloadStatus.Error);
        log.DownloadTaskFileId.ShouldBe(file.Id);
        log.DownloadTaskPhotoId.ShouldBe(file.ParentId);
        log.DownloadTaskPhotoAlbumId.ShouldBe(file.Parent!.ParentId);
        (await dbContext.DownloadTaskMovieFileLogs.Select(x => x.Message).ToListAsync(CancellationToken)).ShouldBe([
            "movie-control",
        ]);
    }

    [Test]
    [Arguments(DownloadTaskType.PhotoAlbum, false)]
    [Arguments(DownloadTaskType.PhotoAlbum, true)]
    [Arguments(DownloadTaskType.PhotoImage, false)]
    [Arguments(DownloadTaskType.PhotoImage, true)]
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
        dbContext.DownloadTaskPhotoImageFileLogs.AddRange(
            PhotoLog(file, "photo-1"),
            PhotoLog(file, "photo-2"),
            PhotoLog(file, "photo-3"),
            PhotoLog(sibling, "sibling")
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var logs = await dbContext
            .DownloadTaskPhotoImageFileLogs.Where(x => x.DownloadTaskFileId == file.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        logs.Select(x => x.Message).ShouldBe(["photo-1", "photo-2", "photo-3"]);
        var key = file.ToKey() with
        {
            Type = type,
            Id = type switch
            {
                DownloadTaskType.PhotoAlbum => file.Parent!.ParentId,
                DownloadTaskType.PhotoImage => file.ParentId,
                _ => file.Id,
            },
        };

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
    [Arguments(DownloadTaskType.PhotoImage)]
    [Arguments(DownloadTaskType.PhotoData)]
    [Arguments(DownloadTaskType.PhotoPart)]
    public async Task ShouldDeleteOnlyRequestedPhotoLogs_WhenSiblingLogsExist(DownloadTaskType type)
    {
        // Arrange
        await SetupDatabase(62313, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var file = await AddPhotoTask(dbContext, 1);
        var sibling = await AddPhotoTask(dbContext, 2);
        dbContext.DownloadTaskPhotoImageFileLogs.AddRange(PhotoLog(file, "target"), PhotoLog(sibling, "sibling"));
        await dbContext.SaveChangesAsync(CancellationToken);
        (
            await dbContext
                .DownloadTaskPhotoImageFileLogs.OrderBy(x => x.Id)
                .Select(x => x.Message)
                .ToListAsync(CancellationToken)
        ).ShouldBe(["target", "sibling"]);
        var key = file.ToKey() with
        {
            Type = type,
            Id = type switch
            {
                DownloadTaskType.PhotoAlbum => file.Parent!.ParentId,
                DownloadTaskType.PhotoImage => file.ParentId,
                _ => file.Id,
            },
        };

        // Act
        var result = await dbContext.DeleteDownloadTaskLogsAsync(key, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(1);
        (
            await dbContext
                .DownloadTaskPhotoImageFileLogs.Select(x => new { x.DownloadTaskFileId, x.Message })
                .ToListAsync(CancellationToken)
        ).ShouldBe([new { DownloadTaskFileId = sibling.Id, Message = "sibling" }]);
    }

    [Test]
    public async Task ShouldAggregateAlbumLogsWithoutMixingImageScopes_WhenAlbumHasSeveralImages()
    {
        var seed = await SetupDatabase(62314, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var albums = FakeData
            .GetDownloadTaskPhotoAlbum(seed)
            .RuleFor(x => x.Children, _ => FakeData.GetDownloadTaskPhotoImage(seed).Generate(2))
            .Generate(1);
        albums.SetRelationshipIds(library.PlexServerId, library.Id);
        dbContext.DownloadTaskPhotoAlbums.AddRange(albums);
        await dbContext.SaveChangesAsync(CancellationToken);
        var album = albums.Single();
        var images = album.Children.ToList();
        await dbContext.CreateDownloadClientLog(
            images[0].Children.Single().ToKey(),
            NotificationLevel.Information,
            DownloadStatus.Downloading,
            "first-image"
        );
        await dbContext.CreateDownloadClientLog(
            images[1].Children.Single().ToKey(),
            NotificationLevel.Information,
            DownloadStatus.Downloading,
            "second-image"
        );

        var albumLogs = await dbContext.GetDownloadTaskLogsAsync(album.ToKey(), null, null, CancellationToken);
        albumLogs.IsSuccess.ShouldBeTrue();
        albumLogs.Value.Select(x => x.Message).ShouldBe(["first-image", "second-image"]);
        var imageLogs = await dbContext.GetDownloadTaskLogsAsync(images[0].ToKey(), null, null, CancellationToken);
        imageLogs.IsSuccess.ShouldBeTrue();
        imageLogs.Value.Select(x => x.Message).ShouldBe(["first-image"]);

        var deleted = await dbContext.DeleteDownloadTaskLogsAsync(images[0].ToKey(), CancellationToken);
        deleted.IsSuccess.ShouldBeTrue();
        deleted.Value.ShouldBe(1);
        albumLogs = await dbContext.GetDownloadTaskLogsAsync(album.ToKey(), null, null, CancellationToken);
        albumLogs.IsSuccess.ShouldBeTrue();
        albumLogs.Value.Select(x => x.Message).ShouldBe(["second-image"]);
    }

    private static async Task<DownloadTaskPhotoImageFile> AddPhotoTask(IReaparrDbContext dbContext, int index)
    {
        var library = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum);
        var albums = FakeData
            .GetDownloadTaskPhotoAlbum(new Seed(index))
            .RuleFor(
                x => x.Children,
                _ => FakeData.GetDownloadTaskPhotoImage(new Seed(index + 100)).Generate(1)
            )
            .Generate(1);
        albums.SetRelationshipIds(library.PlexServerId, library.Id);
        dbContext.DownloadTaskPhotoAlbums.AddRange(albums);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return albums.Single().Children.Single().Children.Single();
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
            DownloadTaskPhotoAlbumId = imageFile.Parent!.ParentId,
        };
}
