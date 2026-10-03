using Microsoft.Extensions.DependencyInjection;
using Reaparr.Settings;

namespace Reaparr.Application.UnitTests;

public class UpdateUserSettingsEndpointUnitTests
    : BaseEndpointUnitTest<UpdateUserSettingsEndpoint, UpdateUserSettingsEndpointRequest, SettingsModelDTO>
{
    [Test]
    public async Task ShouldRejectNullSettings_WhenNoSettingsBodyIsProvided()
    {
        // Arrange
        var settings = new UserSettings();
        var request = new UpdateUserSettingsEndpointRequest { SettingsModelDto = null };

        // Act
        var result = await TestEndpointHandleAsync(request, services => services.AddSingleton<IUserSettings>(settings));

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ValidationErrors.Count.ShouldBe(1);
        result.ValidationErrors[0].PropertyName.ShouldBe(nameof(UpdateUserSettingsEndpointRequest.SettingsModelDto));
        settings.DownloadManagerSettings.DownloadSchedule.Enabled.ShouldBeFalse();
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task ShouldRetainCurrentPolicy_WhenAChangePointLimitIsNonPositive(int invalidLimit)
    {
        // Arrange
        var settings = new UserSettings();
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Monday"] = new() { ["00:00"] = 100 } },
        };
        settings.ServerSettings.SetDownloadSpeedLimit("retained", 200);
        var dto = settings.ToDTO();
        dto.DownloadManagerSettings.DownloadSchedule.Days = new() { ["Monday"] = new() { ["09:30"] = invalidLimit } };

        // Act
        var result = await TestEndpointHandleAsync(
            new UpdateUserSettingsEndpointRequest { SettingsModelDto = dto },
            services => services.AddSingleton<IUserSettings>(settings)
        );

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ValidationErrors.Count.ShouldBe(1);
        settings.DownloadManagerSettings.DownloadSchedule.Enabled.ShouldBeTrue();
        settings.DownloadManagerSettings.DownloadSchedule.Days.Keys.ShouldBe(["Monday"]);
        settings
            .DownloadManagerSettings.DownloadSchedule.Days["Monday"]
            .ShouldBe(new Dictionary<string, int?> { ["00:00"] = 100 }, ignoreOrder: true);
        settings.ServerSettings.GetDownloadSpeedLimit("retained").ShouldBe(200);
    }

    [Test]
    [Arguments("monday", "09:30")]
    [Arguments("funday", "09:30")]
    [Arguments("Monday", "9:30")]
    [Arguments("Monday", "24:00")]
    [Arguments("Monday", "09:15")]
    [Arguments("Monday", "09:60")]
    [Arguments("Monday", "")]
    public async Task ShouldRetainCurrentPolicy_WhenAChangePointKeyIsInvalid(string day, string time)
    {
        // Arrange
        var settings = new UserSettings();
        var dto = settings.ToDTO();
        dto.DownloadManagerSettings.DownloadSchedule.Enabled = true;
        dto.DownloadManagerSettings.DownloadSchedule.Days = new() { [day] = new() { [time] = 100 } };

        // Act
        var result = await TestEndpointHandleAsync(
            new UpdateUserSettingsEndpointRequest { SettingsModelDto = dto },
            services => services.AddSingleton<IUserSettings>(settings)
        );

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ValidationErrors.Count.ShouldBe(1);
        settings.DownloadManagerSettings.DownloadSchedule.Enabled.ShouldBeFalse();
        settings.DownloadManagerSettings.DownloadSchedule.Days.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldRejectAnUnknownUiClock_WithoutApplyingScheduleChanges()
    {
        // Arrange
        var settings = new UserSettings();
        var previousTimeZone = settings.DateTimeSettings.TimeZone;
        var dto = settings.ToDTO();
        dto.DateTimeSettings.TimeZone = "Not/A_Timezone";
        dto.DownloadManagerSettings.DownloadSchedule.Enabled = true;

        // Act
        var result = await TestEndpointHandleAsync(
            new UpdateUserSettingsEndpointRequest { SettingsModelDto = dto },
            services => services.AddSingleton<IUserSettings>(settings)
        );

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ValidationErrors.Count.ShouldBe(1);
        settings.DateTimeSettings.TimeZone.ShouldBe(previousTimeZone);
        settings.DownloadManagerSettings.DownloadSchedule.Enabled.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldUseTheUiTimezoneAndPreserveManualCaps_WhenTheScheduleIsUpdated()
    {
        // Arrange
        var settings = new UserSettings();
        settings.ServerSettings.SetDownloadSpeedLimit("retained", 200);
        settings.AuthenticationSettings.HeaderAuthentication.Enabled = true;
        settings.AuthenticationSettings.HeaderAuthentication.TrustedProxies = ["10.100.0.4"];
        var dto = settings.ToDTO();
        dto.DateTimeSettings.TimeZone = "Asia/Kathmandu";
        dto.DownloadManagerSettings.DownloadSchedule.Enabled = true;
        dto.DownloadManagerSettings.DownloadSchedule.Days = new()
        {
            ["Tuesday"] = new() { ["09:30"] = 123, ["17:00"] = null },
        };

        // Act
        var result = await TestEndpointHandleAsync(
            new UpdateUserSettingsEndpointRequest { SettingsModelDto = dto },
            services => services.AddSingleton<IUserSettings>(settings)
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.ValidationErrors.Count.ShouldBe(0);
        result.StatusCode.ShouldBe(200);
        settings.DateTimeSettings.TimeZone.ShouldBe("Asia/Kathmandu");
        settings.DownloadManagerSettings.DownloadSchedule.Enabled.ShouldBeTrue();
        settings.DownloadManagerSettings.DownloadSchedule.Days.Keys.ShouldBe(["Tuesday"]);
        settings
            .DownloadManagerSettings.DownloadSchedule.Days["Tuesday"]
            .ShouldBe(new Dictionary<string, int?> { ["09:30"] = 123, ["17:00"] = null }, ignoreOrder: true);
        settings.ServerSettings.GetDownloadSpeedLimit("retained").ShouldBe(200);
        settings.AuthenticationSettings.HeaderAuthentication.Enabled.ShouldBeTrue();
        settings.AuthenticationSettings.HeaderAuthentication.TrustedProxies.ShouldBe(new[] { "10.100.0.4" });
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldRetainTheCurrentBudget_WhenScheduleDataIsMissing(bool missingSchedule)
    {
        // Arrange
        var settings = new UserSettings();
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Monday"] = new() { ["00:00"] = 100 } },
        };
        var dto = settings.ToDTO();
        if (missingSchedule)
            dto.DownloadManagerSettings.DownloadSchedule = null!;
        else
            dto.DownloadManagerSettings.DownloadSchedule.Days = null!;

        // Act
        var result = await TestEndpointHandleAsync(
            new UpdateUserSettingsEndpointRequest { SettingsModelDto = dto },
            services => services.AddSingleton<IUserSettings>(settings)
        );

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ValidationErrors.Count.ShouldBe(1);
        settings.DownloadManagerSettings.DownloadSchedule.Enabled.ShouldBeTrue();
        settings.DownloadManagerSettings.DownloadSchedule.Days.Keys.ShouldBe(["Monday"]);
        settings
            .DownloadManagerSettings.DownloadSchedule.Days["Monday"]
            .ShouldBe(new Dictionary<string, int?> { ["00:00"] = 100 }, ignoreOrder: true);
    }
}
