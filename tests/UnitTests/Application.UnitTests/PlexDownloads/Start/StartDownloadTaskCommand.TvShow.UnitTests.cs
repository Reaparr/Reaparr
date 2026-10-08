namespace Reaparr.Application.UnitTests;

public class StartDownloadTaskCommandTvShowUnitTests : BaseUnitTest<StartDownloadTaskCommandHandler>
{
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
