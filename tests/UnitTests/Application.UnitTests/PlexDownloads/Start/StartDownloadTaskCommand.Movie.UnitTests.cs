namespace Reaparr.Application.UnitTests;

public class StartDownloadTaskCommandMovieUnitTests : BaseUnitTest<StartDownloadTaskCommandHandler>
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

}
