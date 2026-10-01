namespace Reaparr.Application.UnitTests;

public class StartDownloadTaskCommandUnitTests : BaseUnitTest<StartDownloadTaskCommandHandler>
{
    [Test]
    public async Task ShouldHaveFailedResult_WhenGivenAnInvalidId()
    {
        // Arrange
        await SetupDatabase(63209);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(Guid.Empty), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Has404NotFoundError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldHaveFailedResult_WhenServerIsPausedByUser()
    {
        // Arrange
        await SetupDatabase(
            55109,
            x =>
            {
                x.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var movieTask = await dbContext.DownloadTaskMovie.FirstAsync(CancellationToken);
        await dbContext
            .PlexServers.Where(x => x.Id == movieTask.PlexServerId)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDownloadsPausedByUser, true), CancellationToken);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
    }

    [Test]
    public async Task ShouldStartMoveJob_WhenDownloadTaskIsInDownloadFinishedStatus()
    {
        // Arrange
        await SetupDatabase(
            11234,
            x =>
            {
                x.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var movieDownloadTasks = await dbContext.DownloadTaskMovieFile.AsTracking().ToListAsync(CancellationToken);
        movieDownloadTasks.SetDownloadStatus(DownloadStatus.DownloadFinished);
        await dbContext.SaveChangesAsync(CancellationToken);

        var movieTask = await dbContext.DownloadTaskMovie.FirstAsync(CancellationToken);

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert: move job started, download job never touched
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Once());
    }

    [Test]
    public async Task ShouldHaveFailedResult_WhenStartMoveDownloadFileJobFails()
    {
        // Arrange
        await SetupDatabase(
            11235,
            x =>
            {
                x.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var movieDownloadTasks = await dbContext.DownloadTaskMovieFile.AsTracking().ToListAsync(CancellationToken);
        movieDownloadTasks.SetDownloadStatus(DownloadStatus.DownloadFinished);
        await dbContext.SaveChangesAsync(CancellationToken);

        var movieTask = await dbContext.DownloadTaskMovie.FirstAsync(CancellationToken);

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Move scheduler error"));

        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message.Contains("Move scheduler error"));
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
    }

    [Test]
    public async Task ShouldRestoreMovePausedStatus_WhenStartMoveDownloadFileJobFails()
    {
        // Arrange
        await SetupDatabase(11236, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var fileTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var movieTask = await dbContext.DownloadTaskMovie.FirstAsync(CancellationToken);
        await dbContext.SetDownloadStatus(fileTask.ToKey(), DownloadStatus.MovePaused);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(key => key == fileTask.ToKey()),
                    DownloadStatus.DownloadFinished,
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(key => key == fileTask.ToKey()),
                    DownloadStatus.MovePaused,
                    CancellationToken.None
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(fileTask.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StartMoveDownloadFileJob(fileTask.ToKey(), CancellationToken))
            .ReturnsAsync(Result.Fail("Move scheduler error"))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
    }

    [Test]
    public async Task ShouldStartMoveJob_WhenDownloadTaskIsInMoveErrorStatus()
    {
        // Arrange
        await SetupDatabase(
            55678,
            x =>
            {
                x.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var movieDownloadTasks = await dbContext.DownloadTaskMovieFile.AsTracking().ToListAsync(CancellationToken);
        movieDownloadTasks.SetDownloadStatus(DownloadStatus.MoveError);
        await dbContext.SaveChangesAsync(CancellationToken);

        var movieTask = await dbContext.DownloadTaskMovie.FirstAsync(CancellationToken);

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert: move job retried, download job never touched
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Once());
    }

    [Test]
    public async Task ShouldNotPauseDownloadTasksInFileTransfer_WhenADownloadTaskIsAlreadyTransferring()
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
            .Verifiable(Times.AtLeastOnce());
        await SetupDatabase(
            96318,
            x =>
            {
                x.TvShowDownloadTasksCount = 5;
                x.TvShowSeasonDownloadTasksCount = 2;
                x.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var tvShowDownloadTasks = await dbContext
            .DownloadTaskTvShow.AsTracking()
            .Include(x => x.Children)
                .ThenInclude(x => x.Children)
                    .ThenInclude(x => x.Children)
            .ToListAsync(CancellationToken);

        var alreadyMergingTask = tvShowDownloadTasks.First();
        var pausedMergeTask = tvShowDownloadTasks.Last();

        alreadyMergingTask.SetDownloadStatus(DownloadStatus.Moving);
        pausedMergeTask.SetDownloadStatus(DownloadStatus.MovePaused);
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once);

        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask).Verifiable(Times.Once);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(pausedMergeTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldNotPauseTheActiveDownloads_WhenThatActiveDownloadTaskIsStarted()
    {
        // Arrange
        await SetupDatabase(
            31917,
            x =>
            {
                x.TvShowDownloadTasksCount = 5;
                x.TvShowSeasonDownloadTasksCount = 2;
                x.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var tvShowDownloadTasks = await dbContext
            .DownloadTaskTvShow.AsTracking()
            .Include(x => x.Children)
                .ThenInclude(x => x.Children)
                    .ThenInclude(x => x.Children)
            .ToListAsync(CancellationToken);

        tvShowDownloadTasks.SetDownloadStatus(DownloadStatus.Completed);
        var lastDownloadTask = tvShowDownloadTasks.Last();
        lastDownloadTask.SetDownloadStatus(DownloadStatus.Queued);
        await dbContext.SaveChangesAsync(CancellationToken);

        var orderedDownloadTasks = await IDbContext.GetDownloadableChildTasks(
            lastDownloadTask.ToKey(),
            CancellationToken
        );
        orderedDownloadTasks.Count.ShouldBeGreaterThan(0);
        var downloadingTask = orderedDownloadTasks.First();

        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == downloadingTask.Id)
            .ExecuteUpdateAsync(
                p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([downloadingTask.ToKey()]);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock.SetupCommand(It.IsAny<PauseDownloadTaskCommand>).ReturnOk();
        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(lastDownloadTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var downloadTasks = await IDbContext.GetDownloadableChildTasks(lastDownloadTask.ToKey(), CancellationToken);
        downloadTasks.Count(x => x.DownloadStatus == DownloadStatus.Downloading).ShouldBe(1);
        downloadTasks.Count(x => x.DownloadStatus == DownloadStatus.Queued).ShouldBe(downloadTasks.Count - 1);

        // Verify that the downloading task was not paused as we are starting one that is already downloading
        Mock.VerifyEventPublished(It.IsAny<PauseDownloadTaskCommand>, Times.Never());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Once());
    }

    [Test]
    public async Task ShouldStartPausedEpisode_WhenStartingPausedTvShowTask()
    {
        // Arrange
        await SetupDatabase(
            44821,
            x =>
            {
                x.TvShowDownloadTasksCount = 1;
                x.TvShowSeasonDownloadTasksCount = 1;
                x.TvShowEpisodeCount = 5;
                x.TvShowEpisodeDownloadTasksCount = 5;
            }
        );

        var tvShow = await IDbContext.DownloadTaskTvShow.AsNoTracking().FirstAsync(CancellationToken);
        var orderedChildTasks = await IDbContext.GetDownloadableChildTasks(tvShow.ToKey(), CancellationToken);

        orderedChildTasks.Count.ShouldBeGreaterThan(1);
        var queuedTask = orderedChildTasks[0];
        var pausedTask = orderedChildTasks[1];

        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == queuedTask.Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Queued), CancellationToken);
        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == pausedTask.Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Paused), CancellationToken);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(key => key.Id == pausedTask.Id),
                    DownloadStatus.Queued,
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);

        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(tvShow.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.Is<DownloadTaskKey>(k => k.Id == pausedTask.Id), CancellationToken),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldStartFirstPausedEpisodeAndQueueOtherPausedEpisodes_WhenStartingTvShowTask()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.Is<DownloadStatus>(s => s == DownloadStatus.Queued),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.AtLeastOnce());
        await SetupDatabase(
            44822,
            x =>
            {
                x.TvShowDownloadTasksCount = 1;
                x.TvShowSeasonDownloadTasksCount = 1;
                x.TvShowEpisodeCount = 3;
                x.TvShowEpisodeDownloadTasksCount = 3;
            }
        );

        var tvShow = await IDbContext.DownloadTaskTvShow.AsNoTracking().FirstAsync(CancellationToken);
        var orderedChildTasks = await IDbContext.GetDownloadableChildTasks(tvShow.ToKey(), CancellationToken);

        orderedChildTasks.Count.ShouldBeGreaterThan(1);
        var firstPausedTask = orderedChildTasks[0];
        var secondPausedTask = orderedChildTasks[1];

        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == firstPausedTask.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.DownloadStatus, DownloadStatus.Paused).SetProperty(x => x.DownloadSpeed, 1111),
                CancellationToken
            );
        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == secondPausedTask.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.DownloadStatus, DownloadStatus.Paused).SetProperty(x => x.DownloadSpeed, 9999),
                CancellationToken
            );

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);

        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(tvShow.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.Is<DownloadTaskKey>(k => k.Id == firstPausedTask.Id), CancellationToken),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.Is<DownloadStatus>(s => s == DownloadStatus.Queued),
                        It.IsAny<CancellationToken>()
                    ),
                Times.AtLeastOnce()
            );
    }

    [Test]
    public async Task ShouldStartFirstStoppedEpisodeAndQueueOtherStoppedEpisodes_WhenStartingTvShowTask()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.Is<DownloadStatus>(s => s == DownloadStatus.Queued),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.AtLeastOnce());
        await SetupDatabase(
            44823,
            x =>
            {
                x.TvShowDownloadTasksCount = 1;
                x.TvShowSeasonDownloadTasksCount = 1;
                x.TvShowEpisodeCount = 3;
                x.TvShowEpisodeDownloadTasksCount = 3;
            }
        );

        var tvShow = await IDbContext.DownloadTaskTvShow.AsNoTracking().FirstAsync(CancellationToken);
        var orderedChildTasks = await IDbContext.GetDownloadableChildTasks(tvShow.ToKey(), CancellationToken);

        orderedChildTasks.Count.ShouldBeGreaterThan(2);
        var firstStoppedTask = orderedChildTasks[0];
        var secondStoppedTask = orderedChildTasks[1];

        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == firstStoppedTask.Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Stopped), CancellationToken);
        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == secondStoppedTask.Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Stopped), CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);

        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(tvShow.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x =>
                    x.StartDownloadTaskJob(It.Is<DownloadTaskKey>(k => k.Id == firstStoppedTask.Id), CancellationToken),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.Is<DownloadStatus>(s => s == DownloadStatus.Queued),
                        It.IsAny<CancellationToken>()
                    ),
                Times.AtLeastOnce()
            );
    }

    [Test]
    public async Task ShouldStartFirstStoppedEpisodeAndQueueOtherStoppedEpisodes_WhenStartingSeasonTask()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.Is<DownloadStatus>(s => s == DownloadStatus.Queued),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.AtLeastOnce());
        await SetupDatabase(
            44824,
            x =>
            {
                x.TvShowDownloadTasksCount = 1;
                x.TvShowSeasonDownloadTasksCount = 1;
                x.TvShowEpisodeCount = 3;
                x.TvShowEpisodeDownloadTasksCount = 3;
            }
        );

        var season = await IDbContext.DownloadTaskTvShowSeason.AsNoTracking().FirstAsync(CancellationToken);
        var orderedChildTasks = await IDbContext.GetDownloadableChildTasks(season.ToKey(), CancellationToken);

        orderedChildTasks.Count.ShouldBeGreaterThan(2);
        var firstStoppedTask = orderedChildTasks[0];
        var secondStoppedTask = orderedChildTasks[1];

        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == firstStoppedTask.Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Stopped), CancellationToken);
        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == secondStoppedTask.Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Stopped), CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);

        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(season.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x =>
                    x.StartDownloadTaskJob(It.Is<DownloadTaskKey>(k => k.Id == firstStoppedTask.Id), CancellationToken),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.Is<DownloadStatus>(s => s == DownloadStatus.Queued),
                        It.IsAny<CancellationToken>()
                    ),
                Times.AtLeastOnce()
            );
    }

    [Test]
    public async Task ShouldQueueOtherStoppedChildren_WhenStartingStoppedTvShowWithStoppedSeasons()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.Is<DownloadStatus>(s => s == DownloadStatus.Queued),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.AtLeastOnce());
        await SetupDatabase(
            44825,
            x =>
            {
                x.TvShowDownloadTasksCount = 1;
                x.TvShowSeasonDownloadTasksCount = 2;
                x.TvShowEpisodeDownloadTasksCount = 2;
            }
        );

        var tvShow = await IDbContext.DownloadTaskTvShow.AsNoTracking().FirstAsync(CancellationToken);

        await IDbContext
            .DownloadTaskTvShow.Where(x => x.Id == tvShow.Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Stopped), CancellationToken);
        await IDbContext
            .DownloadTaskTvShowSeason.Where(x => x.ParentId == tvShow.Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Stopped), CancellationToken);
        await IDbContext.DownloadTaskTvShowEpisodeFile.ExecuteUpdateAsync(
            p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Stopped),
            CancellationToken
        );

        var orderedChildTasks = await IDbContext.GetDownloadableChildTasks(tvShow.ToKey(), CancellationToken);
        orderedChildTasks.Count.ShouldBeGreaterThan(1);

        var firstStoppedTask = orderedChildTasks[0];

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);

        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(tvShow.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x =>
                    x.StartDownloadTaskJob(It.Is<DownloadTaskKey>(k => k.Id == firstStoppedTask.Id), CancellationToken),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.Is<DownloadStatus>(s => s == DownloadStatus.Queued),
                        It.IsAny<CancellationToken>()
                    ),
                Times.AtLeastOnce()
            );
    }

    [Test]
    public async Task ShouldStartNextStoppedChild_WhenFirstChildIsCompletedOnStoppedTvShow()
    {
        // Arrange
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.Is<DownloadStatus>(s => s == DownloadStatus.Queued),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.AtLeastOnce());
        await SetupDatabase(
            44826,
            x =>
            {
                x.TvShowDownloadTasksCount = 1;
                x.TvShowSeasonDownloadTasksCount = 2;
                x.TvShowEpisodeDownloadTasksCount = 2;
            }
        );

        var tvShow = await IDbContext.DownloadTaskTvShow.AsNoTracking().FirstAsync(CancellationToken);

        await IDbContext
            .DownloadTaskTvShow.Where(x => x.Id == tvShow.Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Stopped), CancellationToken);
        await IDbContext
            .DownloadTaskTvShowSeason.Where(x => x.ParentId == tvShow.Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Stopped), CancellationToken);
        await IDbContext.DownloadTaskTvShowEpisodeFile.ExecuteUpdateAsync(
            p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Stopped),
            CancellationToken
        );

        var orderedChildTasks = await IDbContext.GetDownloadableChildTasks(tvShow.ToKey(), CancellationToken);
        orderedChildTasks.Count.ShouldBeGreaterThan(2);

        var completedTask = orderedChildTasks[0];
        var taskToStart = orderedChildTasks[1];

        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == completedTask.Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Completed), CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);

        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(tvShow.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.Is<DownloadTaskKey>(k => k.Id == taskToStart.Id), CancellationToken),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.Is<DownloadStatus>(s => s == DownloadStatus.Queued),
                        It.IsAny<CancellationToken>()
                    ),
                Times.AtLeastOnce()
            );
    }

    [Test]
    public async Task ShouldPauseTheActiveDownloads_WhenAnotherDownloadTaskIsStarted()
    {
        // Arrange
        await SetupDatabase(
            76276,
            x =>
            {
                x.TvShowDownloadTasksCount = 5;
                x.TvShowSeasonDownloadTasksCount = 2;
                x.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var tvShowDownloadTasks = await dbContext
            .DownloadTaskTvShow.AsTracking()
            .Include(x => x.Children)
                .ThenInclude(x => x.Children)
                    .ThenInclude(x => x.Children)
            .ToListAsync(CancellationToken.None);

        tvShowDownloadTasks.SetDownloadStatus(DownloadStatus.Completed);
        var lastDownloadTask = tvShowDownloadTasks.Last();
        lastDownloadTask.SetDownloadStatus(DownloadStatus.Queued);
        await dbContext.SaveChangesAsync(CancellationToken);

        var orderedDownloadTasks = await IDbContext.GetDownloadableChildTasks(
            lastDownloadTask.ToKey(),
            CancellationToken
        );
        orderedDownloadTasks.Count.ShouldBeGreaterThan(1);
        var downloadingTask = orderedDownloadTasks[1];

        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == downloadingTask.Id)
            .ExecuteUpdateAsync(
                p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([downloadingTask.ToKey()]);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock.SetupCommand(It.IsAny<PauseDownloadTaskCommand>)
            .Returns(
                async (PauseDownloadTaskCommand command, CancellationToken ct) =>
                {
                    var key = await dbContext.GetDownloadTaskKeyAsync(command.DownloadTaskGuid, ct);
                    key.ShouldNotBeNull();
                    await dbContext.SetDownloadStatus(key, DownloadStatus.Queued);
                    return Result.Ok();
                }
            );
        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(lastDownloadTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var downloadTasks = await IDbContext.GetDownloadableChildTasks(lastDownloadTask.ToKey(), CancellationToken);
        downloadTasks.Count(x => x.DownloadStatus == DownloadStatus.Downloading).ShouldBe(0);
        downloadTasks.Count(x => x.DownloadStatus == DownloadStatus.Queued).ShouldBe(downloadTasks.Count);

        // Verify that the downloading task was not paused as we are starting one that is already downloading
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<PauseDownloadTaskCommand>(), It.IsAny<CancellationToken>()), Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Once());
    }

    [Test]
    public async Task ShouldHaveFailedResult_WhenPausingActiveDownloadFails()
    {
        // Arrange
        await SetupDatabase(
            76277,
            x =>
            {
                x.TvShowDownloadTasksCount = 5;
                x.TvShowSeasonDownloadTasksCount = 2;
                x.TvShowEpisodeCount = 2;
            }
        );

        var dbContext = IDbContext;
        var tvShowDownloadTasks = await dbContext
            .DownloadTaskTvShow.AsTracking()
            .Include(x => x.Children)
                .ThenInclude(x => x.Children)
                    .ThenInclude(x => x.Children)
            .ToListAsync(CancellationToken.None);

        tvShowDownloadTasks.SetDownloadStatus(DownloadStatus.Completed);
        var lastDownloadTask = tvShowDownloadTasks.Last();
        lastDownloadTask.SetDownloadStatus(DownloadStatus.Queued);
        await dbContext.SaveChangesAsync(CancellationToken);

        var orderedDownloadTasks = await IDbContext.GetDownloadableChildTasks(
            lastDownloadTask.ToKey(),
            CancellationToken
        );
        orderedDownloadTasks.Count.ShouldBeGreaterThan(1);
        var downloadingTask = orderedDownloadTasks[1];

        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == downloadingTask.Id)
            .ExecuteUpdateAsync(
                p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([downloadingTask.ToKey()]);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock.SetupCommand(It.IsAny<PauseDownloadTaskCommand>).ReturnsAsync(Result.Fail("Pause command failed"));
        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(lastDownloadTask.Id), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message.Contains("Pause command failed"));
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<PauseDownloadTaskCommand>(), It.IsAny<CancellationToken>()), Times.Once());
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
    }

    [Test]
    public async Task ShouldHaveFailedResult_WhenNoDownloadableChildTasksExist()
    {
        // Arrange — seed two movies; delete all file tasks of the first so its downloadable child list is empty
        await SetupDatabase(
            11101,
            x =>
            {
                x.MovieDownloadTasksCount = 2;
            }
        );

        var dbContext = IDbContext;
        var movieTasks = await dbContext.DownloadTaskMovie.ToListAsync(CancellationToken);
        movieTasks.Count.ShouldBeGreaterThanOrEqualTo(2);

        var firstMovieId = movieTasks[0].Id;
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.ParentId == firstMovieId)
            .ExecuteDeleteAsync(CancellationToken);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(firstMovieId), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
    }

    [Test]
    public async Task ShouldHaveFailedResult_WhenSelectedChildIsInCompletedPhase()
    {
        // Arrange
        await SetupDatabase(
            11102,
            x =>
            {
                x.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var movieFileTasks = await dbContext.DownloadTaskMovieFile.AsTracking().ToListAsync(CancellationToken);
        movieFileTasks.SetDownloadStatus(DownloadStatus.Completed);
        await dbContext.SaveChangesAsync(CancellationToken);

        var movieTask = await dbContext.DownloadTaskMovie.FirstAsync(CancellationToken);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
    }

    [Test]
    public async Task ShouldHaveFailedResult_WhenStartDownloadTaskJobFails()
    {
        // Arrange
        await SetupDatabase(
            11103,
            x =>
            {
                x.MovieDownloadTasksCount = 1;
            }
        );

        var movieTask = await IDbContext.DownloadTaskMovie.FirstAsync(CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Scheduler error"));

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
    }

    [Test]
    public async Task ShouldRestorePausedStatus_WhenStartDownloadTaskJobFails()
    {
        // Arrange
        await SetupDatabase(11108, config => config.MovieDownloadTasksCount = 1);
        var dbContext = IDbContext;
        var fileTask = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var movieTask = await dbContext.DownloadTaskMovie.FirstAsync(CancellationToken);
        await dbContext.SetDownloadStatus(fileTask.ToKey(), DownloadStatus.Paused);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(key => key == fileTask.ToKey()),
                    DownloadStatus.Queued,
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(key => key == fileTask.ToKey()),
                    DownloadStatus.Paused,
                    CancellationToken.None
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(fileTask.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(fileTask.ToKey(), CancellationToken))
            .ReturnsAsync(Result.Fail("Scheduler error"))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Never());
    }

    [Test]
    public async Task ShouldSkipStartDownloadJob_WhenTaskIsAlreadyDownloading()
    {
        // Arrange
        await SetupDatabase(
            11104,
            x =>
            {
                x.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var movieFileTasks = await dbContext.DownloadTaskMovieFile.AsTracking().ToListAsync(CancellationToken);
        movieFileTasks.SetDownloadStatus(DownloadStatus.Downloading);
        await dbContext.SaveChangesAsync(CancellationToken);

        var movieTask = await dbContext.DownloadTaskMovie.FirstAsync(CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Once());
    }

    [Test]
    public async Task ShouldSkipStartMoveJob_WhenFileIsAlreadyMoving()
    {
        // Arrange
        await SetupDatabase(
            11105,
            x =>
            {
                x.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var movieFileTasks = await dbContext.DownloadTaskMovieFile.AsTracking().ToListAsync(CancellationToken);
        movieFileTasks.SetDownloadStatus(DownloadStatus.Moving);
        await dbContext.SaveChangesAsync(CancellationToken);

        var movieTask = await dbContext.DownloadTaskMovie.FirstAsync(CancellationToken);

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.IsAny<DownloadTaskKey>(),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Once());
    }

    [Test]
    public async Task ShouldStartDownloadJob_WhenMovieTaskIsPaused()
    {
        // Arrange
        await SetupDatabase(
            11106,
            x =>
            {
                x.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var movieFileTasks = await dbContext.DownloadTaskMovieFile.AsTracking().ToListAsync(CancellationToken);
        movieFileTasks.SetDownloadStatus(DownloadStatus.Paused);
        await dbContext.SaveChangesAsync(CancellationToken);

        var movieTask = await dbContext.DownloadTaskMovie.FirstAsync(CancellationToken);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(key => key.Id == movieFileTasks.Single().Id),
                    DownloadStatus.Queued,
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);

        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Movie tasks have no sibling-queuing side-effects; the single file task starts directly
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.VerifyEventPublished(It.IsAny<PauseDownloadTaskCommand>, Times.Never());
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.VerifyEventPublished(It.IsAny<CheckDownloadQueueEvent>, Times.Once());
    }

    [Test]
    public async Task ShouldNotQueueMovePausedSibling_WhenStartingPausedTvShow()
    {
        // Arrange — a paused download must resume without changing a sibling's transfer phase.
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.Is<DownloadStatus>(s => s == DownloadStatus.Queued),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.AtLeastOnce());
        await SetupDatabase(
            11107,
            x =>
            {
                x.TvShowDownloadTasksCount = 1;
                x.TvShowSeasonDownloadTasksCount = 1;
                x.TvShowEpisodeCount = 3;
                x.TvShowEpisodeDownloadTasksCount = 3;
            }
        );

        var tvShow = await IDbContext.DownloadTaskTvShow.AsNoTracking().FirstAsync(CancellationToken);
        var orderedChildTasks = await IDbContext.GetDownloadableChildTasks(tvShow.ToKey(), CancellationToken);
        orderedChildTasks.Count.ShouldBeGreaterThan(1);

        var firstPausedTask = orderedChildTasks[0];
        var secondPausedTask = orderedChildTasks[1];

        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == firstPausedTask.Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Paused), CancellationToken);
        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == secondPausedTask.Id)
            .ExecuteUpdateAsync(
                p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.MovePaused),
                CancellationToken
            );

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);

        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new StartDownloadTaskCommand(tvShow.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x =>
                    x.StartDownloadTaskJob(
                        It.Is<DownloadTaskKey>(key => key.Id == firstPausedTask.Id),
                        CancellationToken
                    ),
                Times.Once()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(key => key.Id == secondPausedTask.Id),
                        It.IsAny<DownloadStatus>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
    }
}
