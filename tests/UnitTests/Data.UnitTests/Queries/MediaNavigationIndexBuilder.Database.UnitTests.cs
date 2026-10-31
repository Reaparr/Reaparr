using SortDirection = Reaparr.Domain.SortDirection;

namespace Reaparr.Data.UnitTests;

public class MediaNavigationIndexBuilderDatabaseUnitTests : BaseUnitTest
{
    private static readonly (
        string SearchTitle,
        int Duration,
        DateTime AddedAt,
        DateTime? UpdatedAt,
        long MediaSize,
        int Rank
    )[] SnapshotValues =
    [
        ("Alpha", 599, new(2026, 1, 15), null, 999_999_999L, 10),
        ("Bravo", 600, new(2026, 2, 1), new(2026, 2, 20), 1_000_000_000L, 50),
        ("Bravo Two", 600, new(2026, 2, 1), new(2026, 2, 20), 1_000_000_000L, 60),
        ("Charlie", 1_200, new(2026, 3, 15), new(2026, 3, 20), 2_000_000_000L, 70),
        ("Filtered", 1_800, new(2026, 4, 1), new(2026, 4, 20), 3_000_000_000L, 30),
    ];

    [Test]
    [Arguments(SortType.Duration, SortDirection.Asc, new[] { "0–10 min", "10–20 min", "20–30 min" })]
    [Arguments(SortType.Duration, SortDirection.Desc, new[] { "20–30 min", "10–20 min", "0–10 min" })]
    [Arguments(SortType.AddedAt, SortDirection.Asc, new[] { "2026-01", "2026-02", "2026-03" })]
    [Arguments(SortType.AddedAt, SortDirection.Desc, new[] { "2026-03", "2026-02", "2026-01" })]
    [Arguments(SortType.UpdatedAt, SortDirection.Asc, new[] { "#", "2026-02", "2026-03" })]
    [Arguments(SortType.UpdatedAt, SortDirection.Desc, new[] { "2026-03", "2026-02", "#" })]
    [Arguments(SortType.MediaSize, SortDirection.Asc, new[] { "0–1 GB", "1–2 GB", "2–3 GB" })]
    [Arguments(SortType.MediaSize, SortDirection.Desc, new[] { "2–3 GB", "1–2 GB", "0–1 GB" })]
    public async Task ShouldReturnFilteredBucketOrdinalsFromSqlite(
        SortType sortType,
        SortDirection direction,
        string[] expectedLabels
    )
    {
        // Arrange
        var filteredVideoId = await SetupSnapshotDatabaseAsync();
        using var dbContext = IDbContext;
        var rows = CreateNavigationRows(dbContext.MediaOverviewOtherVideoSnapshots, filteredVideoId);

        // Act
        var result = await MediaNavigationIndexBuilder.BuildAsync(
            rows,
            new Sort(sortType, direction),
            CancellationToken
        );

        // Assert
        result
            .Select(x => (x.Label, x.Index))
            .ShouldBe(new[] { (expectedLabels[0], 0), (expectedLabels[1], 1), (expectedLabels[2], 3) });
    }

    [Test]
    public async Task ShouldPreserveRequestedField_WhenDefaultingNoSortNavigationToAscending()
    {
        // Arrange
        var filteredVideoId = await SetupSnapshotDatabaseAsync();
        using var dbContext = IDbContext;
        var rows = CreateNavigationRows(dbContext.MediaOverviewOtherVideoSnapshots, filteredVideoId)
            .Where(x => x.SearchTitle != "Bravo Two");

        // Act
        var result = await MediaNavigationIndexBuilder.BuildAsync(
            rows,
            new Sort(SortType.MediaSize, SortDirection.NoSort),
            CancellationToken
        );

        // Assert
        result.Select(x => (x.Label, x.Index)).ShouldBe(new[] { ("0–1 GB", 0), ("1–2 GB", 1), ("2–3 GB", 2) });
    }

    private async Task<int> SetupSnapshotDatabaseAsync()
    {
        await SetupDatabase(
            90422,
            config =>
            {
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoCount = 5;
            }
        );

        using var dbContext = IDbContext;
        var libraryId = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.OtherVideos)
            .Select(x => x.Id)
            .SingleAsync(CancellationToken);
        var videos = await dbContext.PlexOtherVideos.OrderBy(x => x.Id).ToListAsync(CancellationToken);

        dbContext.MediaOverviewOtherVideoSnapshots.AddRange(
            videos.Select(
                (video, index) =>
                {
                    var value = SnapshotValues[index];
                    return new MediaOverviewOtherVideoSnapshot
                    {
                        PlexOtherVideoId = video.Id,
                        PlexLibraryId = libraryId,
                        SearchTitle = value.SearchTitle,
                        NavigationTitleLabel = "A",
                        Duration = value.Duration,
                        AddedAt = value.AddedAt,
                        UpdatedAt = value.UpdatedAt,
                        MediaSize = value.MediaSize,
                        TitleRank = value.Rank,
                        AddedAtRank = value.Rank,
                        UpdatedAtRank = value.Rank,
                        DurationRank = value.Rank,
                        MediaSizeRank = value.Rank,
                        QualityRank = value.Rank,
                        YearRank = value.Rank,
                        Year = 2026,
                        VideoQuality = VideoQuality.FullHD,
                    };
                }
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        return videos[4].Id;
    }

    private static IQueryable<MediaNavigationIndexRow> CreateNavigationRows(
        IQueryable<MediaOverviewOtherVideoSnapshot> snapshots,
        int filteredVideoId
    ) =>
        snapshots
            .Where(x => x.PlexOtherVideoId != filteredVideoId)
            .Select(x => new MediaNavigationIndexRow
            {
                SearchTitle = x.SearchTitle,
                Year = x.Year,
                QualityValue = (int?)x.VideoQuality,
                Duration = x.Duration,
                AddedAt = x.AddedAt,
                UpdatedAt = x.UpdatedAt,
                MediaSize = x.MediaSize,
                SortRank = x.TitleRank,
            });
}
