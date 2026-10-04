using Microsoft.Extensions.DependencyInjection;
using Quartz;
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

    [Test]
    [Arguments("timezone")]
    [Arguments("limit")]
    [Arguments("enabled")]
    [Arguments("removed-day")]
    public async Task ShouldRefreshDownloadScheduling_WhenRelevantSettingsChange(string change)
    {
        // Arrange
        var settings = new UserSettings { DateTimeSettings = { TimeZone = "UTC" } };
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Monday"] = new() { ["09:30"] = 100 } },
        };
        var dto = settings.ToDTO();
        switch (change)
        {
            case "timezone":
                dto.DateTimeSettings.TimeZone = "Asia/Kathmandu";
                break;
            case "limit":
                dto.DownloadManagerSettings.DownloadSchedule.Days = new() { ["Monday"] = new() { ["09:30"] = 200 } };
                break;
            case "enabled":
                dto.DownloadManagerSettings.DownloadSchedule.Enabled = false;
                break;
            case "removed-day":
                dto.DownloadManagerSettings.DownloadSchedule.Days = new();
                break;
        }

        var scheduler = new Mock<IScheduler>(MockBehavior.Strict);
        var jobKey = UpdateScheduledDownloadLimitsJob.GetJobKey();
        var triggerKey = UpdateScheduledDownloadLimitsJob.GetTriggerKey();
        string? observedTimeZone = null;
        DownloadSchedule? observedSchedule = null;
        if (change == "timezone")
            scheduler
                .Setup(x =>
                    x.RescheduleJob(
                        triggerKey,
                        It.Is<ITrigger>(trigger =>
                            trigger.Key.Equals(triggerKey)
                            && trigger.JobKey.Equals(jobKey)
                            && trigger is ICronTrigger
                            && ((ICronTrigger)trigger).TimeZone.Id == "Asia/Kathmandu"
                            && ((ICronTrigger)trigger).CronExpressionString == "0 0,30 * * * ?"
                        ),
                        CancellationToken.None
                    )
                )
                .ReturnsAsync((DateTimeOffset?)DateTimeOffset.UnixEpoch)
                .Verifiable(Times.Once());
        scheduler
            .Setup(x => x.TriggerJob(jobKey, CancellationToken.None))
            .Callback(() =>
            {
                observedTimeZone = settings.DateTimeSettings.TimeZone;
                observedSchedule = settings.DownloadManagerSettings.DownloadSchedule;
            })
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestEndpointHandleAsync(
            new UpdateUserSettingsEndpointRequest { SettingsModelDto = dto },
            services =>
            {
                services.AddSingleton<IUserSettings>(settings);
                services.AddSingleton(scheduler.Object);
            }
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.ValidationErrors.Count.ShouldBe(0);
        result.StatusCode.ShouldBe(200);
        observedTimeZone.ShouldBe(dto.DateTimeSettings.TimeZone);
        observedSchedule.ShouldNotBeNull();
        observedSchedule.Enabled.ShouldBe(dto.DownloadManagerSettings.DownloadSchedule.Enabled);
        observedSchedule.Days.Keys.ShouldBe(dto.DownloadManagerSettings.DownloadSchedule.Days.Keys, ignoreOrder: true);
        foreach (var (day, points) in dto.DownloadManagerSettings.DownloadSchedule.Days)
            observedSchedule.Days[day].ShouldBe(points, ignoreOrder: true);
        scheduler.Verify();
        if (change != "timezone")
            scheduler.Verify(
                x => x.RescheduleJob(It.IsAny<TriggerKey>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldLeaveQuartzUnchanged_WhenOnlyUnrelatedSettingsAndPointOrderChange()
    {
        // Arrange
        var settings = new UserSettings();
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new()
            {
                ["Monday"] = new() { ["09:00"] = 100, ["17:00"] = null },
                ["Tuesday"] = new() { ["12:00"] = 200 },
            },
        };
        var dto = settings.ToDTO();
        dto.GeneralSettings.DisableAnimatedBackground = !settings.GeneralSettings.DisableAnimatedBackground;
        dto.DownloadManagerSettings.DownloadSchedule.Days = new()
        {
            ["Tuesday"] = new() { ["12:00"] = 200 },
            ["Monday"] = new() { ["17:00"] = null, ["09:00"] = 100 },
        };
        var scheduler = new Mock<IScheduler>(MockBehavior.Strict);

        // Act
        var result = await TestEndpointHandleAsync(
            new UpdateUserSettingsEndpointRequest { SettingsModelDto = dto },
            services =>
            {
                services.AddSingleton<IUserSettings>(settings);
                services.AddSingleton(scheduler.Object);
            }
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.ValidationErrors.Count.ShouldBe(0);
        result.StatusCode.ShouldBe(200);
        settings.GeneralSettings.DisableAnimatedBackground.ShouldBe(dto.GeneralSettings.DisableAnimatedBackground);
        scheduler.Verify(
            x => x.RescheduleJob(It.IsAny<TriggerKey>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
        scheduler.Verify(x => x.TriggerJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldKeepAppliedSettings_WhenQuartzCannotRefreshTheSchedule(bool failRescheduling)
    {
        // Arrange
        var settings = new UserSettings { DateTimeSettings = { TimeZone = "UTC" } };
        var dto = settings.ToDTO();
        dto.DownloadManagerSettings.DownloadSchedule.Enabled = true;
        if (failRescheduling)
            dto.DateTimeSettings.TimeZone = "Asia/Kathmandu";
        var scheduler = new Mock<IScheduler>(MockBehavior.Strict);
        var jobKey = UpdateScheduledDownloadLimitsJob.GetJobKey();
        if (failRescheduling)
            scheduler
                .Setup(x =>
                    x.RescheduleJob(
                        UpdateScheduledDownloadLimitsJob.GetTriggerKey(),
                        It.Is<ITrigger>(trigger =>
                            trigger.JobKey.Equals(jobKey)
                            && trigger is ICronTrigger
                            && ((ICronTrigger)trigger).TimeZone.Id == "Asia/Kathmandu"
                        ),
                        CancellationToken.None
                    )
                )
                .ThrowsAsync(new SchedulerException("scheduler unavailable"))
                .Verifiable(Times.Once());
        else
            scheduler
                .Setup(x => x.TriggerJob(jobKey, CancellationToken.None))
                .ThrowsAsync(new SchedulerException("scheduler unavailable"))
                .Verifiable(Times.Once());

        // Act
        var result = await TestEndpointHandleAsync(
            new UpdateUserSettingsEndpointRequest { SettingsModelDto = dto },
            services =>
            {
                services.AddSingleton<IUserSettings>(settings);
                services.AddSingleton(scheduler.Object);
            }
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.ValidationErrors.Count.ShouldBe(0);
        result.StatusCode.ShouldBe(200);
        settings.DownloadManagerSettings.DownloadSchedule.Enabled.ShouldBeTrue();
        settings.DateTimeSettings.TimeZone.ShouldBe(dto.DateTimeSettings.TimeZone);
        scheduler.Verify();
        if (failRescheduling)
            scheduler.Verify(x => x.TriggerJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never());
        else
            scheduler.Verify(
                x => x.RescheduleJob(It.IsAny<TriggerKey>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldKeepTheLatestTimezone_WhenSettingsUpdatesOverlap()
    {
        // Arrange
        var settings = new UserSettings { DateTimeSettings = { TimeZone = "UTC" } };
        var firstDto = settings.ToDTO();
        firstDto.DateTimeSettings.TimeZone = "Asia/Kathmandu";
        var secondDto = settings.ToDTO();
        secondDto.DateTimeSettings.TimeZone = "America/New_York";
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRescheduled = new TaskCompletionSource<DateTimeOffset?>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var calls = new List<string>();
        var scheduler = new Mock<IScheduler>(MockBehavior.Strict);
        var jobKey = UpdateScheduledDownloadLimitsJob.GetJobKey();
        var triggerKey = UpdateScheduledDownloadLimitsJob.GetTriggerKey();
        scheduler
            .Setup(x =>
                x.RescheduleJob(
                    triggerKey,
                    It.Is<ITrigger>(trigger =>
                        trigger.Key.Equals(triggerKey) && trigger.JobKey.Equals(jobKey) && trigger is ICronTrigger
                    ),
                    CancellationToken.None
                )
            )
            .Returns<TriggerKey, ITrigger, CancellationToken>(
                (_, trigger, _) =>
                {
                    var zone = ((ICronTrigger)trigger).TimeZone.Id;
                    calls.Add("reschedule " + zone);
                    if (zone != "Asia/Kathmandu")
                        return Task.FromResult<DateTimeOffset?>(DateTimeOffset.UnixEpoch);

                    firstEntered.TrySetResult();
                    return firstRescheduled.Task;
                }
            )
            .Verifiable(Times.Exactly(2));
        scheduler
            .Setup(x => x.TriggerJob(jobKey, CancellationToken.None))
            .Callback(() => calls.Add("trigger " + settings.DateTimeSettings.TimeZone))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));

        // Act
        var first = TestEndpointHandleAsync(
            new UpdateUserSettingsEndpointRequest { SettingsModelDto = firstDto },
            services =>
            {
                services.AddSingleton<IUserSettings>(settings);
                services.AddSingleton(scheduler.Object);
            }
        );
        try
        {
            await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            var second = TestEndpointHandleAsync(
                new UpdateUserSettingsEndpointRequest { SettingsModelDto = secondDto },
                services =>
                {
                    services.AddSingleton<IUserSettings>(settings);
                    services.AddSingleton(scheduler.Object);
                }
            );
            try
            {
                settings.DateTimeSettings.TimeZone.ShouldBe("Asia/Kathmandu");
                second.IsCompleted.ShouldBeFalse();
            }
            finally
            {
                firstRescheduled.TrySetResult(DateTimeOffset.UnixEpoch);
                await Task.WhenAll(first, second);
            }

            // Assert
            (await first).IsValid.ShouldBeTrue();
            (await first).ValidationErrors.Count.ShouldBe(0);
            (await first).StatusCode.ShouldBe(200);
            (await second).IsValid.ShouldBeTrue();
            (await second).ValidationErrors.Count.ShouldBe(0);
            (await second).StatusCode.ShouldBe(200);
            settings.DateTimeSettings.TimeZone.ShouldBe("America/New_York");
            calls.ShouldBe(
                new[]
                {
                    "reschedule Asia/Kathmandu",
                    "trigger Asia/Kathmandu",
                    "reschedule America/New_York",
                    "trigger America/New_York",
                }
            );
            scheduler.Verify();
        }
        finally
        {
            firstRescheduled.TrySetResult(DateTimeOffset.UnixEpoch);
            await first;
        }
    }
}
