using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Moq.Protected;
using PostHog;
using Reaparr.Identity.Contracts;

namespace Reaparr.Application.UnitTests;

public class TelemetryNativeCaptureUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldExportNativeSettingsProfilesAndOriginalExceptionChains_WhenTelemetryIsConfigured()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = "https://telemetry.example.test";
            build.TelemetryProjectToken = "phc_test";
        });
        const string identity = "d6d3fd61-601c-4f83-999d-2e9ec6e41113";
        Mock.Mock<IUserService>().Setup(x => x.GetTelemetryId()).ReturnsAsync(identity).Verifiable(Times.Once());
        var bodies = new ConcurrentQueue<string>();
        var requests = new ConcurrentQueue<(HttpMethod Method, Uri? Uri, string? MediaType)>();
        HttpHandlerMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken token) =>
            {
                requests.Enqueue((
                    request.Method,
                    request.RequestUri,
                    request.Content?.Headers.ContentType?.MediaType
                ));
                bodies.Enqueue(await request.Content!.ReadAsStringAsync(token));
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            })
            .Verifiable(Times.AtLeastOnce());
        using var httpClient = new HttpClient(HttpHandlerMock.Object);
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient).Verifiable(Times.Once());
        using var client = new PostHogClient(new PostHogOptions
        {
            ProjectToken = "phc_test",
            HostUrl = new Uri("https://telemetry.example.test"),
            EnableCompression = false,
        }, httpClientFactory: factory.Object);
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var settings = new UserSettings();
        settings.DownloadManagerSettings.DownloadSegments = 0;
        settings.DownloadManagerSettings.KeepCompletedInDownloadFolder = false;
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Monday"] = new() { ["09:00"] = 100, ["18:00"] = null } },
        };
        settings.NetworkSettings.ReverseProxyUrl = "http://192.168.1.5:5000/reaparr?section=42";
        settings.IntegrationsSettings.Sonarr.SonarrApiKey = "excluded-legacy-api-key";
        using var telemetry = new Telemetry(client, Mock.Container.Resolve<IAppBuildInfo>(),
            Mock.Mock<IUserService>().Object, source, settings);
        using var activity = new Activity(nameof(ShouldExportNativeSettingsProfilesAndOriginalExceptionChains_WhenTelemetryIsConfigured))
            .SetIdFormat(ActivityIdFormat.W3C).Start();
        const string session = "0197af2c-8533-71b3-a351-80da30db084a";
        activity.SetTag(TelemetryProtocol.SessionIdTag, session);
        var exception = CreateException();

        // Act
        await telemetry.InitializeAsync();
        telemetry.NotificationsCleared(0);
        telemetry.CaptureException(exception, TelemetryActivity.Download);
        await client.FlushAsync();

        // Assert
        var events = new List<JsonElement>();
        foreach (var body in bodies)
        {
            using var batch = JsonDocument.Parse(body);
            var root = batch.RootElement;
            root.GetProperty("api_key").GetString().ShouldBe("phc_test");
            root.GetProperty("batch").ValueKind.ShouldBe(JsonValueKind.Array);
            events.AddRange(root.GetProperty("batch").EnumerateArray().Select(item => item.Clone()));
        }
        requests.ShouldNotBeEmpty();
        requests.Select(request => request.Method).ShouldAllBe(method => method == HttpMethod.Post);
        requests
            .Select(request => request.Uri)
            .ShouldAllBe(uri => uri == new Uri("https://telemetry.example.test/batch"));
        requests.Select(request => request.MediaType).ShouldAllBe(mediaType => mediaType == "application/json");
        HttpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(message =>
                message.Method == HttpMethod.Post
                    && message.RequestUri == new Uri("https://telemetry.example.test/batch")
                    && message.Content!.Headers.ContentType!.MediaType == "application/json"
            ),
            ItExpr.IsAny<CancellationToken>()
        );
        factory.Verify();
        var profileEvent = events.Single(item => item.GetProperty("event").GetString() == TelemetryProtocol.ProfileEvent);
        var profileProperties = profileEvent.GetProperty("properties");
        profileProperties.GetProperty(TelemetryProtocol.DistinctId).GetString().ShouldBe(identity);
        var profile = profileProperties.GetProperty(TelemetryProtocol.PersonProperties);
        profile.GetProperty("settings.downloadManagerSettings.keepCompletedInDownloadFolder").GetBoolean().ShouldBeFalse();
        profile.GetProperty("settings.downloadManagerSettings.downloadSegments").GetInt32().ShouldBe(0);
        profile.GetProperty("settings.downloadManagerSettings.downloadSchedule").GetProperty("days")
            .GetProperty("Monday").GetProperty("18:00").ValueKind.ShouldBe(JsonValueKind.Null);
        profile.GetProperty("settings.networkSettings.reverseProxyUrl").GetString().ShouldBe("http://192.168.1.5:5000/reaparr?section=42");
        profile.GetRawText().ShouldNotContain("excluded-legacy-api-key");
        profile.EnumerateObject().All(property => property.Name.StartsWith(TelemetryProfileProperty.SettingsPrefix, StringComparison.Ordinal)).ShouldBeTrue();
        var business = events.Single(item => item.GetProperty("event").GetString() == nameof(TelemetryEvent.NotificationsCleared)).GetProperty("properties");
        business.GetProperty(TelemetryProperty.DeletedCount).GetInt32().ShouldBe(0);
        business.GetProperty(TelemetryProperty.Outcome).GetString().ShouldBe(nameof(TelemetryOutcome.NoOp));
        var error = events.Single(item => item.GetProperty("event").GetString() == TelemetryProtocol.ExceptionEvent).GetProperty("properties");
        error.GetProperty(TelemetryProtocol.TraceId).GetString().ShouldBe(activity.TraceId.ToHexString());
        error.GetProperty(TelemetryProtocol.SpanId).GetString().ShouldBe(activity.SpanId.ToHexString());
        error.GetProperty(TelemetryProtocol.SessionId).GetString().ShouldBe(session);
        error.GetProperty(TelemetryProperty.Stage).GetString().ShouldBe(nameof(TelemetryActivity.Download));
        var exceptions = error.GetProperty("$exception_list").EnumerateArray().ToArray();
        exceptions.Select(item => item.GetProperty("type").GetString()).ShouldContain(typeof(InvalidOperationException).FullName);
        exceptions.Select(item => item.GetProperty("type").GetString()).ShouldContain(typeof(IOException).FullName);
        error.GetRawText().ShouldContain("root-exception-text");
        error.GetRawText().ShouldContain("inner-exception-text");
        Mock.Mock<IUserService>().Verify();
    }

    [Test]
    public async Task ShouldKeepDiagnosticAdmissionSuccessful_WhenNativeBatchDeliveryIsRejected()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = "https://telemetry.example.test";
            build.TelemetryProjectToken = "phc_test";
        });
        const string identity = "a564b3b1-0bc4-4c51-8a30-bf24863b8d9c";
        var user = new Mock<IUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(identity).Verifiable(Times.Once());
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var deliveredEvents = new ConcurrentQueue<JsonElement>();
        var requestBodies = new ConcurrentQueue<string>();
        var requests = new ConcurrentQueue<(HttpMethod Method, Uri? Uri, string? MediaType)>();
        HttpHandlerMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken token) =>
            {
                requests.Enqueue((
                    request.Method,
                    request.RequestUri,
                    request.Content?.Headers.ContentType?.MediaType
                ));
                var body = await request.Content!.ReadAsStringAsync(token);
                requestBodies.Enqueue(body);
                using var batch = JsonDocument.Parse(body);
                foreach (var item in batch.RootElement.GetProperty("batch").EnumerateArray())
                    deliveredEvents.Enqueue(item.Clone());
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") };
            })
            .Verifiable(Times.AtLeastOnce());
        using var httpClient = new HttpClient(HttpHandlerMock.Object);
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient).Verifiable(Times.Once());
        using var client = new PostHogClient(new PostHogOptions
        {
            ProjectToken = "phc_test",
            HostUrl = new Uri("https://telemetry.example.test"),
            EnableCompression = false,
        }, httpClientFactory: factory.Object);
        using var telemetry = new Telemetry(client, Mock.Container.Resolve<IAppBuildInfo>(),
            user.Object, source, new UserSettings());
        await telemetry.InitializeAsync();

        // Act
        var result = telemetry.SendDiagnostic(TelemetryDiagnosticOrigin.Backend);
        await client.FlushAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Successes.Single().Message.ShouldBe("Diagnostic event accepted for sending");
        deliveredEvents.Where(item => item.GetProperty("event").GetString() == TelemetryProtocol.DiagnosticEvent)
            .Select(item => item.GetProperty("properties").GetProperty(TelemetryProtocol.DistinctId).GetString())
            .Distinct().ShouldBe([identity]);
        deliveredEvents.Where(item => item.GetProperty("event").GetString() == TelemetryProtocol.ExceptionEvent).ShouldBeEmpty();
        requestBodies.ShouldNotBeEmpty();
        foreach (var body in requestBodies)
        {
            using var batch = JsonDocument.Parse(body);
            batch.RootElement.GetProperty("api_key").GetString().ShouldBe("phc_test");
            batch.RootElement.GetProperty("batch").ValueKind.ShouldBe(JsonValueKind.Array);
        }
        requests.ShouldNotBeEmpty();
        requests.Select(request => request.Method).ShouldAllBe(method => method == HttpMethod.Post);
        requests
            .Select(request => request.Uri)
            .ShouldAllBe(uri => uri == new Uri("https://telemetry.example.test/batch"));
        requests.Select(request => request.MediaType).ShouldAllBe(mediaType => mediaType == "application/json");
        HttpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(message =>
                message.Method == HttpMethod.Post
                    && message.RequestUri == new Uri("https://telemetry.example.test/batch")
                    && message.Content!.Headers.ContentType!.MediaType == "application/json"
            ),
            ItExpr.IsAny<CancellationToken>()
        );
        factory.Verify();
        user.Verify();
    }

    [Test]
    [Arguments(TelemetryEvent.MediaQueryFinished, TelemetryOutcome.Failed, true)]
    [Arguments(TelemetryEvent.MediaQueryFinished, TelemetryOutcome.Failed, false)]
    [Arguments(TelemetryEvent.MediaQueryFinished, TelemetryOutcome.Rejected, true)]
    [Arguments(TelemetryEvent.MediaQueryFinished, TelemetryOutcome.Cancelled, true)]
    [Arguments(TelemetryEvent.DownloadPreviewFinished, TelemetryOutcome.Failed, false)]
    [Arguments(TelemetryEvent.DownloadPreviewFinished, TelemetryOutcome.Rejected, false)]
    [Arguments(TelemetryEvent.DownloadPreviewFinished, TelemetryOutcome.Cancelled, false)]
    public async Task ShouldCaptureOnlyUnexpectedQueryFailures_WhenTheResultIsConsumed(
        TelemetryEvent eventName,
        TelemetryOutcome outcome,
        bool hasSearch
    )
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = "https://telemetry.example.test";
            build.TelemetryProjectToken = "phc_test";
        });
        const string identity = "ba407754-d97e-46d8-a848-b723bfab84b1";
        Exception? exception = outcome switch
        {
            TelemetryOutcome.Failed => CreateException(),
            TelemetryOutcome.Cancelled => new OperationCanceledException(),
            _ => null,
        };
        var result = exception is null
            ? Result.Fail("Expected rejection")
            : Result.Fail(new ExceptionalError(exception));
        var operation = eventName == TelemetryEvent.MediaQueryFinished
            ? TelemetryActivity.MediaQuery
            : TelemetryActivity.DownloadPreview;
        var emitsBusinessEvent = eventName == TelemetryEvent.DownloadPreviewFinished || hasSearch;
        var events = new List<(string Name, Dictionary<string, object> Properties)>();
        var exceptions = new List<Exception>();
        var user = new Mock<IUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(identity).Verifiable(Times.Once());
        var client = new Mock<IPostHogClient>(MockBehavior.Strict);
        client.Setup(x => x.Capture(identity, It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), null, null, null))
            .Callback<string, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (_, name, properties, _, _, _) => events.Add((name, properties!)))
            .Returns(true)
            .Verifiable(Times.Exactly(emitsBusinessEvent ? 2 : 1));
        client.Setup(x => x.CaptureException(
                It.IsAny<Exception>(), identity,
                It.Is<Dictionary<string, object>>(properties =>
                    (string)properties[TelemetryProperty.Stage] == operation.ToString()),
                null, null, null))
            .Callback<Exception, string, Dictionary<string, object>?, GroupCollection?, object?, DateTimeOffset?>(
                (captured, _, _, _, _, _) => exceptions.Add(captured))
            .Returns(true)
            .Verifiable(outcome == TelemetryOutcome.Failed ? Times.Once() : Times.Never());
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        using var telemetry = new Telemetry(client.Object, Mock.Container.Resolve<IAppBuildInfo>(),
            user.Object, source, new UserSettings());
        await telemetry.InitializeAsync();
        events.Clear();
        using var activity = new Activity(nameof(ShouldCaptureOnlyUnexpectedQueryFailures_WhenTheResultIsConsumed)).Start();

        // Act
        if (eventName == TelemetryEvent.MediaQueryFinished)
        {
            telemetry.MediaQueryFinished(
                new MediaQueryTelemetryDTO
                {
                    Search = hasSearch ? "arrival" : null,
                    MediaType = PlexMediaType.Movie,
                },
                null,
                result.ToResult<PlexMediaStatisticsDTO>()
            );
        }
        else
        {
            telemetry.DownloadPreviewFinished(
                new DownloadPreviewRequest
                {
                    DownloadMedias =
                    [
                        new()
                        {
                            MediaIds = [17],
                            Type = PlexMediaType.Movie,
                            PlexServerId = 2,
                            PlexLibraryId = 3,
                            SelectionKind = null,
                            Qualities =
                            [
                                new()
                                {
                                    MediaId = 17,
                                    DataId = 4,
                                    MediaDataType = PlexMediaType.Movie,
                                    VideoQuality = VideoQuality.FullHD,
                                },
                            ],
                        },
                    ],
                },
                result,
                []
            );
        }

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        Exception[] expectedExceptions = outcome == TelemetryOutcome.Failed ? [exception!] : [];
        exceptions.ShouldBe(expectedExceptions);
        events.Select(x => (x.Name, Outcome: (string)x.Properties[TelemetryProperty.Outcome])).ShouldBe(
            emitsBusinessEvent ? new[] { (eventName.ToString(), outcome.ToString()) } : []
        );
        activity.Status.ShouldBe(
            outcome == TelemetryOutcome.Cancelled ? ActivityStatusCode.Unset : ActivityStatusCode.Error
        );
        user.Verify();
        client.Verify();
    }

    [Test]
    public async Task ShouldReplaceMoveSourcePath_WhenResumingFromTheRenamedFile()
    {
        // Arrange
        SetAppBuildInfo(build =>
        {
            build.TelemetryHost = "https://telemetry.example.test";
            build.TelemetryProjectToken = "phc_test";
        });
        const string identity = "39a5654a-3a52-4bb6-9704-a3219144c7e4";
        var downloadTask = FakeData.GetDownloadTaskMovieFile(new Seed(3043)).Generate();
        var sourcePath = downloadTask.DownloadFilePath;
        var resumedPath = sourcePath.RemoveReapTempSuffix();
        var destinationPath = downloadTask.DestinationFilePath;
        var user = new Mock<IUserService>(MockBehavior.Strict);
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(identity).Verifiable(Times.Once());
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source.Equals;
        listener.Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded;
        ActivitySource.AddActivityListener(listener);
        using var telemetry = new Telemetry(new Mock<IPostHogClient>().Object, Mock.Container.Resolve<IAppBuildInfo>(),
            user.Object, source, new UserSettings());
        await telemetry.InitializeAsync();
        using var activity = telemetry.StartMoveDownloadFile(
            downloadTask, sourcePath, destinationPath, false, TelemetryRetentionReason.Destination
        );
        activity.ShouldNotBeNull().GetTagItem(TelemetryProperty.Downloads.SourcePath).ShouldBe(sourcePath);
        resumedPath.ShouldNotBe(sourcePath);

        // Act
        telemetry.SetMoveDownloadFileSourcePath(activity, resumedPath);

        // Assert
        activity.GetTagItem(TelemetryProperty.Downloads.SourcePath).ShouldBe(resumedPath);
        activity.GetTagItem(TelemetryProperty.Downloads.DownloadSourcePathTag).ShouldBeNull();
        activity.GetTagItem(TelemetryProperty.Downloads.DestinationPath).ShouldBe(destinationPath);
        activity.GetTagItem(TelemetryProperty.TaskId).ShouldBe(downloadTask.Id);
        user.Verify();
    }

    private static Exception CreateException()
    {
        try
        {
            throw new InvalidOperationException("Password=root-exception-text", new IOException("Authorization: Basic inner-exception-text"));
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }
}
