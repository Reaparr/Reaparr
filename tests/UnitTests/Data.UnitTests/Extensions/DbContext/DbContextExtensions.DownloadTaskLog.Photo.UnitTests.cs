namespace Reaparr.Data.UnitTests;

public class DbContextExtensionsDownloadTaskLogPhotoUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldPersistPhotoLogWithFileIdOnly_WhenMovieLogUsesTheSameIdentity()
    {
        // Arrange
        var seed = await SetupDatabase(
            62311,
            config =>
            {
                config.PlexPhotoLibraryCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var folders = await AddPhotoFolders(
            dbContext,
            library,
            seed,
            rootCount: 1,
            filesPerFolder: 1,
            includeNested: false
        );
        var file = folders.Single(x => x.ParentId is null).Files.Single();
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
            file.ToKey(),
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
        (await dbContext.DownloadTaskMovieFileLogs.Select(x => x.Message).ToListAsync(CancellationToken)).ShouldBe([
            "movie-control",
        ]);
        (
            await dbContext
                .DownloadTaskPhotoImageFileLogs.Select(x => x.DownloadTaskFileId)
                .ToListAsync(CancellationToken)
        ).ShouldBe([file.Id]);
    }

    [Test]
    [Arguments(DownloadTaskType.PhotoFolder, false)]
    [Arguments(DownloadTaskType.PhotoFolder, true)]
    [Arguments(DownloadTaskType.PhotoImage, false)]
    [Arguments(DownloadTaskType.PhotoImage, true)]
    public async Task ShouldReturnOrderedPhotoLogsForRequestedScope_WhenNestedAndSiblingLogsExist(
        DownloadTaskType type,
        bool incremental
    )
    {
        // Arrange
        var seed = await SetupDatabase(62312, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var folders = await AddPhotoFolders(
            dbContext,
            library,
            seed,
            rootCount: 2,
            filesPerFolder: 1,
            includeNested: true
        );
        var roots = folders.Where(x => x.ParentId is null).OrderBy(x => x.Id).ToArray();
        var root = roots[0];
        var child = folders.Single(x => x.ParentId == root.Id);
        var sibling = roots[1];
        var targetFile = root.Files.Single();
        var nestedFile = child.Files.Single();
        var targetLogs = new[]
        {
            PhotoLog(targetFile, "photo-1"),
            PhotoLog(targetFile, "photo-2"),
            PhotoLog(targetFile, "photo-3"),
        };
        var nestedLogs = new[] { PhotoLog(nestedFile, "nested-1"), PhotoLog(nestedFile, "nested-2") };
        var siblingLogs = new[] { PhotoLog(sibling.Files.Single(), "sibling") };
        dbContext.DownloadTaskPhotoImageFileLogs.AddRange(targetLogs.Concat(nestedLogs).Concat(siblingLogs));
        await dbContext.SaveChangesAsync(CancellationToken);
        var key = type == DownloadTaskType.PhotoFolder ? root.ToKey() : targetFile.ToKey();

        // Act
        var result = await dbContext.GetDownloadTaskLogsAsync(
            key,
            incremental ? targetLogs[0].Id : null,
            incremental ? 1 : null,
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var expected = type == DownloadTaskType.PhotoFolder ? targetLogs.Concat(nestedLogs).ToArray() : targetLogs;
        if (incremental)
            expected = [expected[1]];
        result.Value.Select(x => (x.Id, x.Message)).ShouldBe(expected.Select(x => (x.Id, x.Message)));
    }

    [Test]
    public async Task ShouldDeleteOnlyRequestedPhotoLogs_WhenDirectFileAndFolderScopesOverlap()
    {
        // Arrange
        var seed = await SetupDatabase(62313, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var folders = await AddPhotoFolders(
            dbContext,
            library,
            seed,
            rootCount: 2,
            filesPerFolder: 1,
            includeNested: true
        );
        var roots = folders.Where(x => x.ParentId is null).OrderBy(x => x.Id).ToArray();
        var targetRoot = roots[0];
        var targetChild = folders.Single(x => x.ParentId == targetRoot.Id);
        var targetFile = targetRoot.Files.Single();
        var nestedFile = targetChild.Files.Single();
        var controlFile = roots[1].Files.Single();
        dbContext.DownloadTaskPhotoImageFileLogs.AddRange(
            PhotoLog(targetFile, "target-file"),
            PhotoLog(nestedFile, "target-nested"),
            PhotoLog(controlFile, "control")
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var fileResult = await dbContext.DeleteDownloadTaskLogsAsync(targetFile.ToKey(), CancellationToken);

        // Assert
        fileResult.IsSuccess.ShouldBeTrue();
        fileResult.Errors.Count.ShouldBe(0);
        fileResult.Value.ShouldBe(1);
        (
            await dbContext
                .DownloadTaskPhotoImageFileLogs.Select(x => new { x.DownloadTaskFileId, x.Message })
                .OrderBy(x => x.DownloadTaskFileId)
                .ToListAsync(CancellationToken)
        ).ShouldBe(
            new[]
            {
                new { DownloadTaskFileId = nestedFile.Id, Message = "target-nested" },
                new { DownloadTaskFileId = controlFile.Id, Message = "control" },
            }.OrderBy(x => x.DownloadTaskFileId)
        );

        var folderResult = await dbContext.DeleteDownloadTaskLogsAsync(targetRoot.ToKey(), CancellationToken);
        folderResult.IsSuccess.ShouldBeTrue();
        folderResult.Errors.Count.ShouldBe(0);
        folderResult.Value.ShouldBe(1);
        (
            await dbContext
                .DownloadTaskPhotoImageFileLogs.Select(x => new { x.DownloadTaskFileId, x.Message })
                .SingleAsync(CancellationToken)
        ).ShouldBe(new { DownloadTaskFileId = controlFile.Id, Message = "control" });
    }

    [Test]
    public async Task ShouldKeepPhotoFolderLogsIsolatedByRoot_WhenSeveralDirectFoldersShareALibrary()
    {
        // Arrange
        var seed = await SetupDatabase(62314, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var folders = await AddPhotoFolders(
            dbContext,
            library,
            seed,
            rootCount: 2,
            filesPerFolder: 1,
            includeNested: false
        );
        var roots = folders.Where(x => x.ParentId is null).OrderBy(x => x.Id).ToArray();
        var firstFile = roots[0].Files.Single();
        var secondFile = roots[1].Files.Single();
        dbContext.DownloadTaskPhotoImageFileLogs.AddRange(
            PhotoLog(firstFile, "first-image"),
            PhotoLog(secondFile, "second-image")
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var firstLogs = await dbContext.GetDownloadTaskLogsAsync(roots[0].ToKey(), null, null, CancellationToken);
        var secondLogs = await dbContext.GetDownloadTaskLogsAsync(roots[1].ToKey(), null, null, CancellationToken);

        // Assert
        firstLogs.IsSuccess.ShouldBeTrue();
        firstLogs.Errors.Count.ShouldBe(0);
        firstLogs.Value.Select(x => x.Message).ShouldBe(["first-image"]);
        secondLogs.IsSuccess.ShouldBeTrue();
        secondLogs.Errors.Count.ShouldBe(0);
        secondLogs.Value.Select(x => x.Message).ShouldBe(["second-image"]);
        (
            await dbContext
                .DownloadTaskPhotoImageFileLogs.Select(x => x.DownloadTaskFileId)
                .OrderBy(x => x)
                .ToListAsync(CancellationToken)
        ).ShouldBe(new[] { firstFile.Id, secondFile.Id }.OrderBy(x => x));
    }

    private static async Task<List<DownloadTaskPhotoFolder>> AddPhotoFolders(
        IReaparrDbContext dbContext,
        PlexLibrary library,
        Seed seed,
        int rootCount,
        int filesPerFolder,
        bool includeNested
    )
    {
        var roots = FakeData
            .GetDownloadTaskPhotoFolder(seed, config => config.PhotoImageDownloadTasksCount = filesPerFolder)
            .RuleFor(x => x.Id, faker => faker.Random.Guid())
            .Generate(rootCount)
            .ToList();
        var folders = new List<DownloadTaskPhotoFolder>();
        for (var index = 0; index < roots.Count; index++)
        {
            var root = roots[index];
            root.RelativePath = string.Empty;
            root.DownloadRootPath = "/downloads";
            root.DestinationRootPath = $"/destination/{root.Id:N}";
            foreach (var file in root.Files)
            {
                file.DirectoryMeta.DownloadRootPath = root.DownloadRootPath;
                file.DirectoryMeta.DestinationRootPath = root.DestinationRootPath;
            }
            folders.Add(root);
            if (!includeNested)
                continue;

            var child = FakeData
                .GetDownloadTaskPhotoFolder(
                    new Seed(seed.Next()),
                    config => config.PhotoImageDownloadTasksCount = filesPerFolder
                )
                .Generate();
            child.Parent = root;
            child.ParentId = root.Id;
            child.RelativePath = $"nested-{index}";
            root.Children.Add(child);
            folders.Add(child);
        }

        folders.SetRelationshipIds(library.PlexServerId, library.Id);
        dbContext.DownloadTaskPhotoFolders.AddRange(roots);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return folders;
    }

    private static DownloadTaskPhotoImageFileLog PhotoLog(DownloadTaskPhotoImageFile imageFile, string message) =>
        new()
        {
            Message = message,
            LogLevel = NotificationLevel.Information,
            Status = DownloadStatus.Downloading,
            CreatedAt = new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc),
            DownloadTaskFileId = imageFile.Id,
        };
}
