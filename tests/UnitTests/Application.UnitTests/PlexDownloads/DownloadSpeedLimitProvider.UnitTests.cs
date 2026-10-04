using Reaparr.Settings;

namespace Reaparr.Application.UnitTests;

public class DownloadSpeedLimitProviderUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(0, null, 0L)]
    [Arguments(200, null, 204800L)]
    [Arguments(0, 12345L, 12345L)]
    [Arguments(200, 12345L, 12345L)]
    [Arguments(10, 12345L, 10240L)]
    public void ShouldApplyManualLimitAsHardCeiling_WhenScheduledLimitExists(
        int manualLimitKb,
        long? scheduledLimit,
        long expected
    )
    {
        // Arrange
        var settings = new UserSettings();
        settings.ServerSettings.SetDownloadSpeedLimit("machine1", manualLimitKb);
        using var sut = new DownloadSpeedLimitProvider(settings);
        if (scheduledLimit.HasValue)
            sut.SetScheduledDownloadSpeedLimits(new Dictionary<string, long> { ["machine1"] = scheduledLimit.Value });

        // Act
        var effectiveLimit = sut.GetEffectiveDownloadSpeedLimit("machine1");

        // Assert
        effectiveLimit.ShouldBe(expected);
        settings.ServerSettings.GetDownloadSpeedLimit("machine1").ShouldBe(manualLimitKb);
    }

    [Test]
    public void ShouldReplaceCompleteAllocationAndClearStaleGrants_WhenPublishingScheduledLimits()
    {
        // Arrange
        var settings = new UserSettings();
        using var sut = new DownloadSpeedLimitProvider(settings);
        sut.SetScheduledDownloadSpeedLimits(new Dictionary<string, long> { ["machine1"] = 100, ["machine2"] = 200 });

        // Act
        sut.SetScheduledDownloadSpeedLimits(new Dictionary<string, long> { ["machine2"] = 300 });

        // Assert
        sut.GetEffectiveDownloadSpeedLimit("machine1").ShouldBe(0);
        sut.GetEffectiveDownloadSpeedLimit("machine2").ShouldBe(300);
        settings.ServerSettings.Data.ShouldBeEmpty();

        // Act
        sut.SetScheduledDownloadSpeedLimits(new Dictionary<string, long>());

        // Assert
        sut.GetEffectiveDownloadSpeedLimit("machine2").ShouldBe(0);
    }

    [Test]
    public void ShouldSnapshotAllocation_WhenCallerMutatesItsDictionaryAfterPublication()
    {
        // Arrange
        using var sut = new DownloadSpeedLimitProvider(new UserSettings());
        var allocation = new Dictionary<string, long> { ["machine1"] = 100 };
        sut.SetScheduledDownloadSpeedLimits(allocation);

        // Act
        allocation["machine1"] = 900;
        allocation["machine2"] = 500;

        // Assert
        sut.GetEffectiveDownloadSpeedLimit("machine1").ShouldBe(100);
        sut.GetEffectiveDownloadSpeedLimit("machine2").ShouldBe(0);
    }

    [Test]
    public void ShouldEmitCurrentEffectiveLimitImmediately_WhenAllocationPredatesSubscription()
    {
        // Arrange
        var settings = new UserSettings();
        settings.ServerSettings.SetDownloadSpeedLimit("machine1", 200);
        using var sut = new DownloadSpeedLimitProvider(settings);
        sut.SetScheduledDownloadSpeedLimits(new Dictionary<string, long> { ["machine1"] = 12345 });
        var emittedValues = new List<long>();

        // Act
        using var subscription = sut.GetEffectiveDownloadSpeedLimitObservable("machine1").Subscribe(emittedValues.Add);

        // Assert
        emittedValues.ShouldBe([12345L]);
    }

    [Test]
    public void ShouldReactToManualAndScheduledChanges_WhenEffectiveLimitChanges()
    {
        // Arrange
        var settings = new UserSettings();
        using var sut = new DownloadSpeedLimitProvider(settings);
        var emittedValues = new List<long>();
        using var subscription = sut.GetEffectiveDownloadSpeedLimitObservable("machine1").Subscribe(emittedValues.Add);

        // Act
        settings.ServerSettings.SetDownloadSpeedLimit("machine1", 20);
        sut.SetScheduledDownloadSpeedLimits(new Dictionary<string, long> { ["machine1"] = 15000 });
        settings.ServerSettings.SetDownloadSpeedLimit("machine1", 10);
        sut.SetScheduledDownloadSpeedLimits(new Dictionary<string, long>());

        // Assert
        emittedValues.ShouldBe([0L, 20480L, 15000L, 10240L]);
    }

    [Test]
    public void ShouldNotChangeConfiguredSettingsOrPublishSettingsChange_WhenAllocationIsPublished()
    {
        // Arrange
        var settings = new UserSettings();
        settings.ServerSettings.SetDownloadSpeedLimit("machine1", 200);
        using var sut = new DownloadSpeedLimitProvider(settings);
        var settingsChangeCount = 0;
        using var subscription = settings.ServerSettings.HasChanged.Subscribe(_ => settingsChangeCount++);
        var persistedBefore = UserSettingsSerializer.Serialize(settings);

        // Act
        sut.SetScheduledDownloadSpeedLimits(new Dictionary<string, long> { ["machine1"] = 12345 });

        // Assert
        settingsChangeCount.ShouldBe(1);
        UserSettingsSerializer.Serialize(settings).ShouldBe(persistedBefore);
        settings.ServerSettings.GetDownloadSpeedLimit("machine1").ShouldBe(200);
        sut.GetEffectiveDownloadSpeedLimit("machine1").ShouldBe(12345);
    }

    [Test]
    public void ShouldPreserve64BitLimitsAndUnlimitedSentinel_WhenReadingEffectiveLimit()
    {
        // Arrange
        var settings = new UserSettings();
        settings.ServerSettings.SetDownloadSpeedLimit("limited", int.MaxValue);
        settings.ServerSettings.SetDownloadSpeedLimit("unlimited", 0);
        using var sut = new DownloadSpeedLimitProvider(settings);

        // Act
        var maximumConfiguredLimit = sut.GetEffectiveDownloadSpeedLimit("limited");
        var unlimited = sut.GetEffectiveDownloadSpeedLimit("unlimited");
        sut.SetScheduledDownloadSpeedLimits(new Dictionary<string, long> { ["unlimited"] = 1 });
        var minimumScheduledLimit = sut.GetEffectiveDownloadSpeedLimit("unlimited");

        // Assert
        maximumConfiguredLimit.ShouldBe(2199023254528L);
        unlimited.ShouldBe(0);
        minimumScheduledLimit.ShouldBe(1);
    }

    [Test]
    [Arguments(0L)]
    [Arguments(-1L)]
    public void ShouldRejectNonPositiveScheduledLimitsWithoutPublishingAPartialAllocation_WhenPublishingAllocation(
        long invalidLimit
    )
    {
        // Arrange
        using var sut = new DownloadSpeedLimitProvider(new UserSettings());
        sut.SetScheduledDownloadSpeedLimits(new Dictionary<string, long> { ["machine1"] = 100 });

        // Act
        Should.Throw<ArgumentOutOfRangeException>(() =>
            sut.SetScheduledDownloadSpeedLimits(
                new Dictionary<string, long> { ["machine1"] = 200, ["machine2"] = invalidLimit }
            )
        );

        // Assert
        sut.GetEffectiveDownloadSpeedLimit("machine1").ShouldBe(100);
        sut.GetEffectiveDownloadSpeedLimit("machine2").ShouldBe(0);
    }
}
