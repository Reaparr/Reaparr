using System.IO.Abstractions;
using Autofac;
using Reaparr.Environment;
using Reaparr.Settings.Contracts;

namespace Reaparr.Settings.UnitTests;

public class ConfigManagerSetupUnitTests : BaseUnitTest<ConfigManager>
{
    [Test]
    public void ShouldLoadConfigDuringSetup_WhenConfigFileAlreadyExists()
    {
        // Arrange
        SetupFileSystem();
        var settings = new UserSettings();
        var persisted = new UserSettings();
        persisted.DateTimeSettings.TimeZone = "Asia/Kathmandu";
        persisted.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Monday"] = new() { ["09:30"] = 123 } },
        };
        persisted.ServerSettings.SetDownloadSpeedLimit("retained", 200);
        SetupDependencies(builder => builder.RegisterInstance(settings).As<IUserSettings>());
        var paths = Mock.Container.Resolve<IPathProvider>();
        var file = Mock.Container.Resolve<IFile>();
        Mock.Container.Resolve<IDirectory>().CreateDirectory(paths.ConfigDirectory);
        var json = UserSettingsSerializer.Serialize(persisted);
        file.WriteAllText(paths.ConfigFileLocation, json);

        // Act
        var result = Sut.Setup();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        settings.DateTimeSettings.TimeZone.ShouldBe("Asia/Kathmandu");
        settings.DownloadManagerSettings.DownloadSchedule.Enabled.ShouldBeTrue();
        settings.DownloadManagerSettings.DownloadSchedule.Days.Keys.ShouldBe(["Monday"]);
        settings
            .DownloadManagerSettings.DownloadSchedule.Days["Monday"]
            .ShouldBe(new Dictionary<string, int?> { ["09:30"] = 123 }, ignoreOrder: true);
        settings.ServerSettings.GetDownloadSpeedLimit("retained").ShouldBe(200);
        file.ReadAllText(paths.ConfigFileLocation).ShouldBe(json);
    }

    [Test]
    public void ShouldCreateConfigFile_WhenConfigFileDoesNotExists()
    {
        // Arrange
        SetupFileSystem();
        var settings = new UserSettings();
        settings.DateTimeSettings.TimeZone = "America/New_York";
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Sunday"] = new() { ["23:30"] = 321 } },
        };
        settings.ServerSettings.SetDownloadSpeedLimit("retained", 200);
        SetupDependencies(builder => builder.RegisterInstance(settings).As<IUserSettings>());
        var path = Mock.Container.Resolve<IPathProvider>().ConfigFileLocation;
        var file = Mock.Container.Resolve<IFile>();

        // Act
        var result = Sut.Setup();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var saved = UserSettingsSerializer.Deserialize(file.ReadAllText(path));
        saved.DateTimeSettings.TimeZone.ShouldBe("America/New_York");
        saved.DownloadManagerSettings.DownloadSchedule.Enabled.ShouldBeTrue();
        saved.DownloadManagerSettings.DownloadSchedule.Days.Keys.ShouldBe(["Sunday"]);
        saved
            .DownloadManagerSettings.DownloadSchedule.Days["Sunday"]
            .ShouldBe(new Dictionary<string, int?> { ["23:30"] = 321 }, ignoreOrder: true);
        saved.ServerSettings.GetDownloadSpeedLimit("retained").ShouldBe(200);
    }
}
