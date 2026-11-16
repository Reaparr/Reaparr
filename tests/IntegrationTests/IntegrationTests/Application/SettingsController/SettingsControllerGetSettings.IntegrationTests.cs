using Reaparr.Settings;
using Reaparr.Settings.Contracts;

namespace Reaparr.IntegrationTests;

public class SettingsControllerGetSettingsIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldHaveDefaultSettings_OnFirstTimeBoot()
    {
        // Arrange
        using var container = await CreateContainer(4564);

        // Act
        var client = container.GetApiClient();
        await client.SignIn();

        var response = await client.GETAsync<GetUserSettingsEndpoint, ResultDTO<SettingsModelDTO>>();
        response.Response.IsSuccessStatusCode.ShouldBeTrue();

        // Assert
        var result = response.Result;
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        var settingsModel = result.Value.ToModel();
        var expected = new UserSettings();

        // Assert deterministic modules match defaults
        settingsModel.AuthenticationSettings.ShouldBeEquivalentTo(expected.AuthenticationSettings);
        settingsModel.GeneralSettings.ShouldBeEquivalentTo(expected.GeneralSettings);
        settingsModel.ConfirmationSettings.ShouldBeEquivalentTo(expected.ConfirmationSettings);
        settingsModel.DateTimeSettings.ShouldBeEquivalentTo(expected.DateTimeSettings);
        settingsModel.DisplaySettings.ShouldBeEquivalentTo(expected.DisplaySettings);
        settingsModel.DownloadManagerSettings.ShouldBeEquivalentTo(expected.DownloadManagerSettings);
        settingsModel.LanguageSettings.ShouldBeEquivalentTo(expected.LanguageSettings);
        settingsModel.DebugSettings.ShouldBeEquivalentTo(expected.DebugSettings);
        settingsModel.ServerSettings.ShouldBeEquivalentTo(expected.ServerSettings);

        // IntegrationsSettings has randomized fields; assert invariants and deterministic fields
        settingsModel.IntegrationsSettings.Sonarr.ShouldBeEquivalentTo(expected.IntegrationsSettings.Sonarr);
        settingsModel.IntegrationsSettings.Radarr.ShouldBeEquivalentTo(expected.IntegrationsSettings.Radarr);
        settingsModel.IntegrationsSettings.DownloadClientUsername.ShouldBe(
            expected.IntegrationsSettings.DownloadClientUsername
        );
        var parsedApiKey = Guid.Parse(settingsModel.IntegrationsSettings.ReaparrApiKey);
        parsedApiKey.ShouldNotBe(Guid.Empty);
        settingsModel.IntegrationsSettings.DownloadClientPassword.Length.ShouldBe(32);
    }
}
