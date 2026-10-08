namespace Reaparr.Application.UnitTests;

public class CleanUpDownloadTaskFoldersMovieUnitTests : BaseCommandUnitTest<CleanUpDownloadTaskFoldersCommand>
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ShouldRemoveOnlyEmptyMediaFolder_WhenCleaningCompletedMovie(bool directoryExists, bool containsFile)
    {
        // Arrange
        await SetupDatabase(63001, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var file = await dbContext.DownloadTaskMovieFile.AsTracking().SingleAsync(CancellationToken);
        var paths = Mock.Container.Resolve<IPathProvider>();
        file.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
        file.DownloadStatus = DownloadStatus.Completed;
        dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        SetupFileSystem(fs =>
        {
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
        fs.Directory.Exists(Path.GetDirectoryName(file.DownloadDirectory)!).ShouldBeTrue();
        if (containsFile)
            fs.File.ReadAllText(file.DownloadFilePath).ShouldBe("target bytes");
        var retained = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        retained.Id.ShouldBe(file.Id);
        retained.DownloadStatus.ShouldBe(DownloadStatus.Completed);
    }

    [Test]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Completed)]
    [Arguments(DownloadStatus.Deleted)]
    public async Task ShouldRespectMovieSiblingStatusWhenCleaningMovie(DownloadStatus siblingStatus)
    {
        // Arrange
        await SetupDatabase(63002, config => config.MovieDownloadTasksCount = 3);
        var dbContext = IDbContext;
        var movieFiles = await dbContext.DownloadTaskMovieFile.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        movieFiles.Count.ShouldBe(3);
        var target = movieFiles[0];
        var sibling = movieFiles[1];
        var control = movieFiles[2];
        var paths = Mock.Container.Resolve<IPathProvider>();
        var root = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DownloadRootPath = root;
        target.DirectoryMeta.MovieFolder = "Movies/shared/file";
        target.DownloadStatus = DownloadStatus.Completed;
        sibling.DirectoryMeta.DownloadRootPath = Path.Combine(root, "Movies");
        sibling.DirectoryMeta.MovieFolder = "shared/file";
        sibling.DownloadStatus = siblingStatus;
        control.DirectoryMeta.DownloadRootPath = root;
        control.DirectoryMeta.MovieFolder = "Movies/control";
        control.DownloadStatus = DownloadStatus.Downloading;
        foreach (var file in movieFiles)
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
        fs.Directory.Exists(Path.GetDirectoryName(target.DownloadDirectory)!).ShouldBeTrue();
        (await dbContext.DownloadTaskMovieFile.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(new[] { target.Id, sibling.Id, control.Id }.Order());
    }

    [Test]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Completed)]
    [Arguments(DownloadStatus.Deleted)]
    public async Task ShouldRespectEpisodeSiblingStatusWhenCleaningMovie(DownloadStatus siblingStatus)
    {
        // Arrange
        await SetupDatabase(63003, config =>
        {
            config.MovieDownloadTasksCount = 3;
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var movieFiles = await dbContext.DownloadTaskMovieFile.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        movieFiles.Count.ShouldBe(3);
        var target = movieFiles[0];
        var control = movieFiles[2];
        var sibling = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().SingleAsync(CancellationToken);
        var paths = Mock.Container.Resolve<IPathProvider>();
        var root = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DownloadRootPath = root;
        target.DirectoryMeta.MovieFolder = "TvShows/shared/file";
        target.DownloadStatus = DownloadStatus.Completed;
        control.DirectoryMeta.DownloadRootPath = root;
        control.DirectoryMeta.MovieFolder = "Movies/control";
        control.DownloadStatus = DownloadStatus.Downloading;
        sibling.DirectoryMeta.DownloadRootPath = Path.Combine(root, "Movies");
        sibling.DirectoryMeta.TvShowFolder = "shared";
        sibling.DirectoryMeta.SeasonFolder = "file";
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
        fs.Directory.Exists(Path.GetDirectoryName(target.DownloadDirectory)!).ShouldBeTrue();
        (await dbContext.DownloadTaskMovieFile.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(new[] { target.Id, control.Id }.Order());
        (await dbContext.DownloadTaskTvShowEpisodeFile.Select(x => x.Id).SingleAsync(CancellationToken)).ShouldBe(sibling.Id);
    }

    [Test]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Completed)]
    [Arguments(DownloadStatus.Deleted)]
    public async Task ShouldRespectPhotoSiblingStatusWhenCleaningMovie(DownloadStatus siblingStatus)
    {
        // Arrange
        await SetupDatabase(63004, config =>
        {
            config.MovieDownloadTasksCount = 3;
            config.PlexPhotoLibraryCount = 1;
        });
        var dbContext = IDbContext;
        var movieFiles = await dbContext.DownloadTaskMovieFile.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        movieFiles.Count.ShouldBe(3);
        var target = movieFiles[0];
        var control = movieFiles[2];
        var library = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum);
        var album = FakeData
            .GetDownloadTaskPhotoAlbum(new Seed(63004))
            .RuleFor(x => x.Children, _ => FakeData.GetDownloadTaskPhotoImage(new Seed(63005)).Generate(1))
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
        target.DirectoryMeta.MovieFolder = "Photos/shared/file";
        target.DownloadStatus = DownloadStatus.Completed;
        control.DirectoryMeta.DownloadRootPath = root;
        control.DirectoryMeta.MovieFolder = "Movies/control";
        control.DownloadStatus = DownloadStatus.Downloading;
        sibling.DirectoryMeta.DownloadRootPath = Path.Combine(root, "Movies");
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
        fs.Directory.Exists(Path.GetDirectoryName(target.DownloadDirectory)!).ShouldBeTrue();
        (await dbContext.DownloadTaskMovieFile.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(new[] { target.Id, control.Id }.Order());
        (await dbContext.DownloadTaskPhotoImageFiles.Select(x => x.Id).SingleAsync(CancellationToken)).ShouldBe(sibling.Id);
    }

    [Test]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Completed)]
    [Arguments(DownloadStatus.Deleted)]
    public async Task ShouldRespectMusicSiblingStatusWhenCleaningMovie(DownloadStatus siblingStatus)
    {
        // Arrange
        await SetupDatabase(63006, config =>
        {
            config.MovieDownloadTasksCount = 3;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistDownloadTasksCount = 1;
            config.MusicAlbumDownloadTasksCount = 1;
            config.MusicTrackDownloadTasksCount = 1;
            config.MusicTrackFileDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var movieFiles = await dbContext.DownloadTaskMovieFile.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        movieFiles.Count.ShouldBe(3);
        var target = movieFiles[0];
        var control = movieFiles[2];
        var sibling = await dbContext.DownloadTaskMusicTrackFiles.SingleAsync(CancellationToken);
        var paths = Mock.Container.Resolve<IPathProvider>();
        var root = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DownloadRootPath = root;
        target.DirectoryMeta.MovieFolder = "Music/shared/file";
        target.DownloadStatus = DownloadStatus.Completed;
        control.DirectoryMeta.DownloadRootPath = root;
        control.DirectoryMeta.MovieFolder = "Movies/control";
        control.DownloadStatus = DownloadStatus.Downloading;
        sibling.DirectoryMeta.DownloadRootPath = Path.Combine(root, "Movies");
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
        fs.Directory.Exists(Path.GetDirectoryName(target.DownloadDirectory)!).ShouldBeTrue();
        (await dbContext.DownloadTaskMovieFile.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(new[] { target.Id, control.Id }.Order());
        (await dbContext.DownloadTaskMusicTrackFiles.Select(x => x.Id).SingleAsync(CancellationToken)).ShouldBe(sibling.Id);
    }

    [Test]
    [Arguments(DownloadStatus.Downloading)]
    [Arguments(DownloadStatus.Completed)]
    [Arguments(DownloadStatus.Deleted)]
    public async Task ShouldRespectOtherVideoSiblingStatusWhenCleaningMovie(DownloadStatus siblingStatus)
    {
        // Arrange
        await SetupDatabase(63007, config =>
        {
            config.MovieDownloadTasksCount = 3;
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoDownloadTasksCount = 1;
            config.OtherVideoFileDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var movieFiles = await dbContext.DownloadTaskMovieFile.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        movieFiles.Count.ShouldBe(3);
        var target = movieFiles[0];
        var control = movieFiles[2];
        var sibling = await dbContext.DownloadTaskOtherVideoFiles.SingleAsync(CancellationToken);
        var paths = Mock.Container.Resolve<IPathProvider>();
        var root = paths.DefaultDownloadsDestinationFolder;
        target.DirectoryMeta.DownloadRootPath = root;
        target.DirectoryMeta.MovieFolder = "OtherVideos/shared/file";
        target.DownloadStatus = DownloadStatus.Completed;
        control.DirectoryMeta.DownloadRootPath = root;
        control.DirectoryMeta.MovieFolder = "Movies/control";
        control.DownloadStatus = DownloadStatus.Downloading;
        sibling.DirectoryMeta.DownloadRootPath = Path.Combine(root, "Movies");
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
        fs.Directory.Exists(Path.GetDirectoryName(target.DownloadDirectory)!).ShouldBeTrue();
        (await dbContext.DownloadTaskMovieFile.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(new[] { target.Id, control.Id }.Order());
        (await dbContext.DownloadTaskOtherVideoFiles.Select(x => x.Id).SingleAsync(CancellationToken)).ShouldBe(sibling.Id);
    }

    [Test]
    public async Task ShouldNotDeleteMovieDownloadDirectory_WhenCleaningCompletedMovie()
    {
        // Arrange
        await SetupDatabase(63008, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var target = await dbContext.DownloadTaskMovieFile.AsTracking().SingleAsync(CancellationToken);
        target.DirectoryMeta.DownloadRootPath = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;
        target.DownloadStatus = DownloadStatus.Completed;
        dbContext.Entry(target).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var parent = Path.GetDirectoryName(target.DownloadDirectory)!;
        Mock.Mock<IPath>()
            .Setup(x => x.GetDirectoryName(target.DownloadFilePath))
            .Returns(target.DownloadDirectory)
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

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(target.ToKey()));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IPath>().Verify();
        Mock.Mock<IDirectory>().Verify();
        Mock.Mock<IPath>().Verify(x => x.GetDirectoryName(target.DownloadDirectory), Times.Never());
        Mock.Mock<IDirectory>().Verify(x => x.Exists(parent), Times.Never());
        Mock.Mock<IDirectory>().Verify(x => x.Delete(parent), Times.Never());
    }
}
