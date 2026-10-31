namespace Reaparr.Application.UnitTests;

public class RestartDownloadTaskCommandUnitTests : BaseUnitTest<RestartDownloadTaskCommandHandler>
{
    [Test]
    public async Task ShouldRequeueDownloadTasks_WhenRestartingValidId()
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
            .Returns(
                async (DownloadTaskKey key, DownloadStatus status, CancellationToken _) =>
                    await IDbContext.SetDownloadStatus(key, status)
            );

        await SetupDatabase(
            72153,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        var movieTask = downloadTasks.First();
        var childKeys = await IDbContext.GetDownloadableChildTaskKeys(movieTask.ToKey(), CancellationToken);

        childKeys.Count.ShouldBeGreaterThan(0);

        var sourceMedia = await IDbContext
            .PlexMovieData.Select(x => new
            {
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PlexMovie!.PlexApiRatingKey,
            })
            .FirstAsync(CancellationToken);

        await IDbContext
            .DownloadTaskMovieFile.Where(x => x.ParentId == movieTask.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.PlexApiMediaId, sourceMedia.PlexApiMediaId)
                        .SetProperty(x => x.PlexApiPartId, sourceMedia.PlexApiPartId)
                        .SetProperty(x => x.PlexApiRatingKey, sourceMedia.PlexApiRatingKey),
                CancellationToken
            );

        foreach (var childKey in childKeys)
        {
            await IDbContext
                .DownloadTaskMovieFile.Where(x => x.Id == childKey.Id)
                .ExecuteUpdateAsync(
                    p => p.SetProperty(x => x.DownloadStatus, DownloadStatus.Stopped),
                    CancellationToken
                );
        }

