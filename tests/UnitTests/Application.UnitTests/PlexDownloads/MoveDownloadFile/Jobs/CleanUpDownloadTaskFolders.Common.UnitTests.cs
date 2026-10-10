namespace Reaparr.Application.UnitTests;

public class CleanUpDownloadTaskFoldersCommonUnitTests : BaseCommandUnitTest<CleanUpDownloadTaskFoldersCommand>
{
    [Test]
    public async Task ShouldRejectNullCommandBeforeExecutingHandler()
    {
        // Arrange
        CleanUpDownloadTaskFoldersCommand command = null!;

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
    }

    [Test]
    public async Task ShouldReturnNotFound_WhenDownloadTaskDoesNotExist()
    {
        // Arrange
        await SetupDatabase(61001);
        var command = new CleanUpDownloadTaskFoldersCommand(
            new DownloadTaskKey
            {
                Id = Guid.NewGuid(),
                Type = DownloadTaskType.EpisodeData,
                PlexServerId = 1,
                PlexLibraryId = 1,
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Has404NotFoundError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenDownloadRootHasNotBeenResolved()
    {
        // Arrange
        await SetupDatabase(61002, config =>
        {
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var file = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().SingleAsync(CancellationToken);
        file.DirectoryMeta.DownloadRootPath = string.Empty;
        dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        file.DownloadDirectory.ShouldBeEmpty();
        file.DownloadFilePath.ShouldNotBeEmpty();

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(file.ToKey()));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<IPath>().Verify(x => x.GetDirectoryName(file.DownloadFilePath), Times.Once());
        Mock.Mock<IDirectory>().Verify(x => x.Exists(It.IsAny<string>()), Times.Never());
        Mock.Mock<IDirectory>().Verify(x => x.Delete(It.IsAny<string>()), Times.Never());
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenFilePathResolutionThrows()
    {
        // Arrange
        await SetupDatabase(61003, config =>
        {
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var file = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().SingleAsync(CancellationToken);
        file.DirectoryMeta.DownloadRootPath = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;
        dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        Mock.Mock<IPath>()
            .Setup(x => x.GetDirectoryName(file.DownloadFilePath))
            .Throws(new InvalidOperationException("path failure"))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(file.ToKey()));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.HasException<InvalidOperationException>().ShouldBeTrue();
        Mock.Mock<IPath>().Verify();
        Mock.Mock<IDirectory>().Verify(x => x.Exists(It.IsAny<string>()), Times.Never());
        Mock.Mock<IDirectory>().Verify(x => x.Delete(It.IsAny<string>()), Times.Never());
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenFilePathResolutionReturnsEmpty()
    {
        // Arrange
        await SetupDatabase(61004, config =>
        {
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var file = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().SingleAsync(CancellationToken);
        file.DirectoryMeta.DownloadRootPath = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;
        dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        Mock.Mock<IPath>()
            .Setup(x => x.GetDirectoryName(file.DownloadFilePath))
            .Returns(string.Empty)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(file.ToKey()));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<IPath>().Verify();
        Mock.Mock<IDirectory>().Verify(x => x.Exists(It.IsAny<string>()), Times.Never());
        Mock.Mock<IDirectory>().Verify(x => x.Delete(It.IsAny<string>()), Times.Never());
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenDownloadDirectoryEntriesCannotBeRead()
    {
        // Arrange
        await SetupDatabase(61005, config =>
        {
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var file = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().SingleAsync(CancellationToken);
        file.DirectoryMeta.DownloadRootPath = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;
        dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        Mock.Mock<IPath>()
            .Setup(x => x.GetDirectoryName(file.DownloadFilePath))
            .Returns(file.DownloadDirectory)
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.Exists(file.DownloadDirectory))
            .Returns(true)
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.GetFileSystemEntries(file.DownloadDirectory))
            .Throws(new UnauthorizedAccessException("denied"))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(file.ToKey()));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.HasException<UnauthorizedAccessException>().ShouldBeTrue();
        Mock.Mock<IPath>().Verify();
        Mock.Mock<IDirectory>().Verify();
        Mock.Mock<IDirectory>().Verify(x => x.Delete(It.IsAny<string>()), Times.Never());
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenDownloadDirectoryCannotBeDeleted()
    {
        // Arrange
        await SetupDatabase(61006, config =>
        {
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var file = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().SingleAsync(CancellationToken);
        file.DirectoryMeta.DownloadRootPath = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;
        dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        Mock.Mock<IPath>()
            .Setup(x => x.GetDirectoryName(file.DownloadFilePath))
            .Returns(file.DownloadDirectory)
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.Exists(file.DownloadDirectory))
            .Returns(true)
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.GetFileSystemEntries(file.DownloadDirectory))
            .Returns([])
            .Verifiable(Times.Once());
        Mock.Mock<IDirectory>()
            .Setup(x => x.Delete(file.DownloadDirectory))
            .Throws(new UnauthorizedAccessException("denied"))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(file.ToKey()));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.HasException<UnauthorizedAccessException>().ShouldBeTrue();
        Mock.Mock<IPath>().Verify();
        Mock.Mock<IDirectory>().Verify();
        Mock.Mock<IDirectory>().Verify(x => x.Delete(file.DownloadDirectory), Times.Once());
    }

    [Test]
    public async Task ShouldReturnSuccessWithoutDeleting_WhenDownloadDirectoryDoesNotExist()
    {
        // Arrange
        await SetupDatabase(61007, config =>
        {
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var file = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().SingleAsync(CancellationToken);
        file.DirectoryMeta.DownloadRootPath = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;
        dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        var controlPath = Path.Combine(Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder, "retained.bin");
        byte[] controlBytes = [71, 72];
        SetupFileSystem(fs => fs.AddFile(controlPath, new MockFileData(controlBytes)));

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(file.ToKey()));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var fs = Mock.Container.Resolve<IFileSystem>();
        fs.Directory.Exists(file.DownloadDirectory).ShouldBeFalse();
        fs.File.ReadAllBytes(controlPath).ShouldBe(controlBytes);
    }

    [Test]
    public async Task ShouldPreserveNonEmptyDownloadDirectory_WhenDirectoryContainsEntries()
    {
        // Arrange
        await SetupDatabase(61008, config =>
        {
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var file = await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().SingleAsync(CancellationToken);
        file.DirectoryMeta.DownloadRootPath = Mock.Container.Resolve<IPathProvider>().DefaultDownloadsDestinationFolder;
        dbContext.Entry(file).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(CancellationToken);
        SetupFileSystem(fs =>
        {
            fs.AddDirectory(file.DownloadDirectory);
            fs.AddFile(file.DownloadFilePath, new MockFileData("target bytes"));
        });

        // Act
        var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(file.ToKey()));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var fs = Mock.Container.Resolve<IFileSystem>();
        fs.Directory.Exists(file.DownloadDirectory).ShouldBeTrue();
        fs.File.ReadAllText(file.DownloadFilePath).ShouldBe("target bytes");
    }

    [Test]
    public async Task ShouldKeepEveryDownloadCategoryRoot_WhenFileIsDirectlyInRoot()
    {
        // Arrange
        await SetupDatabase(
            61009,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
                config.MusicArtistDownloadTasksCount = 1;
                config.MusicAlbumDownloadTasksCount = 1;
                config.MusicTrackDownloadTasksCount = 1;
                config.MusicTrackFileDownloadTasksCount = 1;
                config.PhotoAlbumDownloadTasksCount = 1;
                config.PhotoImageDownloadTasksCount = 1;
                config.PhotoImageFileDownloadTasksCount = 1;
                config.OtherVideoDownloadTasksCount = 1;
                config.OtherVideoFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var files = new DownloadTaskFileBase[]
        {
            await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken),
            await dbContext.DownloadTaskTvShowEpisodeFile.SingleAsync(CancellationToken),
            await dbContext.DownloadTaskMusicTrackFiles.SingleAsync(CancellationToken),
            await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken),
            await dbContext.DownloadTaskOtherVideoFiles.SingleAsync(CancellationToken),
        };
        var paths = Mock.Container.Resolve<IPathProvider>();
        foreach (var file in files)
        {
            file.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
            file.DirectoryMeta.MovieFolder = string.Empty;
            file.DirectoryMeta.TvShowFolder = string.Empty;
            file.DirectoryMeta.SeasonFolder = string.Empty;
            file.DirectoryMeta.MusicArtistFolder = string.Empty;
            file.DirectoryMeta.MusicAlbumFolder = string.Empty;
            file.DirectoryMeta.PhotoAlbumFolder = string.Empty;
            file.DirectoryMeta.OtherVideoFolder = string.Empty;
            file.DownloadStatus = DownloadStatus.Completed;
            dbContext.Entry(file).State = EntityState.Modified;
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        SetupFileSystem(fs =>
        {
            foreach (var file in files)
                fs.AddDirectory(file.DownloadDirectory);
        });

        // Act
        foreach (var file in files)
        {
            var result = await TestHandlerExecuteAsync(new CleanUpDownloadTaskFoldersCommand(file.ToKey()));

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Errors.Count.ShouldBe(0);
        }

        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        foreach (var file in files)
            fileSystem.Directory.Exists(file.DirectoryMeta.GetDownloadCategoryDirectory(file.DownloadTaskType)).ShouldBeTrue();
    }
}
