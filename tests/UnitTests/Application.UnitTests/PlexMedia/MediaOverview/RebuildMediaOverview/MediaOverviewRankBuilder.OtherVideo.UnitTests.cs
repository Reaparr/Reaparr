using NaturalSort.Extension;

namespace Reaparr.Application.UnitTests;

public class MediaOverviewRankBuilderOtherVideoUnitTests
{
    [Test]
    public void ShouldAssignExpectedRanks_ForEveryOtherVideoSort()
    {
        // Arrange
        var snapshots = FakeData.GetMediaOverviewOtherVideoSnapshots(new Seed(13)).Generate(4);
        var values = FakeData.GetMediaOverviewRankValues(new Seed(14)).Generate(snapshots.Count);
        var valuesById = snapshots
            .Select((snapshot, index) => new { snapshot.PlexOtherVideoId, Value = values[index] })
            .ToDictionary(x => x.PlexOtherVideoId, x => x.Value);
        var titleComparer = StringComparison.OrdinalIgnoreCase.WithNaturalSort();

        // Act
        var result = snapshots.AssignRanks(valuesById);
        var expectedRanks = Enumerable.Range(0, snapshots.Count).ToArray();

        // Assert
        result
            .OrderBy(x => x.TitleRank)
            .Select(x => x.PlexOtherVideoId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexOtherVideoId].SearchTitle, titleComparer)
                    .ThenBy(x => x.PlexOtherVideoId)
                    .Select(x => x.PlexOtherVideoId)
                    .ToArray()
            );
        result
            .OrderBy(x => x.YearRank)
            .Select(x => x.PlexOtherVideoId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexOtherVideoId].Year)
                    .ThenBy(x => x.PlexOtherVideoId)
                    .Select(x => x.PlexOtherVideoId)
                    .ToArray()
            );
        result
            .OrderBy(x => x.AddedAtRank)
            .Select(x => x.PlexOtherVideoId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexOtherVideoId].AddedAt)
                    .ThenBy(x => x.PlexOtherVideoId)
                    .Select(x => x.PlexOtherVideoId)
                    .ToArray()
            );
        result
            .OrderBy(x => x.UpdatedAtRank)
            .Select(x => x.PlexOtherVideoId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexOtherVideoId].UpdatedAt)
                    .ThenBy(x => x.PlexOtherVideoId)
                    .Select(x => x.PlexOtherVideoId)
                    .ToArray()
            );
        result
            .OrderBy(x => x.DurationRank)
            .Select(x => x.PlexOtherVideoId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexOtherVideoId].Duration)
                    .ThenBy(x => x.PlexOtherVideoId)
                    .Select(x => x.PlexOtherVideoId)
                    .ToArray()
            );
        result
            .OrderBy(x => x.MediaSizeRank)
            .Select(x => x.PlexOtherVideoId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexOtherVideoId].MediaSize)
                    .ThenBy(x => x.PlexOtherVideoId)
                    .Select(x => x.PlexOtherVideoId)
                    .ToArray()
            );
        result.Select(x => x.TitleRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.YearRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.AddedAtRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.UpdatedAtRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.DurationRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.MediaSizeRank).OrderBy(x => x).ShouldBe(expectedRanks);
    }
}
