namespace Reaparr.Application.UnitTests;

public class DownloadQueueCheckDownloadQueueUnitTests : BaseUnitTest<DownloadQueue>
{
    [Test]
    public async Task ShouldHaveNoUpdates_WhenGivenAnEmptyList()
    {
        // Arrange
        await SetupDatabase(54691);

        // Act
        Sut.Setup();
        var result = await Sut.CheckDownloadQueue([]);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(0);
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldHaveNoStartCommands_WhenServerIsAlreadyDownloading()
    {
        await SetupDatabase(
            78970,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        // Arrange
        // Set up a task that's actually downloading in the database
        var dbContext = IDbContext;
        var downloadTasks = await dbContext
            .DownloadTaskMovie.AsTracking()
            .Where(x => x.PlexServerId == 1)
            .IncludeAll()
            .ToListAsync(CancellationToken);

        var firstMovieDownloadTask = downloadTasks[0];
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == firstMovieDownloadTask.Children.First().Id)
            .ExecuteUpdateAsync(
                p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([firstMovieDownloadTask.Children.First().ToKey()]);
        // Act
        Sut.Setup();
        var result = await Sut.CheckDownloadQueueServer(1);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task ShouldNotStartNextDownloadTask_WhenJobIsStillRunningBeforeDatabaseStatusIsDownloading()
    {
        // Arrange
        await SetupDatabase(
            78971,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        var activeTask = downloadTasks[0].Children[0];
        activeTask.DownloadStatus.ShouldBe(DownloadStatus.Queued);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([activeTask.ToKey()]);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();

        // Act
        var result = await Sut.CheckDownloadQueueServer(activeTask.PlexServerId);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(0);
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldStartDownloadTask_WhenAnotherServerHasActiveDownloadJob()
    {
        // Arrange
        await SetupDatabase(
            78972,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.MovieDownloadTasksCount = 2;
            }
        );
        var dbContext = IDbContext;
        var otherLibrary = await dbContext
            .PlexLibraries.OrderByDescending(x => x.PlexServerId)
            .FirstAsync(CancellationToken);
        var movedTask = await dbContext
            .DownloadTaskMovie.AsTracking()
            .Include(x => x.Children)
            .OrderByDescending(x => x.Id)
            .FirstAsync(CancellationToken);
        new List<DownloadTaskMovie> { movedTask }.SetRelationshipIds(otherLibrary.PlexServerId, otherLibrary.Id);
        await dbContext.SaveChangesAsync(CancellationToken);

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        var serverIds = downloadTasks.Select(x => x.PlexServerId).Distinct().Order().ToArray();
        serverIds.Length.ShouldBe(2);

        var activeServerId = serverIds[0];
        var targetServerId = serverIds[1];
        var activeTask = downloadTasks.First(x => x.PlexServerId == activeServerId).Children[0];
        var targetTask = downloadTasks.First(x => x.PlexServerId == targetServerId).Children[0];
        activeTask.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        targetTask.DownloadStatus.ShouldBe(DownloadStatus.Queued);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .Returns(
                (int serverId) =>
                    Task.FromResult<List<DownloadTaskKey>>(serverId == activeServerId ? [activeTask.ToKey()] : [])
            );
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();

        // Act
        var result = await Sut.CheckDownloadQueueServer(targetServerId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldNotBeNull();
        result.Value.PlexServerId.ShouldBe(targetServerId);
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x =>
                    x.StartDownloadTaskJob(
                        It.Is<DownloadTaskKey>(key => key.PlexServerId == targetServerId),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldNotStartDownloads_WhenServerIsPausedByUser()
    {
        // Arrange
        await SetupDatabase(
            70112,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.MovieDownloadTasksCount = 2;
            }
        );

        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.AsTracking().FirstAsync(CancellationToken);
        server.IsDownloadsPausedByUser = true;
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();

        // Act
        var result = await Sut.CheckDownloadQueueServer(server.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
    }

    [Test]
    public async Task ShouldSkipDownloadQueue_WhenServerIsDisabled()
    {
        // Arrange
        await SetupDatabase(
            70113,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.MovieDownloadTasksCount = 2;
            }
        );

        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);

        await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsServerDownloading(It.IsAny<int>()))
            .ReturnsAsync(false)
            .Verifiable(Times.Never());

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk()
            .Verifiable(Times.Never());

        // Act
        var result = await Sut.CheckDownloadQueueServer(server.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
        Mock.Mock<IDownloadTaskScheduler>().Verify();
    }

    [Test]
    public async Task ShouldHaveOneDownloadTaskStarted_WhenGivenMovieDownloadTasks()
    {
        // Arrange
        await SetupDatabase(
            5000,
            config =>
            {
                config.MovieCount = 2;
                config.MovieDownloadTasksCount = 2;
            }
        );

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);

        // Act
        var result = await Sut.CheckDownloadQueueServer(downloadTasks[0].PlexServerId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var startedDownloadTask = downloadTasks[0].Children[0];
        result.Value.Id.ShouldBe(startedDownloadTask.Id);
    }

    [Test]
    public async Task ShouldHaveNextQueuedDownloadTask_WhenGivenAMovieDownloadTasksWithCompleted()
    {
        // Arrange
        await SetupDatabase(
            45430,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 10;
                config.MovieDownloadTasksCount = 5;
            }
        );

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );
        downloadTasks[0].SetDownloadStatus(DownloadStatus.Completed);
        await IDbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);

        // Act
        var result = await Sut.CheckDownloadQueueServer(downloadTasks[0].PlexServerId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        downloadTasks[0].Children[0].DownloadTaskType.ShouldBe(DownloadTaskType.MovieData);
        result.Value.Id.ShouldBe(downloadTasks[0].Children[0].Id);
    }

    [Test]
    public async Task ShouldHaveNextQueuedDownloadTask_WhenGivenATvShowsDownloadTasksWithCompleted()
    {
        // Arrange
        await SetupDatabase(
            25225,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.TvShowCount = 10;
                config.TvShowDownloadTasksCount = 2;
                config.TvShowSeasonDownloadTasksCount = 2;
                config.TvShowEpisodeDownloadTasksCount = 2;
            }
        );
        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );
        downloadTasks[0].SetDownloadStatus(DownloadStatus.Completed);
        await IDbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);

        // Act
        var result = await Sut.CheckDownloadQueueServer(1);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(downloadTasks[0].Children[0].Children[0].Children[0].Id);
    }

