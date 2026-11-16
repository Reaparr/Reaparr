namespace Reaparr.Build.UnitTests;

public class DesktopCommandSettingsUnitTests : BaseUnitTest
{
    [Test]
    public void ShouldFailValidation_WhenRuntimeIdentifierIsMissing()
    {
        // Arrange
        var sut = new DesktopCommandSettings { Version = "1.2.3", InformationalVersion = "1.2.3-dev.1" };

        // Act
        var result = sut.Validate();

        // Assert
        result.Successful.ShouldBeFalse();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("runtime identifier is required");
    }

    [Test]
    public void ShouldFailValidation_WhenVersionIsMissing()
    {
        // Arrange
        var sut = new DesktopCommandSettings { RuntimeIdentifier = "linux-x64", InformationalVersion = "1.2.3-dev.1" };

        // Act
        var result = sut.Validate();

        // Assert
        result.Successful.ShouldBeFalse();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("build version is required");
    }

    [Test]
    public void ShouldFailValidation_WhenInformationalVersionIsMissing()
    {
        // Arrange
        var sut = new DesktopCommandSettings { RuntimeIdentifier = "linux-x64", Version = "1.2.3" };

        // Act
        var result = sut.Validate();

        // Assert
        result.Successful.ShouldBeFalse();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("informational version is required");
    }

    [Test]
    public void ShouldFailValidation_WhenRuntimeIdentifierIsUnsupported()
    {
        // Arrange
        var sut = new DesktopCommandSettings
        {
            RuntimeIdentifier = "freebsd-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
        };

        // Act
        var result = sut.Validate();

        // Assert
        result.Successful.ShouldBeFalse();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("Unsupported desktop RID 'freebsd-x64'");
    }

    [Test]
    public void ShouldFailValidation_WhenLinuxPackageLaunchModeIsUnsupported()
    {
        // Arrange
        var sut = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
            LaunchMode = "invalid",
        };

        // Act
        var result = sut.Validate();

        // Assert
        result.Successful.ShouldBeFalse();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("Unsupported launch mode 'invalid'");
    }

    [Test]
    public void ShouldPassValidation_WhenNonLinuxRIDHasUnsupportedLaunchMode()
    {
        // Arrange
        var sut = new DesktopCommandSettings
        {
            RuntimeIdentifier = "win-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
            LaunchMode = "invalid",
        };

        // Act
        var result = sut.Validate();

        // Assert
        result.Successful.ShouldBeTrue();
        result.Message.ShouldBeNullOrWhiteSpace();
    }

    [Test]
    public void ShouldPassValidation_WhenRequiredSettingsAreValid()
    {
        // Arrange
        var sut = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
        };

        // Act
        var result = sut.Validate();

        // Assert
        result.Successful.ShouldBeTrue();
        result.Message.ShouldBeNullOrWhiteSpace();
    }

    [Test]
    [Arguments("https://telemetry.eu.example.test", "phc_public-token_123")]
    [Arguments("https://telemetry.self-hosted.example:8443/", "phc_public-token_456")]
    public void ShouldPassValidation_WhenTelemetryConfigurationIsPublicAndComplete(string host, string projectToken)
    {
        // Arrange
        var sut = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
            TelemetryHost = host,
            TelemetryProjectToken = projectToken,
        };

        // Act
        var result = sut.Validate();

        // Assert
        result.Successful.ShouldBeTrue();
        result.Message.ShouldBeNullOrWhiteSpace();
    }

    [Test]
    [Arguments("https://telemetry.example.test", "")]
    [Arguments("", "phc_public-token")]
    public void ShouldFailValidation_WhenTelemetryConfigurationIsPartial(string host, string projectToken)
    {
        // Arrange
        var sut = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
            TelemetryHost = host,
            TelemetryProjectToken = projectToken,
        };

        // Act
        var result = sut.Validate();

        // Assert
        result.Successful.ShouldBeFalse();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("supplied together");
    }

    [Test]
    [Arguments("http://telemetry.example.test", "phc_public-token")]
    [Arguments("https://telemetry.example.test/path", "phc_public-token")]
    [Arguments("https://telemetry.example.test?source=test", "phc_public-token")]
    [Arguments("https://telemetry.example.test#fragment", "phc_public-token")]
    [Arguments("https://user:password@telemetry.example.test", "phc_public-token")]
    [Arguments("https://telemetry.example.test//", "phc_public-token")]
    [Arguments("https://telemetry.example.test:not-a-port", "phc_public-token")]
    [Arguments("https://telemetry.example.test:99999", "phc_public-token")]
    [Arguments("https://telemetry.example.test", "phx_private-token")]
    [Arguments("https://telemetry.example.test", "phc_public token")]
    public void ShouldFailValidation_WhenTelemetryConfigurationIsInvalid(string host, string projectToken)
    {
        // Arrange
        var sut = new DesktopCommandSettings
        {
            RuntimeIdentifier = "linux-x64",
            Version = "1.2.3",
            InformationalVersion = "1.2.3-dev.1",
            TelemetryHost = host,
            TelemetryProjectToken = projectToken,
        };

        // Act
        var result = sut.Validate();

        // Assert
        result.Successful.ShouldBeFalse();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("Telemetry");
    }
}
