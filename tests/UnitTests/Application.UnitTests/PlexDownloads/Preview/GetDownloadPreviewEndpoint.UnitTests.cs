using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PostHog;
using Reaparr.Identity.Contracts;

namespace Reaparr.Application.UnitTests;

public class GetDownloadPreviewEndpointUnitTests
    : BaseEndpointUnitTest<GetDownloadPreviewEndpoint, DownloadPreviewRequest, DownloadPreviewContainerDTO>
{
    [Test]
    public async Task ShouldProjectPreviewHierarchyAndCaptureActualReport_WhenPreviewSucceeds()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = "https://telemetry.example.test";
            build.TelemetryProjectToken = "phc_test";
        });
        const string identity = "3d4f9f7d-6f2a-4d1e-ae3d-9afafef2bf4b";
        var episode = new DownloadPreview
        {
            Id = 203,
            Title = "Episode one",
            Size = 512,
            ChildCount = 0,
            MediaType = PlexMediaType.Episode,
            TvShowId = 201,
            SeasonId = 202,
            ArtistId = 0,
            AlbumId = 0,
            Qualities = [],
            Children = [],
        };
        var season = new DownloadPreview
        {
            Id = 202,
            Title = "Season one",
            Size = 512,
            ChildCount = 1,
            MediaType = PlexMediaType.Season,
            TvShowId = 201,
            SeasonId = 202,
            ArtistId = 0,
            AlbumId = 0,
            Qualities = [],
            Children = [episode],
        };
        var movie = new DownloadPreview
        {
            Id = 101,
            Title = "The movie",
            Size = 4096,
            ChildCount = 1,
            MediaType = PlexMediaType.Movie,
            TvShowId = 0,
            SeasonId = 0,
            ArtistId = 0,
            AlbumId = 0,
            Qualities =
            [
                new PlexMediaQuality
                {
                    MediaDataType = PlexMediaType.Movie,
                    VideoQuality = VideoQuality.FullHD,
                    MediaId = 101,
                    DataId = 301,
                },
            ],
            Children = [],
        };
        var show = new DownloadPreview
        {
            Id = 201,
            Title = "The show",
            Size = 1024,
            ChildCount = 1,
            MediaType = PlexMediaType.TvShow,
            TvShowId = 201,
            SeasonId = 0,
            ArtistId = 0,
            AlbumId = 0,
            Qualities = [],
            Children = [season],
        };
        var request = new DownloadPreviewRequest
        {
            DownloadMedias =
            [
                new DownloadMediaDTO
                {
                    MediaIds = [101],
                    Qualities = [],
                    Type = PlexMediaType.Movie,
                    PlexServerId = 3,
                    PlexLibraryId = 4,
                    SelectionKind = null,
                },
                new DownloadMediaDTO
                {
                    MediaIds = [201],
                    Qualities = [],
                    Type = PlexMediaType.TvShow,
                    PlexServerId = 3,
                    PlexLibraryId = 5,
                    SelectionKind = null,
                },
            ],
            DestinationFolderPathId = 17,
            CustomDestinationFolderPath = "/downloads",
        };
        var commandResult = Result.Ok(new List<DownloadPreview> { movie, show });
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var user = new Mock<IUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(identity).Verifiable(Times.Once());
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        client
            .Setup(x =>
                x.Capture(
                    identity,
                    TelemetryProtocol.ProfileEvent,
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Returns(true)
            .Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(
                    identity,
                    nameof(TelemetryEvent.DownloadPreviewFinished),
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Once());
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetDownloadPreviewQuery>(query => ReferenceEquals(query.Request, request)),
                    CancellationToken
                )
            )
            .ReturnsAsync(commandResult)
            .Verifiable(Times.Once());
        await telemetry.InitializeAsync();

        // Act
        var outcome = await TestEndpointHandleAsync(request, services => services.AddSingleton<ITelemetry>(telemetry));

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var response = (
            await JsonSerializer.DeserializeAsync<ResultDTO<DownloadPreviewContainerDTO>>(
                outcome.Endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var value = response.Value.ShouldNotBeNull();
        value.TotalSize.ShouldBe(movie.Size + show.Size);
        value
            .Previews.Select(x => (x.Title, x.Type, x.Size, x.Children.Count))
            .ShouldBe([
                (movie.Title, PlexMediaType.Movie, movie.Size, 0),
                (show.Title, PlexMediaType.TvShow, show.Size, 1),
            ]);
        var responseShow = value.Previews[1];
        responseShow.Children[0].Title.ShouldBe(season.Title);
        responseShow.Children[0].Type.ShouldBe(PlexMediaType.Season);
        responseShow
            .Children[0]
            .Children.Select(x => (x.Title, x.Type, x.Size))
            .ShouldBe([(episode.Title, PlexMediaType.Episode, episode.Size)]);
        value
            .Previews[0]
            .Qualities.Select(x => (x.MediaDataType, x.VideoQuality, x.MediaId, x.DataId))
            .ShouldBe([(PlexMediaType.Movie, VideoQuality.FullHD, 101, 301)]);
        value.Expanded.Count.ShouldBe(2);
        value.Previews.Select(x => value.Expanded[x.Key]).ShouldBe([true, true]);

        events.Select(x => x.Name).ShouldBe([nameof(TelemetryEvent.DownloadPreviewFinished)]);
        var properties = events.Single().Properties;
        properties[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.Succeeded));
        properties[TelemetryProperty.Downloads.SelectedCount].ShouldBe(2);
        properties[TelemetryProperty.Downloads.SelectionCount].ShouldBe(2);
        ((IEnumerable<string>)properties[TelemetryProperty.Downloads.SelectedMediaTypes]).ShouldBe([
            nameof(PlexMediaType.Movie),
            nameof(PlexMediaType.TvShow),
        ]);
        properties[TelemetryProperty.Downloads.DestinationFolderPathId].ShouldBe(17);
        properties[TelemetryProperty.Downloads.CustomDestinationFolderPath].ShouldBe("/downloads");
        properties[TelemetryProperty.Downloads.PreviewCount].ShouldBe(2);
        properties[TelemetryProperty.Downloads.PreviewTotalSize].ShouldBe(movie.Size + show.Size);
        properties[TelemetryProperty.Downloads.PreviewTotalChildren].ShouldBe(movie.ChildCount + show.ChildCount);
        var report = (
            (IEnumerable<Dictionary<string, object>>)properties[TelemetryProperty.Downloads.Report]
        ).ToArray();
        report
            .Select(x =>
                (
                    (int)x[TelemetryProperty.Downloads.ReportId],
                    (string)x[TelemetryProperty.Downloads.ReportTitle],
                    (long)x[TelemetryProperty.Downloads.ReportSize],
                    (int)x[TelemetryProperty.Downloads.ReportChildCount],
                    (string)x[TelemetryProperty.Downloads.ReportMediaType]
                )
            )
            .ShouldBe([
                (movie.Id, movie.Title, movie.Size, movie.ChildCount, movie.MediaType.ToString()),
                (show.Id, show.Title, show.Size, show.ChildCount, show.MediaType.ToString()),
            ]);
        Mock.Mock<ICommandExecutor>().Verify();
        user.Verify();
        client.Verify();
        client.Verify(
            x =>
                x.CaptureException(
                    It.IsAny<Exception>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                ),
            Times.Never()
        );
    }

    [Test]
    public async Task ShouldReturnNoOpPreviewAndCaptureEmptyReport_WhenNoMediaIsEligible()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = "https://telemetry.example.test";
            build.TelemetryProjectToken = "phc_test";
        });
        const string identity = "a1c1aa1d-2eb2-4f58-bc32-faf2a9bb0e39";
        var request = new DownloadPreviewRequest
        {
            DownloadMedias =
            [
                new DownloadMediaDTO
                {
                    MediaIds = [901],
                    Qualities = [],
                    Type = PlexMediaType.Movie,
                    PlexServerId = 3,
                    PlexLibraryId = 4,
                    SelectionKind = null,
                },
            ],
        };
        var commandResult = Result.Ok(new List<DownloadPreview>());
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var user = new Mock<IUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(identity).Verifiable(Times.Once());
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        client
            .Setup(x =>
                x.Capture(
                    identity,
                    TelemetryProtocol.ProfileEvent,
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Returns(true)
            .Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(
                    identity,
                    nameof(TelemetryEvent.DownloadPreviewFinished),
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Once());
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetDownloadPreviewQuery>(query => ReferenceEquals(query.Request, request)),
                    CancellationToken
                )
            )
            .ReturnsAsync(commandResult)
            .Verifiable(Times.Once());
        await telemetry.InitializeAsync();

        // Act
        var outcome = await TestEndpointHandleAsync(request, services => services.AddSingleton<ITelemetry>(telemetry));

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var response = (
            await JsonSerializer.DeserializeAsync<ResultDTO<DownloadPreviewContainerDTO>>(
                outcome.Endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var value = response.Value.ShouldNotBeNull();
        value.TotalSize.ShouldBe(0);
        value.Previews.ShouldBeEmpty();
        value.Expanded.ShouldBeEmpty();

        events.Select(x => x.Name).ShouldBe([nameof(TelemetryEvent.DownloadPreviewFinished)]);
        var properties = events.Single().Properties;
        properties[TelemetryProperty.Outcome].ShouldBe(nameof(TelemetryOutcome.NoOp));
        properties[TelemetryProperty.Downloads.SelectedCount].ShouldBe(1);
        properties[TelemetryProperty.Downloads.SelectionCount].ShouldBe(1);
        ((IEnumerable<string>)properties[TelemetryProperty.Downloads.SelectedMediaTypes]).ShouldBe([
            nameof(PlexMediaType.Movie),
        ]);
        properties[TelemetryProperty.Downloads.CustomDestinationFolderPath].ShouldBe(string.Empty);
        properties.ContainsKey(TelemetryProperty.Downloads.DestinationFolderPathId).ShouldBeFalse();
        properties[TelemetryProperty.Downloads.PreviewCount].ShouldBe(0);
        properties[TelemetryProperty.Downloads.PreviewTotalSize].ShouldBe(0L);
        properties[TelemetryProperty.Downloads.PreviewTotalChildren].ShouldBe(0);
        ((IEnumerable<Dictionary<string, object>>)properties[TelemetryProperty.Downloads.Report]).ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
        user.Verify();
        client.Verify();
        client.Verify(
            x =>
                x.CaptureException(
                    It.IsAny<Exception>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                ),
            Times.Never()
        );
    }

    [Test]
    [Arguments(TelemetryOutcome.Rejected)]
    [Arguments(TelemetryOutcome.Cancelled)]
    public async Task ShouldCaptureRejectedOrCancelledOutcomeWithoutPreviewReport(TelemetryOutcome expectedOutcome)
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = "https://telemetry.example.test";
            build.TelemetryProjectToken = "phc_test";
        });
        const string identity = "c52e6b64-cc42-476e-88f6-2ec4bd50d780";
        var request = new DownloadPreviewRequest
        {
            DownloadMedias =
            [
                new DownloadMediaDTO
                {
                    MediaIds = [401],
                    Qualities = [],
                    Type = PlexMediaType.OtherVideos,
                    PlexServerId = 8,
                    PlexLibraryId = 9,
                    SelectionKind = null,
                },
            ],
        };
        var commandResult =
            expectedOutcome == TelemetryOutcome.Rejected
                ? ResultExtensions.Create400BadRequestResult("Preview rejected").ToResult<List<DownloadPreview>>()
                : ResultExtensions.TaskIsCancelled("Preview generation").ToResult<List<DownloadPreview>>();
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var user = new Mock<IUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(identity).Verifiable(Times.Once());
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        client
            .Setup(x =>
                x.Capture(
                    identity,
                    TelemetryProtocol.ProfileEvent,
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Returns(true)
            .Verifiable(Times.Once());
        client
            .Setup(x =>
                x.Capture(
                    identity,
                    nameof(TelemetryEvent.DownloadPreviewFinished),
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                )
            )
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!))
            )
            .Returns(true)
            .Verifiable(Times.Once());
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        using var telemetry = new Telemetry(
            client.Object,
            Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            user.Object,
            source,
            new UserSettings()
        );
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetDownloadPreviewQuery>(query => ReferenceEquals(query.Request, request)),
                    CancellationToken
                )
            )
            .ReturnsAsync(commandResult)
            .Verifiable(Times.Once());
        await telemetry.InitializeAsync();

        // Act
        var outcome = await TestEndpointHandleAsync(request, services => services.AddSingleton<ITelemetry>(telemetry));

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(
            expectedOutcome == TelemetryOutcome.Rejected
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status500InternalServerError
        );
        var response = (
            await JsonSerializer.DeserializeAsync<ResultDTO<DownloadPreviewContainerDTO>>(
                outcome.Endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeFalse();
        response.Errors.Count.ShouldBe(1);
        response.Value.ShouldBeNull();

        events.Select(x => x.Name).ShouldBe([nameof(TelemetryEvent.DownloadPreviewFinished)]);
        var properties = events.Single().Properties;
        properties[TelemetryProperty.Outcome].ShouldBe(expectedOutcome.ToString());
        properties[TelemetryProperty.Downloads.SelectedCount].ShouldBe(1);
        properties[TelemetryProperty.Downloads.SelectionCount].ShouldBe(1);
        ((IEnumerable<string>)properties[TelemetryProperty.Downloads.SelectedMediaTypes]).ShouldBe([
            nameof(PlexMediaType.OtherVideos),
        ]);
        properties.ContainsKey(TelemetryProperty.Downloads.PreviewCount).ShouldBeFalse();
        properties.ContainsKey(TelemetryProperty.Downloads.PreviewTotalSize).ShouldBeFalse();
        properties.ContainsKey(TelemetryProperty.Downloads.PreviewTotalChildren).ShouldBeFalse();
        properties.ContainsKey(TelemetryProperty.Downloads.Report).ShouldBeFalse();
        Mock.Mock<ICommandExecutor>().Verify();
        user.Verify();
        client.Verify();
        client.Verify(
            x =>
                x.CaptureException(
                    It.IsAny<Exception>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, object>>(),
                    null,
                    null,
                    null
                ),
            Times.Never()
        );
    }

    [Test]
    public async Task ShouldRejectEmptySelectionBeforePreviewCommandAndTelemetry_WhenValidationFails()
    {
        // Arrange
        var request = new DownloadPreviewRequest { DownloadMedias = [] };

        // Act
        var outcome = await TestEndpointHandleAsync(request);

        // Assert
        outcome.HasValidator.ShouldBeTrue();
        outcome.IsValid.ShouldBeFalse();
        outcome.ValidationErrors.Select(x => x.PropertyName).ShouldBe([nameof(DownloadPreviewRequest.DownloadMedias)]);
        outcome.Response.ShouldBeNull();
        outcome.Endpoint.HttpContext.Response.Body.Length.ShouldBe(0);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<GetDownloadPreviewQuery>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.DownloadPreviewFinished(
                        It.IsAny<DownloadPreviewRequest>(),
                        It.IsAny<ResultBase>(),
                        It.IsAny<
                            IReadOnlyCollection<(
                                int Id,
                                string Title,
                                long Size,
                                int ChildCount,
                                PlexMediaType MediaType
                            )>
                        >()
                    ),
                Times.Never()
            );
    }
}
