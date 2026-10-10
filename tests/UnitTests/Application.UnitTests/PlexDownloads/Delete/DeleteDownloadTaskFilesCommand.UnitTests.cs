namespace Reaparr.Application.UnitTests;

public class DeleteDownloadTaskFilesCommandUnitTests : BaseUnitTest<DeleteDownloadTaskFilesCommandHandler>
{
    [Test]
    public async Task ShouldDeletePlainFileFromDownloadDirectory_WhenTaskIsCompleted()
    {
        // Arrange
        await SetupDatabase(
            84001,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var movieFileTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var movieFileKey = await dbContext.DownloadTaskMovieFile.ProjectToKey().FirstAsync(CancellationToken);

        // Completed tasks have the plain file (no .reaptemp) in the download directory.
        var plainFilePath = movieFileTask.DownloadFilePath.RemoveReapTempSuffix();
        SetupFileSystem(fs => fs.AddFile(plainFilePath, new MockFileData([])));

        // Act
        var result = await Sut.ExecuteAsync(new DeleteDownloadTaskFilesCommand([movieFileKey]), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var file = Mock.Container.Resolve<IFile>();
        file.Exists(plainFilePath).ShouldBeFalse();
    }

    [Test]
    public async Task ShouldDeleteReapTempFileFromDownloadDirectory_WhenFileHasTempSuffix()
    {
        // Arrange
        await SetupDatabase(
            84002,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var movieFileTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var movieFileKey = await dbContext.DownloadTaskMovieFile.ProjectToKey().FirstAsync(CancellationToken);

        // Active download: file still has .reaptemp suffix.
        var reapTempPath = movieFileTask.DownloadFilePath;
        SetupFileSystem(fs => fs.AddFile(reapTempPath, new MockFileData([])));

        // Act
        var result = await Sut.ExecuteAsync(new DeleteDownloadTaskFilesCommand([movieFileKey]), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var file = Mock.Container.Resolve<IFile>();
        file.Exists(reapTempPath).ShouldBeFalse();
    }

    [Test]
    public async Task ShouldDeleteBothReapTempAndPlainFiles_WhenBothExist()
    {
        // Arrange
        await SetupDatabase(
            84014,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var movieFileTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var movieFileKey = await dbContext.DownloadTaskMovieFile.ProjectToKey().FirstAsync(CancellationToken);

        var reapTempPath = movieFileTask.DownloadFilePath;
        var plainPath = reapTempPath.RemoveReapTempSuffix();

        SetupFileSystem(fs =>
        {
            fs.AddFile(reapTempPath, new MockFileData([]));
            fs.AddFile(plainPath, new MockFileData([]));
        });

        // Act
        var result = await Sut.ExecuteAsync(new DeleteDownloadTaskFilesCommand([movieFileKey]), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var file = Mock.Container.Resolve<IFile>();
        file.Exists(reapTempPath).ShouldBeFalse();
        file.Exists(plainPath).ShouldBeFalse();
    }

    [Test]
    public async Task ShouldSucceed_WhenFileIsNotPresentOnDisk()
    {
        // Arrange — no files on disk; handler should succeed gracefully.
        await SetupDatabase(
            84003,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );
        var movieFileKey = await IDbContext.DownloadTaskMovieFile.ProjectToKey().FirstAsync(CancellationToken);

        SetupFileSystem();

        // Act
        var result = await Sut.ExecuteAsync(new DeleteDownloadTaskFilesCommand([movieFileKey]), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldDeleteFiles_WhenMultipleKeysAreGiven()
    {
        // Arrange
        await SetupDatabase(
            84004,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieCount = 2;
                config.MovieDownloadTasksCount = 2;
            }
        );
        var dbContext = IDbContext;
        var movieFileTasks = await dbContext.DownloadTaskMovieFile.ToListAsync(CancellationToken);
        movieFileTasks.Count.ShouldBe(2);
        var movieFileKeys = await dbContext.DownloadTaskMovieFile.ProjectToKey().ToListAsync(CancellationToken);

        var plainPaths = movieFileTasks.Select(t => t.DownloadFilePath.RemoveReapTempSuffix()).ToList();
        SetupFileSystem(fs =>
        {
            foreach (var path in plainPaths)
                fs.AddFile(path, new MockFileData([]));
        });

        // Act
        var result = await Sut.ExecuteAsync(new DeleteDownloadTaskFilesCommand(movieFileKeys), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var file = Mock.Container.Resolve<IFile>();
        foreach (var path in plainPaths)
            file.Exists(path).ShouldBeFalse();
    }

    [Test]
    public async Task ShouldDeleteEmptyDirectoryAfterFileDeletion_WhenDirectoryBecomesEmpty()
    {
        // Arrange
        await SetupDatabase(
            84005,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var movieFileTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var movieFileKey = await dbContext.DownloadTaskMovieFile.ProjectToKey().FirstAsync(CancellationToken);
        var plainFilePath = movieFileTask.DownloadFilePath.RemoveReapTempSuffix();

        SetupFileSystem(fs => fs.AddFile(plainFilePath, new MockFileData([])));

        // Act
        var result = await Sut.ExecuteAsync(new DeleteDownloadTaskFilesCommand([movieFileKey]), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var directory = Mock.Container.Resolve<IDirectory>();
        directory.Exists(movieFileTask.DownloadDirectory).ShouldBeFalse();
    }

    [Test]
    public async Task ShouldNotDeleteDirectory_WhenOtherFilesRemainInIt()
    {
        // Arrange — place an unrelated sibling file in the same download directory so it stays
        // non-empty after the task's own file is deleted.
        await SetupDatabase(
            84006,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var movieFileTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var movieFileKey = await dbContext.DownloadTaskMovieFile.ProjectToKey().FirstAsync(CancellationToken);

        var pathToDelete = movieFileTask.DownloadFilePath.RemoveReapTempSuffix();

        // Sibling file that is not owned by this task — directory must survive.
        var siblingPath = Path.Combine(movieFileTask.DownloadDirectory, "sibling.mkv");

        SetupFileSystem(fs =>
        {
            fs.AddFile(pathToDelete, new MockFileData([]));
            fs.AddFile(siblingPath, new MockFileData([]));
        });

        // Act — only delete the task's file; sibling remains
        var result = await Sut.ExecuteAsync(new DeleteDownloadTaskFilesCommand([movieFileKey]), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var file = Mock.Container.Resolve<IFile>();
        file.Exists(pathToDelete).ShouldBeFalse();
        file.Exists(siblingPath).ShouldBeTrue();

        // Directory still exists because siblingPath is still there.
        var directory = Mock.Container.Resolve<IDirectory>();
        directory.Exists(movieFileTask.DownloadDirectory).ShouldBeTrue();
    }

    [Test]
    public async Task ShouldHandleEpisodeFileKeys()
    {
        // Arrange — TV show episode file task
        await SetupDatabase(
            84007,
            config =>
            {
                config.PlexServerCount = 1;
                config.TvShowCount = 1;
                config.TvShowDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var episodeFileTask = await dbContext.DownloadTaskTvShowEpisodeFile.FirstAsync(CancellationToken);
        var episodeFileKey = await dbContext.DownloadTaskTvShowEpisodeFile.ProjectToKey().FirstAsync(CancellationToken);
        var plainFilePath = episodeFileTask.DownloadFilePath.RemoveReapTempSuffix();

        SetupFileSystem(fs => fs.AddFile(plainFilePath, new MockFileData([])));

        // Act
        var result = await Sut.ExecuteAsync(new DeleteDownloadTaskFilesCommand([episodeFileKey]), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var file = Mock.Container.Resolve<IFile>();
        file.Exists(plainFilePath).ShouldBeFalse();
    }

    [Test]
    public async Task ShouldNotDeleteMoviesCategoryFolderOrDownloadRoot_WhenMovieFolderBecomesEmpty()
    {
        // Arrange
        // Scenario: the only movie in the category is deleted. The task folder empties out
        // and is removed, but recursion must stop at …/Movies/ (the category stop-root)
        // and must never touch …/Downloads/ above it.
        await SetupDatabase(
            84010,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var movieFileTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var movieFileKey = await dbContext.DownloadTaskMovieFile.ProjectToKey().FirstAsync(CancellationToken);
        var plainFilePath = movieFileTask.DownloadFilePath.RemoveReapTempSuffix();

        // e.g. /Downloads/Movies/SomeMovie  →  category folder = /Downloads/Movies
        var movieTaskFolder = movieFileTask.DownloadDirectory;
        var moviesCategoryFolder = Path.GetDirectoryName(movieTaskFolder.TrimEnd(Path.DirectorySeparatorChar))!;

        var downloadRoot = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;

        SetupFileSystem(fs => fs.AddFile(plainFilePath, new MockFileData([])));

        // Act
        var result = await Sut.ExecuteAsync(new DeleteDownloadTaskFilesCommand([movieFileKey]), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var file = Mock.Container.Resolve<IFile>();
        var directory = Mock.Container.Resolve<IDirectory>();
        file.Exists(plainFilePath).ShouldBeFalse();
        directory.Exists(movieTaskFolder).ShouldBeFalse(); // task folder removed (was empty)
        directory.Exists(moviesCategoryFolder).ShouldBeTrue(); // stopRoot — must survive
        directory.Exists(downloadRoot).ShouldBeTrue(); // download root — must survive
    }

    [Test]
    public async Task ShouldNotDeleteTvShowsCategoryFolderOrDownloadRoot_WhenSeasonAndShowFoldersBecomeEmpty()
    {
        // Arrange
        // Scenario: the only episode of a show is deleted. Season folder and show folder both
        // empty out and are removed, but recursion must stop at …/TvShows/ (the category
        // stop-root) and must never touch …/Downloads/ above it.
        await SetupDatabase(
            84011,
            config =>
            {
                config.PlexServerCount = 1;
                config.TvShowCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var episodeFileTask = await dbContext.DownloadTaskTvShowEpisodeFile.FirstAsync(CancellationToken);
        var episodeFileKey = await dbContext.DownloadTaskTvShowEpisodeFile.ProjectToKey().FirstAsync(CancellationToken);
        var plainFilePath = episodeFileTask.DownloadFilePath.RemoveReapTempSuffix();

        // e.g. /Downloads/TvShows/SomeShow/Season 1  →  ancestors:
        //   showFolder          = /Downloads/TvShows/SomeShow
        //   tvShowsCategoryFolder = /Downloads/TvShows
        var seasonFolder = episodeFileTask.DownloadDirectory;
        var showFolder = Path.GetDirectoryName(seasonFolder.TrimEnd(Path.DirectorySeparatorChar))!;
        var tvShowsCategoryFolder = Path.GetDirectoryName(showFolder.TrimEnd(Path.DirectorySeparatorChar))!;
        var downloadRoot = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;

        SetupFileSystem(fs => fs.AddFile(plainFilePath, new MockFileData([])));

        // Act
        var result = await Sut.ExecuteAsync(new DeleteDownloadTaskFilesCommand([episodeFileKey]), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var file = Mock.Container.Resolve<IFile>();
        var directory = Mock.Container.Resolve<IDirectory>();
        file.Exists(plainFilePath).ShouldBeFalse();
        directory.Exists(seasonFolder).ShouldBeFalse(); // season folder removed (was empty)
        directory.Exists(showFolder).ShouldBeFalse(); // show folder removed (was empty)
        directory.Exists(tvShowsCategoryFolder).ShouldBeTrue(); // stopRoot — must survive
        directory.Exists(downloadRoot).ShouldBeTrue(); // download root — must survive
    }

    [Test]
    public async Task ShouldPreserveShowFolder_WhenOnlyOneOfTwoSeasonFoldersBecomesEmpty()
    {
        // Arrange
        // Scenario: a show has two seasons; only the first season's episode is deleted.
        // The first season folder empties out and is removed, but the show folder must
        // survive because it still contains the second season folder (with its file).
        await SetupDatabase(
            84012,
            config =>
            {
                config.PlexServerCount = 1;
                config.TvShowCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 2;
                config.TvShowEpisodeDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var episodeFileTasks = await dbContext.DownloadTaskTvShowEpisodeFile.ToListAsync(CancellationToken);
        episodeFileTasks.Count.ShouldBe(2);
        var episodeFileKeys = await dbContext
            .DownloadTaskTvShowEpisodeFile.ProjectToKey()
            .ToListAsync(CancellationToken);

        var season1Task = episodeFileTasks[0];
        var season2Task = episodeFileTasks[1];
        var season1PlainPath = season1Task.DownloadFilePath.RemoveReapTempSuffix();
        var season2PlainPath = season2Task.DownloadFilePath.RemoveReapTempSuffix();

        var season1Folder = season1Task.DownloadDirectory;
        var season2Folder = season2Task.DownloadDirectory;
        var showFolder = Path.GetDirectoryName(season1Folder.TrimEnd(Path.DirectorySeparatorChar))!;
        var tvShowsCategoryFolder = Path.GetDirectoryName(showFolder.TrimEnd(Path.DirectorySeparatorChar))!;

        // Both season files exist on disk; only season 1's episode is in the delete command.
        SetupFileSystem(fs =>
        {
            fs.AddFile(season1PlainPath, new MockFileData([]));
            fs.AddFile(season2PlainPath, new MockFileData([]));
        });

        // Act — delete only season 1's episode
        var result = await Sut.ExecuteAsync(
            new DeleteDownloadTaskFilesCommand([episodeFileKeys[0]]),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var file = Mock.Container.Resolve<IFile>();
        var directory = Mock.Container.Resolve<IDirectory>();
        file.Exists(season1PlainPath).ShouldBeFalse(); // season 1 file removed
        file.Exists(season2PlainPath).ShouldBeTrue(); // season 2 file untouched
        directory.Exists(season1Folder).ShouldBeFalse(); // season 1 folder removed (was empty)
        directory.Exists(season2Folder).ShouldBeTrue(); // season 2 folder still has its file
        directory.Exists(showFolder).ShouldBeTrue(); // show folder not empty (season 2 still there)
        directory.Exists(tvShowsCategoryFolder).ShouldBeTrue(); // stopRoot — must survive
    }

    [Test]
    public async Task ShouldCleanUpBothCategorySubfolders_WhenMixedMovieAndEpisodeKeysAreDeleted()
    {
        // Arrange
        // Scenario: a command targets one movie file and one TV episode file simultaneously.
        // Both task folders must be cleaned up, and both Movies/ and TvShows/ category
        // folders (the stop-roots) must survive even though they become childless.
        await SetupDatabase(
            84013,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
                config.TvShowCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var movieFileTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var movieFileKey = await dbContext.DownloadTaskMovieFile.ProjectToKey().FirstAsync(CancellationToken);
        var episodeFileTask = await dbContext.DownloadTaskTvShowEpisodeFile.FirstAsync(CancellationToken);
        var episodeFileKey = await dbContext.DownloadTaskTvShowEpisodeFile.ProjectToKey().FirstAsync(CancellationToken);

        var moviePlainPath = movieFileTask.DownloadFilePath.RemoveReapTempSuffix();
        var episodePlainPath = episodeFileTask.DownloadFilePath.RemoveReapTempSuffix();

        var movieTaskFolder = movieFileTask.DownloadDirectory;
        var moviesCategoryFolder = Path.GetDirectoryName(movieTaskFolder.TrimEnd(Path.DirectorySeparatorChar))!;
        var seasonFolder = episodeFileTask.DownloadDirectory;
        var showFolder = Path.GetDirectoryName(seasonFolder.TrimEnd(Path.DirectorySeparatorChar))!;
        var tvShowsCategoryFolder = Path.GetDirectoryName(showFolder.TrimEnd(Path.DirectorySeparatorChar))!;
        var downloadRoot = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;

        SetupFileSystem(fs =>
        {
            fs.AddFile(moviePlainPath, new MockFileData([]));
            fs.AddFile(episodePlainPath, new MockFileData([]));
        });

        // Act — delete both in one command
        var result = await Sut.ExecuteAsync(
            new DeleteDownloadTaskFilesCommand([movieFileKey, episodeFileKey]),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var file = Mock.Container.Resolve<IFile>();
        var directory = Mock.Container.Resolve<IDirectory>();
        file.Exists(moviePlainPath).ShouldBeFalse();
        file.Exists(episodePlainPath).ShouldBeFalse();
        directory.Exists(movieTaskFolder).ShouldBeFalse(); // movie task folder removed (was empty)
        directory.Exists(seasonFolder).ShouldBeFalse(); // season folder removed (was empty)
        directory.Exists(showFolder).ShouldBeFalse(); // show folder removed (was empty)
        directory.Exists(moviesCategoryFolder).ShouldBeTrue(); // Movies/ stopRoot — must survive
        directory.Exists(tvShowsCategoryFolder).ShouldBeTrue(); // TvShows/ stopRoot — must survive
        directory.Exists(downloadRoot).ShouldBeTrue(); // download root — must survive
    }

    [Test]
    [Arguments(DownloadTaskType.MusicTrackData, "Music", "empty")]
    [Arguments(DownloadTaskType.PhotoData, "Photos", "empty")]
    [Arguments(DownloadTaskType.OtherVideoData, "OtherVideos", "empty")]
    [Arguments(DownloadTaskType.MusicTrackData, "Music", "active")]
    [Arguments(DownloadTaskType.PhotoData, "Photos", "active")]
    [Arguments(DownloadTaskType.OtherVideoData, "OtherVideos", "active")]
    [Arguments(DownloadTaskType.MusicTrackData, "Music", "unrelated")]
    [Arguments(DownloadTaskType.PhotoData, "Photos", "unrelated")]
    [Arguments(DownloadTaskType.OtherVideoData, "OtherVideos", "unrelated")]
    public async Task ShouldPreserveNewFamilyCategoryRoots_WhenCleaningTaskFiles(
        DownloadTaskType taskType,
        string category,
        string remainingContent
    )
    {
        await SetupDatabase(
            84015,
            config =>
            {
                config.MusicArtistDownloadTasksCount = taskType == DownloadTaskType.MusicTrackData ? 1 : 0;
                config.MusicAlbumDownloadTasksCount = taskType == DownloadTaskType.MusicTrackData ? 1 : 0;
                config.MusicTrackDownloadTasksCount = taskType == DownloadTaskType.MusicTrackData ? 2 : 0;
                config.MusicTrackFileDownloadTasksCount = taskType == DownloadTaskType.MusicTrackData ? 1 : 0;
                config.PhotoAlbumDownloadTasksCount = taskType == DownloadTaskType.PhotoData ? 1 : 0;
                config.PhotoImageDownloadTasksCount = taskType == DownloadTaskType.PhotoData ? 2 : 0;
                config.PhotoImageFileDownloadTasksCount = taskType == DownloadTaskType.PhotoData ? 1 : 0;
                config.OtherVideoDownloadTasksCount = taskType == DownloadTaskType.OtherVideoData ? 2 : 0;
                config.OtherVideoFileDownloadTasksCount = taskType == DownloadTaskType.OtherVideoData ? 1 : 0;
            }
        );
        var dbContext = IDbContext;
        List<DownloadTaskFileBase> tasks =
        [
            .. await dbContext.DownloadTaskMusicTrackFiles.ToListAsync(CancellationToken),
            .. await dbContext.DownloadTaskPhotoImageFiles.ToListAsync(CancellationToken),
            .. await dbContext.DownloadTaskOtherVideoFiles.ToListAsync(CancellationToken),
        ];
        var target = tasks[0];
        var control = tasks[1];
        control.DirectoryMeta.OtherVideoFolder = target.DirectoryMeta.OtherVideoFolder;
        control.DownloadStatus = remainingContent == "active" ? DownloadStatus.Downloading : DownloadStatus.Completed;
        dbContext.Entry(control).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        control.DownloadDirectory.ShouldBe(target.DownloadDirectory);

        var plainPath = target.DownloadFilePath.RemoveReapTempSuffix();
        var categoryRoot = Path.Combine(target.DirectoryMeta.DownloadRootPath, category);
        var unrelatedPath = Path.Combine(target.DownloadDirectory, "unrelated.bin");
        byte[] unrelatedBytes = [99, 100];
        SetupFileSystem(fs =>
        {
            fs.AddFile(target.DownloadFilePath, new MockFileData(new byte[] { 1, 2, 3 }));
            fs.AddFile(plainPath, new MockFileData(new byte[] { 4, 5, 6 }));
            if (remainingContent == "unrelated")
                fs.AddFile(unrelatedPath, new MockFileData(unrelatedBytes));
        });

        var result = await Sut.ExecuteAsync(new DeleteDownloadTaskFilesCommand([target.ToKey()]), CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        fileSystem.File.Exists(target.DownloadFilePath).ShouldBeFalse();
        fileSystem.File.Exists(plainPath).ShouldBeFalse();
        fileSystem.Directory.Exists(categoryRoot).ShouldBeTrue();
        fileSystem.Directory.Exists(target.DirectoryMeta.DownloadRootPath).ShouldBeTrue();
        fileSystem.Directory.Exists(target.DownloadDirectory).ShouldBe(remainingContent != "empty");
        if (remainingContent == "empty" && taskType == DownloadTaskType.MusicTrackData)
            fileSystem.Directory.Exists(Path.GetDirectoryName(target.DownloadDirectory)!).ShouldBeFalse();
        if (remainingContent == "unrelated")
            fileSystem.File.ReadAllBytes(unrelatedPath).ShouldBe(unrelatedBytes);

        var persistedControl = await IDbContext.GetDownloadTaskFileAsync(control.ToKey(), CancellationToken);
        persistedControl.ShouldNotBeNull();
        persistedControl.DownloadStatus.ShouldBe(control.DownloadStatus);
        persistedControl.FileName.ShouldBe(control.FileName);
        persistedControl.DirectoryMeta.ShouldBe(control.DirectoryMeta);
    }
}
