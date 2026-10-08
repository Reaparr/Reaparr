namespace Reaparr.Application.UnitTests;

public class CleanUpDownloadTaskFoldersEpisodeUnitTests : BaseCommandUnitTest<CleanUpDownloadTaskFoldersCommand>
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ShouldRemoveOnlyEmptyMediaFolders_WhenCleaningCompletedEpisode(
        bool directoryExists,
        bool containsFile
    )
    {
        // Arrange
        await SetupDatabase(62001, config =>
        {
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var file = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().SingleAsync(CancellationToken);
        var paths = Mock.Container.Resolve<IPathProvider>();
        file.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
        file.DownloadStatus = DownloadStatus.Completed;
        dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var controlPath = Path.Combine(paths.DefaultDownloadsDestinationFolder, "TvShows", "control", "retained.mkv");
        SetupFileSystem(fs =>
        {
            fs.AddFile(controlPath, new MockFileData("control bytes"));
            if (directoryExists)
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
        fs.Directory.Exists(Path.Combine(paths.DefaultDownloadsDestinationFolder, "TvShows")).ShouldBeTrue();
        fs.File.ReadAllText(controlPath).ShouldBe("control bytes");
        if (containsFile)
            fs.File.ReadAllText(file.DownloadFilePath).ShouldBe("target bytes");
        var retained = await dbContext.DownloadTaskTvShowEpisodeFile.SingleAsync(CancellationToken);
        retained.Id.ShouldBe(file.Id);
        retained.DownloadStatus.ShouldBe(DownloadStatus.Completed);
    }

    [Test]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Completed)]
    [Arguments(DownloadStatus.Deleted)]
    public async Task ShouldRespectMovieSiblingStatusWhenCleaningEpisode(DownloadStatus siblingStatus)
    {
        // Arrange
        await SetupDatabase(62002, config =>
        {
            config.MovieDownloadTasksCount = 1;
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 3;
        });
        var dbContext = IDbContext;
        var episodeFiles = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        episodeFiles.Count.ShouldBe(3);
        var target = episodeFiles[0];
        var control = episodeFiles[2];
        var sibling = await dbContext.DownloadTaskMovieFile.AsTracking().SingleAsync(CancellationToken);
        var paths = Mock.Container.Resolve<IPathProvider>();
        var root = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DownloadRootPath = root;
        target.DirectoryMeta.TvShowFolder = "Movies/shared";
        target.DirectoryMeta.SeasonFolder = "file";
        target.DownloadStatus = DownloadStatus.Completed;
        control.DirectoryMeta.DownloadRootPath = root;
        control.DirectoryMeta.TvShowFolder = "TvShows";
        control.DirectoryMeta.SeasonFolder = "control";
        control.DownloadStatus = DownloadStatus.Downloading;
        sibling.DirectoryMeta.DownloadRootPath = Path.Combine(root, "TvShows");
        sibling.DirectoryMeta.MovieFolder = "shared/file";
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
        (await dbContext.DownloadTaskTvShowEpisodeFile.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(new[] { target.Id, control.Id }.Order());
        (await dbContext.DownloadTaskMovieFile.Select(x => x.Id).SingleAsync(CancellationToken)).ShouldBe(sibling.Id);
    }

    [Test]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Completed)]
    [Arguments(DownloadStatus.Deleted)]
    public async Task ShouldRespectEpisodeSiblingStatusWhenCleaningEpisode(DownloadStatus siblingStatus)
    {
        // Arrange
        await SetupDatabase(62003, config =>
        {
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 3;
        });
        var dbContext = IDbContext;
        var episodeFiles = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        episodeFiles.Count.ShouldBe(3);
        var target = episodeFiles[0];
        var sibling = episodeFiles[1];
        var control = episodeFiles[2];
        var paths = Mock.Container.Resolve<IPathProvider>();
        var root = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DownloadRootPath = root;
        target.DirectoryMeta.TvShowFolder = "TvShows/shared";
        target.DirectoryMeta.SeasonFolder = "file";
        target.DownloadStatus = DownloadStatus.Completed;
        sibling.DirectoryMeta.DownloadRootPath = Path.Combine(root, "TvShows");
        sibling.DirectoryMeta.TvShowFolder = "shared";
        sibling.DirectoryMeta.SeasonFolder = "file";
        sibling.DownloadStatus = siblingStatus;
        control.DirectoryMeta.DownloadRootPath = root;
        control.DirectoryMeta.TvShowFolder = "TvShows";
        control.DirectoryMeta.SeasonFolder = "control";
        control.DownloadStatus = DownloadStatus.Downloading;
        foreach (var file in episodeFiles)
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
        (await dbContext.DownloadTaskTvShowEpisodeFile.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(new[] { target.Id, sibling.Id, control.Id }.Order());
    }

    [Test]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Completed)]
    [Arguments(DownloadStatus.Deleted)]
    public async Task ShouldRespectPhotoSiblingStatusWhenCleaningEpisode(DownloadStatus siblingStatus)
    {
        // Arrange
        await SetupDatabase(62004, config =>
        {
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 3;
            config.PlexPhotoLibraryCount = 1;
        });
        var dbContext = IDbContext;
        var episodeFiles = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        episodeFiles.Count.ShouldBe(3);
        var target = episodeFiles[0];
        var control = episodeFiles[2];
        var library = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum);
        var album = FakeData
            .GetDownloadTaskPhotoAlbum(new Seed(62004))
            .RuleFor(x => x.Children, _ => FakeData.GetDownloadTaskPhotoImage(new Seed(62005)).Generate(1))
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
        target.DirectoryMeta.TvShowFolder = "Photos/shared";
        target.DirectoryMeta.SeasonFolder = "file";
        target.DownloadStatus = DownloadStatus.Completed;
        control.DirectoryMeta.DownloadRootPath = root;
        control.DirectoryMeta.TvShowFolder = "TvShows";
        control.DirectoryMeta.SeasonFolder = "control";
        control.DownloadStatus = DownloadStatus.Downloading;
        sibling.DirectoryMeta.DownloadRootPath = Path.Combine(root, "TvShows");
        sibling.DirectoryMeta.PhotoAlbumFolder = "shared/file";
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
        (await dbContext.DownloadTaskTvShowEpisodeFile.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(new[] { target.Id, control.Id }.Order());
        (await dbContext.DownloadTaskPhotoImageFiles.Select(x => x.Id).SingleAsync(CancellationToken)).ShouldBe(sibling.Id);
    }

    [Test]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Completed)]
    [Arguments(DownloadStatus.Deleted)]
    public async Task ShouldRespectMusicSiblingStatusWhenCleaningEpisode(DownloadStatus siblingStatus)
    {
        // Arrange
        await SetupDatabase(62006, config =>
        {
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 3;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistDownloadTasksCount = 1;
            config.MusicAlbumDownloadTasksCount = 1;
            config.MusicTrackDownloadTasksCount = 1;
            config.MusicTrackFileDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var episodeFiles = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        episodeFiles.Count.ShouldBe(3);
        var target = episodeFiles[0];
        var control = episodeFiles[2];
        var sibling = await dbContext.DownloadTaskMusicTrackFiles.SingleAsync(CancellationToken);
        var paths = Mock.Container.Resolve<IPathProvider>();
        var root = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DownloadRootPath = root;
        target.DirectoryMeta.TvShowFolder = "Music/shared";
        target.DirectoryMeta.SeasonFolder = "file";
        target.DownloadStatus = DownloadStatus.Completed;
        control.DirectoryMeta.DownloadRootPath = root;
        control.DirectoryMeta.TvShowFolder = "TvShows";
        control.DirectoryMeta.SeasonFolder = "control";
        control.DownloadStatus = DownloadStatus.Downloading;
        sibling.DirectoryMeta.DownloadRootPath = Path.Combine(root, "TvShows");
        sibling.DirectoryMeta.MusicArtistFolder = "shared";
        sibling.DirectoryMeta.MusicAlbumFolder = "file";
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
        (await dbContext.DownloadTaskTvShowEpisodeFile.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(new[] { target.Id, control.Id }.Order());
        (await dbContext.DownloadTaskMusicTrackFiles.Select(x => x.Id).SingleAsync(CancellationToken)).ShouldBe(sibling.Id);
    }

    [Test]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Completed)]
    [Arguments(DownloadStatus.Deleted)]
    public async Task ShouldRespectOtherVideoSiblingStatusWhenCleaningEpisode(DownloadStatus siblingStatus)
    {
        // Arrange
        await SetupDatabase(62007, config =>
        {
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 3;
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoDownloadTasksCount = 1;
            config.OtherVideoFileDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var episodeFiles = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        episodeFiles.Count.ShouldBe(3);
        var target = episodeFiles[0];
        var control = episodeFiles[2];
        var sibling = await dbContext.DownloadTaskOtherVideoFiles.SingleAsync(CancellationToken);
        var paths = Mock.Container.Resolve<IPathProvider>();
        var root = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DownloadRootPath = root;
        target.DirectoryMeta.TvShowFolder = "OtherVideos/shared";
        target.DirectoryMeta.SeasonFolder = "file";
        target.DownloadStatus = DownloadStatus.Completed;
        control.DirectoryMeta.DownloadRootPath = root;
        control.DirectoryMeta.TvShowFolder = "TvShows";
        control.DirectoryMeta.SeasonFolder = "control";
        control.DownloadStatus = DownloadStatus.Downloading;
        sibling.DirectoryMeta.DownloadRootPath = Path.Combine(root, "TvShows");
        sibling.DirectoryMeta.OtherVideoFolder = "shared/file";
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
        (await dbContext.DownloadTaskTvShowEpisodeFile.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(new[] { target.Id, control.Id }.Order());
        (await dbContext.DownloadTaskOtherVideoFiles.Select(x => x.Id).SingleAsync(CancellationToken)).ShouldBe(sibling.Id);
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenSecondCleanupPathResolutionFails()
    {
        // Arrange
        await SetupDatabase(62008, config =>
        {
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var target = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().SingleAsync(CancellationToken);
        target.DirectoryMeta.DownloadRootPath = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;
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
        Mock.Mock<IDirectory>()
            .Setup(x => x.Exists(target.DownloadDirectory))
            .Returns(true)
            .Verifiable(Times.Once());
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
        await SetupDatabase(62009, config =>
        {
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var target = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().SingleAsync(CancellationToken);
        target.DirectoryMeta.DownloadRootPath = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;
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
        Mock.Mock<IDirectory>()
            .Setup(x => x.Exists(target.DownloadDirectory))
            .Returns(true)
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.GetFileSystemEntries(target.DownloadDirectory))
            .Returns([])
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.Delete(target.DownloadDirectory))
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.Exists(parent))
            .Returns(true)
            .Verifiable(Times.Once());
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
        await SetupDatabase(62010, config =>
        {
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var target = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().SingleAsync(CancellationToken);
        target.DirectoryMeta.DownloadRootPath = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;
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
        Mock.Mock<IDirectory>()
            .Setup(x => x.Exists(target.DownloadDirectory))
            .Returns(true)
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.GetFileSystemEntries(target.DownloadDirectory))
            .Returns([])
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>().Setup(x => x.Delete(target.DownloadDirectory)).Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.Exists(parent))
            .Returns(true)
            .Verifiable(Times.Once());
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
