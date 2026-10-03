using Reaparr.Settings.Contracts;

namespace Reaparr.Settings.UnitTests;

public class UserSettingsSerializerUnitTests : BaseUnitTest
{
    [Test]
    [Arguments("")]
    [Arguments("{}")]
    public void ShouldSetAllDefaultValues_WhenAnEmptyJSONStringIsGiven(string json)
    {
        // Act
        var result = UserSettingsSerializer.Deserialize(json);

        // Assert
        result.ShouldNotBeNull();

        var defaultSettings = new UserSettings();
        result.GeneralSettings.ShouldBeEquivalentTo(defaultSettings.GeneralSettings);
        result.ConfirmationSettings.ShouldBeEquivalentTo(defaultSettings.ConfirmationSettings);
        result.DateTimeSettings.ShouldBeEquivalentTo(defaultSettings.DateTimeSettings);
        result.DisplaySettings.ShouldBeEquivalentTo(defaultSettings.DisplaySettings);
        result.DownloadManagerSettings.ShouldBeEquivalentTo(defaultSettings.DownloadManagerSettings);
        result.LanguageSettings.ShouldBeEquivalentTo(defaultSettings.LanguageSettings);
        result.DebugSettings.ShouldBeEquivalentTo(defaultSettings.DebugSettings);
        result.ServerSettings.Data.ShouldBeEquivalentTo(defaultSettings.ServerSettings.Data);
    }

    [Test]
    public void ShouldHaveCorrectSettingsValuesParsed_WhenAValidJSONStringHasBeenGiven()
    {
        // Arrange
        var settingsJson =
            "{\"GeneralSettings\":{\"FirstTimeSetup\":false,\"ActiveAccountId\":0,\"DebugMode\":false,\"DisableAnimatedBackground\":false},\"ConfirmationSettings\":{\"AskDownloadMovieConfirmation\":false,\"AskDownloadTvShowConfirmation\":false,\"AskDownloadSeasonConfirmation\":true,\"AskDownloadEpisodeConfirmation\":true},\"DateTimeSettings\":{\"ShortDateFormat\":\"dd/MM/yyyy\",\"LongDateFormat\":\"EEEE, dd MMMM yyyy\",\"TimeFormat\":\"HH:mm:ss\",\"TimeZone\":\"UTC\",\"ShowRelativeDates\":true},\"DisplaySettings\":{\"TvShowViewMode\":\"Poster\",\"MovieViewMode\":\"Poster\"},\"DownloadManagerSettings\":{\"DownloadSegments\":4},\"LanguageSettings\":{\"Language\":\"nl-NL\"},\"ServerSettings\":{\"Data\":[{\"PlexServerName\":\"\",\"MachineIdentifier\":\"70799fbb07f8a4268bc2b443ac63e6e0ca6b81c8\",\"DownloadSpeedLimit\":0,\"Hidden\":false},{\"PlexServerName\":\"\",\"MachineIdentifier\":\"e9439c968d0b9f6ad634369d7bb90f3cf87b1b5\",\"DownloadSpeedLimit\":0,\"Hidden\":false},{\"PlexServerName\":\"\",\"MachineIdentifier\":\"f43caadf1346a7134e138ec89ed4e721c4033033\",\"DownloadSpeedLimit\":0,\"Hidden\":true},{\"PlexServerName\":\"\",\"MachineIdentifier\":\"94c791bac4a4a0f7dc3e98c91f14f42e03207bb1\",\"DownloadSpeedLimit\":0,\"Hidden\":false}]},\"DebugSettings\":{\"DebugModeEnabled\":false,\"MaskServerNames\":false,\"MaskLibraryNames\":false}}";

        // Act
        var sut = UserSettingsSerializer.Deserialize(settingsJson);

        // Assert
        sut.GeneralSettings.FirstTimeSetup.ShouldBeFalse();
        sut.GeneralSettings.ActiveAccountId.ShouldBe(0);
        sut.GeneralSettings.DisableAnimatedBackground.ShouldBeFalse();

        sut.ConfirmationSettings.AskDownloadMovieConfirmation.ShouldBeFalse();
        sut.ConfirmationSettings.AskDownloadTvShowConfirmation.ShouldBeFalse();
        sut.ConfirmationSettings.AskDownloadSeasonConfirmation.ShouldBeTrue();
        sut.ConfirmationSettings.AskDownloadEpisodeConfirmation.ShouldBeTrue();

        sut.DateTimeSettings.ShortDateFormat.ShouldBe("dd/MM/yyyy");
        sut.DateTimeSettings.LongDateFormat.ShouldBe("EEEE, dd MMMM yyyy");
        sut.DateTimeSettings.TimeFormat.ShouldBe("HH:mm:ss");
        sut.DateTimeSettings.TimeZone.ShouldBe("UTC");

        sut.DisplaySettings.TvShowViewMode.ShouldBe(ViewMode.Poster);
        sut.DisplaySettings.MovieViewMode.ShouldBe(ViewMode.Poster);

        sut.DownloadManagerSettings.DownloadSegments.ShouldBe(4);

        sut.LanguageSettings.Language.ShouldBe("nl-NL");

        sut.ServerSettings.Data.Count.ShouldBe(4);
        sut.ServerSettings.Data[0].PlexServerName.ShouldBeEmpty();
        sut.ServerSettings.Data[0].MachineIdentifier.ShouldBe("70799fbb07f8a4268bc2b443ac63e6e0ca6b81c8");
        sut.ServerSettings.Data[0].DownloadSpeedLimit.ShouldBe(0);
        sut.ServerSettings.Data[1].PlexServerName.ShouldBeEmpty();
        sut.ServerSettings.Data[1].MachineIdentifier.ShouldBe("e9439c968d0b9f6ad634369d7bb90f3cf87b1b5");
        sut.ServerSettings.Data[1].DownloadSpeedLimit.ShouldBe(0);
        sut.ServerSettings.Data[2].PlexServerName.ShouldBeEmpty();
        sut.ServerSettings.Data[2].MachineIdentifier.ShouldBe("f43caadf1346a7134e138ec89ed4e721c4033033");
        sut.ServerSettings.Data[2].DownloadSpeedLimit.ShouldBe(0);
        sut.ServerSettings.Data[3].PlexServerName.ShouldBeEmpty();
        sut.ServerSettings.Data[3].MachineIdentifier.ShouldBe("94c791bac4a4a0f7dc3e98c91f14f42e03207bb1");
        sut.ServerSettings.Data[3].DownloadSpeedLimit.ShouldBe(0);
    }

