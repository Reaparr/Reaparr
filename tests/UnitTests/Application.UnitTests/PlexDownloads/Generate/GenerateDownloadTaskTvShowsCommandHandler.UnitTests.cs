namespace Reaparr.Application.UnitTests;

public class GenerateDownloadTaskTvShowsCommandHandlerUnitTests
    : BaseUnitTest<GenerateDownloadTaskTvShowsCommandHandler>
{
    [Test]
    public void GenerateDownloadTaskTvShowsCommandValidator_ShouldRejectNullRequest()
    {
        // Arrange
        var validator = new GenerateDownloadTaskTvShowsCommandValidator();
        var command = new GenerateDownloadTaskTvShowsCommand((CreateDownloadTasksRequest)null!);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(x => x.PropertyName == nameof(GenerateDownloadTaskTvShowsCommand.Request));
    }

    [Test]
    public async Task ShouldHaveInsertedValidDownloadTaskTvShowsInDatabase_WhenGivenValidPlexTvShows()
    {
        // Arrange
        await SetupDatabase(5954, config =>
        {
            config.PlexServerCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.TvShowCount = 5;
            config.TvShowSeasonCount = 3;
            config.TvShowEpisodeCount = 3;
        });
        var plexTvShows = await IDbContext.PlexTvShows.IncludeAll().ToListAsync(CancellationToken);
        plexTvShows.ShouldNotBeEmpty();

        var firstTvShow = plexTvShows.First();
        var plexServerId = firstTvShow.PlexServerId;
        var plexLibraryId = firstTvShow.PlexLibraryId;

        var tvShows = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.TvShow,
                SelectionKind = null,
                MediaIds = plexTvShows.Select(x => x.Id).ToList(),
                PlexServerId = plexServerId,
                PlexLibraryId = plexLibraryId,
                Qualities = [],
            },
        };

        Mock.SetupCommand(It.IsAny<GenerateDownloadTaskTvShowSeasonsCommand>)
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport()));

        // Act
        var command = new GenerateDownloadTaskTvShowsCommand(tvShows);
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var downloadTaskTvShows = await IDbContext.GetAllDownloadTasksByServerAsync(
            cancellationToken: CancellationToken
        );

        downloadTaskTvShows.Count.ShouldBe(5);

        foreach (var downloadTaskTvShow in downloadTaskTvShows)
        {
            downloadTaskTvShow.Id.ShouldNotBe(Guid.Empty);
            downloadTaskTvShow.RatingKey.ShouldBeGreaterThan(0);
            downloadTaskTvShow.Title.ShouldNotBeEmpty();
            downloadTaskTvShow.FullTitle.ShouldNotBeEmpty();
            downloadTaskTvShow.DownloadStatus.ShouldBe(DownloadStatus.Queued);

            downloadTaskTvShow.PlexServerId.ShouldBe(plexServerId);
            downloadTaskTvShow.PlexLibraryId.ShouldBe(plexLibraryId);

            downloadTaskTvShow.DownloadTaskType.ShouldBe(DownloadTaskType.TvShow);
            downloadTaskTvShow.MediaType.ShouldBe(PlexMediaType.TvShow);
            downloadTaskTvShow.Children.ShouldBeEmpty();
        }

        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GenerateDownloadTaskTvShowSeasonsCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldForwardCustomDestinationFolderPath_WhenCreatingSeasonsCommand()
    {
        // Arrange
        await SetupDatabase(5955, config =>
        {
            config.PlexServerCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 1;
        });
        var plexTvShows = await IDbContext.PlexTvShows.IncludeAll().ToListAsync(CancellationToken);
        plexTvShows.ShouldNotBeEmpty();

        var firstTvShow = plexTvShows.First();

        var tvShows = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.TvShow,
                SelectionKind = null,
                MediaIds = plexTvShows.Select(x => x.Id).ToList(),
                PlexServerId = firstTvShow.PlexServerId,
                PlexLibraryId = firstTvShow.PlexLibraryId,
                Qualities = [],
            },
        };

        const string customPath = "/custom/destination/path";
        Mock.SetupCommand(It.IsAny<GenerateDownloadTaskTvShowSeasonsCommand>)
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport()));

        // Act
        var request = new CreateDownloadTasksRequest(tvShows, null, customPath);
        var command = new GenerateDownloadTaskTvShowsCommand(request);
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<GenerateDownloadTaskTvShowSeasonsCommand>(cmd =>
                            cmd.Request.CustomDestinationFolderPath == customPath
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldHaveFailedResult_WhenCreatingSeasonDownloadTasksFails()
    {
        // Arrange
        await SetupDatabase(5956, config =>
        {
            config.PlexServerCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 1;
        });
        var plexTvShows = await IDbContext.PlexTvShows.IncludeAll().ToListAsync(CancellationToken);
        plexTvShows.ShouldNotBeEmpty();

        var firstTvShow = plexTvShows.First();

        var tvShows = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.TvShow,
                SelectionKind = null,
                MediaIds = plexTvShows.Select(x => x.Id).ToList(),
                PlexServerId = firstTvShow.PlexServerId,
                PlexLibraryId = firstTvShow.PlexLibraryId,
                Qualities = [],
            },
        };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GenerateDownloadTaskTvShowSeasonsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Season task generation failed"));

        // Act
        var command = new GenerateDownloadTaskTvShowsCommand(tvShows);
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message.Contains("Season task generation failed"));

        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GenerateDownloadTaskTvShowSeasonsCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }
}
