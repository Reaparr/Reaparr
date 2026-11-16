namespace Reaparr.Application.UnitTests;

public class DeleteDownloadTaskEndpointUnitTests
    : BaseEndpointUnitTest<DeleteDownloadTaskEndpoint, DeleteDownloadTaskEndpointRequest, BaseResultDTO>
{
    [Test]
    public async Task ShouldDispatchDeleteCommand_WhenDownloadTaskIdIsGiven()
    {
        // Arrange
        await SetupDatabase(45210, config =>
        {
            config.PlexServerCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });

        var dbContext = IDbContext;
        var episodeFileId = await dbContext
            .DownloadTaskTvShowEpisodeFile.Select(x => x.Id)
            .SingleAsync(CancellationToken);

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
            .Setup(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
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
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.DownloadDeletionProcessed(
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { episodeFileId })),
                    It.Is<IReadOnlyCollection<DownloadTaskKey>>(keys => keys.Count > 0),
                    It.Is<ResultBase>(r => r.IsSuccess)
                )
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new DeleteDownloadTaskEndpointRequest { DownloadTaskIds = [episodeFileId] }
        );
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StopDownloadTaskCommand>(cmd => cmd.DownloadTaskGuid == episodeFileId),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTasksByKeyCommand>(cmd => cmd.Keys.Any(k => k.Id == episodeFileId)),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        (
            await dbContext.DownloadTaskTvShowEpisodeFile.AnyAsync(x => x.Id == episodeFileId, CancellationToken)
        ).ShouldBeFalse();
        (await dbContext.DownloadTaskTvShowEpisode.AnyAsync(CancellationToken)).ShouldBeFalse();
        (await dbContext.DownloadTaskTvShowSeason.AnyAsync(CancellationToken)).ShouldBeFalse();
        (await dbContext.DownloadTaskTvShow.AnyAsync(CancellationToken)).ShouldBeFalse();
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldStopDownloadingAndDispatchDelete_WhenTaskIsActivelyDownloading()
    {
        // Arrange
        await SetupDatabase(45211, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });

        var dbContext = IDbContext;
        var movieId = await dbContext.DownloadTaskMovie.Select(x => x.Id).SingleAsync(CancellationToken);

        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x =>
                x.OnStatusChangedAsync(
                    It.Is<DownloadTaskKey>(k => k.Id == movieId),
                    DownloadStatus.Deleted,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .Returns(
                (DeleteDownloadTasksByKeyCommand command, CancellationToken ct) =>
                    new DeleteDownloadTasksByKeyCommandHandler(
                        dbContext,
                        Mock.Mock<IDownloadTaskUpdateDispatcher>().Object
                    ).ExecuteAsync(command, ct)
            );

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new DeleteDownloadTaskEndpointRequest { DownloadTaskIds = [movieId] }
        );
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StopDownloadTaskCommand>(cmd => cmd.DownloadTaskGuid == movieId),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTasksByKeyCommand>(cmd => cmd.Keys.Any(k => k.Id == movieId)),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
        (await dbContext.DownloadTaskMovie.AnyAsync(x => x.Id == movieId, CancellationToken)).ShouldBeFalse();
        (await dbContext.DownloadTaskMovieFile.AnyAsync(CancellationToken)).ShouldBeFalse();
    }

    [Test]
    public async Task ShouldReturnSuccessWithoutDispatchingDelete_WhenResolvedKeysAreMissing()
    {
        // Arrange
        await SetupDatabase(45212);

        var dbContext = IDbContext;
        var movieCountBefore = await dbContext.DownloadTaskMovie.CountAsync(CancellationToken);
        var movieFileCountBefore = await dbContext.DownloadTaskMovieFile.CountAsync(CancellationToken);
        var tvShowCountBefore = await dbContext.DownloadTaskTvShow.CountAsync(CancellationToken);
        var tvShowEpisodeFileCountBefore = await dbContext.DownloadTaskTvShowEpisodeFile.CountAsync(CancellationToken);

        var missingId = Guid.NewGuid();
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.DownloadDeletionProcessed(
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { missingId })),
                    It.Is<IReadOnlyCollection<DownloadTaskKey>>(keys => keys.Count == 0),
                    It.Is<ResultBase>(r => r.IsSuccess)
                )
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            new DeleteDownloadTaskEndpointRequest { DownloadTaskIds = [missingId] }
        );
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
        (await dbContext.DownloadTaskMovie.CountAsync(CancellationToken)).ShouldBe(movieCountBefore);
        (await dbContext.DownloadTaskMovieFile.CountAsync(CancellationToken)).ShouldBe(movieFileCountBefore);
        (await dbContext.DownloadTaskTvShow.CountAsync(CancellationToken)).ShouldBe(tvShowCountBefore);
        (await dbContext.DownloadTaskTvShowEpisodeFile.CountAsync(CancellationToken)).ShouldBe(
            tvShowEpisodeFileCountBefore
        );
        Mock.Mock<ITelemetry>().Verify();
    }
}
