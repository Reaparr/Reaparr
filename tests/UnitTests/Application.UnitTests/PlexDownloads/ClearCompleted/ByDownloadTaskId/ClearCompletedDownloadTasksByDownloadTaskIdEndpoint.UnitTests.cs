namespace Reaparr.Application.UnitTests;

public class ClearCompletedDownloadTasksByDownloadTaskIdEndpointUnitTests
    : BaseEndpointUnitTest<ClearCompletedDownloadTasksByDownloadTaskIdEndpoint, List<Guid>, ResultDTO<CountResponseDTO>>
{
    [Test]
    public async Task ShouldRemoveOnlySpecifiedCompletedDownloadTasks_WhenCalledWithGuidList()
    {
        // Arrange
        await SetupDatabase(
            213132,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieDownloadTasksCount = 10;
            }
        );

        var dbContext = IDbContext;
        var downloadTasks = await dbContext
            .DownloadTaskMovie.AsTracking()
            .Include(x => x.Children)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

        downloadTasks.SetDownloadStatus(DownloadStatus.Completed);
        await dbContext.SaveChangesAsync(CancellationToken);
        var selectedTasks = downloadTasks.Take(5).ToList();
        var retainedTasks = downloadTasks.Skip(5).ToList();
        var expectedSelectedIds = selectedTasks.Select(x => x.Id).Order().ToArray();
        var expectedSelectedKeys = selectedTasks.Select(x => x.ToKey()).OrderBy(x => x.Id).ToArray();
        var expectedRetainedIds = retainedTasks.Select(x => x.Id).Order().ToArray();
        var expectedRetainedFileIds = retainedTasks
            .SelectMany(x => x.Children)
            .Select(x => x.Id)
            .Order()
            .ToArray();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DownloadStatus.Deleted,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(5));

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .Returns(
                (DeleteDownloadTasksByKeyCommand command, CancellationToken ct) =>
                    new DeleteDownloadTasksByKeyCommandHandler(
                        dbContext,
                        Mock.Mock<IDownloadTaskUpdateDispatcher>().Object
                    ).ExecuteAsync(command, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.IsAny<ClearCompletedDownloadTasksByDownloadTaskKeyCommand>(), It.IsAny<CancellationToken>())
            )
            .Returns(
                (ClearCompletedDownloadTasksByDownloadTaskKeyCommand command, CancellationToken ct) =>
                    new ClearCompletedDownloadTasksByDownloadTaskKeyCommandHandler(
                        dbContext,
                        Mock.Mock<ICommandExecutor>().Object
                    ).ExecuteAsync(command, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.CompletedDownloadsCleared(
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Order().SequenceEqual(expectedSelectedIds)),
                    It.Is<IReadOnlyCollection<DownloadTaskKey>>(keys =>
                        keys.OrderBy(downloadTaskKey => downloadTaskKey.Id).SequenceEqual(expectedSelectedKeys)
                    ),
                    It.Is<Result<int>>(r => r.IsSuccess && r.Value == 5)
                )
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(selectedTasks.Select(x => x.Id).ToList());
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldNotBeNull().Count.ShouldBe(5);
        (
            await dbContext.DownloadTaskMovie.Select(x => x.Id).Order().ToArrayAsync(CancellationToken)
        ).ShouldBe(expectedRetainedIds);
        (
            await dbContext.DownloadTaskMovieFile.Select(x => x.Id).Order().ToArrayAsync(CancellationToken)
        ).ShouldBe(expectedRetainedFileIds);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.IsAny<ClearCompletedDownloadTasksByDownloadTaskKeyCommand>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldNotRemoveDownloadTasks_WhenTasksAreNotCompleted()
    {
        // Arrange
        await SetupDatabase(
            88341,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieDownloadTasksCount = 5;
            }
        );

        var dbContext = IDbContext;
        var downloadTasks = await dbContext
            .DownloadTaskMovie.AsTracking()
            .Include(x => x.Children)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

        downloadTasks.SetDownloadStatus(DownloadStatus.Downloading);
        await dbContext.SaveChangesAsync(CancellationToken);
        var expectedIds = downloadTasks.Select(x => x.Id).Order().ToArray();
        var expectedKeys = downloadTasks.Select(x => x.ToKey()).OrderBy(x => x.Id).ToArray();
        var expectedFileIds = downloadTasks
            .SelectMany(x => x.Children)
            .Select(x => x.Id)
            .Order()
            .ToArray();

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never());

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.IsAny<ClearCompletedDownloadTasksByDownloadTaskKeyCommand>(), It.IsAny<CancellationToken>())
            )
            .Returns(
                (ClearCompletedDownloadTasksByDownloadTaskKeyCommand command, CancellationToken ct) =>
                    new ClearCompletedDownloadTasksByDownloadTaskKeyCommandHandler(
                        dbContext,
                        Mock.Mock<ICommandExecutor>().Object
                    ).ExecuteAsync(command, ct)
            )
            .Verifiable(Times.Once());

        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.CompletedDownloadsCleared(
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Order().SequenceEqual(expectedIds)),
                    It.Is<IReadOnlyCollection<DownloadTaskKey>>(keys =>
                        keys.OrderBy(downloadTaskKey => downloadTaskKey.Id).SequenceEqual(expectedKeys)
                    ),
                    It.Is<Result<int>>(r => r.IsSuccess && r.Value == 0)
                )
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(downloadTasks.Select(x => x.Id).ToList());
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldNotBeNull().Count.ShouldBe(0);
        (
            await dbContext.DownloadTaskMovie.Select(x => x.Id).Order().ToArrayAsync(CancellationToken)
        ).ShouldBe(expectedIds);
        (
            await dbContext.DownloadTaskMovieFile.Select(x => x.Id).Order().ToArrayAsync(CancellationToken)
        ).ShouldBe(expectedFileIds);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.IsAny<ClearCompletedDownloadTasksByDownloadTaskKeyCommand>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldRemoveOrphanedTvShowParents_WhenLastCompletedEpisodeFileIsClearedById()
    {
        // Arrange
        await SetupDatabase(
            66812,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
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
        await dbContext.SaveChangesAsync(CancellationToken);

        var episodeFileId = tvShowDownloadTasks
            .SelectMany(x => x.Children)
            .SelectMany(x => x.Children)
            .SelectMany(x => x.Children)
            .Select(x => x.Id)
            .Single();

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.IsAny<DownloadTaskKey>(),
                    DownloadStatus.Deleted,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(4));

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .Returns(
                (DeleteDownloadTasksByKeyCommand command, CancellationToken ct) =>
                    new DeleteDownloadTasksByKeyCommandHandler(
                        dbContext,
                        Mock.Mock<IDownloadTaskUpdateDispatcher>().Object
                    ).ExecuteAsync(command, ct)
            );
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.IsAny<ClearCompletedDownloadTasksByDownloadTaskKeyCommand>(), It.IsAny<CancellationToken>())
            )
            .Returns(
                (ClearCompletedDownloadTasksByDownloadTaskKeyCommand command, CancellationToken ct) =>
                    new ClearCompletedDownloadTasksByDownloadTaskKeyCommandHandler(
                        dbContext,
                        Mock.Mock<ICommandExecutor>().Object
                    ).ExecuteAsync(command, ct)
            );

        // Act
        var endpointResult = await TestEndpointHandleAsync([episodeFileId]);
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.IsAny<ClearCompletedDownloadTasksByDownloadTaskKeyCommand>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }
}
