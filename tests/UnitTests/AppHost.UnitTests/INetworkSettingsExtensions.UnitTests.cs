using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Reaparr.Settings.Contracts;

namespace Reaparr.AppHost.UnitTests;

// ReSharper disable once InconsistentNaming
public class INetworkSettingsExtensionsUnitTests : BaseUnitTest
{
    [Test]
    public void ShouldDisableForwardedHeaders_WhenAllowlistContainsOnlyBlankEntries()
    {
        // Arrange
        var networkSettings = NetworkSettingsModule.Create();
        networkSettings.TrustProxyHeaders = true;
        networkSettings.AllowedProxyIps = ["", "  ", "\t"];

        // Act
        var options = networkSettings.BuildForwardedHeadersOptions();

        // Assert
        options.ForwardedHeaders.ShouldBe(ForwardedHeaders.None);
    }

    [Test]
    public void ShouldDisableForwardedHeaders_WhenAllowlistContainsOnlyMalformedIp()
    {
        // Arrange
        var networkSettings = NetworkSettingsModule.Create();
        networkSettings.TrustProxyHeaders = true;
        networkSettings.AllowedProxyIps = ["not-an-ip"];

        // Act
        var options = networkSettings.BuildForwardedHeadersOptions();

        // Assert
        options.ForwardedHeaders.ShouldBe(ForwardedHeaders.None);
    }

    [Test]
    public void ShouldDisableForwardedHeaders_WhenAllowlistContainsOnlyMalformedCidr()
    {
        // Arrange
        var networkSettings = NetworkSettingsModule.Create();
        networkSettings.TrustProxyHeaders = true;
        networkSettings.AllowedProxyIps = ["192.0.2.0/not-a-prefix"];

        // Act
        var options = networkSettings.BuildForwardedHeadersOptions();

        // Assert
        options.ForwardedHeaders.ShouldBe(ForwardedHeaders.None);
    }

    [Test]
    public void ShouldKeepForwardedHeadersDisabled_WhenTrustProxyHeadersIsFalse()
    {
        // Arrange
        var networkSettings = NetworkSettingsModule.Create();
        networkSettings.AllowedProxyIps = ["192.0.2.10"];

        // Act
        var options = networkSettings.BuildForwardedHeadersOptions();

        // Assert
        options.ForwardedHeaders.ShouldBe(ForwardedHeaders.None);
    }

    [Test]
    public void ShouldPreserveFrameworkKnownProxyDefaults_WhenAllowlistIsGenuinelyEmpty()
    {
        // Arrange
        var expectedDefaults = new ForwardedHeadersOptions();
        var networkSettings = NetworkSettingsModule.Create();
        networkSettings.TrustProxyHeaders = true;
        networkSettings.AllowedProxyIps = [];

        // Act
        var options = networkSettings.BuildForwardedHeadersOptions();

        // Assert
        options.ForwardedHeaders.ShouldBe(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
        );
        options.ForwardLimit.ShouldBe(1);
        options
            .KnownProxies.Select(ipAddress => ipAddress.ToString())
            .ShouldBe(expectedDefaults.KnownProxies.Select(ipAddress => ipAddress.ToString()));
        options
            .KnownIPNetworks.Select(network => network.ToString())
            .ShouldBe(expectedDefaults.KnownIPNetworks.Select(network => network.ToString()));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void ShouldRetainValidProxyRestrictions_WhenAllowlistContainsOptionalInvalidEntries(
        bool includeInvalidEntries
    )
    {
        // Arrange
        var networkSettings = NetworkSettingsModule.Create();
        networkSettings.TrustProxyHeaders = true;
        networkSettings.AllowedProxyIps = ["192.0.2.10", "192.0.2.0/24"];
        if (includeInvalidEntries)
            networkSettings.AllowedProxyIps.AddRange(["not-an-ip", "192.0.2.0/not-a-prefix", "  "]);

        // Act
        var options = networkSettings.BuildForwardedHeadersOptions();

        // Assert
        options.ForwardedHeaders.ShouldBe(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
        );
        options.ForwardLimit.ShouldBe(1);
        options.KnownProxies.Select(ipAddress => ipAddress.ToString()).ShouldBe(["192.0.2.10"]);
        options.KnownIPNetworks.Select(network => network.ToString()).ShouldBe(["192.0.2.0/24"]);
    }
}
