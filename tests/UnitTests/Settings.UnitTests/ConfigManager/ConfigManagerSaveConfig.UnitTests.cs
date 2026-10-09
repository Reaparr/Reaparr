using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json.Nodes;
using Autofac;
using Reaparr.Environment;
using Reaparr.Settings.Contracts;

namespace Reaparr.Settings.UnitTests;

public class ConfigManagerSaveConfigUnitTests : BaseUnitTest<ConfigManager>
{
    [Test]
    [Arguments(ViewMode.Table, ViewMode.Poster, ViewMode.Poster, true, false, true, false, true, false)]
    [Arguments(ViewMode.Poster, ViewMode.Table, ViewMode.Poster, false, true, true, false, false, true)]
    [Arguments(ViewMode.Poster, ViewMode.Poster, ViewMode.Table, false, false, false, true, true, true)]
    public void ShouldPersistIndependentFamilyPreferences_WhenApiSettingsAreSavedAndReloaded(
        ViewMode musicView,
        ViewMode photoView,
        ViewMode otherVideosView,
        bool artistConfirmation,
        bool albumConfirmation,
        bool trackConfirmation,
        bool photoAlbumConfirmation,
        bool photoImageConfirmation,
        bool otherVideosConfirmation
    )
    {
        // Arrange
        var settings = new UserSettings();
        settings.GeneralSettings.FirstTimeSetup = false;
        settings.LanguageSettings.Language = "fr-FR";
        settings.DisplaySettings.MovieViewMode = ViewMode.Table;
        settings.DisplaySettings.TvShowViewMode = ViewMode.Poster;
        settings.ConfirmationSettings.AskDownloadMovieConfirmation = false;
        settings.ConfirmationSettings.AskDownloadTvShowConfirmation = true;
        settings.ConfirmationSettings.AskDownloadSeasonConfirmation = false;
        settings.ConfirmationSettings.AskDownloadEpisodeConfirmation = false;
        var request = settings.ToDTO();
        request.DisplaySettings.MusicArtistViewMode = musicView;
        request.DisplaySettings.PhotoAlbumViewMode = photoView;
        request.DisplaySettings.OtherVideosViewMode = otherVideosView;
        request.ConfirmationSettings.AskDownloadMusicArtistConfirmation = artistConfirmation;
        request.ConfirmationSettings.AskDownloadMusicAlbumConfirmation = albumConfirmation;
        request.ConfirmationSettings.AskDownloadMusicTrackConfirmation = trackConfirmation;
        request.ConfirmationSettings.AskDownloadPhotoAlbumConfirmation = photoAlbumConfirmation;
        request.ConfirmationSettings.AskDownloadPhotoImageConfirmation = photoImageConfirmation;
        request.ConfirmationSettings.AskDownloadOtherVideosConfirmation = otherVideosConfirmation;
        SetupDependencies(builder => builder.RegisterInstance<IUserSettings>(settings));
        SetupFileSystem(system => system.AddDirectory(Mock.Container.Resolve<IPathProvider>().ConfigDirectory));
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var paths = Mock.Container.Resolve<IPathProvider>();
        fileSystem.File.Exists(paths.ConfigFileLocation).ShouldBeFalse();
        var loadedSettings = new UserSettings();
        var reader = new ConfigManager(
            Mock.Container.Resolve<ILogger>(),
            paths,
            loadedSettings,
            fileSystem.File,
            fileSystem.Path,
            fileSystem.Directory
        );

        // Act
        settings.UpdateSettings(request.ToModel());
        var saveResult = Sut.SaveConfig();
        var loadResult = reader.LoadConfig();

        // Assert
        saveResult.IsSuccess.ShouldBeTrue();
        saveResult.Errors.Count.ShouldBe(0);
        loadResult.IsSuccess.ShouldBeTrue();
        loadResult.Errors.Count.ShouldBe(0);
        fileSystem.File.Exists(paths.ConfigFileLocation).ShouldBeTrue();
        loadedSettings.ToDTO().ShouldBeEquivalentTo(request);
    }

