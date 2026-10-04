using System.IO.Abstractions;
using Autofac;
using Reaparr.Environment;
using Reaparr.Settings.Contracts;

namespace Reaparr.Settings.UnitTests;

public class ConfigManagerSaveConfigUnitTests : BaseUnitTest<ConfigManager>
{
    [Test]
    public void ShouldPersistCurrentSettings_WhenTheConfigurationWriteSucceeds()
    {
        // Arrange
        SetupFileSystem();
        var settings = new UserSettings
        {
            DateTimeSettings = { TimeZone = "Asia/Kathmandu" },
            DownloadManagerSettings =
            {
                DownloadSchedule = new DownloadSchedule
                {
                    Enabled = true,
                    Days = new()
                    {
                        ["Monday"] = new() { ["09:30"] = 123, ["17:00"] = null },
                    },
                },
            },
        };
        settings.ServerSettings.SetDownloadSpeedLimit("retained", 200);
        SetupDependencies(builder => builder.RegisterInstance(settings).As<IUserSettings>());
        var path = Mock.Container.Resolve<IPathProvider>().ConfigFileLocation;
        var file = Mock.Container.Resolve<IFile>();

        // Act
        var result = Sut.SaveConfig();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var saved = UserSettingsSerializer.Deserialize(file.ReadAllText(path));
        saved.DateTimeSettings.TimeZone.ShouldBe("Asia/Kathmandu");
        saved.DownloadManagerSettings.DownloadSchedule.Enabled.ShouldBeTrue();
        saved.DownloadManagerSettings.DownloadSchedule.Days.Keys.ShouldBe(["Monday"]);
        saved
            .DownloadManagerSettings.DownloadSchedule.Days["Monday"]
            .ShouldBe(new Dictionary<string, int?> { ["09:30"] = 123, ["17:00"] = null }, ignoreOrder: true);
        saved.ServerSettings.GetDownloadSpeedLimit("retained").ShouldBe(200);
    }

    [Test]
    public void ShouldReturnFailure_WhenTheConfigurationWriteFails()
    {
        // Arrange
        var settings = new UserSettings { DateTimeSettings = { TimeZone = "Asia/Kathmandu" } };
        var file = new Mock<IFile>(MockBehavior.Strict);
        SetupDependencies(builder =>
        {
            builder.RegisterInstance(settings).As<IUserSettings>();
            builder.RegisterInstance(file.Object).As<IFile>();
        });
        var path = Mock.Container.Resolve<IPathProvider>().ConfigFileLocation;
        var expectedJson = UserSettingsSerializer.Serialize(settings);
        file.Setup(x => x.WriteAllText(path, expectedJson))
            .Throws(new IOException("write failed"))
            .Verifiable(Times.Once());

        // Act
        var result = Sut.SaveConfig();

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        file.Verify();
    }
}
