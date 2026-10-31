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
        var result = MediaNavigationIndexBuilder.Build(rows, SortType.Title);

        // Assert
        result.Select(x => (x.Label, x.Index)).ShouldBe([("#", 0), ("A", 1), ("B", 3)]);
    }

    [Test]
    public void ShouldBuildYearNavigationIndexes_WhenRowsAreSortedByYear()
    {
        // Arrange
        var rows = new[] { CreateRow(year: 1999), CreateRow(year: 1999), CreateRow(year: 2001) };

        // Act
        var result = MediaNavigationIndexBuilder.Build(rows, SortType.Year);

        // Assert
        result.Select(x => (x.Label, x.Index)).ShouldBe([("1999", 0), ("2001", 2)]);
    }

    [Test]
    public void ShouldBuildVideoQualityNavigationIndexes_WhenRowsAreSortedByVideoQuality()
    {
        // Arrange
        var rows = new[]
        {
            CreateRow(qualityValue: 480), // SD (480p)
            CreateRow(qualityValue: 480), // SD (480p)
            CreateRow(qualityValue: 1080), // FullHD (1080p)
        };

        // Act
        var result = MediaNavigationIndexBuilder.Build(rows, SortType.VideoQuality);

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
        var result = MediaNavigationIndexBuilder.Build(rows, SortType.Duration);

        // Assert
        result.Select(x => (x.Label, x.Index)).ShouldBe([("0–10 min", 0), ("10–20 min", 2)]);
    }

    [Test]
    public void ShouldUseUnknownLabel_WhenNullableSortValueIsMissing()
    {
        // Arrange
        var rows = new[] { CreateRow(year: null, duration: null) };

        // Act
        var yearResult = MediaNavigationIndexBuilder.Build(rows, SortType.Year);
        var durationResult = MediaNavigationIndexBuilder.Build(rows, SortType.Duration);

        // Assert
        yearResult.Select(x => (x.Label, x.Index)).ShouldBe([("#", 0)]);
        durationResult.Select(x => (x.Label, x.Index)).ShouldBe([("#", 0)]);
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
        var result = MediaNavigationIndexBuilder.Build(rows, SortType.AddedAt);

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
        var result = MediaNavigationIndexBuilder.Build(rows, SortType.MediaSize);

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
    ) =>
        new()
        {
            SearchTitle = title,
            Year = year,
            QualityValue = qualityValue,
            Duration = duration,
            AddedAt = addedAt ?? new DateTime(2026, 1, 1),
            UpdatedAt = updatedAt,
            MediaSize = mediaSize,
        };
}
