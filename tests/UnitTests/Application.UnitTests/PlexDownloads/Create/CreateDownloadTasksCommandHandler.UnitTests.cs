namespace Reaparr.Application.UnitTests;

public class CreateDownloadTasksCommandHandlerUnitTests : BaseCommandUnitTest<CreateDownloadTasksCommand>
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldGenerateOnlySelectedTypes_WhenMovieAndTvSelectionsIncludeOptionalDescendants(
        bool includeDescendants
    )
    {
        // Arrange
        var types = includeDescendants
            ? new[] { PlexMediaType.Movie, PlexMediaType.TvShow, PlexMediaType.Season, PlexMediaType.Episode }
            : [PlexMediaType.Movie, PlexMediaType.TvShow];
        var command = new CreateDownloadTasksCommand(
            types
                .Select(type => new DownloadMediaDTO
                {
                    Type = type,
                    PlexServerId = type == PlexMediaType.Movie ? 1 : 2,
                    PlexLibraryId = type == PlexMediaType.Movie ? 11 : 22,
                    MediaIds = [101, 102],
                    Qualities = [],
                })
                .ToList()
        );
        var expected = new DownloadTaskCreationReport
        {
            Movies = 3,
            TvShows = 2,
            Seasons = includeDescendants ? 4 : 0,
            Episodes = includeDescendants ? 10 : 0,
        };

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskMoviesCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport { Movies = 3 }))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskTvShowsCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport { TvShows = 2 }))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskTvShowSeasonsCommand>(c => c.Request == command.Request),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport { Seasons = 4 }))
            .Verifiable(includeDescendants ? Times.Once() : Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskTvShowEpisodesCommand>(c => c.Request == command.Request),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport { Episodes = 10 }))
            .Verifiable(includeDescendants ? Times.Once() : Times.Never());
        Mock.Mock<IEventPublisher>()
            .Setup(x =>
                x.PublishAsync(
                    It.Is<CheckDownloadQueueEvent>(e => e.PlexServerIds.SequenceEqual(new[] { 1, 2 })),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(values =>
                        values.SequenceEqual(new[] { RefreshDataType.DownloadTasks })
                    )
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(expected);
        result.Value.Total.ShouldBe(includeDescendants ? 19 : 5);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldRejectMissingSelections_WhenRequestIsNullOrSelectionListIsEmpty(bool nullRequest)
    {
        // Arrange
        var command = nullRequest
            ? new CreateDownloadTasksCommand((CreateDownloadTasksRequest)null!)
            : new CreateDownloadTasksCommand([]);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ICommand<Result<DownloadTaskCreationReport>>>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()), Times.Never());
    }

    [Test]
    [Arguments(PlexMediaType.Music, false)]
    [Arguments(PlexMediaType.Music, true)]
    [Arguments(PlexMediaType.Album, false)]
    [Arguments(PlexMediaType.Album, true)]
    [Arguments(PlexMediaType.Track, false)]
    [Arguments(PlexMediaType.Track, true)]
    [Arguments(PlexMediaType.PhotoAlbum, false)]
    [Arguments(PlexMediaType.PhotoAlbum, true)]
    [Arguments(PlexMediaType.Photos, false)]
    [Arguments(PlexMediaType.Photos, true)]
    [Arguments(PlexMediaType.OtherVideos, false)]
    [Arguments(PlexMediaType.OtherVideos, true)]
    public async Task ShouldRejectUnsupportedSelectionsBeforeGeneration_WhenGivenAloneOrMixedWithMovie(
        PlexMediaType type,
        bool mixedWithMovie
    )
    {
        // Arrange
        var selections = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = type,
                PlexServerId = 1,
                PlexLibraryId = 11,
                MediaIds = [101],
                Qualities = [],
            },
        };
        if (mixedWithMovie)
            selections.Insert(
                0,
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.Movie,
                    PlexServerId = 1,
                    PlexLibraryId = 11,
                    MediaIds = [102],
                    Qualities = [],
                }
            );
        var command = new CreateDownloadTasksCommand(selections);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ICommand<Result<DownloadTaskCreationReport>>>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()), Times.Never());
    }

    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.TvShow)]
    [Arguments(PlexMediaType.Season)]
    [Arguments(PlexMediaType.Episode)]
    public async Task ShouldStopWithoutQueueNotification_WhenSelectedGeneratorFails(PlexMediaType failedType)
    {
        // Arrange
        var command = new CreateDownloadTasksCommand(
            new[] { PlexMediaType.Movie, PlexMediaType.TvShow, PlexMediaType.Season, PlexMediaType.Episode }
                .Select(type => new DownloadMediaDTO
                {
                    Type = type,
                    PlexServerId = 1,
                    PlexLibraryId = 11,
                    MediaIds = [101],
                    Qualities = [],
                })
                .ToList()
        );
        var error = new Error("Generation failure");

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskMoviesCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .ReturnsAsync(
                failedType == PlexMediaType.Movie
                    ? Result.Fail<DownloadTaskCreationReport>(error)
                    : Result.Ok(new DownloadTaskCreationReport { Movies = 3 })
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskTvShowsCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .ReturnsAsync(
                failedType == PlexMediaType.TvShow
                    ? Result.Fail<DownloadTaskCreationReport>(error)
                    : Result.Ok(new DownloadTaskCreationReport { TvShows = 2 })
            )
            .Verifiable(failedType == PlexMediaType.Movie ? Times.Never() : Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskTvShowSeasonsCommand>(c => c.Request == command.Request),
                    CancellationToken
                )
            )
            .ReturnsAsync(
                failedType == PlexMediaType.Season
                    ? Result.Fail<DownloadTaskCreationReport>(error)
                    : Result.Ok(new DownloadTaskCreationReport { Seasons = 4 })
            )
            .Verifiable(failedType is PlexMediaType.Movie or PlexMediaType.TvShow ? Times.Never() : Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskTvShowEpisodesCommand>(c => c.Request == command.Request),
                    CancellationToken
                )
            )
            .ReturnsAsync(
                failedType == PlexMediaType.Episode
                    ? Result.Fail<DownloadTaskCreationReport>(error)
                    : Result.Ok(new DownloadTaskCreationReport { Episodes = 10 })
            )
            .Verifiable(failedType == PlexMediaType.Episode ? Times.Once() : Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ShouldBeSameAs(error);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()), Times.Never());
    }
}