    [Test]
    public void ShouldPreserveExistingPreferencesAndDefaultNewFamilies_WhenLoadingLegacyConfig()
    {
        // Arrange
        var original = new UserSettings();
        original.GeneralSettings.FirstTimeSetup = false;
        original.LanguageSettings.Language = "fr-FR";
        original.DisplaySettings.MovieViewMode = ViewMode.Table;
        original.DisplaySettings.TvShowViewMode = ViewMode.Table;
        original.ConfirmationSettings.AskDownloadMovieConfirmation = false;
        original.ConfirmationSettings.AskDownloadTvShowConfirmation = false;
        original.ConfirmationSettings.AskDownloadSeasonConfirmation = false;
        original.ConfirmationSettings.AskDownloadEpisodeConfirmation = false;
        var expected = original.ToDTO();
        var json = JsonNode.Parse(UserSettingsSerializer.Serialize(original))!.AsObject();
        var display = json[nameof(UserSettings.DisplaySettings)]!.AsObject();
        display.Remove(nameof(DisplaySettingsModule.MusicArtistViewMode));
        display.Remove(nameof(DisplaySettingsModule.PhotoAlbumViewMode));
        display.Remove(nameof(DisplaySettingsModule.OtherVideosViewMode));
        var confirmations = json[nameof(UserSettings.ConfirmationSettings)]!.AsObject();
        confirmations.Remove(nameof(ConfirmationSettingsModule.AskDownloadMusicArtistConfirmation));
        confirmations.Remove(nameof(ConfirmationSettingsModule.AskDownloadMusicAlbumConfirmation));
        confirmations.Remove(nameof(ConfirmationSettingsModule.AskDownloadMusicTrackConfirmation));
        confirmations.Remove(nameof(ConfirmationSettingsModule.AskDownloadPhotoAlbumConfirmation));
        confirmations.Remove(nameof(ConfirmationSettingsModule.AskDownloadPhotoImageConfirmation));
        confirmations.Remove(nameof(ConfirmationSettingsModule.AskDownloadOtherVideosConfirmation));
        var legacyJson = json.ToJsonString();
        var loadedSettings = new UserSettings();
        SetupDependencies(builder => builder.RegisterInstance<IUserSettings>(loadedSettings));
        SetupFileSystem(system =>
        {
            var paths = Mock.Container.Resolve<IPathProvider>();
            system.AddDirectory(paths.ConfigDirectory);
            system.AddFile(paths.ConfigFileLocation, new MockFileData(legacyJson));
        });
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var configPath = Mock.Container.Resolve<IPathProvider>().ConfigFileLocation;
        fileSystem.File.ReadAllText(configPath).ShouldBe(legacyJson);

        // Act
        var result = Sut.LoadConfig();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        loadedSettings.ToDTO().ShouldBeEquivalentTo(expected);
        loadedSettings.DisplaySettings.MusicArtistViewMode.ShouldBe(ViewMode.Poster);
        loadedSettings.DisplaySettings.PhotoAlbumViewMode.ShouldBe(ViewMode.Poster);
        loadedSettings.DisplaySettings.OtherVideosViewMode.ShouldBe(ViewMode.Poster);
        loadedSettings.ConfirmationSettings.AskDownloadMusicArtistConfirmation.ShouldBeTrue();
        loadedSettings.ConfirmationSettings.AskDownloadMusicAlbumConfirmation.ShouldBeTrue();
        loadedSettings.ConfirmationSettings.AskDownloadMusicTrackConfirmation.ShouldBeTrue();
        loadedSettings.ConfirmationSettings.AskDownloadPhotoAlbumConfirmation.ShouldBeTrue();
        loadedSettings.ConfirmationSettings.AskDownloadPhotoImageConfirmation.ShouldBeTrue();
        loadedSettings.ConfirmationSettings.AskDownloadOtherVideosConfirmation.ShouldBeTrue();
        fileSystem.File.ReadAllText(configPath).ShouldBe(legacyJson);
    }

    [Test]
    public void ShouldReturnFailureWithoutChangingSettingsOrConfig_WhenWritingFails()
    {
        // Arrange
        var settings = new UserSettings();
        settings.DisplaySettings.MusicArtistViewMode = ViewMode.Table;
        settings.ConfirmationSettings.AskDownloadPhotoImageConfirmation = false;
        var expected = settings.ToDTO();
        var previousJson = UserSettingsSerializer.Serialize(new UserSettings());
        var writeJson = UserSettingsSerializer.Serialize(settings);
        var file = new Mock<IFile>(MockBehavior.Strict);
        SetupDependencies(builder =>
        {
            builder.RegisterInstance<IUserSettings>(settings);
            builder.RegisterInstance(file.Object).As<IFile>();
        });
        SetupFileSystem(system =>
        {
            var paths = Mock.Container.Resolve<IPathProvider>();
            system.AddDirectory(paths.ConfigDirectory);
            system.AddFile(paths.ConfigFileLocation, new MockFileData(previousJson));
        });
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        var configPath = Mock.Container.Resolve<IPathProvider>().ConfigFileLocation;
        fileSystem.File.ReadAllText(configPath).ShouldBe(previousJson);
        file.Setup(x => x.WriteAllText(configPath, writeJson))
            .Throws(new IOException("Config write denied"))
            .Verifiable(Times.Once());

        // Act
        var result = Sut.SaveConfig();

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        settings.ToDTO().ShouldBeEquivalentTo(expected);
        fileSystem.File.ReadAllText(configPath).ShouldBe(previousJson);
        file.Verify();
    }
}
