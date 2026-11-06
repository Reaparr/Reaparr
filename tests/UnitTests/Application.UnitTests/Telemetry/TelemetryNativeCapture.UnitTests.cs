using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Moq.Protected;
using PostHog;
using Reaparr.Identity.Contracts;

namespace Reaparr.Application.UnitTests;

public class TelemetryNativeCaptureUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldExportNativeSettingsProfilesAndOriginalExceptionChains()
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
        HttpHandlerMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken token) =>
            {
                bodies.Enqueue(await request.Content!.ReadAsStringAsync(token));
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            });
        using var httpClient = new HttpClient(HttpHandlerMock.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);
        using var client = new PostHogClient(new PostHogOptions
        {
            ProjectToken = "phc_test",
            HostUrl = new Uri("https://telemetry.example.test"),
            EnableCompression = false,
        }, httpClientFactory: factory.Object);
        using var services = new ServiceCollection().AddScoped(_ => Mock.Mock<IUserService>().Object).BuildServiceProvider();
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
            services.GetRequiredService<IServiceScopeFactory>(), source, settings);
        using var activity = new Activity(nameof(ShouldExportNativeSettingsProfilesAndOriginalExceptionChains))
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
            events.AddRange(batch.RootElement.GetProperty("batch").EnumerateArray().Select(item => item.Clone()));
        }
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
        using var services = new ServiceCollection().AddScoped(_ => user.Object).BuildServiceProvider();
        using var source = new ActivitySource(TelemetryProtocol.ActivitySourceName);
        var deliveredEvents = new ConcurrentQueue<JsonElement>();
        HttpHandlerMock.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken token) =>
            {
                using var batch = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                foreach (var item in batch.RootElement.GetProperty("batch").EnumerateArray())
                    deliveredEvents.Enqueue(item.Clone());
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") };
            });
        using var httpClient = new HttpClient(HttpHandlerMock.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);
        using var client = new PostHogClient(new PostHogOptions
        {
            ProjectToken = "phc_test",
            HostUrl = new Uri("https://telemetry.example.test"),
            EnableCompression = false,
        }, httpClientFactory: factory.Object);
        using var telemetry = new Telemetry(client, Mock.Container.Resolve<IAppBuildInfo>(),
            services.GetRequiredService<IServiceScopeFactory>(), source, new UserSettings());
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
