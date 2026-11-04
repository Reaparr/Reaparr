using PostHog;
using Reaparr.Domain;
using Reaparr.Identity.Contracts;

namespace Reaparr.Application.UnitTests;

public class TelemetryUnitTests : BaseUnitTest<Telemetry>
{
    private const string TelemetryId = "5e2c5c48-aa0f-4f21-8c65-94e72b2bd389";
    private const string TelemetryHost = "https://telemetry.example.test";

    [Test]
    [Arguments(TelemetryDiagnosticOrigin.Backend, "backend", "0.40.0", "stable")]
    [Arguments(TelemetryDiagnosticOrigin.Frontend, "frontend", "0.40.0-dev.1", "dev")]
    public async Task ShouldQueueTelemetryIdAndApprovedProperties_WhenConfigured(
        TelemetryDiagnosticOrigin origin,
        string expectedOrigin,
        string release,
        string environment
    )
    {
        // Arrange
        SetAppBuildInfo(x =>
        {
            x.TelemetryHost = TelemetryHost;
            x.TelemetryProjectToken = "phc_test";
            x.InformationalVersion = release;
        });
        Mock.Mock<IUserService>()
            .Setup(x => x.GetTelemetryId())
            .ReturnsAsync(TelemetryId)
            .Verifiable(Times.Once());
        Mock.Mock<IPostHogClient>()
            .Setup(x =>
                x.Capture(
                    TelemetryId,
                    "reaparr diagnostic",
                    It.Is<Dictionary<string, object>>(p =>
                        p.Count == 8
                        && (bool)p["$process_person_profile"] == false
                        && (bool)p["$geoip_disable"] == true
                        && (string)p["origin"] == expectedOrigin
                        && (string)p["environment"] == environment
                        && (string)p["release"] == release
                        && (string)p["runtime_mode"] == "docker"
                        && (string)p["platform"] == "Linux"
                        && (string)p["runtime_identifier"] == "linux-x64"
                    ),
                    null,
                    null,
                    null
                )
            )
            .Returns(true)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.TrackAsync(
            "reaparr diagnostic",
            new Dictionary<string, object?> { ["origin"] = origin }
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Successes.Single().Message.ShouldBe("Diagnostic event accepted for sending");
        Mock.Mock<IPostHogClient>().Verify();
        Mock.Mock<IPostHogClient>().Verify(x => x.FlushAsync(), Times.Never());
        Mock.Mock<IPostHogClient>().Verify(x => x.Dispose(), Times.Never());
        Mock.Mock<IUserService>().Verify();
    }

    [Test]
    public async Task ShouldNotLookUpTelemetryIdOrCapture_WhenUnconfigured()
    {
        // Arrange
        SetAppBuildInfo(x =>
        {
            x.TelemetryHost = "";
            x.TelemetryProjectToken = "";
        });

        // Act
        var result = await Sut.TrackAsync(
            "reaparr diagnostic",
            new Dictionary<string, object?> { ["origin"] = TelemetryDiagnosticOrigin.Backend }
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.FindStatusCode().ShouldBe(503);
        Mock.Mock<IPostHogClient>().Invocations.ShouldBeEmpty();
        Mock.Mock<IUserService>().Verify(x => x.GetTelemetryId(), Times.Never());
    }

    [Test]
    [Arguments("other event", "origin", 0)]
    [Arguments("reaparr diagnostic", "distinct_id", 0)]
    [Arguments("reaparr diagnostic", "origin", 99)]
    public async Task ShouldRejectInvalidSchema_WithoutCapturing(string eventName, string key, int origin)
    {
        // Arrange
        SetAppBuildInfo(x =>
        {
            x.TelemetryHost = TelemetryHost;
            x.TelemetryProjectToken = "phc_test";
        });

        // Act
        var result = await Sut.TrackAsync(
            eventName,
            new Dictionary<string, object?> { [key] = (TelemetryDiagnosticOrigin)origin }
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.FindStatusCode().ShouldBe(400);
        Mock.Mock<IPostHogClient>().Invocations.ShouldBeEmpty();
        Mock.Mock<IUserService>().Verify(x => x.GetTelemetryId(), Times.Never());
    }


    [Test]
    public async Task ShouldUseCurrentTelemetryId_WhenTheIdentityChangesBetweenEvents()
    {
        // Arrange
        SetAppBuildInfo(x =>
        {
            x.TelemetryHost = TelemetryHost;
            x.TelemetryProjectToken = "phc_test";
        });
        const string refreshedTelemetryId = "43dd2dd3-629f-4ef4-8ed8-f2ab017bcdaa";
        var telemetryIds = new Queue<string>([TelemetryId, refreshedTelemetryId]);
        Mock.Mock<IUserService>()
            .Setup(x => x.GetTelemetryId())
            .ReturnsAsync(() => telemetryIds.Dequeue())
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IPostHogClient>()
            .Setup(x =>
                x.Capture(
                    TelemetryId,
                    "reaparr diagnostic",
                    It.Is<Dictionary<string, object>>(p => (string)p["origin"] == "backend"),
                    null,
                    null,
                    null
                )
            )
            .Returns(true)
            .Verifiable(Times.Once());
        Mock.Mock<IPostHogClient>()
            .Setup(x =>
                x.Capture(
                    refreshedTelemetryId,
                    "reaparr diagnostic",
                    It.Is<Dictionary<string, object>>(p => (string)p["origin"] == "frontend"),
                    null,
                    null,
                    null
                )
            )
            .Returns(true)
            .Verifiable(Times.Once());

        // Act
        var first = await Sut.TrackAsync(
            "reaparr diagnostic",
            new Dictionary<string, object?> { ["origin"] = TelemetryDiagnosticOrigin.Backend }
        );
        var second = await Sut.TrackAsync(
            "reaparr diagnostic",
            new Dictionary<string, object?> { ["origin"] = TelemetryDiagnosticOrigin.Frontend }
        );

        // Assert
        first.IsSuccess.ShouldBeTrue();
        first.Errors.Count.ShouldBe(0);
        second.IsSuccess.ShouldBeTrue();
        second.Errors.Count.ShouldBe(0);
        Mock.Mock<IUserService>().Verify();
        Mock.Mock<IPostHogClient>().Verify();
    }

    [Test]
    public async Task ShouldReportUnavailable_WhenSdkDeclinesCapture()
    {
        // Arrange
        SetAppBuildInfo(x =>
        {
            x.TelemetryHost = TelemetryHost;
            x.TelemetryProjectToken = "phc_test";
        });
        Mock.Mock<IUserService>()
            .Setup(x => x.GetTelemetryId())
            .ReturnsAsync(TelemetryId)
            .Verifiable(Times.Once());
        Mock.Mock<IPostHogClient>()
            .Setup(x =>
                x.Capture(
                    TelemetryId,
                    "reaparr diagnostic",
                    It.Is<Dictionary<string, object>>(p => (string)p["origin"] == "backend"),
                    null,
                    null,
                    null
                )
            )
            .Returns(false)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.TrackAsync(
            "reaparr diagnostic",
            new Dictionary<string, object?> { ["origin"] = TelemetryDiagnosticOrigin.Backend }
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.FindStatusCode().ShouldBe(503);
        Mock.Mock<IUserService>().Verify();
        Mock.Mock<IPostHogClient>().Verify();
        Mock.Mock<IPostHogClient>().Verify(x => x.FlushAsync(), Times.Never());
    }

    [Test]
    public async Task ShouldNotExposeSdkExceptionDetails_WhenCaptureThrows()
    {
        // Arrange
        SetAppBuildInfo(x =>
        {
            x.TelemetryHost = TelemetryHost;
            x.TelemetryProjectToken = "phc_test";
        });
        Mock.Mock<IUserService>()
            .Setup(x => x.GetTelemetryId())
            .ReturnsAsync(TelemetryId)
            .Verifiable(Times.Once());
        Mock.Mock<IPostHogClient>()
            .Setup(x =>
                x.Capture(
                    TelemetryId,
                    "reaparr diagnostic",
                    It.Is<Dictionary<string, object>>(p => (string)p["origin"] == "backend"),
                    null,
                    null,
                    null
                )
            )
            .Throws(new InvalidOperationException("phc_secret SDK details"))
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.TrackAsync(
            "reaparr diagnostic",
            new Dictionary<string, object?> { ["origin"] = TelemetryDiagnosticOrigin.Backend }
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.FindStatusCode().ShouldBe(503);
        result.Errors[0].Message.ShouldNotContain("phc_secret");
        result.Errors.OfType<ExceptionalError>().ShouldBeEmpty();
        Mock.Mock<IUserService>().Verify();
        Mock.Mock<IPostHogClient>().Verify();
    }

    [Test]
    [Arguments("distinct_id")]
    [Arguments("$geoip_disable")]
    [Arguments("email")]
    [Arguments("release")]
    public async Task ShouldRejectExtraProperties_WhenCallerAttemptsEnrichment(string key)
    {
        // Arrange
        SetAppBuildInfo(x =>
        {
            x.TelemetryHost = TelemetryHost;
            x.TelemetryProjectToken = "phc_test";
        });

        // Act
        var result = await Sut.TrackAsync(
            "reaparr diagnostic",
            new Dictionary<string, object?> { ["origin"] = TelemetryDiagnosticOrigin.Backend, [key] = "private" }
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.FindStatusCode().ShouldBe(400);
        Mock.Mock<IPostHogClient>().Invocations.ShouldBeEmpty();
        Mock.Mock<IUserService>().Verify(x => x.GetTelemetryId(), Times.Never());
    }
}