    [Test]
    public async Task ShouldStartNextDownloadTask_WhenJobIsStillRunningButDatabaseStatusIsDownloadFinished()
    {
        // Arrange
        // This test simulates the race condition where a download job has finished and updated
        // the database status to DownloadFinished, but the Quartz job is still in the process
        // of cleaning up and hasn't been removed from the scheduler yet.
        await SetupDatabase(
            88234,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 3;
                config.MovieDownloadTasksCount = 3;
            }
        );

        var dbContext = IDbContext;
        var downloadTasks = await dbContext
            .DownloadTaskMovie.AsTracking()
            .Where(x => x.PlexServerId == 1)
            .IncludeAll()
            .ToListAsync(CancellationToken);

        // Set the first task to DownloadFinished (simulating a just-completed download)
        // We set this on the file-level task (child) which is what actually gets downloaded
        var firstMovieDownloadTask = downloadTasks[0];
        var firstFileTaskId = firstMovieDownloadTask.Children.First().Id;
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == firstFileTaskId)
            .ExecuteUpdateAsync(
                p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.DownloadFinished),
                CancellationToken
            );

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([firstMovieDownloadTask.Children.First().ToKey()]);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();

        // Act
        var result = await Sut.CheckDownloadQueueServer(1);

        // Assert
        // Should succeed and start the next queued task (any queued task that's not the finished one)
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Id.ShouldNotBe(firstFileTaskId);
        result.Value.DownloadStatus.ShouldBe(DownloadStatus.Queued);

        // Verify that StartDownloadTaskJob was called
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldNotStartNextDownloadTask_WhenJobIsStillRunningAndDatabaseStatusIsDownloading()
    {
        // Arrange
        // This test verifies that we still block when a task is actively downloading
        await SetupDatabase(
            66451,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 3;
                config.MovieDownloadTasksCount = 3;
            }
        );

        var dbContext = IDbContext;
        var downloadTasks = await dbContext
            .DownloadTaskMovie.AsTracking()
            .Where(x => x.PlexServerId == 1)
            .IncludeAll()
            .ToListAsync(CancellationToken);

        // Set the first task to Downloading (actively downloading)
        var firstMovieDownloadTask = downloadTasks[0];
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == firstMovieDownloadTask.Children.First().Id)
            .ExecuteUpdateAsync(
                p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([firstMovieDownloadTask.Children.First().ToKey()]);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();

        // Act
        var result = await Sut.CheckDownloadQueueServer(1);

        // Assert
        // Should fail because there's an active download
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(0);

        // Verify that StartDownloadTaskJob was NOT called
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
    }
}
