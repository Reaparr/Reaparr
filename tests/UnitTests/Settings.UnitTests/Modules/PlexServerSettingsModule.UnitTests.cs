using Reaparr.Settings.Contracts;

namespace Reaparr.Settings.UnitTests;

public class PlexServerSettingsModuleUnitTests : BaseUnitTest<PlexServerSettingsModule>
{
    [Test]
    public void ShouldRenameAServerByItsMachineIdentifier_WhenItDoesNotExist()
    {
        // Arrange
        var sut = PlexServerSettingsModule.Create();
        var machineIdentifier = "test";

        // Act
        sut.SetServerName(machineIdentifier, "test-name");

        // Assert
        sut.GetServerNameAlias(machineIdentifier).ShouldBe("test-name");
        sut.Data.Count.ShouldBe(1);
    }

    [Test]
    public void ShouldRenameAServerByItsMachineIdentifier_WhenItAlreadyExists()
    {
        // Arrange
        var sut = PlexServerSettingsModule.Create();
        var machineIdentifier = "test";
        sut.SetServerName(machineIdentifier, "test-name");

        // Act
        sut.SetServerName(machineIdentifier, "test-name-2");

        // Assert
        sut.GetServerNameAlias(machineIdentifier).ShouldBe("test-name-2");
        sut.Data.Count.ShouldBe(1);
    }

    [Test]
    public void ShouldReturnConfiguredLimitInKilobytes_WhenReadingManualLimit()
    {
        // Arrange
        var sut = PlexServerSettingsModule.Create();
        sut.SetDownloadSpeedLimit("machine1", 200);

        // Act
        var speedLimit = sut.GetDownloadSpeedLimit("machine1");

        // Assert
        speedLimit.ShouldBe(200);
    }

}
