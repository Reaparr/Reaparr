using System.IO.Abstractions;
using System.Reactive.Linq;
using Autofac;
using Reaparr.Environment;
using Reaparr.Settings.Contracts;

namespace Reaparr.Settings.UnitTests;

public class ConfigManagerSaveConfigUnitTests : BaseUnitTest<ConfigManager>
{
    [Test]
    public void ShouldPublishSavedConfiguration_AfterTheConfigurationWriteSucceeds()
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
                        ["monday"] = new() { ["09:30"] = 123, ["17:00"] = null },
                    },
                },
            },
        };
        settings.ServerSettings.SetDownloadSpeedLimit("retained", 200);
        SetupDependencies(builder => builder.RegisterInstance(settings).As<IUserSettings>());
        var path = Mock.Container.Resolve<IPathProvider>().ConfigFileLocation;
        var observed = new List<UserSettings>();
        var file = Mock.Container.Resolve<IFile>();
        var sut = Sut;
        using var subscription = sut.SettingsSaved.Subscribe(_ =>
            observed.Add(UserSettingsSerializer.Deserialize(file.ReadAllText(path)))
        );

        // Act
        var result = sut.SaveConfig();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        observed.Count.ShouldBe(1);
        var saved = observed[0];
        saved.DateTimeSettings.TimeZone.ShouldBe("Asia/Kathmandu");
        saved.DownloadManagerSettings.DownloadSchedule.Enabled.ShouldBeTrue();
        saved.DownloadManagerSettings.DownloadSchedule.Days.Keys.ShouldBe(["monday"]);
        saved
            .DownloadManagerSettings.DownloadSchedule.Days["monday"]
            .ShouldBe(new Dictionary<string, int?> { ["09:30"] = 123, ["17:00"] = null }, ignoreOrder: true);
        saved.ServerSettings.GetDownloadSpeedLimit("retained").ShouldBe(200);
    }

    [Test]
    public void ShouldNotPublishSettingsSaved_WhenTheConfigurationWriteFails()
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
        var notifications = 0;
        var sut = Sut;
        using var subscription = sut.SettingsSaved.Subscribe(_ => notifications++);
        file.Setup(x => x.WriteAllText(path, It.IsAny<string>()))
            .Throws(new IOException("write failed"))
            .Verifiable(Times.Once());

        // Act
        var result = sut.SaveConfig();

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        notifications.ShouldBe(0);
        file.Verify();
    }
}
