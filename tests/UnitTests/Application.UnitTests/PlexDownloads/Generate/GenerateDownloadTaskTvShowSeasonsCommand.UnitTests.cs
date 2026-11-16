namespace Reaparr.Application.UnitTests;

public class GenerateDownloadTaskTvShowSeasonsCommandUnitTests
    : BaseCommandUnitTest<GenerateDownloadTaskTvShowSeasonsCommand>
{
    [Test]
    public void GenerateDownloadTaskTvShowSeasonsCommandValidator_ShouldRejectNullRequest()
    {
        // Arrange
        var command = new GenerateDownloadTaskTvShowSeasonsCommand(null!);
        var validator = new GenerateDownloadTaskTvShowSeasonsCommandValidator();

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(x => x.PropertyName == nameof(GenerateDownloadTaskTvShowSeasonsCommand.Request));
    }

    [Test]
    public async Task ShouldHaveFailedResult_WhenCreatingEpisodeDownloadTasksFails()
    {
        // Arrange
        await SetupDatabase(5957, config =>
        {
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 1;
        });

        var plexSeasons = await IDbContext.PlexTvShowSeason.IncludeAll().ToListAsync(CancellationToken);

        var seasons = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Season,
                SelectionKind = null,
                MediaIds = plexSeasons.Select(x => x.Id).ToList(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GenerateDownloadTaskTvShowEpisodesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Episode task generation failed"));

        // Act
        var command = new GenerateDownloadTaskTvShowSeasonsCommand(new CreateDownloadTasksRequest(seasons));
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message.Contains("Episode task generation failed"));

        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GenerateDownloadTaskTvShowEpisodesCommand>(), It.IsAny<CancellationToken>()),
                Times.Once()
            );
    }
}