        Mock.SetupCommand(It.IsAny<StopDownloadTaskCommand>).ReturnsAsync(Result.Ok());
        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new RestartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);

        foreach (var childKey in childKeys)
        {
            var task = await IDbContext.GetDownloadTaskFileAsync(childKey, CancellationToken);
            task.ShouldNotBeNull();
            task.DownloadStatus.ShouldBe(DownloadStatus.Queued);

            Mock.VerifyEventPublished(() => new StopDownloadTaskCommand(childKey.Id), Times.Once());
            Mock.Mock<IDownloadTaskUpdateDispatcher>()
                .Verify(
                    x =>
                        x.OnStatusChangedAsync(
                            It.Is<DownloadTaskKey>(k => k == childKey),
                            DownloadStatus.Restarting,
                            It.IsAny<CancellationToken>()
                        ),
                    Times.Once()
                );
        }

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k.Id == movieTask.Id),
                        DownloadStatus.Restarting,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );

        Mock.Mock<IEventPublisher>()
            .Verify(
                x =>
                    x.PublishAsync(
                        It.Is<CheckDownloadQueueEvent>(e =>
                            e.PlexServerIds.SequenceEqual(new[] { movieTask.PlexServerId })
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldSetSourceUnavailable_WhenSourceCannotBeResolvedOnRestart()
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
            .Returns(
                async (DownloadTaskKey key, DownloadStatus status, CancellationToken _) =>
                {
                    await IDbContext.SetDownloadStatus(key, status);
                }
            );

        await SetupDatabase(72154, config => config.MovieDownloadTasksCount = 1);

        await IDbContext.PlexMovieData.ExecuteDeleteAsync(CancellationToken);

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        var movieTask = downloadTasks.First();
        var childKeys = await IDbContext.GetDownloadableChildTaskKeys(movieTask.ToKey(), CancellationToken);

        childKeys.Count.ShouldBeGreaterThan(0);

        Mock.SetupCommand(It.IsAny<StopDownloadTaskCommand>).ReturnsAsync(Result.Ok());
        Mock.PublishEvent(It.IsAny<CheckDownloadQueueEvent>).Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new RestartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);

        foreach (var childKey in childKeys)
        {
            var task = await IDbContext.GetDownloadTaskFileAsync(childKey, CancellationToken);
            task.ShouldNotBeNull();
            task.DownloadStatus.ShouldBe(DownloadStatus.SourceUnavailable);

            Mock.Mock<IDownloadTaskUpdateDispatcher>()
                .Verify(
                    x =>
                        x.OnStatusChangedAsync(
                            It.Is<DownloadTaskKey>(k => k == childKey),
                            DownloadStatus.SourceUnavailable,
                            It.IsAny<CancellationToken>()
                        ),
                    Times.Once()
                );

            Mock.Mock<IDownloadTaskUpdateDispatcher>()
                .Verify(
                    x =>
                        x.OnStatusChangedAsync(
                            It.Is<DownloadTaskKey>(k => k == childKey),
                            DownloadStatus.Queued,
                            It.IsAny<CancellationToken>()
                        ),
                    Times.Never()
                );
        }

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k.Id == movieTask.Id),
                        DownloadStatus.Restarting,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );

        Mock.Mock<IEventPublisher>()
            .Verify(
                x =>
                    x.PublishAsync(
                        It.Is<CheckDownloadQueueEvent>(e =>
                            e.PlexServerIds.SequenceEqual(new[] { movieTask.PlexServerId })
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldReturnNotFound_WhenDownloadTaskDoesNotExist()
    {
        // Arrange
        await SetupDatabase(72155, config => config.MovieDownloadTasksCount = 1);

        var missingId = Guid.NewGuid();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());

        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());

        // Act
        var result = await Sut.ExecuteAsync(new RestartDownloadTaskCommand(missingId), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(0);
        result.Errors.Select(x => x.Message).ShouldContain(x => x.Contains(nameof(DownloadTaskGeneric)));

        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldReturnStopFailure_AndNotPublishQueueEvent_WhenStoppingChildTaskFails()
    {
        // Arrange
        await SetupDatabase(
            72156,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        var movieTask = downloadTasks.First();
        var childKeys = await IDbContext.GetDownloadableChildTaskKeys(movieTask.ToKey(), CancellationToken);
        childKeys.Count.ShouldBeGreaterThan(0);

        var failedChild = childKeys.First();
        var stopFailure = Result.Fail("Stop failed for test");

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (DownloadTaskKey key, DownloadStatus status, CancellationToken _) =>
                {
                    await IDbContext.SetDownloadStatus(key, status);
                }
            );

        Mock.SetupCommand(() => new StopDownloadTaskCommand(failedChild.Id))
            .ReturnsAsync(stopFailure)
            .Verifiable(Times.Once());

        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());

        // Act
        var result = await Sut.ExecuteAsync(new RestartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBeGreaterThan(0);
        result.Errors.Select(x => x.Message).ShouldContain(x => x.Contains("Stop failed for test"));

        var failedChildAfter = await IDbContext.GetDownloadTaskFileAsync(failedChild, CancellationToken);
        failedChildAfter.ShouldNotBeNull();
        failedChildAfter.DownloadStatus.ShouldNotBe(DownloadStatus.Restarting);

        Mock.VerifyEventPublished(() => new StopDownloadTaskCommand(failedChild.Id), Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k == failedChild),
                        DownloadStatus.Restarting,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        Mock.Mock<IEventPublisher>().Verify();
    }

    [Test]
    public async Task ShouldSetRestartingForParent_WhenRestartingValidId()
    {
        // Arrange
        await SetupDatabase(
            72157,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var movieTask = (
            await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken)
        ).First();
        var childKeys = await IDbContext.GetDownloadableChildTaskKeys(movieTask.ToKey(), CancellationToken);
        childKeys.Count.ShouldBeGreaterThan(0);

        var sourceMedia = await IDbContext
            .PlexMovieData.Select(x => new
            {
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PlexMovie!.PlexApiRatingKey,
            })
            .FirstAsync(CancellationToken);

        await IDbContext
            .DownloadTaskMovieFile.Where(x => x.ParentId == movieTask.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.PlexApiMediaId, sourceMedia.PlexApiMediaId)
                        .SetProperty(x => x.PlexApiPartId, sourceMedia.PlexApiPartId)
                        .SetProperty(x => x.PlexApiRatingKey, sourceMedia.PlexApiRatingKey),
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
            .Returns(
                async (DownloadTaskKey key, DownloadStatus status, CancellationToken _) =>
                {
                    await IDbContext.SetDownloadStatus(key, status);
                }
            );

        Mock.SetupCommand<Result>(x => x is StopDownloadTaskCommand).ReturnsAsync(Result.Ok());
        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new RestartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);

        var parentAfter = await IDbContext.GetDownloadTaskAsync(movieTask.ToKey(), CancellationToken);
        parentAfter.ShouldNotBeNull();
        parentAfter.DownloadStatus.ShouldBe(DownloadStatus.Queued);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Verify(
                x =>
                    x.OnStatusChangedAsync(
                        It.Is<DownloadTaskKey>(k => k.Id == movieTask.Id),
                        DownloadStatus.Restarting,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldPublishQueueEventOnce_WhenRestartSucceeds()
    {
        // Arrange
        await SetupDatabase(
            72158,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var movieTask = (
            await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken)
        ).First();
        var childKeys = await IDbContext.GetDownloadableChildTaskKeys(movieTask.ToKey(), CancellationToken);
        childKeys.Count.ShouldBeGreaterThan(0);

        var sourceMedia = await IDbContext
            .PlexMovieData.Select(x => new
            {
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PlexMovie!.PlexApiRatingKey,
            })
            .FirstAsync(CancellationToken);

        await IDbContext
            .DownloadTaskMovieFile.Where(x => x.ParentId == movieTask.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.PlexApiMediaId, sourceMedia.PlexApiMediaId)
                        .SetProperty(x => x.PlexApiPartId, sourceMedia.PlexApiPartId)
                        .SetProperty(x => x.PlexApiRatingKey, sourceMedia.PlexApiRatingKey),
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
            .Returns(
                async (DownloadTaskKey key, DownloadStatus status, CancellationToken _) =>
                {
                    await IDbContext.SetDownloadStatus(key, status);
                }
            );

        Mock.SetupCommand<Result>(x => x is StopDownloadTaskCommand).ReturnsAsync(Result.Ok());
        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new RestartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);

        foreach (var childKey in childKeys)
        {
            var childAfter = await IDbContext.GetDownloadTaskFileAsync(childKey, CancellationToken);
            childAfter.ShouldNotBeNull();
            childAfter.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        }

        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldInvokeStopCommandForEveryChild_WhenRestartingValidId()
    {
        // Arrange
        await SetupDatabase(
            72159,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var movieTask = (
            await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken)
        ).First();
        var childKeys = await IDbContext.GetDownloadableChildTaskKeys(movieTask.ToKey(), CancellationToken);
        childKeys.Count.ShouldBeGreaterThan(0);

        var sourceMedia = await IDbContext
            .PlexMovieData.Select(x => new
            {
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PlexMovie!.PlexApiRatingKey,
            })
            .FirstAsync(CancellationToken);

        await IDbContext
            .DownloadTaskMovieFile.Where(x => x.ParentId == movieTask.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.PlexApiMediaId, sourceMedia.PlexApiMediaId)
                        .SetProperty(x => x.PlexApiPartId, sourceMedia.PlexApiPartId)
                        .SetProperty(x => x.PlexApiRatingKey, sourceMedia.PlexApiRatingKey),
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
            .Returns(
                async (DownloadTaskKey key, DownloadStatus status, CancellationToken _) =>
                {
                    await IDbContext.SetDownloadStatus(key, status);
                }
            );

        Mock.SetupCommand<Result>(x => x is StopDownloadTaskCommand).ReturnsAsync(Result.Ok());
        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new RestartDownloadTaskCommand(movieTask.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);

        foreach (var childKey in childKeys)
        {
            var childAfter = await IDbContext.GetDownloadTaskFileAsync(childKey, CancellationToken);
            childAfter.ShouldNotBeNull();
            childAfter.DownloadStatus.ShouldBe(DownloadStatus.Queued);
            Mock.VerifyEventPublished(() => new StopDownloadTaskCommand(childKey.Id), Times.Once());
        }
    }

    [Test]
    public async Task ShouldMapMovieFieldsStrictly_WhenRestartRefreshesMovieDownloadTask()
    {
        // Arrange
        await SetupDatabase(
            72160,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var parent = (await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken)).First();
        var childKey = (await IDbContext.GetDownloadableChildTaskKeys(parent.ToKey(), CancellationToken)).First();

        var sourceMedia = await IDbContext
            .PlexMovieData.Select(x => new
            {
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PlexMovie!.PlexApiRatingKey,
            })
            .FirstAsync(CancellationToken);

        await IDbContext
            .DownloadTaskMovieFile.Where(x => x.ParentId == parent.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.PlexApiMediaId, sourceMedia.PlexApiMediaId)
                        .SetProperty(x => x.PlexApiPartId, sourceMedia.PlexApiPartId)
                        .SetProperty(x => x.PlexApiRatingKey, sourceMedia.PlexApiRatingKey),
                CancellationToken
            );

        var before = await IDbContext.DownloadTaskMovieFile.FirstAsync(x => x.Id == childKey.Id, CancellationToken);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    It.IsAny<DownloadStatus>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (DownloadTaskKey key, DownloadStatus status, CancellationToken _) =>
                {
                    await IDbContext.SetDownloadStatus(key, status);
                }
            );

        Mock.SetupCommand<Result>(x => x is StopDownloadTaskCommand).ReturnsAsync(Result.Ok());
        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new RestartDownloadTaskCommand(parent.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);

        var after = await IDbContext.DownloadTaskMovieFile.FirstAsync(x => x.Id == childKey.Id, CancellationToken);
        after.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        after.Id.ShouldBe(before.Id);
        after.ParentId.ShouldBe(before.ParentId);
        after.PlexApiMediaId.ShouldBe(before.PlexApiMediaId);
        after.PlexApiPartId.ShouldBe(before.PlexApiPartId);
        after.HashId.ShouldBe(before.HashId);
        after.DestinationFolderPathId.ShouldBe(before.DestinationFolderPathId);
        after.DataReceived.ShouldBe(0);
        after.DataTotal.ShouldBe(before.DataTotal);
        after.DownloadSpeed.ShouldBe(0);
        after.FileTransferSpeed.ShouldBe(0);
        after.FileDataTransferred.ShouldBe(0);
        after.TimeRemaining.ShouldBe(0);
        after.DownloadClientType.ShouldBe(before.DownloadClientType);
        after.DirectoryMeta.ShouldNotBeNull();
        after.DirectoryMeta.DownloadRootPath.ShouldBe(before.DirectoryMeta.DownloadRootPath);
        after.DirectoryMeta.DestinationRootPath.ShouldBe(before.DirectoryMeta.DestinationRootPath);
        after.DirectoryMeta.KeepCompletedInDownloadFolder.ShouldBe(before.DirectoryMeta.KeepCompletedInDownloadFolder);
        after.DirectoryMeta.MovieFolder.ShouldNotBeNullOrWhiteSpace();
        after.FileName.ShouldNotBeNullOrWhiteSpace();
        after.FullTitle.ShouldContain(after.FileName);
    }

    [Test]
    public async Task ShouldMapEpisodeFieldsStrictly_WhenRestartRefreshesEpisodeDownloadTask()
    {
        // Arrange
        await SetupDatabase(
            72161,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 5;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
                config.TvShowDownloadTasksCount = 1;
            }
        );

        var parent = (await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken)).First();
        var childKey = (await IDbContext.GetDownloadableChildTaskKeys(parent.ToKey(), CancellationToken)).First();

        var sourceMedia = await IDbContext
            .PlexTvShowEpisodeData.Select(x => new
            {
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PlexTvShowEpisode!.PlexApiRatingKey,
            })
            .FirstOrDefaultAsync(CancellationToken);
        sourceMedia.ShouldNotBeNull();

        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == childKey.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.PlexApiMediaId, sourceMedia.PlexApiMediaId)
                        .SetProperty(x => x.PlexApiPartId, sourceMedia.PlexApiPartId)
                        .SetProperty(x => x.PlexApiRatingKey, sourceMedia.PlexApiRatingKey),
                CancellationToken
            );

        var before = await IDbContext.DownloadTaskTvShowEpisodeFile.FirstAsync(
            x => x.Id == childKey.Id,
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
            .Returns(
                async (DownloadTaskKey key, DownloadStatus status, CancellationToken _) =>
                {
                    await IDbContext.SetDownloadStatus(key, status);
                }
            );

        Mock.SetupCommand<Result>(x => x is StopDownloadTaskCommand).ReturnsAsync(Result.Ok());
        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Sut.ExecuteAsync(new RestartDownloadTaskCommand(parent.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);

        var after = await IDbContext.DownloadTaskTvShowEpisodeFile.FirstAsync(
            x => x.Id == childKey.Id,
            CancellationToken
        );
        after.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        after.Id.ShouldBe(before.Id);
        after.ParentId.ShouldBe(before.ParentId);
        after.PlexApiMediaId.ShouldBe(before.PlexApiMediaId);
        after.PlexApiPartId.ShouldBe(before.PlexApiPartId);
        after.HashId.ShouldBe(before.HashId);
        after.DestinationFolderPathId.ShouldBe(before.DestinationFolderPathId);
        after.DataReceived.ShouldBe(0);
        after.DataTotal.ShouldBe(before.DataTotal);
        after.DownloadSpeed.ShouldBe(0);
        after.FileTransferSpeed.ShouldBe(0);
        after.FileDataTransferred.ShouldBe(0);
        after.TimeRemaining.ShouldBe(0);
        after.DownloadClientType.ShouldBe(before.DownloadClientType);
        after.DirectoryMeta.ShouldNotBeNull();
        after.DirectoryMeta.DownloadRootPath.ShouldBe(before.DirectoryMeta.DownloadRootPath);
        after.DirectoryMeta.DestinationRootPath.ShouldBe(before.DirectoryMeta.DestinationRootPath);
        after.DirectoryMeta.KeepCompletedInDownloadFolder.ShouldBe(before.DirectoryMeta.KeepCompletedInDownloadFolder);
        after.DirectoryMeta.TvShowFolder.ShouldNotBeNullOrWhiteSpace();
        after.DirectoryMeta.SeasonFolder.ShouldNotBeNullOrWhiteSpace();
        after.FileName.ShouldNotBeNullOrWhiteSpace();
        after.FullTitle.ShouldContain(after.FileName);
    }
}
