using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Reaparr.Settings.Contracts;

namespace Reaparr.Settings.UnitTests;

public class UserSettingsResetUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldPublishCurrentModuleChanges_WhenSettingsChangeAfterReset()
    {
        // Arrange
        var sut = new UserSettings();
        var language = sut.LanguageSettings;
        var oldApiKey = sut.IntegrationsSettings.ReaparrApiKey;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        // Resume outside the producer callback before sending the next throttled change.
        var resetNotification = sut.SettingsUpdated.ObserveOn(TaskPoolScheduler.Default).Take(1).ToTask(timeout.Token);

        // Act
        sut.Reset();
        await resetNotification;
        var changedNotification = sut.SettingsUpdated.Select(x => x.ToDTO()).Take(1).ToTask(timeout.Token);
        language.Language = "fr-FR";
        var published = await changedNotification;

        // Assert
        published.LanguageSettings.Language.ShouldBe("fr-FR");
        sut.LanguageSettings.Language.ShouldBe("fr-FR");
        sut.LanguageSettings.ShouldBeSameAs(language);
        sut.IntegrationsSettings.ReaparrApiKey.ShouldNotBe(oldApiKey);
        Guid.TryParse(sut.IntegrationsSettings.ReaparrApiKey, out _).ShouldBeTrue();
    }

    [Test]
    public void ShouldHaveDefaultSettingsValues_WhenResetHasBeenCalled()
    {
        // Arrange
        UserSettings sut = new();

        // Act
        var changedSettings = new UserSettings
        {
            DateTimeSettings = DateTimeSettingsModule.Create(),
            ConfirmationSettings = new ConfirmationSettingsModule
            {
                AskDownloadEpisodeConfirmation = false,
                AskDownloadMovieConfirmation = false,
                AskDownloadSeasonConfirmation = false,
                AskDownloadTvShowConfirmation = false,
            },
            LanguageSettings = new LanguageSettingsModule { Language = string.Empty },
            DisplaySettings = DisplaySettingsModule.Create(),
            GeneralSettings = GeneralSettingsModule.Create(),
            ServerSettings = PlexServerSettingsModule.Create(),
            DownloadManagerSettings = DownloadManagerSettingsModule.Create(),
            IntegrationsSettings = IntegrationsSettings.Create(),
            AuthenticationSettings = AuthenticationModule.Create(),
            DebugSettings = DebugSettingsModule.Create(),
        };
        sut.UpdateSettings(changedSettings);
        sut.Reset();

        // Assert
        var expected = new UserSettings();

        // Assert deterministic modules against fresh defaults
        sut.AuthenticationSettings.ShouldBeEquivalentTo(expected.AuthenticationSettings);
        sut.GeneralSettings.ShouldBeEquivalentTo(expected.GeneralSettings);
        sut.ConfirmationSettings.ShouldBeEquivalentTo(expected.ConfirmationSettings);
        sut.DateTimeSettings.ShouldBeEquivalentTo(expected.DateTimeSettings);
        sut.DisplaySettings.ShouldBeEquivalentTo(expected.DisplaySettings);
        sut.DownloadManagerSettings.ShouldBeEquivalentTo(expected.DownloadManagerSettings);
        sut.LanguageSettings.ShouldBeEquivalentTo(expected.LanguageSettings);
        sut.DebugSettings.ShouldBeEquivalentTo(expected.DebugSettings);
        sut.ServerSettings.ShouldBeEquivalentTo(expected.ServerSettings);

        // IntegrationsSettings contains randomized values for some fields; assert invariants instead
        sut.IntegrationsSettings.Sonarr.ShouldBeEquivalentTo(expected.IntegrationsSettings.Sonarr);
        sut.IntegrationsSettings.Radarr.ShouldBeEquivalentTo(expected.IntegrationsSettings.Radarr);
        Guid.TryParse(sut.IntegrationsSettings.ReaparrApiKey, out _).ShouldBeTrue();
    }
}
