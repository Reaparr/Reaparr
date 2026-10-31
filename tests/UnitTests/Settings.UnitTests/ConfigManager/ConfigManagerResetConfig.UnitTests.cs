using System.IO.Abstractions;
using Autofac;
using Reaparr.Environment;
using Reaparr.Settings.Contracts;

namespace Reaparr.Settings.UnitTests;

public class ConfigManagerResetConfigUnitTests : BaseUnitTest<ConfigManager>
{
    [Test]
    [Arguments(nameof(ConfigManager.ResetConfig))]
    [Arguments(nameof(ConfigManager.LoadConfig))]
    [Arguments(nameof(ConfigManager.Setup))]
    public void ShouldPersistResetSettings_WhenResetOrParseRecoverySucceeds(string entryPoint)
    {
        // Arrange
        SetupFileSystem();
        var settings = new UserSettings();
        settings.DateTimeSettings.TimeZone = "Asia/Kathmandu";
        SetupDependencies(builder => builder.RegisterInstance(settings).As<IUserSettings>());
        var paths = Mock.Container.Resolve<IPathProvider>();
        var file = Mock.Container.Resolve<IFile>();
        Mock.Container.Resolve<IDirectory>().CreateDirectory(paths.ConfigDirectory);
        const string originalContents = "{ invalid settings";
        file.WriteAllText(paths.ConfigFileLocation, originalContents);
        var defaultTimeZone = DateTimeSettingsModule.Create().TimeZone;
        settings.DateTimeSettings.TimeZone.ShouldBe("Asia/Kathmandu");
        file.ReadAllText(paths.ConfigFileLocation).ShouldBe(originalContents);

        // Act
        var result = entryPoint switch
        {
            nameof(ConfigManager.ResetConfig) => Sut.ResetConfig(),
            nameof(ConfigManager.LoadConfig) => Sut.LoadConfig(),
            _ => Sut.Setup(),
        };

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        settings.DateTimeSettings.TimeZone.ShouldBe(defaultTimeZone);
        file.ReadAllText(paths.ConfigFileLocation).ShouldBe(UserSettingsSerializer.Serialize(settings));
    }

    [Test]
    [Arguments(nameof(ConfigManager.ResetConfig), false)]
    [Arguments(nameof(ConfigManager.LoadConfig), false)]
    [Arguments(nameof(ConfigManager.LoadConfig), true)]
    [Arguments(nameof(ConfigManager.Setup), false)]
    [Arguments(nameof(ConfigManager.Setup), true)]
    public void ShouldFailRecoveryAndPreserveWriteError_WhenResetCannotSave(string entryPoint, bool readFails)
    {
        // Arrange
        SetupFileSystem();
        var paths = Mock.Container.Resolve<IPathProvider>();
        var storedFile = Mock.Container.Resolve<IFile>();
        var directory = Mock.Container.Resolve<IDirectory>();
        directory.CreateDirectory(paths.ConfigDirectory);
        const string originalContents = "{ invalid settings";
        storedFile.WriteAllText(paths.ConfigFileLocation, originalContents);
        var writeFailure = new UnauthorizedAccessException("Config directory is not writable");
        var file = new Mock<IFile>();
        file.Setup(x => x.Exists(It.IsAny<string>())).Returns<string>(storedFile.Exists);
        if (readFails)
            file.Setup(x => x.ReadAllText(paths.ConfigFileLocation)).Throws(new IOException("Config cannot be read"));
        else
            file.Setup(x => x.ReadAllText(paths.ConfigFileLocation)).Returns(originalContents);
        file.Setup(x => x.WriteAllText(paths.ConfigFileLocation, It.IsAny<string>())).Throws(writeFailure);
        var manager = new ConfigManager(
            Log,
            paths,
            new UserSettings(),
            file.Object,
            Mock.Container.Resolve<IPath>(),
            directory
        );

        // Act
        var result = entryPoint switch
        {
            nameof(ConfigManager.ResetConfig) => manager.ResetConfig(),
            nameof(ConfigManager.LoadConfig) => manager.LoadConfig(),
            _ => manager.Setup(),
        };

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(3);
        result.Errors.OfType<ExceptionalError>().Single().Exception.ShouldBeSameAs(writeFailure);
        result.Errors.ShouldContain(x => x.Message == "Failed to save a new config after resetting");
        storedFile.ReadAllText(paths.ConfigFileLocation).ShouldBe(originalContents);
    }
}
