namespace Reaparr.Application.UnitTests;

public class DeterminePlexDownloadClientCommandUnitTests : BaseCommandUnitTest<DeterminePlexDownloadClientCommand>
{
    [Test]
    public async Task ShouldReturnDirect_WhenStreamDownloaderIsDisabled()
    {
        // Arrange
        await SetupDatabase(
            91101,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = IDbContext.DownloadTaskMovieFile.First();

        Mock.Mock<IServerSettingsModule>()
            .Setup(x => x.GetAllowStreamDownloader(It.IsAny<string>()))
            .Returns(false)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexDownloadClientType>(
            new DeterminePlexDownloadClientCommand(
                downloadTask.PlexServerId,
                downloadTask.ToKey(),
                $"/library/metadata/{downloadTask.PlexApiRatingKey}"
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(PlexDownloadClientType.Direct);
        Mock.Mock<IServerSettingsModule>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GetDashTranscodeDecisionCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldReturnDirect_WhenTranscodeUrlSuggestsDirect()
    {
        // Arrange
        await SetupDatabase(
            91102,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = IDbContext.DownloadTaskMovieFile.First();
        var expectedPath = $"/library/metadata/{downloadTask.PlexApiRatingKey}";

        Mock.Mock<IServerSettingsModule>()
            .Setup(x => x.GetAllowStreamDownloader(It.IsAny<string>()))
            .Returns(true)
            .Verifiable(Times.Once());

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetDashTranscodeDecisionCommand>(c =>
                        c.PlexServerId == downloadTask.PlexServerId && c.DecisionRequest.MetaDataPath == expectedPath
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result.Ok(
                    new GetDashTranscodeDecisionResult
                    {
                        GeneralDecisionCode = "1000",
                        GeneralDecisionText = "Direct play",
                        TranscodeDecisionCode = "1000",
                        TranscodeDecisionText = "Direct play",
                        VideoDecision = "copy",
                        AudioDecision = "copy",
                        PartDecision = "directplay",
                        TranscodedQuality = VideoQuality.FullHD,
                        SuggestedClientType = PlexDownloadClientType.Direct,
                    }
                )
            )
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexDownloadClientType>(
            new DeterminePlexDownloadClientCommand(downloadTask.PlexServerId, downloadTask.ToKey(), expectedPath)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(PlexDownloadClientType.Direct);
        Mock.Mock<IServerSettingsModule>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldReturnDash_WhenTranscodeUrlDoesNotSuggestDirect()
    {
        // Arrange
        await SetupDatabase(
            91103,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = IDbContext.DownloadTaskMovieFile.First();
        var expectedPath = $"/library/metadata/{downloadTask.PlexApiRatingKey}";

        Mock.Mock<IServerSettingsModule>()
            .Setup(x => x.GetAllowStreamDownloader(It.IsAny<string>()))
            .Returns(true)
            .Verifiable(Times.Once());

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetDashTranscodeDecisionCommand>(c =>
                        c.PlexServerId == downloadTask.PlexServerId && c.DecisionRequest.MetaDataPath == expectedPath
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result.Ok(
                    new GetDashTranscodeDecisionResult
                    {
                        GeneralDecisionCode = "1000",
                        GeneralDecisionText = "Transcode",
                        TranscodeDecisionCode = "1001",
                        TranscodeDecisionText = "Transcode required",
                        VideoDecision = "transcode",
                        AudioDecision = "copy",
                        PartDecision = "transcode",
                        TranscodedQuality = VideoQuality.HD,
                        SuggestedClientType = PlexDownloadClientType.Dash,
                    }
                )
            )
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexDownloadClientType>(
            new DeterminePlexDownloadClientCommand(downloadTask.PlexServerId, downloadTask.ToKey(), expectedPath)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(PlexDownloadClientType.Dash);
        Mock.Mock<IServerSettingsModule>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldReturnFailure_WhenTranscodeDecisionFails()
    {
        // Arrange
        await SetupDatabase(
            91104,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = IDbContext.DownloadTaskMovieFile.First();
        var expectedPath = $"/library/metadata/{downloadTask.PlexApiRatingKey}";

        Mock.Mock<IServerSettingsModule>()
            .Setup(x => x.GetAllowStreamDownloader(It.IsAny<string>()))
            .Returns(true)
            .Verifiable(Times.Once());

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetDashTranscodeDecisionCommand>(c =>
                        c.PlexServerId == downloadTask.PlexServerId && c.DecisionRequest.MetaDataPath == expectedPath
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Fail<GetDashTranscodeDecisionResult>("decision failed"))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexDownloadClientType>(
            new DeterminePlexDownloadClientCommand(downloadTask.PlexServerId, downloadTask.ToKey(), expectedPath)
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(x => x.Message).ShouldBe(["decision failed"]);
        Mock.Mock<IServerSettingsModule>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
    }
}
