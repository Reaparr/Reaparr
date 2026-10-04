using NaturalSort.Extension;

namespace Reaparr.Application.UnitTests;

public class MediaOverviewRankBuilderMusicArtistUnitTests
{
    [Test]
    public void ShouldAssignExpectedRanks_ForEveryMusicArtistSort()
    {
        // Arrange
        var snapshots = FakeData.GetMediaOverviewMusicArtistSnapshots(new Seed(8)).Generate(4);
        var values = FakeData.GetMediaOverviewRankValues(new Seed(9)).Generate(snapshots.Count);
        var valuesById = snapshots
            .Select((snapshot, index) => new { snapshot.PlexArtistId, Value = values[index] })
            .ToDictionary(x => x.PlexArtistId, x => x.Value);
        var titleComparer = StringComparison.OrdinalIgnoreCase.WithNaturalSort();

        // Act
        var result = snapshots.AssignRanks(valuesById);
        var expectedRanks = Enumerable.Range(0, snapshots.Count).ToArray();

        // Assert
        result
            .OrderBy(x => x.TitleRank)
            .Select(x => x.PlexArtistId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexArtistId].SearchTitle, titleComparer)
                    .ThenBy(x => x.PlexArtistId)
                    .Select(x => x.PlexArtistId)
                    .ToArray()
            );
        result
            .OrderBy(x => x.YearRank)
            .Select(x => x.PlexArtistId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexArtistId].Year)
                    .ThenBy(x => x.PlexArtistId)
                    .Select(x => x.PlexArtistId)
                    .ToArray()
            );
        result
            .OrderBy(x => x.AddedAtRank)
            .Select(x => x.PlexArtistId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexArtistId].AddedAt)
                    .ThenBy(x => x.PlexArtistId)
                    .Select(x => x.PlexArtistId)
                    .ToArray()
            );
        result
            .OrderBy(x => x.UpdatedAtRank)
            .Select(x => x.PlexArtistId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexArtistId].UpdatedAt)
                    .ThenBy(x => x.PlexArtistId)
                    .Select(x => x.PlexArtistId)
                    .ToArray()
            );
        result
            .OrderBy(x => x.DurationRank)
            .Select(x => x.PlexArtistId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexArtistId].Duration)
                    .ThenBy(x => x.PlexArtistId)
                    .Select(x => x.PlexArtistId)
                    .ToArray()
            );
        result
            .OrderBy(x => x.MediaSizeRank)
            .Select(x => x.PlexArtistId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexArtistId].MediaSize)
                    .ThenBy(x => x.PlexArtistId)
                    .Select(x => x.PlexArtistId)
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
