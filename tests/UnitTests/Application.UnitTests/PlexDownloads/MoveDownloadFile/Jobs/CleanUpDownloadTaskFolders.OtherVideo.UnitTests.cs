namespace Reaparr.Application.UnitTests;

public class CleanUpDownloadTaskFoldersOtherVideoUnitTests : BaseCommandUnitTest<CleanUpDownloadTaskFoldersCommand>
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldRemoveOnlyEmptyMediaFolder_WhenCleaningCompletedOtherVideo(bool containsFile)
    {
        // Arrange
        await SetupDatabase(
            66001,
            config =>
            {
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoDownloadTasksCount = 1;
                config.OtherVideoFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var file = await dbContext.DownloadTaskOtherVideoFiles.SingleAsync(CancellationToken);
        var paths = Mock.Container.Resolve<IPathProvider>();
        file.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
        file.DirectoryMeta.OtherVideoFolder = "shared/file";
        file.DownloadStatus = DownloadStatus.Completed;
        dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        SetupFileSystem(fs =>
        {
            fs.AddDirectory(file.DownloadDirectory);
            if (containsFile)
                fs.AddFile(file.DownloadFilePath, new MockFileData("target bytes"));
        });

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(file.ToKey()));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var fs = Mock.Container.Resolve<IFileSystem>();
        fs.Directory.Exists(file.DownloadDirectory).ShouldBe(containsFile);
        fs.Directory.Exists(Path.GetDirectoryName(file.DownloadDirectory)!).ShouldBe(containsFile);
        fs.Directory.Exists(file.DirectoryMeta.GetDownloadCategoryDirectory(file.DownloadTaskType)).ShouldBeTrue();
        if (containsFile)
            fs.File.ReadAllText(file.DownloadFilePath).ShouldBe("target bytes");
        (await dbContext.DownloadTaskOtherVideoFiles.Select(x => x.Id).SingleAsync(CancellationToken)).ShouldBe(file.Id);
    }

    [Test]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Completed)]
    [Arguments(DownloadStatus.Deleted)]
    public async Task ShouldRespectMovieSiblingStatusWhenCleaningOtherVideo(DownloadStatus siblingStatus)
    {
        // Arrange
        await SetupDatabase(66002, config =>
        {
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoDownloadTasksCount = 2;
            config.OtherVideoFileDownloadTasksCount = 1;
            config.MovieDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var videoFiles = await dbContext.DownloadTaskOtherVideoFiles
            .OrderBy(x => x.PlexApiRatingKey)
            .ToArrayAsync(CancellationToken);
        var target = videoFiles[0];
        var control = videoFiles[1];
        var sibling = await dbContext.DownloadTaskMovieFile.AsTracking().SingleAsync(CancellationToken);
        var paths = Mock.Container.Resolve<IPathProvider>();
        var root = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DownloadRootPath = root;
        target.DirectoryMeta.OtherVideoFolder = "Movies/shared/file";
        target.DownloadStatus = DownloadStatus.Completed;
        control.DirectoryMeta.DownloadRootPath = root;
        control.DirectoryMeta.OtherVideoFolder = "OtherVideos/control";
        control.DownloadStatus = DownloadStatus.Downloading;
        sibling.DirectoryMeta.DownloadRootPath = Path.Combine(root, "OtherVideos");
        sibling.DirectoryMeta.MovieFolder = "shared/file";
        sibling.DownloadStatus = siblingStatus;
        foreach (var file in new DownloadTaskFileBase[] { target, control, sibling })
            dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        target.DownloadDirectory.ShouldBe(sibling.DownloadDirectory);
        target.DownloadDirectory.ShouldNotBe(control.DownloadDirectory);
        SetupFileSystem(fs => fs.AddDirectory(target.DownloadDirectory));

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(target.ToKey()));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var fs = Mock.Container.Resolve<IFileSystem>();
        var active = siblingStatus is not (DownloadStatus.Completed or DownloadStatus.Deleted);
        fs.Directory.Exists(target.DownloadDirectory).ShouldBe(active);
        fs.Directory.Exists(Path.GetDirectoryName(target.DownloadDirectory)!).ShouldBe(active);
        (await dbContext.DownloadTaskOtherVideoFiles.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(videoFiles.Select(x => x.Id).Order());
        (await dbContext.DownloadTaskMovieFile.Select(x => x.Id).SingleAsync(CancellationToken)).ShouldBe(sibling.Id);
    }

    [Test]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Completed)]
    [Arguments(DownloadStatus.Deleted)]
    public async Task ShouldRespectEpisodeSiblingStatusWhenCleaningOtherVideo(DownloadStatus siblingStatus)
    {
        // Arrange
        await SetupDatabase(66003, config =>
        {
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoDownloadTasksCount = 2;
            config.OtherVideoFileDownloadTasksCount = 1;
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var videoFiles = await dbContext.DownloadTaskOtherVideoFiles
            .OrderBy(x => x.PlexApiRatingKey)
            .ToArrayAsync(CancellationToken);
        var target = videoFiles[0];
        var control = videoFiles[1];
        var sibling = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().SingleAsync(CancellationToken);
        var paths = Mock.Container.Resolve<IPathProvider>();
        var root = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DownloadRootPath = root;
        target.DirectoryMeta.OtherVideoFolder = "TvShows/shared/file";
        target.DownloadStatus = DownloadStatus.Completed;
        control.DirectoryMeta.DownloadRootPath = root;
        control.DirectoryMeta.OtherVideoFolder = "OtherVideos/control";
        control.DownloadStatus = DownloadStatus.Downloading;
        sibling.DirectoryMeta.DownloadRootPath = Path.Combine(root, "OtherVideos");
        sibling.DirectoryMeta.TvShowFolder = "shared";
        sibling.DirectoryMeta.SeasonFolder = "file";
        sibling.DownloadStatus = siblingStatus;
        foreach (var file in new DownloadTaskFileBase[] { target, control, sibling })
            dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        target.DownloadDirectory.ShouldBe(sibling.DownloadDirectory);
        target.DownloadDirectory.ShouldNotBe(control.DownloadDirectory);
        SetupFileSystem(fs => fs.AddDirectory(target.DownloadDirectory));

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(target.ToKey()));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var fs = Mock.Container.Resolve<IFileSystem>();
        var active = siblingStatus is not (DownloadStatus.Completed or DownloadStatus.Deleted);
        fs.Directory.Exists(target.DownloadDirectory).ShouldBe(active);
        fs.Directory.Exists(Path.GetDirectoryName(target.DownloadDirectory)!).ShouldBe(active);
        (await dbContext.DownloadTaskOtherVideoFiles.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(videoFiles.Select(x => x.Id).Order());
        (await dbContext.DownloadTaskTvShowEpisodeFile.Select(x => x.Id).SingleAsync(CancellationToken)).ShouldBe(sibling.Id);
    }

    [Test]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Completed)]
    [Arguments(DownloadStatus.Deleted)]
    public async Task ShouldRespectPhotoSiblingStatusWhenCleaningOtherVideo(DownloadStatus siblingStatus)
    {
        // Arrange
        await SetupDatabase(66004, config =>
        {
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoDownloadTasksCount = 2;
            config.OtherVideoFileDownloadTasksCount = 1;
            config.PlexPhotoLibraryCount = 1;
        });
        var dbContext = IDbContext;
        var videoFiles = await dbContext.DownloadTaskOtherVideoFiles
            .OrderBy(x => x.PlexApiRatingKey)
            .ToArrayAsync(CancellationToken);
        var target = videoFiles[0];
        var control = videoFiles[1];
        var library = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum);
        var album = FakeData
            .GetDownloadTaskPhotoAlbum(new Seed(66004))
            .RuleFor(x => x.Children, _ => FakeData.GetDownloadTaskPhotoImage(new Seed(66005)).Generate(1))
            .Generate();
        var photoFiles = album.Children.SelectMany(x => x.Children).Cast<DownloadTaskFileBase>().ToList();
        foreach (var node in new DownloadTaskBase[] { album }.Concat(album.Children).Concat(photoFiles))
        {
            node.PlexServerId = library.PlexServerId;
            node.PlexLibraryId = library.Id;
        }
        dbContext.DownloadTaskPhotoAlbums.Add(album);
        var sibling = photoFiles.Single();
        var paths = Mock.Container.Resolve<IPathProvider>();
        var root = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DownloadRootPath = root;
        target.DirectoryMeta.OtherVideoFolder = "Photos/shared/file";
        target.DownloadStatus = DownloadStatus.Completed;
        control.DirectoryMeta.DownloadRootPath = root;
        control.DirectoryMeta.OtherVideoFolder = "OtherVideos/control";
        control.DownloadStatus = DownloadStatus.Downloading;
        sibling.DirectoryMeta.DownloadRootPath = Path.Combine(root, "OtherVideos");
        sibling.DirectoryMeta.PhotoAlbumFolder = "shared/file";
        sibling.DownloadStatus = siblingStatus;
        foreach (var file in new DownloadTaskFileBase[] { target, control })
            dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        target.DownloadDirectory.ShouldBe(sibling.DownloadDirectory);
        target.DownloadDirectory.ShouldNotBe(control.DownloadDirectory);
        SetupFileSystem(fs => fs.AddDirectory(target.DownloadDirectory));

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(target.ToKey()));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var fs = Mock.Container.Resolve<IFileSystem>();
        var active = siblingStatus is not (DownloadStatus.Completed or DownloadStatus.Deleted);
        fs.Directory.Exists(target.DownloadDirectory).ShouldBe(active);
        fs.Directory.Exists(Path.GetDirectoryName(target.DownloadDirectory)!).ShouldBe(active);
        (await dbContext.DownloadTaskOtherVideoFiles.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(new[] { target.Id, control.Id }.Order());
        (await dbContext.DownloadTaskPhotoImageFiles.Select(x => x.Id).SingleAsync(CancellationToken)).ShouldBe(sibling.Id);
    }

    [Test]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Completed)]
    [Arguments(DownloadStatus.Deleted)]
    public async Task ShouldRespectMusicSiblingStatusWhenCleaningOtherVideo(DownloadStatus siblingStatus)
    {
        // Arrange
        await SetupDatabase(66005, config =>
        {
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoDownloadTasksCount = 2;
            config.OtherVideoFileDownloadTasksCount = 1;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistDownloadTasksCount = 1;
            config.MusicAlbumDownloadTasksCount = 1;
            config.MusicTrackDownloadTasksCount = 1;
            config.MusicTrackFileDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var videoFiles = await dbContext.DownloadTaskOtherVideoFiles
            .OrderBy(x => x.PlexApiRatingKey)
            .ToArrayAsync(CancellationToken);
        var target = videoFiles[0];
        var control = videoFiles[1];
        var sibling = await dbContext.DownloadTaskMusicTrackFiles.SingleAsync(CancellationToken);
        var paths = Mock.Container.Resolve<IPathProvider>();
        var root = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DownloadRootPath = root;
        target.DirectoryMeta.OtherVideoFolder = "Music/shared/file";
        target.DownloadStatus = DownloadStatus.Completed;
        control.DirectoryMeta.DownloadRootPath = root;
        control.DirectoryMeta.OtherVideoFolder = "OtherVideos/control";
        control.DownloadStatus = DownloadStatus.Downloading;
        sibling.DirectoryMeta.DownloadRootPath = Path.Combine(root, "OtherVideos");
        sibling.DirectoryMeta.MusicArtistFolder = "shared";
        sibling.DirectoryMeta.MusicAlbumFolder = "file";
        sibling.DownloadStatus = siblingStatus;
        foreach (var file in new DownloadTaskFileBase[] { target, control, sibling })
            dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        target.DownloadDirectory.ShouldBe(sibling.DownloadDirectory);
        target.DownloadDirectory.ShouldNotBe(control.DownloadDirectory);
        SetupFileSystem(fs => fs.AddDirectory(target.DownloadDirectory));

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(target.ToKey()));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var fs = Mock.Container.Resolve<IFileSystem>();
        var active = siblingStatus is not (DownloadStatus.Completed or DownloadStatus.Deleted);
        fs.Directory.Exists(target.DownloadDirectory).ShouldBe(active);
        fs.Directory.Exists(Path.GetDirectoryName(target.DownloadDirectory)!).ShouldBe(active);
        (await dbContext.DownloadTaskOtherVideoFiles.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(new[] { target.Id, control.Id }.Order());
        (await dbContext.DownloadTaskMusicTrackFiles.Select(x => x.Id).SingleAsync(CancellationToken)).ShouldBe(sibling.Id);
    }

    [Test]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Completed)]
    [Arguments(DownloadStatus.Deleted)]
    public async Task ShouldRespectOtherVideoSiblingStatusWhenCleaningOtherVideo(DownloadStatus siblingStatus)
    {
        // Arrange
        await SetupDatabase(
            66006,
            config =>
            {
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoDownloadTasksCount = 3;
                config.OtherVideoFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var videoFiles = await dbContext.DownloadTaskOtherVideoFiles
            .OrderBy(x => x.PlexApiRatingKey)
            .ToArrayAsync(CancellationToken);
        var target = videoFiles[0];
        var sibling = videoFiles[1];
        var control = videoFiles[2];
        var paths = Mock.Container.Resolve<IPathProvider>();
        var root = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DownloadRootPath = root;
        target.DirectoryMeta.OtherVideoFolder = "OtherVideos/shared/file";
        target.DownloadStatus = DownloadStatus.Completed;
        sibling.DirectoryMeta.DownloadRootPath = Path.Combine(root, "OtherVideos");
        sibling.DirectoryMeta.OtherVideoFolder = "shared/file";
        sibling.DownloadStatus = siblingStatus;
        control.DirectoryMeta.DownloadRootPath = root;
        control.DirectoryMeta.OtherVideoFolder = "OtherVideos/control";
        foreach (var file in new DownloadTaskFileBase[] { target, sibling, control })
            dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        target.DownloadDirectory.ShouldBe(sibling.DownloadDirectory);
        target.DownloadDirectory.ShouldNotBe(control.DownloadDirectory);
        SetupFileSystem(fs => fs.AddDirectory(target.DownloadDirectory));

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(target.ToKey()));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var fs = Mock.Container.Resolve<IFileSystem>();
        var active = siblingStatus is not (DownloadStatus.Completed or DownloadStatus.Deleted);
        fs.Directory.Exists(target.DownloadDirectory).ShouldBe(active);
        fs.Directory.Exists(Path.GetDirectoryName(target.DownloadDirectory)!).ShouldBe(active);
        (await dbContext.DownloadTaskOtherVideoFiles.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(new[] { target.Id, sibling.Id, control.Id }.Order());
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenSecondCleanupPathResolutionFails()
    {
        // Arrange
        await SetupDatabase(
            66007,
            config =>
            {
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoDownloadTasksCount = 1;
                config.OtherVideoFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var target = await dbContext.DownloadTaskOtherVideoFiles.SingleAsync(CancellationToken);
        target.DirectoryMeta.DownloadRootPath = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.OtherVideoFolder = "shared/file";
        dbContext.Entry(target).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var parent = Path.GetDirectoryName(target.DownloadDirectory)!;
        Mock.Mock<IPath>()
            .Setup(x => x.GetDirectoryName(target.DownloadFilePath))
            .Returns(target.DownloadDirectory)
            .Verifiable(Times.Once());
        Mock.Mock<IPath>()
            .Setup(x => x.GetDirectoryName(target.DownloadDirectory))
            .Throws(new InvalidOperationException("second path failure"))
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>().Setup(x => x.Exists(target.DownloadDirectory)).Returns(true).Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.GetFileSystemEntries(target.DownloadDirectory))
            .Returns([])
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>().Setup(x => x.Delete(target.DownloadDirectory)).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(target.ToKey()));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.HasException<InvalidOperationException>().ShouldBeTrue();
        Mock.Mock<IPath>().Verify();
        Mock.Mock<IDirectory>().Verify();
        Mock.Mock<IDirectory>().Verify(x => x.Exists(parent), Times.Never());
        Mock.Mock<IDirectory>().Verify(x => x.Delete(parent), Times.Never());
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenSecondCleanupDirectoryEntriesCannotBeRead()
    {
        // Arrange
        await SetupDatabase(
            66008,
            config =>
            {
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoDownloadTasksCount = 1;
                config.OtherVideoFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var target = await dbContext.DownloadTaskOtherVideoFiles.SingleAsync(CancellationToken);
        target.DirectoryMeta.DownloadRootPath = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.OtherVideoFolder = "shared/file";
        dbContext.Entry(target).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var parent = Path.GetDirectoryName(target.DownloadDirectory)!;
        Mock.Mock<IPath>()
            .Setup(x => x.GetDirectoryName(target.DownloadFilePath))
            .Returns(target.DownloadDirectory)
            .Verifiable(Times.Once());
        Mock.Mock<IPath>()
            .Setup(x => x.GetDirectoryName(target.DownloadDirectory))
            .Returns(parent)
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>().Setup(x => x.Exists(target.DownloadDirectory)).Returns(true).Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.GetFileSystemEntries(target.DownloadDirectory))
            .Returns([])
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>().Setup(x => x.Delete(target.DownloadDirectory)).Verifiable(Times.Once());
        Mock.Mock<IDirectory>().Setup(x => x.Exists(parent)).Returns(true).Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.GetFileSystemEntries(parent))
            .Throws(new UnauthorizedAccessException("second directory denied"))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(target.ToKey()));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.HasException<UnauthorizedAccessException>().ShouldBeTrue();
        Mock.Mock<IPath>().Verify();
        Mock.Mock<IDirectory>().Verify();
        Mock.Mock<IDirectory>().Verify(x => x.Delete(target.DownloadDirectory), Times.Once());
        Mock.Mock<IDirectory>().Verify(x => x.Delete(parent), Times.Never());
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenSecondCleanupDirectoryCannotBeDeleted()
    {
        // Arrange
        await SetupDatabase(
            66009,
            config =>
            {
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoDownloadTasksCount = 1;
                config.OtherVideoFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var target = await dbContext.DownloadTaskOtherVideoFiles.SingleAsync(CancellationToken);
        target.DirectoryMeta.DownloadRootPath = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.OtherVideoFolder = "shared/file";
        dbContext.Entry(target).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var parent = Path.GetDirectoryName(target.DownloadDirectory)!;
        Mock.Mock<IPath>()
            .Setup(x => x.GetDirectoryName(target.DownloadFilePath))
            .Returns(target.DownloadDirectory)
            .Verifiable(Times.Once());
        Mock.Mock<IPath>()
            .Setup(x => x.GetDirectoryName(target.DownloadDirectory))
            .Returns(parent)
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>().Setup(x => x.Exists(target.DownloadDirectory)).Returns(true).Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.GetFileSystemEntries(target.DownloadDirectory))
            .Returns([])
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>().Setup(x => x.Delete(target.DownloadDirectory)).Verifiable(Times.Once());
        Mock.Mock<IDirectory>().Setup(x => x.Exists(parent)).Returns(true).Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.GetFileSystemEntries(parent))
            .Returns([])
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.Delete(parent))
            .Throws(new UnauthorizedAccessException("second delete denied"))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(target.ToKey()));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.HasException<UnauthorizedAccessException>().ShouldBeTrue();
        Mock.Mock<IPath>().Verify();
        Mock.Mock<IDirectory>().Verify();
        Mock.Mock<IDirectory>().Verify(x => x.Delete(target.DownloadDirectory), Times.Once());
        Mock.Mock<IDirectory>().Verify(x => x.Delete(parent), Times.Once());
    }
}
