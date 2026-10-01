namespace Reaparr.Application.UnitTests;

public class PauseDownloadTaskCommandUnitTests : BaseUnitTest<PauseDownloadTaskCommandHandler>
{
    [Test]
    public async Task ShouldHaveFailedResult_WhenGivenAnInvalidId()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        await SetupDatabase(34006);

        // Act
        var result = await Sut.ExecuteAsync(new PauseDownloadTaskCommand(Guid.Empty), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Has404NotFoundError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldHaveFailedResult_WhenTheDownloadTaskCouldNotBeStopped()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        await SetupDatabase(30082, config => config.MovieDownloadTasksCount = 2);
        var movieDownloadTasks = await IDbContext.DownloadTaskMovie.ToListAsync(CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Error"));

        // Act
        var result = await Sut.ExecuteAsync(
            new PauseDownloadTaskCommand(movieDownloadTasks.First().Id),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Has404NotFoundError().ShouldNotBe(true);
    }

    [Test]
    public async Task ShouldHaveSetMovieDownloadTasksToPaused_WhenAtLeastOneValidIdIsGiven()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        await SetupDatabase(9999, config => config.MovieDownloadTasksCount = 2);
        var movieDownloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            cancellationToken: CancellationToken
        );
        var testDownloadTask = movieDownloadTasks.First().ToKey();

        var downloadableTasks = await IDbContext.GetDownloadableChildTaskKeys(testDownloadTask, CancellationToken);
        await IDbContext
            .DownloadTaskMovieFile.Where(x => x.Id == downloadableTasks.First().Id)
            .ExecuteUpdateAsync(
                p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk()
            .Verifiable(Times.Once);

        // Act
        var result = await Sut.ExecuteAsync(new PauseDownloadTaskCommand(testDownloadTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldCallStopDownloadJob_WhenTaskIsDownloadingAndAtLeastOneValidIdIsGiven()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(4));
        await SetupDatabase(
            19965,
            config =>
            {
                config.TvShowDownloadTasksCount = 2;
                config.TvShowSeasonDownloadTasksCount = 2;
                config.TvShowEpisodeDownloadTasksCount = 2;
            }
        );
        var tvShowDownloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            cancellationToken: CancellationToken
        );
        var testDownloadTask = tvShowDownloadTasks.First().ToKey();
        var downloadableTasks = await IDbContext.GetDownloadableChildTaskKeys(testDownloadTask, CancellationToken);

        downloadableTasks.Count.ShouldBeGreaterThan(0);
        var downloadingKey = downloadableTasks.First();

        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == downloadingKey.Id)
            .ExecuteUpdateAsync(
                p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x =>
                x.IsDownloading(
                    It.Is<DownloadTaskKey>(key => key.Id == downloadingKey.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x =>
                x.IsDownloading(
                    It.Is<DownloadTaskKey>(key => key.Id != downloadingKey.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk()
            .Verifiable(Times.Once);

        // Act
        var result = await Sut.ExecuteAsync(new PauseDownloadTaskCommand(testDownloadTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldPauseDownloadingAndMovingTasks_WhenMultipleChildrenAreActive()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        await SetupDatabase(
            45112,
            config =>
            {
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 3;
            }
        );

        var tvShowDownloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            cancellationToken: CancellationToken
        );
        var testDownloadTask = tvShowDownloadTasks.First().ToKey();
        var fileTasks = await IDbContext
            .DownloadTaskTvShowEpisodeFile.AsNoTracking()
            .Where(x => x.PlexServerId == testDownloadTask.PlexServerId)
            .OrderBy(x => x.FullTitle)
            .ToListAsync(CancellationToken);

        fileTasks.Count.ShouldBeGreaterThan(2);
        var downloadingKey = fileTasks[0].ToKey();
        var movingKey = fileTasks[1].ToKey();
        var inactiveKey = fileTasks[2].ToKey();

        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == downloadingKey.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.DownloadStatus, DownloadStatus.Downloading)
                        .SetProperty(x => x.DownloadSpeed, 1234),
                CancellationToken
            );
        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == movingKey.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.DownloadStatus, DownloadStatus.Moving)
                        .SetProperty(x => x.FileTransferSpeed, 4321),
                CancellationToken
            );

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x =>
                x.IsDownloading(
                    It.Is<DownloadTaskKey>(key => key.Id == downloadingKey.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x =>
                x.IsDownloading(
                    It.Is<DownloadTaskKey>(key => key.Id != downloadingKey.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x =>
                x.IsDownloadFileMoving(
                    It.Is<DownloadTaskKey>(key => key.Id == movingKey.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x =>
                x.IsDownloadFileMoving(
                    It.Is<DownloadTaskKey>(key => key.Id != movingKey.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StopMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();

        // Act
        var result = await Sut.ExecuteAsync(new PauseDownloadTaskCommand(testDownloadTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(x => x.StopDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()), Times.Once);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StopMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k.Id == downloadingKey.Id),
                        DownloadStatus.Paused,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k.Id == inactiveKey.Id),
                        DownloadStatus.Paused,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );

        var downloadingTask = await IDbContext.GetDownloadTaskFileAsync(downloadingKey, CancellationToken);
        downloadingTask.ShouldNotBeNull();
        downloadingTask.DownloadStatus.ShouldBe(DownloadStatus.Downloading);
        downloadingTask.DownloadSpeed.ShouldBe(1234);

        var movingTask = await IDbContext.GetDownloadTaskFileAsync(movingKey, CancellationToken);
        movingTask.ShouldNotBeNull();
        movingTask.DownloadStatus.ShouldBe(DownloadStatus.MovePaused);
        movingTask.FileTransferSpeed.ShouldBe(0);

        var inactiveTask = await IDbContext.GetDownloadTaskFileAsync(inactiveKey, CancellationToken);
        inactiveTask.ShouldNotBeNull();
        inactiveTask.DownloadStatus.ShouldBe(DownloadStatus.Queued);
    }

    [Test]
    public async Task ShouldNotResetProgressOrStatus_WhenFileTaskIsMoveFinished()
    {
        // Regression: a MoveFinished task must be skipped by the pause handler.
        // Previously, ResetDownloadTaskProgress zeroed DataReceived, FileDataTransferred,
        // and CurrentFileTransferBytesOffset, causing the progress to visually reset to 0%
        // while the file was already fully moved to its destination.
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        await SetupDatabase(
            57001,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var fileTask = await dbContext.DownloadTaskMovieFile.AsTracking().FirstAsync(CancellationToken);
        var parentTask = await dbContext.DownloadTaskMovie.AsTracking().FirstAsync(CancellationToken);

        // Seed a fully-moved state
        fileTask.DownloadStatus = DownloadStatus.MoveFinished;
        fileTask.DataTotal = 500_000_000;
        fileTask.DataReceived = 500_000_000;
        fileTask.FileDataTransferred = 500_000_000;
        fileTask.CurrentFileTransferBytesOffset = 500_000_000;
        await dbContext.SaveChangesAsync(CancellationToken);

        // These should never be called for a MoveFinished task
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StopMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk()
            .Verifiable(Times.Never);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await Sut.ExecuteAsync(new PauseDownloadTaskCommand(parentTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var after = await IDbContext.GetDownloadTaskFileAsync(fileTask.ToKey(), CancellationToken);
        after.ShouldNotBeNull();
        after.DownloadStatus.ShouldBe(DownloadStatus.MoveFinished); // status must not change
        after.FileDataTransferred.ShouldBe(500_000_000L); // transfer bytes must not reset
        after.CurrentFileTransferBytesOffset.ShouldBe(500_000_000L); // offset must not reset
        after.DataReceived.ShouldBe(500_000_000L); // download bytes must not reset

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StopMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
    }

    [Test]
    public async Task ShouldPreserveTransferProgressAndSetMovePaused_WhenFileTaskIsDownloadFinished()
    {
        // Arrange
        await SetupDatabase(57002, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var fileTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var parentTask = await dbContext.DownloadTaskMovie.FirstAsync(CancellationToken);
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == fileTask.Id)
            .ExecuteUpdateAsync(
                update =>
                    update
                        .SetProperty(x => x.DownloadStatus, DownloadStatus.DownloadFinished)
                        .SetProperty(x => x.DataTotal, 400_000_000L)
                        .SetProperty(x => x.DataReceived, 400_000_000L),
                CancellationToken
            );

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(It.Is<DownloadTaskKey>(key => key.Id == fileTask.Id), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(key => key.Id == fileTask.Id),
                    DownloadStatus.MovePaused,
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new PauseDownloadTaskCommand(parentTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = await dbContext.GetDownloadTaskFileAsync(fileTask.ToKey(), CancellationToken);
        after.ShouldNotBeNull();
        after.DataTotal.ShouldBe(400_000_000L);
        after.DataReceived.ShouldBe(400_000_000L);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StopMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
    }

    [Test]
    public async Task ShouldPreserveDirectDownloadSnapshot_WhenPausingDownloadingTask()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        await SetupDatabase(57003, config => config.MovieDownloadTasksCount = 1);

        var dbContext = IDbContext;
        var fileTask = await dbContext.DownloadTaskMovieFile.AsTracking().FirstAsync(CancellationToken);
        var parentTask = await dbContext.DownloadTaskMovie.AsTracking().FirstAsync(CancellationToken);

        var snapshot = new DirectDownloadSnapshot
        {
            SaveProgress = 51.2,
            Status = 2,
            Urls = ["https://example.test/file.mkv"],
            TotalFileSize = 1_000_000,
            FileName = fileTask.FileName,
            DownloadingFileExtension = ".reaptemp",
            IsSupportDownloadInRange = true,
            Chunks =
            [
                new DirectDownloadSnapshotChunk
                {
                    Id = Guid.NewGuid().ToString(),
                    Start = 0,
                    End = 499_999,
                    Position = 123_456,
                    MaxTryAgainOnFailure = 3,
                    Timeout = 1000,
                },
            ],
        };

        fileTask.DownloadStatus = DownloadStatus.Downloading;
        fileTask.DataReceived = 123_456;
        fileTask.DownloadSpeed = 999;
        fileTask.DirectDownloadSnapshot = snapshot;
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await Sut.ExecuteAsync(new PauseDownloadTaskCommand(parentTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        DownloadStatus.Paused,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );

        var after = await IDbContext.GetDownloadTaskFileAsync(fileTask.ToKey(), CancellationToken);
        after.ShouldNotBeNull();
        after.DownloadStatus.ShouldBe(DownloadStatus.Downloading);
        after.DataReceived.ShouldBe(123_456L);
        after.DirectDownloadSnapshot.ShouldNotBeNull();
        after.DirectDownloadSnapshot!.SaveProgress.ShouldBe(snapshot.SaveProgress);
        after.DirectDownloadSnapshot.Chunks.Count.ShouldBe(1);
    }

    [Test]
    public async Task ShouldStopDownloadTaskJobAndSetAutoPaused_WhenAutoPauseIsTrueAndChildIsActivelyDownloading()
    {
        // Arrange
        await SetupDatabase(67004, config => config.MovieDownloadTasksCount = 1);

        var parentTask = await IDbContext
            .DownloadTaskMovie.OrderBy(x => x.Id)
            .AsTracking()
            .FirstAsync(CancellationToken);
        var childTask = await IDbContext
            .DownloadTaskMovieFile.OrderBy(x => x.Id)
            .AsTracking()
            .FirstAsync(CancellationToken);
        var childKey = childTask.ToKey();

        childTask.DownloadStatus = DownloadStatus.Downloading;
        await IDbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x =>
                x.IsDownloading(It.Is<DownloadTaskKey>(key => key.Id == childKey.Id), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x =>
                x.StopDownloadTaskJob(
                    It.Is<DownloadTaskKey>(key => key.Id == childKey.Id),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnOk()
            .Verifiable(Times.Once);
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(key => key.Id == childKey.Id),
                    DownloadStatus.AutoPaused,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        // Act
        var result = await Sut.ExecuteAsync(
            new PauseDownloadTaskCommand(parentTask.Id, AutoPause: true),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x =>
                    x.StopDownloadTaskJob(
                        It.Is<DownloadTaskKey>(key => key.Id == childKey.Id),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(key => key.Id == childKey.Id),
                        DownloadStatus.AutoPaused,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldSetAutoMovePaused_WhenAutoPauseIsTrueAndStatusIsMoving()
    {
        await SetupDatabase(67002, config => config.MovieDownloadTasksCount = 1);

        var parentTask = await IDbContext.DownloadTaskMovie.AsTracking().FirstAsync(CancellationToken);
        var childTask = await IDbContext.DownloadTaskMovieFile.AsTracking().FirstAsync(CancellationToken);

        await IDbContext
            .DownloadTaskMovieFile.Where(x => x.Id == childTask.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.DownloadStatus, DownloadStatus.Moving)
                        .SetProperty(x => x.FileTransferSpeed, 2048),
                CancellationToken
            );

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        var result = await Sut.ExecuteAsync(
            new PauseDownloadTaskCommand(parentTask.Id, AutoPause: true),
            CancellationToken
        );

        result.IsSuccess.ShouldBeTrue();

        var updated = await IDbContext.GetDownloadTaskFileAsync(childTask.ToKey(), CancellationToken);
        updated.ShouldNotBeNull();
        updated.DownloadStatus.ShouldBe(DownloadStatus.AutoMovePaused);
        updated.FileTransferSpeed.ShouldBe(0);
    }

    [Test]
    public async Task ShouldStopMoveDownloadFileJobAndSetAutoMovePaused_WhenAutoPauseIsTrueAndChildIsActivelyMoving()
    {
        // Arrange
        await SetupDatabase(67005, config => config.MovieDownloadTasksCount = 1);

        var parentTask = await IDbContext.DownloadTaskMovie.AsTracking().FirstAsync(CancellationToken);
        var childTask = await IDbContext.DownloadTaskMovieFile.AsTracking().FirstAsync(CancellationToken);

        await IDbContext
            .DownloadTaskMovieFile.Where(x => x.Id == childTask.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.DownloadStatus, DownloadStatus.Moving)
                        .SetProperty(x => x.FileTransferSpeed, 2048),
                CancellationToken
            );

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x =>
                x.StopMoveDownloadFileJob(
                    It.Is<DownloadTaskKey>(k => k == childTask.ToKey()),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnOk()
            .Verifiable(Times.Once);

        // Act
        var result = await Sut.ExecuteAsync(
            new PauseDownloadTaskCommand(parentTask.Id, AutoPause: true),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StopMoveDownloadFileJob(It.Is<DownloadTaskKey>(k => k == childTask.ToKey()), CancellationToken),
                Times.Once
            );

        var updated = await IDbContext.GetDownloadTaskFileAsync(childTask.ToKey(), CancellationToken);
        updated.ShouldNotBeNull();
        updated.DownloadStatus.ShouldBe(DownloadStatus.AutoMovePaused);
        updated.FileTransferSpeed.ShouldBe(0);
    }

    [Test]
    public async Task ShouldSetAutoPaused_WhenAutoPauseIsTrueAndTaskIsNotActivelyDownloading()
    {
        await SetupDatabase(67003, config => config.MovieDownloadTasksCount = 1);

        var parentTask = await IDbContext.DownloadTaskMovie.AsTracking().FirstAsync(CancellationToken);
        var childTask = await IDbContext.DownloadTaskMovieFile.AsTracking().FirstAsync(CancellationToken);

        childTask.DownloadStatus = DownloadStatus.Queued;
        childTask.DownloadTaskPhase.ShouldBe(DownloadStatus.Queued.ToDownloadTaskPhase());
        await IDbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        var result = await Sut.ExecuteAsync(
            new PauseDownloadTaskCommand(parentTask.Id, AutoPause: true),
            CancellationToken
        );

        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k.Id == childTask.Id),
                        DownloadStatus.AutoPaused,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }
}
