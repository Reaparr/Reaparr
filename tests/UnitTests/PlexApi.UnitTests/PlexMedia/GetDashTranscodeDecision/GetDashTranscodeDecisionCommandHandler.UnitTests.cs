using System.Net;
using LukeHagar.PlexAPI.SDK;
using LukeHagar.PlexAPI.SDK.Models.Components;
using LukeHagar.PlexAPI.SDK.Models.Requests;
using Reaparr.PlexApi.Contracts;

namespace Reaparr.PlexApi.UnitTests;

public class GetDashTranscodeDecisionCommandHandlerUnitTests : BaseUnitTest<GetDashTranscodeDecisionCommandHandler>
{
    private static GetDashTranscodeDecisionCommand CreateCommand(int plexServerId) =>
        new(plexServerId, new TranscodeDecisionRequest("/library/metadata/56828"));

    private void SetupFactory(Mock<IPlexAPI> plexApiMock)
    {
        Mock.Mock<IPlexApiClientFactory>()
            .Setup(x =>
                x.CreateClient(It.IsAny<string>(), It.IsAny<PlexApiClientOptions>(), It.IsAny<CancellationToken>())
            )
            .Returns<string, PlexApiClientOptions, CancellationToken>((_, _, _) => plexApiMock.Object)
            .Verifiable(Times.Once());
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenPlexServerHasNoToken()
    {
        // Arrange
        await SetupDatabase(4101, config => config.PlexServerCount = 1);
        var dbContext = IDbContext;

        var plexServer = await dbContext.PlexServers.FirstOrDefaultAsync(CancellationToken);
        plexServer.ShouldNotBeNull();

        var command = CreateCommand(plexServer.Id);

        Mock.Mock<IPlexApiClientFactory>()
            .Setup(x =>
                x.CreateClient(It.IsAny<string>(), It.IsAny<PlexApiClientOptions>(), It.IsAny<CancellationToken>())
            )
            .Returns(new Mock<IPlexAPI>().Object)
            .Verifiable(Times.Never);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message.Contains("authenticationToken"));

        Mock.Mock<IPlexApiClientFactory>()
            .Verify(
                x =>
                    x.CreateClient(It.IsAny<string>(), It.IsAny<PlexApiClientOptions>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenDecisionResponseIsMissingMediaContainer()
    {
        // Arrange
        await SetupDatabase(4102, config =>
        {
            config.PlexServerCount = 1;
            config.PlexAccountCount = 1;
        });
        var dbContext = IDbContext;

        var plexServer = await dbContext.PlexServers.FirstOrDefaultAsync(CancellationToken);
        plexServer.ShouldNotBeNull();

        var decisionResponse = FakePlexApiData.GetMakeDecisionResponse(
            HttpStatusCode.OK,
            new Seed(4102),
            new MediaContainerWithDecision { MediaContainer = null }
        );

        var plexApiMock = new Mock<IPlexAPI>();
        plexApiMock
            .Setup(x => x.Transcoder.MakeDecisionAsync(It.IsAny<MakeDecisionRequest>()))
            .ReturnsAsync(decisionResponse)
            .Verifiable(Times.Once);

        SetupFactory(plexApiMock);

        var command = CreateCommand(plexServer.Id);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message.Contains("missing MediaContainer"));

        Mock.Mock<IPlexApiClientFactory>().Verify();
        plexApiMock.Verify();
    }

    [Test]
    public async Task ShouldReturnMappedDecisionSummary_WhenDecisionContainsVideoAndAudioStreams()
    {
        // Arrange
        await SetupDatabase(4103, config =>
        {
            config.PlexServerCount = 1;
            config.PlexAccountCount = 1;
        });
        var dbContext = IDbContext;

        var plexServer = await dbContext.PlexServers.FirstOrDefaultAsync(CancellationToken);
        plexServer.ShouldNotBeNull();

        var decisionResponse = FakePlexApiData.GetMakeDecisionResponse(HttpStatusCode.OK, new Seed(4103));

        var plexApiMock = new Mock<IPlexAPI>();
        plexApiMock
            .Setup(x => x.Transcoder.MakeDecisionAsync(It.IsAny<MakeDecisionRequest>()))
            .ReturnsAsync(decisionResponse)
            .Verifiable(Times.Once);

        SetupFactory(plexApiMock);

        var command = CreateCommand(plexServer.Id);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.GeneralDecisionCode.ShouldBe("1001");
        result.Value.GeneralDecisionText.ShouldBe("Direct play not available; Conversion OK.");
        result.Value.TranscodeDecisionCode.ShouldBe("1001");
        result.Value.TranscodeDecisionText.ShouldBe("Direct play not available; Conversion OK.");
        result.Value.VideoDecision.ShouldBe("Copy");
        result.Value.AudioDecision.ShouldBe("Copy");
        result.Value.TranscodedQuality.ShouldBe(VideoQuality.FullHD);

        Mock.Mock<IPlexApiClientFactory>().Verify();
        plexApiMock.Verify();
    }

    [Test]
    public async Task ShouldResolveQualityFromWidth_WhenStreamTitleDoesNotContainAResolution()
    {
        // Arrange
        await SetupDatabase(4104, config =>
        {
            config.PlexServerCount = 1;
            config.PlexAccountCount = 1;
        });
        var dbContext = IDbContext;

        var plexServer = await dbContext.PlexServers.FirstOrDefaultAsync(CancellationToken);
        plexServer.ShouldNotBeNull();

        var mediaContainer = FakePlexApiData.GetMakeDecisionMediaContainerWithWidthFallback();
        var decisionResponse = FakePlexApiData.GetMakeDecisionResponse(
            HttpStatusCode.OK,
            new Seed(4104),
            mediaContainer
        );

        var plexApiMock = new Mock<IPlexAPI>();
        plexApiMock
            .Setup(x => x.Transcoder.MakeDecisionAsync(It.IsAny<MakeDecisionRequest>()))
            .ReturnsAsync(decisionResponse)
            .Verifiable(Times.Once);

        SetupFactory(plexApiMock);

        var command = CreateCommand(plexServer.Id);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TranscodedQuality.ShouldBe(VideoQuality.UHD_4K);

        Mock.Mock<IPlexApiClientFactory>().Verify();
        plexApiMock.Verify();
    }

    public static IEnumerable<Func<(Action<MakeDecisionMediaContainerConfig>, VideoQuality)>> QualityCases()
    {
        yield return () =>
            (
                config =>
                {
                    config.MediaVideoResolution = "720p";
                    config.VideoStreams.Clear();
                    config.VideoStreams.Add(new MakeDecisionVideoStreamConfig { DisplayTitle = "Video Stream" });
                },
                VideoQuality.HD
            );

        yield return () =>
            (
                config =>
                {
                    config.MediaVideoResolution = "weird-resolution";
                    config.VideoStreams.Clear();
                    config.VideoStreams.Add(
                        new MakeDecisionVideoStreamConfig
                        {
                            DisplayTitle = "Video Stream",
                            ExtendedDisplayTitle = "Video Stream",
                            Width = 3840,
                            Height = 2160,
                        }
                    );
                },
                VideoQuality.UHD_4K
            );

        yield return () =>
            (
                config =>
                {
                    config.MediaVideoResolution = null;
                    config.MediaWidth = null;
                    config.MediaHeight = null;
                    config.VideoStreams.Clear();
                    config.VideoStreams.Add(
                        new MakeDecisionVideoStreamConfig
                        {
                            DisplayTitle = "Video Stream",
                            ExtendedDisplayTitle = "Video Stream",
                            Width = 1280,
                            Height = null,
                        }
                    );
                },
                VideoQuality.HD
            );

        yield return () =>
            (
                config =>
                {
                    config.MediaVideoResolution = null;
                    config.MediaWidth = null;
                    config.MediaHeight = null;
                    config.VideoStreams.Clear();
                    config.VideoStreams.Add(
                        new MakeDecisionVideoStreamConfig
                        {
                            DisplayTitle = "Video Stream",
                            ExtendedDisplayTitle = "Video Stream",
                            Width = null,
                            Height = 576,
                        }
                    );
                },
                VideoQuality.DVD
            );

        yield return () =>
            (
                config =>
                {
                    config.MediaVideoResolution = null;
                    config.MediaWidth = 640;
                    config.MediaHeight = null;
                    config.VideoStreams.Clear();
                    config.VideoStreams.Add(
                        new MakeDecisionVideoStreamConfig
                        {
                            DisplayTitle = "Video Stream",
                            ExtendedDisplayTitle = "Video Stream",
                            Width = null,
                            Height = null,
                        }
                    );
                },
                VideoQuality.nHD
            );
    }

    [Test]
    [MethodDataSource(nameof(QualityCases))]
    public async Task ShouldResolveExpectedQuality_ForDifferentDecisionPayloadShapes(
        Action<MakeDecisionMediaContainerConfig> options,
        VideoQuality expectedQuality
    )
    {
        // Arrange
        await SetupDatabase(4105, config =>
        {
            config.PlexServerCount = 1;
            config.PlexAccountCount = 1;
        });
        var dbContext = IDbContext;

        var plexServer = await dbContext.PlexServers.FirstOrDefaultAsync(CancellationToken);
        plexServer.ShouldNotBeNull();

        var mediaContainer = FakePlexApiData.GetMakeDecisionMediaContainer(options);
        var decisionResponse = FakePlexApiData.GetMakeDecisionResponse(
            HttpStatusCode.OK,
            new Seed(4105),
            mediaContainer
        );

        var plexApiMock = new Mock<IPlexAPI>();
        plexApiMock
            .Setup(x => x.Transcoder.MakeDecisionAsync(It.IsAny<MakeDecisionRequest>()))
            .ReturnsAsync(decisionResponse)
            .Verifiable(Times.Once);

        SetupFactory(plexApiMock);

        var command = CreateCommand(plexServer.Id);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TranscodedQuality.ShouldBe(expectedQuality);

        Mock.Mock<IPlexApiClientFactory>().Verify();
        plexApiMock.Verify();
    }

    [Test]
    public async Task ShouldChooseHighestAvailableQuality_WhenDecisionContainsMultipleVideoVariants()
    {
        // Arrange
        await SetupDatabase(4106, config =>
        {
            config.PlexServerCount = 1;
            config.PlexAccountCount = 1;
        });
        var dbContext = IDbContext;

        var plexServer = await dbContext.PlexServers.FirstOrDefaultAsync(CancellationToken);
        plexServer.ShouldNotBeNull();

        var mediaContainer = FakePlexApiData.GetMakeDecisionMediaContainer(config =>
        {
            config.MediaVideoResolution = "480p";
            config.VideoStreams.Clear();
            config.VideoStreams.Add(
                new MakeDecisionVideoStreamConfig
                {
                    DisplayTitle = "480p",
                    ExtendedDisplayTitle = "480p",
                    Width = 854,
                    Height = 480,
                }
            );
            config.VideoStreams.Add(
                new MakeDecisionVideoStreamConfig
                {
                    DisplayTitle = "1080p",
                    ExtendedDisplayTitle = "1080p",
                    Width = 1920,
                    Height = 1080,
                }
            );
            config.VideoStreams.Add(
                new MakeDecisionVideoStreamConfig
                {
                    DisplayTitle = "2160p",
                    ExtendedDisplayTitle = "2160p",
                    Width = 3840,
                    Height = 2160,
                }
            );
        });
        var decisionResponse = FakePlexApiData.GetMakeDecisionResponse(
            HttpStatusCode.OK,
            new Seed(4106),
            mediaContainer
        );

        var plexApiMock = new Mock<IPlexAPI>();
        plexApiMock
            .Setup(x => x.Transcoder.MakeDecisionAsync(It.IsAny<MakeDecisionRequest>()))
            .ReturnsAsync(decisionResponse)
            .Verifiable(Times.Once);

        SetupFactory(plexApiMock);

        var command = CreateCommand(plexServer.Id);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TranscodedQuality.ShouldBe(VideoQuality.UHD_4K);

        Mock.Mock<IPlexApiClientFactory>().Verify();
        plexApiMock.Verify();
    }

    [Test]
    public async Task ShouldSuggestDirectClient_WhenPlexDecidesDirectPlay()
    {
        // Arrange
        await SetupDatabase(4108, config =>
        {
            config.PlexServerCount = 1;
            config.PlexAccountCount = 1;
        });
        var dbContext = IDbContext;

        var plexServer = await dbContext.PlexServers.FirstOrDefaultAsync(CancellationToken);
        plexServer.ShouldNotBeNull();

        var mediaContainer = FakePlexApiData.GetMakeDecisionDirectPlayMediaContainer();
        var decisionResponse = FakePlexApiData.GetMakeDecisionResponse(
            HttpStatusCode.OK,
            new Seed(4108),
            mediaContainer
        );

        var plexApiMock = new Mock<IPlexAPI>();
        plexApiMock
            .Setup(x => x.Transcoder.MakeDecisionAsync(It.IsAny<MakeDecisionRequest>()))
            .ReturnsAsync(decisionResponse)
            .Verifiable(Times.Once);

        SetupFactory(plexApiMock);

        var command = CreateCommand(plexServer.Id);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.SuggestedClientType.ShouldBe(PlexDownloadClientType.Direct);
        result.Value.PartDecision.ShouldBe("directplay");
        result.Value.VideoDecision.ShouldBe("unknown");
        result.Value.AudioDecision.ShouldBe("unknown");
        result.Value.TranscodedQuality.ShouldBe(VideoQuality.SD);

        Mock.Mock<IPlexApiClientFactory>().Verify();
        plexApiMock.Verify();
    }

    [Test]
    public async Task ShouldResolveQualityFromFactoryData_WhenDisplayTitleDoesNotMatchDimensions()
    {
        // Arrange
        await SetupDatabase(4107, config =>
        {
            config.PlexServerCount = 1;
            config.PlexAccountCount = 1;
        });
        var dbContext = IDbContext;

        var plexServer = await dbContext.PlexServers.FirstOrDefaultAsync(CancellationToken);
        plexServer.ShouldNotBeNull();

        var mediaContainer = FakePlexApiData.GetMakeDecisionMediaContainerWithMismatchedDisplayTitle();
        var decisionResponse = FakePlexApiData.GetMakeDecisionResponse(
            HttpStatusCode.OK,
            new Seed(4107),
            mediaContainer
        );

        var plexApiMock = new Mock<IPlexAPI>();
        plexApiMock
            .Setup(x => x.Transcoder.MakeDecisionAsync(It.IsAny<MakeDecisionRequest>()))
            .ReturnsAsync(decisionResponse)
            .Verifiable(Times.Once);

        SetupFactory(plexApiMock);

        var command = CreateCommand(plexServer.Id);

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.GeneralDecisionCode.ShouldBe("1001");
        result.Value.TranscodeDecisionCode.ShouldBe("1001");
        result.Value.VideoDecision.ShouldBe("Transcode");
        result.Value.AudioDecision.ShouldBe("Transcode");
        result.Value.TranscodedQuality.ShouldBe(VideoQuality.SD);

        Mock.Mock<IPlexApiClientFactory>().Verify();
        plexApiMock.Verify();
    }
}
