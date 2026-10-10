namespace Reaparr.Data.UnitTests;

public class MediaNavigationIndexBuilderUnitTests
{
    [Test]
    public void ShouldBuildTitleNavigationIndexes_WhenRowsAreSortedByTitle()
    {
        // Arrange
        var rows = new[]
        {
            CreateRow(title: "2001: A Space Odyssey"),
            CreateRow(title: "Alien"),
            CreateRow(title: "Arrival"),
            CreateRow(title: "Blade Runner"),
        };

        // Act
        var result = MediaNavigationIndexBuilder.Build(rows, "sortIndex");

        // Assert
        result.Select(x => (x.Label, x.Index)).ShouldBe([("#", 0), ("A", 1), ("B", 3)]);
    }

    [Test]
    public void ShouldBuildYearNavigationIndexes_WhenRowsAreSortedByYear()
    {
        // Arrange
        var rows = new[] { CreateRow(year: 1999), CreateRow(year: 1999), CreateRow(year: 2001) };

        // Act
        var result = MediaNavigationIndexBuilder.Build(rows, "year");

        // Assert
        result.Select(x => (x.Label, x.Index)).ShouldBe([("1999", 0), ("2001", 2)]);
    }

    [Test]
    public void ShouldBuildQualityNavigationIndexes_WhenRowsAreSortedByQuality()
    {
        // Arrange
        var rows = new[]
        {
            CreateRow(qualityValue: 480), // SD (480p)
            CreateRow(qualityValue: 480), // SD (480p)
            CreateRow(qualityValue: 1080), // FullHD (1080p)
        };

        // Act
        var result = MediaNavigationIndexBuilder.Build(rows, "quality");

        // Assert
        result.Select(x => (x.Label, x.Index)).ShouldBe([("480", 0), ("1080", 2)]);
    }

    [Test]
    public void ShouldBuildDurationNavigationIndexes_WhenRowsAreSortedByDuration()
    {
        // Arrange
        // 60s = 1min, 599s = just under 10min, 600s = exactly 10min bucket boundary
        var rows = new[] { CreateRow(duration: 60), CreateRow(duration: 599), CreateRow(duration: 600) };

        // Act
        var result = MediaNavigationIndexBuilder.Build(rows, "duration");

        // Assert
        result.Select(x => (x.Label, x.Index)).ShouldBe([("0–10 min", 0), ("10–20 min", 2)]);
    }

    [Test]
    [Arguments("year")]
    [Arguments("duration")]
    public void ShouldUseUnknownLabel_WhenNullableSortValueIsMissing(string sortField)
    {
        // Arrange
        var rows = new[] { CreateRow(year: null, duration: null) };

        // Act
        var result = MediaNavigationIndexBuilder.Build(rows, sortField);

        // Assert
        result.Select(x => (x.Label, x.Index)).ShouldBe([("#", 0)]);
    }

    [Test]
    public void ShouldBuildDateNavigationIndexes_WhenRowsAreSortedByAddedAt()
    {
        // Arrange
        var rows = new[]
        {
            CreateRow(addedAt: new DateTime(2025, 12, 14)),
            CreateRow(addedAt: new DateTime(2026, 1, 1)),
            CreateRow(addedAt: new DateTime(2026, 1, 20)),
        };

        // Act
        var result = MediaNavigationIndexBuilder.Build(rows, "addedAt");

        // Assert
        result.Select(x => (x.Label, x.Index)).ShouldBe([("2025-12", 0), ("2026-01", 1)]);
    }

    [Test]
    public void ShouldBuildMediaSizeNavigationIndexes_WhenRowsAreSortedByMediaSize()
    {
        // Arrange
        var rows = new[]
        {
            CreateRow(mediaSize: 1),
            CreateRow(mediaSize: 999_999_999),
            CreateRow(mediaSize: 1_000_000_000),
        };

        // Act
        var result = MediaNavigationIndexBuilder.Build(rows, "mediaSize");

        // Assert
        result.Select(x => (x.Label, x.Index)).ShouldBe([("0–1 GB", 0), ("1–2 GB", 2)]);
    }

    private static MediaNavigationIndexRow CreateRow(
        string? title = "Title",
        int? year = 2000,
        int? qualityValue = 1080,
        int? duration = 0,
        DateTime? addedAt = null,
        DateTime? updatedAt = null,
        long mediaSize = 0
    ) => new(title, year, qualityValue, duration, addedAt ?? new DateTime(2026, 1, 1), updatedAt, mediaSize);
}