    [Test]
    public void ShouldHaveCorrectSettingsValuesParsed_WhenAInValidJSONStringHasBeenGiven()
    {
        // Arrange
        var settingsJson =
            "{\"GeneralSettings\":{\"FirstTimeSetup\":false,\"ActiveAccountId\":0,\"DisableAnimatedBackground\":false},\"ConfirmationSettings\":{\"AskDownloadTvShowConfirmation\":false,\"AskDownloadSeasonConfirmation\":true},\"DateTimeSettings\":{\"ShortDateFormat\":\"dd/MM/yyyy\",\"TimeFormat\":\"HH:mm:ss\",\"TimeZone\":\"UTC\"},\"DisplaySettings\":{\"MovieViewMode\":\"Poster\"},\"DownloadManagerSettings\":{\"DownloadSegments\":4},\"ServerSettings\":{\"Data\":[{\"PlexServerName\":\"\",\"MachineIdentifier\":\"70799fbb07f8a4268bc2b443ac63e6e0ca6b81c8\"},{\"PlexServerName\":\"\",\"MachineIdentifier\":\"e9439c968d0b9f6ad634369d7bb90f3cf87b1b5\",\"Hidden\":false},{\"PlexServerName\":\"\",\"MachineIdentifier\":\"f43caadf1346a7134e138ec89ed4e721c4033033\",\"Hidden\":true}]}}";

        // Act
        var sut = UserSettingsSerializer.Deserialize(settingsJson);

        // Assert
        sut.GeneralSettings.FirstTimeSetup.ShouldBeFalse();
        sut.GeneralSettings.ActiveAccountId.ShouldBe(0);
        sut.GeneralSettings.DisableAnimatedBackground.ShouldBeFalse();

        sut.ConfirmationSettings.AskDownloadMovieConfirmation.ShouldBeTrue();
        sut.ConfirmationSettings.AskDownloadTvShowConfirmation.ShouldBeFalse();
        sut.ConfirmationSettings.AskDownloadSeasonConfirmation.ShouldBeTrue();
        sut.ConfirmationSettings.AskDownloadEpisodeConfirmation.ShouldBeTrue();

        sut.DateTimeSettings.ShortDateFormat.ShouldBe("dd/MM/yyyy");
        sut.DateTimeSettings.LongDateFormat.ShouldBe("EEEE, dd MMMM yyyy");
        sut.DateTimeSettings.TimeFormat.ShouldBe("HH:mm:ss");
        sut.DateTimeSettings.TimeZone.ShouldBe("UTC");

        sut.DisplaySettings.TvShowViewMode.ShouldBe(ViewMode.Poster);
        sut.DisplaySettings.MovieViewMode.ShouldBe(ViewMode.Poster);

        sut.DownloadManagerSettings.DownloadSegments.ShouldBe(4);

        sut.LanguageSettings.Language.ShouldBe("en-US");

        sut.ServerSettings.Data.Count.ShouldBe(3);
        sut.ServerSettings.Data[0].PlexServerName.ShouldBeEmpty();
        sut.ServerSettings.Data[0].MachineIdentifier.ShouldBe("70799fbb07f8a4268bc2b443ac63e6e0ca6b81c8");
        sut.ServerSettings.Data[0].DownloadSpeedLimit.ShouldBe(0);
        sut.ServerSettings.Data[1].PlexServerName.ShouldBeEmpty();
        sut.ServerSettings.Data[1].MachineIdentifier.ShouldBe("e9439c968d0b9f6ad634369d7bb90f3cf87b1b5");
        sut.ServerSettings.Data[1].DownloadSpeedLimit.ShouldBe(0);
        sut.ServerSettings.Data[2].PlexServerName.ShouldBeEmpty();
        sut.ServerSettings.Data[2].MachineIdentifier.ShouldBe("f43caadf1346a7134e138ec89ed4e721c4033033");
        sut.ServerSettings.Data[2].DownloadSpeedLimit.ShouldBe(0);
    }

