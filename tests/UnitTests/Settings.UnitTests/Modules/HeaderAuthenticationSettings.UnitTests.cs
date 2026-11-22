using System.ComponentModel;
using Reaparr.Settings.Contracts;

namespace Reaparr.Settings.UnitTests;

public class HeaderAuthenticationSettingsUnitTests : BaseUnitTest
{
    [Test]
    public void ShouldCreateWithDefaultValues()
    {
        // Act
        var settings = HeaderAuthenticationSettings.Create();

        // Assert
        settings.Enabled.ShouldBeFalse();
        settings.MappingType.ShouldBe(HeaderMappingType.Username);
        settings.TrustedProxies.ShouldBeEmpty();
        settings.EnableLogging.ShouldBeTrue();
        settings.MaxHeaderLength.ShouldBe(256);
        settings.RequireHttps.ShouldBeTrue();
    }

    [Test]
    public void ShouldBeValid_WhenDisabled()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        settings.Enabled = false;

        // Act
        var isValid = settings.IsValid();

        // Assert
        isValid.ShouldBeTrue();
    }

    [Test]
    public void ShouldBeValid_WhenEnabledWithValidConfiguration()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        settings.Enabled = true;
        settings.MaxHeaderLength = 100;
        settings.TrustedProxies = ["192.168.1.0/24", "10.0.0.1"];

        // Act
        var isValid = settings.IsValid();

        // Assert
        isValid.ShouldBeTrue();
    }

    [Test]
    public void ShouldBeInvalid_WhenEnabledWithZeroMaxHeaderLength()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        settings.Enabled = true;
        settings.MaxHeaderLength = 0;

        // Act
        var isValid = settings.IsValid();

        // Assert
        isValid.ShouldBeFalse();
    }

    [Test]
    public void ShouldBeInvalid_WhenEnabledWithNegativeMaxHeaderLength()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        settings.Enabled = true;
        settings.MaxHeaderLength = -1;

        // Act
        var isValid = settings.IsValid();

        // Assert
        isValid.ShouldBeFalse();
    }

    [Test]
    public void ShouldBeInvalid_WhenTrustedProxyContainsInvalidIp()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        settings.Enabled = true;
        settings.TrustedProxies = ["invalid-ip"];

        // Act
        var isValid = settings.IsValid();

        // Assert
        isValid.ShouldBeFalse();
    }

    [Test]
    public void ShouldBeInvalid_WhenTrustedProxyContainsInvalidCidr()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        settings.Enabled = true;
        settings.TrustedProxies = ["192.168.1.0/33"];

        // Act
        var isValid = settings.IsValid();

        // Assert
        isValid.ShouldBeFalse();
    }

    [Test]
    public void ShouldBeValid_WithValidCidrRanges()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        settings.Enabled = true;
        settings.TrustedProxies = ["192.168.1.0/24", "10.0.0.0/8", "172.16.0.0/12", "127.0.0.1", "::1"];

        // Act
        var isValid = settings.IsValid();

        // Assert
        isValid.ShouldBeTrue();
    }

    [Test]
    public void ShouldBeValid_WithIpv6Addresses()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        settings.Enabled = true;
        settings.TrustedProxies = ["::1", "2001:db8::1", "2001:db8::/32", "fe80::1"];

        // Act
        var isValid = settings.IsValid();

        // Assert
        isValid.ShouldBeTrue();
    }

    [Test]
    [Arguments("192.168.1.1", true)]
    [Arguments("10.0.0.1", true)]
    [Arguments("invalid-ip", false)]
    [Arguments("", false)]
    [Arguments("192.168.1.0/24", true)]
    [Arguments("192.168.1.0/33", false)]
    [Arguments("192.168.1.0/", false)]
    [Arguments("192.168.1.0/24/extra", false)]
    [Arguments("192.168.1.0/0", true)] // Valid CIDR with /0
    [Arguments("192.168.1.0/32", true)] // Valid CIDR with /32
    [Arguments("192.168.1.0/-1", false)] // Invalid negative CIDR
    [Arguments("192.168.1.0/abc", false)] // Invalid non-numeric CIDR
    [Arguments("   ", false)] // Whitespace only
    [Arguments("\t", false)] // Tab only
    [Arguments("\n", false)] // Newline only
    public void ShouldValidateIpOrCidrCorrectly(string input, bool expectedValid)
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        settings.Enabled = true;
        settings.TrustedProxies = [input];

        // Act
        var isValid = settings.IsValid();

        // Assert
        isValid.ShouldBe(expectedValid);
    }

    [Test]
    public void ShouldSupportAllMappingTypes()
    {
        // Arrange & Act
        var usernameMapping = HeaderAuthenticationSettings.Create();
        usernameMapping.MappingType = HeaderMappingType.Username;

        var emailMapping = HeaderAuthenticationSettings.Create();
        emailMapping.MappingType = HeaderMappingType.Email;

        // Assert
        usernameMapping.MappingType.ShouldBe(HeaderMappingType.Username);
        emailMapping.MappingType.ShouldBe(HeaderMappingType.Email);
    }

    [Test]
    public void ShouldSupportLoggingConfiguration()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();

        // Act
        settings.EnableLogging = false;

        // Assert
        settings.EnableLogging.ShouldBeFalse();
    }

    [Test]
    public void ShouldSupportHttpsRequirementConfiguration()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();

        // Act
        settings.RequireHttps = false;

        // Assert
        settings.RequireHttps.ShouldBeFalse();
    }

    [Test]
    public void ShouldSupportMaxHeaderLengthConfiguration()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();

        // Act
        settings.MaxHeaderLength = 512;

        // Assert
        settings.MaxHeaderLength.ShouldBe(512);
    }

    [Test]
    public void ShouldBeValid_WhenTrustedProxiesIsEmpty()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        settings.Enabled = true;
        settings.TrustedProxies = [];

        // Act
        var isValid = settings.IsValid();

        // Assert
        isValid.ShouldBeTrue();
    }

    [Test]
    public void ShouldBeInvalid_WhenTrustedProxiesContainsEmptyString()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        settings.Enabled = true;
        settings.TrustedProxies = [""];

        // Act
        var isValid = settings.IsValid();

        // Assert
        isValid.ShouldBeFalse();
    }

    [Test]
    public void ShouldBeInvalid_WhenTrustedProxiesContainsNullString()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        settings.Enabled = true;
        settings.TrustedProxies = [null!];

        // Act
        var isValid = settings.IsValid();

        // Assert
        isValid.ShouldBeFalse();
    }

    [Test]
    public void ShouldBeInvalid_WhenTrustedProxiesContainsWhitespaceOnlyString()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        settings.Enabled = true;
        settings.TrustedProxies = ["   "];

        // Act
        var isValid = settings.IsValid();

        // Assert
        isValid.ShouldBeFalse();
    }

    [Test]
    public void ShouldUpdateProperties_WhenUpdateMethodIsCalled()
    {
        // Arrange
        var originalSettings = HeaderAuthenticationSettings.Create();
        var updatedSettings = HeaderAuthenticationSettings.Create();
        updatedSettings.Enabled = true;
        updatedSettings.MappingType = HeaderMappingType.Email;
        updatedSettings.MaxHeaderLength = 512;
        updatedSettings.RequireHttps = false;
        updatedSettings.EnableLogging = false;
        updatedSettings.TrustedProxies = ["192.168.1.1"];

        // Act
        originalSettings.Update(updatedSettings);

        // Assert
        originalSettings.Enabled.ShouldBeTrue();
        originalSettings.MappingType.ShouldBe(HeaderMappingType.Email);
        originalSettings.MaxHeaderLength.ShouldBe(512);
        originalSettings.RequireHttps.ShouldBeFalse();
        originalSettings.EnableLogging.ShouldBeFalse();
        originalSettings.TrustedProxies.ShouldContain("192.168.1.1");
    }

    [Test]
    public void ShouldPublishCurrentModelBeforePropertyChanged_WhenPropertyChanges()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        var changedModels = new List<HeaderAuthenticationSettings>();
        var propertyNames = new List<string?>();
        var notificationOrder = new List<string>();
        using var subscription = settings.HasChanged.Subscribe(model =>
        {
            changedModels.Add(model);
            notificationOrder.Add("HasChanged");
        });
        ((INotifyPropertyChanged)settings).PropertyChanged += (_, args) =>
        {
            propertyNames.Add(args.PropertyName);
            notificationOrder.Add("PropertyChanged");
        };

        // Act
        settings.Enabled = true;

        // Assert
        changedModels.Count.ShouldBe(2);
        changedModels.All(model => ReferenceEquals(model, settings)).ShouldBeTrue();
        propertyNames.ShouldBe([nameof(HeaderAuthenticationSettings.Enabled)]);
        notificationOrder.ShouldBe(["HasChanged", "HasChanged", "PropertyChanged"]);
    }

    [Test]
    public void ShouldNotifyObservers_WhenPropertiesChange()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        var changeNotifications = new List<HeaderAuthenticationSettings>();

        using var subscription = settings.HasChanged.Subscribe(changeNotifications.Add);

        // Act
        settings.Enabled = true;
        settings.MaxHeaderLength = 1024;
        settings.MappingType = HeaderMappingType.Email;

        // Assert
        changeNotifications.Count.ShouldBe(4);
        changeNotifications.Last().Enabled.ShouldBeTrue();
        changeNotifications.Last().MaxHeaderLength.ShouldBe(1024);
        changeNotifications.Last().MappingType.ShouldBe(HeaderMappingType.Email);
    }

    [Test]
    public void ShouldNotNotifyObservers_WhenSameValueIsSet()
    {
        // Arrange
        var settings = HeaderAuthenticationSettings.Create();
        var changeNotifications = new List<HeaderAuthenticationSettings>();
        var propertyNames = new List<string?>();

        using var subscription = settings.HasChanged.Subscribe(changeNotifications.Add);
        ((INotifyPropertyChanged)settings).PropertyChanged += (_, args) => propertyNames.Add(args.PropertyName);
        var initialCount = changeNotifications.Count;
        initialCount.ShouldBe(1);

        // Act
        settings.Enabled = false; // Same as default value
        settings.MaxHeaderLength = 256; // Same as default value

        // Assert
        changeNotifications.Count.ShouldBe(initialCount);
        propertyNames.ShouldBeEmpty();
    }
}
