using Reaparr.Settings.Contracts;

namespace Reaparr.Settings.UnitTests;

public class DownloadScheduleUnitTests : BaseUnitTest
{
    [Test]
    public void ShouldCompareEquivalentSchedulesByValue_WhenDictionaryOrderDiffers()
    {
        // Arrange
        var left = new DownloadSchedule
        {
            Enabled = true,
            Days = new()
            {
                ["Monday"] = new() { ["09:00"] = 100, ["17:00"] = null },
                ["Tuesday"] = new() { ["12:00"] = 200 },
            },
        };
        var right = new DownloadSchedule
        {
            Enabled = true,
            Days = new()
            {
                ["Tuesday"] = new() { ["12:00"] = 200 },
                ["Monday"] = new() { ["17:00"] = null, ["09:00"] = 100 },
            },
        };
        var schedules = new HashSet<DownloadSchedule> { left };

        // Act
        var equal = left == right;
        var found = schedules.Contains(right);

        // Assert
        equal.ShouldBeTrue();
        found.ShouldBeTrue();
    }

    [Test]
    [Arguments("enabled")]
    [Arguments("day")]
    [Arguments("time")]
    [Arguments("limit")]
    [Arguments("unlimited")]
    [Arguments("added-point")]
    [Arguments("removed-day")]
    public void ShouldCompareSchedulesAsDifferent_WhenTheirContentsChange(string change)
    {
        // Arrange
        var left = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Monday"] = new() { ["09:00"] = 100, ["17:00"] = null } },
        };
        var right = left with { Days = new() { ["Monday"] = new(left.Days["Monday"]) } };
        switch (change)
        {
            case "enabled":
                right = right with { Enabled = false };
                break;
            case "day":
                right = right with { Days = new() { ["Tuesday"] = right.Days["Monday"] } };
                break;
            case "time":
                right.Days["Monday"] = new() { ["09:30"] = 100, ["17:00"] = null };
                break;
            case "limit":
                right.Days["Monday"]["09:00"] = 200;
                break;
            case "unlimited":
                right.Days["Monday"]["09:00"] = null;
                break;
            case "added-point":
                right.Days["Monday"]["10:00"] = 300;
                break;
            case "removed-day":
                right.Days.Clear();
                break;
        }

        // Act
        var equal = left == right;
        var equalInReverse = right == left;

        // Assert
        equal.ShouldBeFalse();
        equalInReverse.ShouldBeFalse();
    }

    [Test]
    public void ShouldCompareEmptySchedulesByValue_AndTreatNullAsDifferent()
    {
        // Arrange
        var left = new DownloadSchedule();
        var right = new DownloadSchedule();

        // Act
        var equal = left == right;
        var equalToNull = left.Equals((DownloadSchedule?)null);

        // Assert
        equal.ShouldBeTrue();
        equalToNull.ShouldBeFalse();
    }
}