    [Test]
    public void ShouldKeepDailyChangePointsAndManualCaps_WhenSettingsAreReloaded()
    {
        // Arrange
        var settings = new UserSettings();
        settings.DateTimeSettings.TimeZone = "Asia/Kathmandu";
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new()
            {
                ["Monday"] = new() { ["09:00"] = 123, ["17:30"] = null },
                ["Sunday"] = new() { ["23:30"] = int.MaxValue },
            },
        };
        settings.ServerSettings.SetDownloadSpeedLimit("retained", 200);

        // Act
        var restored = UserSettingsSerializer.Deserialize(UserSettingsSerializer.Serialize(settings));

        // Assert
        restored.DateTimeSettings.TimeZone.ShouldBe("Asia/Kathmandu");
        restored.DownloadManagerSettings.DownloadSchedule.Enabled.ShouldBeTrue();
        var days = restored.DownloadManagerSettings.DownloadSchedule.Days;
        days.Keys.ShouldBe(settings.DownloadManagerSettings.DownloadSchedule.Days.Keys, ignoreOrder: true);
        foreach (var (day, points) in settings.DownloadManagerSettings.DownloadSchedule.Days)
            days[day].ShouldBe(points, ignoreOrder: true);
        restored.ServerSettings.GetDownloadSpeedLimit("retained").ShouldBe(200);
    }

    [Test]
    public void ShouldKeepLegacyServerCapsWithoutIntroducingAWeeklyBudget_WhenScheduleDataIsAbsent()
    {
        // Arrange
        var settings = new UserSettings();
        settings.ServerSettings.SetDownloadSpeedLimit("retained", 200);
        var document = System.Text.Json.Nodes.JsonNode.Parse(UserSettingsSerializer.Serialize(settings))!;
        document["DownloadManagerSettings"]!.AsObject().Remove("DownloadSchedule");

        // Act
        var restored = UserSettingsSerializer.Deserialize(document.ToJsonString());

        // Assert
        restored.DownloadManagerSettings.DownloadSchedule.Enabled.ShouldBeFalse();
        restored.DownloadManagerSettings.DownloadSchedule.Days.ShouldBeEmpty();
        restored.ServerSettings.GetDownloadSpeedLimit("retained").ShouldBe(200);
    }
}
