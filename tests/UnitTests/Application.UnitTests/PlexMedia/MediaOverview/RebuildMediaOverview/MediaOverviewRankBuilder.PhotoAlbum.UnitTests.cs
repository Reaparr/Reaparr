using NaturalSort.Extension;

namespace Reaparr.Application.UnitTests;

public class MediaOverviewRankBuilderPhotoAlbumUnitTests
{
    [Test]
    public void ShouldAssignExpectedRanks_ForEveryPhotoAlbumSort()
    {
        // Arrange
        var snapshots = FakeData.GetMediaOverviewPhotoAlbumSnapshots(new Seed(10)).Generate(4);
        var values = FakeData.GetMediaOverviewRankValues(new Seed(11)).Generate(snapshots.Count);
        var valuesById = snapshots
            .Select((snapshot, index) => new { snapshot.PlexPhotoAlbumId, Value = values[index] })
            .ToDictionary(x => x.PlexPhotoAlbumId, x => x.Value);
        var titleComparer = StringComparison.OrdinalIgnoreCase.WithNaturalSort();

        // Act
        var result = snapshots.AssignRanks(valuesById);
        var expectedRanks = Enumerable.Range(0, snapshots.Count).ToArray();

        // Assert
        result
            .OrderBy(x => x.TitleRank)
            .Select(x => x.PlexPhotoAlbumId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexPhotoAlbumId].SearchTitle, titleComparer)
                    .ThenBy(x => x.PlexPhotoAlbumId)
                    .Select(x => x.PlexPhotoAlbumId)
                    .ToArray()
            );
        result
            .OrderBy(x => x.YearRank)
            .Select(x => x.PlexPhotoAlbumId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexPhotoAlbumId].Year)
                    .ThenBy(x => x.PlexPhotoAlbumId)
                    .Select(x => x.PlexPhotoAlbumId)
                    .ToArray()
            );
        result
            .OrderBy(x => x.AddedAtRank)
            .Select(x => x.PlexPhotoAlbumId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexPhotoAlbumId].AddedAt)
                    .ThenBy(x => x.PlexPhotoAlbumId)
                    .Select(x => x.PlexPhotoAlbumId)
                    .ToArray()
            );
        result
            .OrderBy(x => x.UpdatedAtRank)
            .Select(x => x.PlexPhotoAlbumId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexPhotoAlbumId].UpdatedAt)
                    .ThenBy(x => x.PlexPhotoAlbumId)
                    .Select(x => x.PlexPhotoAlbumId)
                    .ToArray()
            );
        result
            .OrderBy(x => x.DurationRank)
            .Select(x => x.PlexPhotoAlbumId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexPhotoAlbumId].Duration)
                    .ThenBy(x => x.PlexPhotoAlbumId)
                    .Select(x => x.PlexPhotoAlbumId)
                    .ToArray()
            );
        result
            .OrderBy(x => x.MediaSizeRank)
            .Select(x => x.PlexPhotoAlbumId)
            .ShouldBe(
                snapshots
                    .OrderBy(x => valuesById[x.PlexPhotoAlbumId].MediaSize)
                    .ThenBy(x => x.PlexPhotoAlbumId)
                    .Select(x => x.PlexPhotoAlbumId)
                    .ToArray()
            );
        result.Select(x => x.TitleRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.YearRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.AddedAtRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.UpdatedAtRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.DurationRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.MediaSizeRank).OrderBy(x => x).ShouldBe(expectedRanks);
    }

    [Test]
    public void ShouldRankNullableFamilyValuesWithoutFabricatingZero()
    {
        // Arrange
        var snapshots = FakeData.GetMediaOverviewPhotoAlbumSnapshots(new Seed(12)).Generate(2);
        snapshots[0].PlexPhotoAlbumId = 2;
        snapshots[1].PlexPhotoAlbumId = 1;
        var values = new Dictionary<int, MediaOverviewRankValue>
        {
            [2] = new("Beta", null, new DateTime(2026, 1, 2), null, null, 2),
            [1] = new("Alpha", 0, new DateTime(2026, 1, 1), null, 0, 1),
        };

        // Act
        var result = snapshots.AssignRanks(values);

        // Assert
        result.OrderBy(x => x.TitleRank).Select(x => x.PlexPhotoAlbumId).ShouldBe([1, 2]);
        result.OrderBy(x => x.YearRank).Select(x => x.PlexPhotoAlbumId).ShouldBe([2, 1]);
        result.OrderBy(x => x.DurationRank).Select(x => x.PlexPhotoAlbumId).ShouldBe([2, 1]);
    }
}
