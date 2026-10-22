namespace Reaparr.Application.UnitTests;

public class MediaOverviewSnapshotQueryExtensionsUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(nameof(BasePlexMedia.SortIndex), false, new[] { "B", "C", "A" })]
    [Arguments(nameof(BasePlexMedia.SortIndex), true, new[] { "A", "C", "B" })]
    [Arguments(nameof(PlexMediaSlimDTO.Year), false, new[] { "C", "A", "B" })]
    [Arguments(nameof(PlexMediaSlimDTO.Year), true, new[] { "B", "A", "C" })]
    [Arguments(nameof(BasePlexMedia.VideoQuality), false, new[] { "A", "C", "B" })]
    [Arguments(nameof(BasePlexMedia.VideoQuality), true, new[] { "B", "C", "A" })]
    [Arguments(nameof(BasePlexMedia.AddedAt), false, new[] { "B", "C", "A" })]
    [Arguments(nameof(BasePlexMedia.AddedAt), true, new[] { "A", "C", "B" })]
    [Arguments(nameof(BasePlexMedia.UpdatedAt), false, new[] { "A", "C", "B" })]
    [Arguments(nameof(BasePlexMedia.UpdatedAt), true, new[] { "B", "C", "A" })]
    [Arguments(nameof(BasePlexMedia.Duration), false, new[] { "B", "A", "C" })]
    [Arguments(nameof(BasePlexMedia.Duration), true, new[] { "C", "A", "B" })]
    [Arguments(nameof(BasePlexMedia.MediaSize), false, new[] { "C", "B", "A" })]
    [Arguments(nameof(BasePlexMedia.MediaSize), true, new[] { "A", "B", "C" })]
    public async Task ShouldOrderOtherVideoRanksThroughSqlite(
        string sortField,
        bool descending,
        string[] expectedLabels
    )
    {
        // Arrange
        await ArrangeOtherVideoSnapshotsAsync();
        var snapshots = IDbContext.MediaOverviewOtherVideoSnapshots;
        var sort = (sortField, descending);

        // Act
        var orderedLabels = await snapshots
            .ApplyRankOrder(sort)
            .Select(x => x.NavigationTitleLabel)
            .ToListAsync(CancellationToken);

        // Assert
        orderedLabels.ShouldBe(expectedLabels);
    }

    [Test]
    [Arguments(nameof(BasePlexMedia.SortIndex), false, new[] { "B", "C", "A" })]
    [Arguments(nameof(BasePlexMedia.SortIndex), true, new[] { "A", "C", "B" })]
    [Arguments(nameof(PlexMediaSlimDTO.Year), false, new[] { "2002", "2000", "2001" })]
    [Arguments(nameof(PlexMediaSlimDTO.Year), true, new[] { "2001", "2000", "2002" })]
    [Arguments(nameof(BasePlexMedia.VideoQuality), false, new[] { "480", "2160", "720" })]
    [Arguments(nameof(BasePlexMedia.VideoQuality), true, new[] { "720", "2160", "480" })]
    [Arguments(nameof(BasePlexMedia.AddedAt), false, new[] { "2026-02", "2026-03", "2026-01" })]
    [Arguments(nameof(BasePlexMedia.AddedAt), true, new[] { "2026-01", "2026-03", "2026-02" })]
    [Arguments(nameof(BasePlexMedia.UpdatedAt), false, new[] { "#", "2026-03", "2026-02" })]
    [Arguments(nameof(BasePlexMedia.UpdatedAt), true, new[] { "2026-02", "2026-03", "#" })]
    [Arguments(nameof(BasePlexMedia.Duration), false, new[] { "20–30 min", "10–20 min", "30–40 min" })]
    [Arguments(nameof(BasePlexMedia.Duration), true, new[] { "30–40 min", "10–20 min", "20–30 min" })]
    [Arguments(nameof(BasePlexMedia.MediaSize), false, new[] { "3–4 GB", "2–3 GB", "1–2 GB" })]
    [Arguments(nameof(BasePlexMedia.MediaSize), true, new[] { "1–2 GB", "2–3 GB", "3–4 GB" })]
    public async Task ShouldBuildOtherVideoNavigationIndexesFromTranslatedRows(
        string sortField,
        bool descending,
        string[] expectedLabels
    )
    {
        // Arrange
        await ArrangeOtherVideoSnapshotsAsync();
        var snapshots = IDbContext.MediaOverviewOtherVideoSnapshots;
        var sort = (sortField, descending);
        var navigationRows = snapshots.SelectNavigationRows(sort);

        // Act
        var projectedRows = await navigationRows
            .OrderBy(x => x.SearchTitle)
            .Select(x => new { x.SearchTitle, x.Year, x.QualityValue, x.SortRank })
            .ToListAsync(CancellationToken);
        var indexes = await MediaNavigationIndexBuilder.BuildAsync(navigationRows, sort, CancellationToken);

        // Assert
        projectedRows.Select(x => (x.SearchTitle, x.Year, x.QualityValue)).ShouldBe(
            [
                (SearchTitle: "A", Year: (int?)2000, QualityValue: (int?)480),
                (SearchTitle: "B", Year: (int?)2001, QualityValue: (int?)720),
                (SearchTitle: "C", Year: (int?)2002, QualityValue: (int?)2160),
            ]
        );
        projectedRows.Select(x => x.SortRank).ShouldBe(
            sortField switch
            {
                nameof(PlexMediaSlimDTO.Year) => new[] { 1, 2, 0 },
                nameof(BasePlexMedia.VideoQuality) => new[] { 0, 2, 1 },
                nameof(BasePlexMedia.AddedAt) => new[] { 2, 0, 1 },
                nameof(BasePlexMedia.UpdatedAt) => new[] { 0, 2, 1 },
                nameof(BasePlexMedia.Duration) => new[] { 1, 0, 2 },
                nameof(BasePlexMedia.MediaSize) => new[] { 2, 1, 0 },
                _ => new[] { 2, 0, 1 },
            }
        );
        indexes.Select(x => (x.Label, x.Index)).ShouldBe(expectedLabels.Select((label, index) => (label, index)));
    }

    [Test]
    [Arguments(nameof(BasePlexMedia.SortIndex), false, new[] { "B", "C", "A" })]
    [Arguments(nameof(BasePlexMedia.SortIndex), true, new[] { "A", "C", "B" })]
    [Arguments(nameof(BasePlexMedia.AddedAt), false, new[] { "2026-02", "2026-03", "2026-01" })]
    [Arguments(nameof(BasePlexMedia.AddedAt), true, new[] { "2026-01", "2026-03", "2026-02" })]
    [Arguments(nameof(BasePlexMedia.UpdatedAt), false, new[] { "#", "2026-03", "2026-02" })]
    [Arguments(nameof(BasePlexMedia.UpdatedAt), true, new[] { "2026-02", "2026-03", "#" })]
    [Arguments(nameof(BasePlexMedia.Duration), false, new[] { "20–30 min", "10–20 min", "30–40 min" })]
    [Arguments(nameof(BasePlexMedia.Duration), true, new[] { "30–40 min", "10–20 min", "20–30 min" })]
    [Arguments(nameof(BasePlexMedia.MediaSize), false, new[] { "3–4 GB", "2–3 GB", "1–2 GB" })]
    [Arguments(nameof(BasePlexMedia.MediaSize), true, new[] { "1–2 GB", "2–3 GB", "3–4 GB" })]
    public async Task ShouldBuildPhotoNavigationIndexesWithCommonRanksThroughSqlite(
        string sortField,
        bool descending,
        string[] expectedLabels
    )
    {
        // Arrange
        await ArrangePhotoSnapshotsAsync();
        var snapshots = IDbContext.MediaOverviewPhotoAlbumSnapshots;
        var sort = (sortField, descending);
        var navigationRows = snapshots.SelectNavigationRows(sort);

        // Act
        var projectedRows = await navigationRows
            .OrderBy(x => x.SearchTitle)
            .Select(x => new { x.SearchTitle, x.Year, x.QualityValue, x.SortRank })
            .ToListAsync(CancellationToken);
        var indexes = await MediaNavigationIndexBuilder.BuildAsync(navigationRows, sort, CancellationToken);

        // Assert
        projectedRows.Select(x => (x.SearchTitle, x.Year, x.QualityValue)).ShouldBe(
            [
                (SearchTitle: "A", Year: (int?)null, QualityValue: (int?)null),
                (SearchTitle: "B", Year: (int?)null, QualityValue: (int?)null),
                (SearchTitle: "C", Year: (int?)null, QualityValue: (int?)null),
            ]
        );
        projectedRows.Select(x => x.SortRank).ShouldBe(
            sortField switch
            {
                nameof(BasePlexMedia.AddedAt) => new[] { 2, 0, 1 },
                nameof(BasePlexMedia.UpdatedAt) => new[] { 0, 2, 1 },
                nameof(BasePlexMedia.Duration) => new[] { 1, 0, 2 },
                nameof(BasePlexMedia.MediaSize) => new[] { 2, 1, 0 },
                _ => new[] { 2, 0, 1 },
            }
        );
        indexes.Select(x => (x.Label, x.Index)).ShouldBe(expectedLabels.Select((label, index) => (label, index)));
    }

    private async Task ArrangeOtherVideoSnapshotsAsync()
    {
        await SetupDatabase(
            84205,
            config =>
            {
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoCount = 3;
            }
        );
        var dbContext = IDbContext;
        var libraryId = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.OtherVideos)
            .Select(x => x.Id)
            .SingleAsync(CancellationToken);
        var mediaIds = await dbContext
            .PlexOtherVideos.Where(x => x.PlexLibraryId == libraryId)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);

        dbContext.MediaOverviewOtherVideoSnapshots.AddRange(
            new MediaOverviewOtherVideoSnapshot
            {
                PlexOtherVideoId = mediaIds[0],
                PlexLibraryId = libraryId,
                SearchTitle = "alpha",
                NavigationTitleLabel = "A",
                Year = 2000,
                VideoQuality = VideoQuality.SD,
                Duration = 600,
                AddedAt = new DateTime(2026, 1, 15),
                UpdatedAt = null,
                MediaSize = 1_000_000_000L,
                TitleRank = 2,
                YearRank = 1,
                QualityRank = 0,
                AddedAtRank = 2,
                UpdatedAtRank = 0,
                DurationRank = 1,
                MediaSizeRank = 2,
            },
            new MediaOverviewOtherVideoSnapshot
            {
                PlexOtherVideoId = mediaIds[1],
                PlexLibraryId = libraryId,
                SearchTitle = "bravo",
                NavigationTitleLabel = "B",
                Year = 2001,
                VideoQuality = VideoQuality.HD,
                Duration = 1_200,
                AddedAt = new DateTime(2026, 2, 1),
                UpdatedAt = new DateTime(2026, 2, 20),
                MediaSize = 2_000_000_000L,
                TitleRank = 0,
                YearRank = 2,
                QualityRank = 2,
                AddedAtRank = 0,
                UpdatedAtRank = 2,
                DurationRank = 0,
                MediaSizeRank = 1,
            },
            new MediaOverviewOtherVideoSnapshot
            {
                PlexOtherVideoId = mediaIds[2],
                PlexLibraryId = libraryId,
                SearchTitle = "charlie",
                NavigationTitleLabel = "C",
                Year = 2002,
                VideoQuality = VideoQuality.UHD_4K,
                Duration = 1_800,
                AddedAt = new DateTime(2026, 3, 15),
                UpdatedAt = new DateTime(2026, 3, 20),
                MediaSize = 3_000_000_000L,
                TitleRank = 1,
                YearRank = 0,
                QualityRank = 1,
                AddedAtRank = 1,
                UpdatedAtRank = 1,
                DurationRank = 2,
                MediaSizeRank = 0,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
    }

    private async Task ArrangePhotoSnapshotsAsync()
    {
        await SetupDatabase(
            84206,
            config =>
            {
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 3;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraryId = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.PhotoAlbum)
            .Select(x => x.Id)
            .SingleAsync(CancellationToken);
        var albumIds = await dbContext
            .PlexPhotoAlbums.Where(x => x.PlexLibraryId == libraryId)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);

        dbContext.MediaOverviewPhotoAlbumSnapshots.AddRange(
            new MediaOverviewPhotoAlbumSnapshot
            {
                PlexPhotoAlbumId = albumIds[0],
                PlexLibraryId = libraryId,
                SearchTitle = "alpha",
                NavigationTitleLabel = "A",
                Duration = 600,
                AddedAt = new DateTime(2026, 1, 15),
                UpdatedAt = null,
                MediaSize = 1_000_000_000L,
                TitleRank = 2,
                AddedAtRank = 2,
                UpdatedAtRank = 0,
                DurationRank = 1,
                MediaSizeRank = 2,
            },
            new MediaOverviewPhotoAlbumSnapshot
            {
                PlexPhotoAlbumId = albumIds[1],
                PlexLibraryId = libraryId,
                SearchTitle = "bravo",
                NavigationTitleLabel = "B",
                Duration = 1_200,
                AddedAt = new DateTime(2026, 2, 1),
                UpdatedAt = new DateTime(2026, 2, 20),
                MediaSize = 2_000_000_000L,
                TitleRank = 0,
                AddedAtRank = 0,
                UpdatedAtRank = 2,
                DurationRank = 0,
                MediaSizeRank = 1,
            },
            new MediaOverviewPhotoAlbumSnapshot
            {
                PlexPhotoAlbumId = albumIds[2],
                PlexLibraryId = libraryId,
                SearchTitle = "charlie",
                NavigationTitleLabel = "C",
                Duration = 1_800,
                AddedAt = new DateTime(2026, 3, 15),
                UpdatedAt = new DateTime(2026, 3, 20),
                MediaSize = 3_000_000_000L,
                TitleRank = 1,
                AddedAtRank = 1,
                UpdatedAtRank = 1,
                DurationRank = 2,
                MediaSizeRank = 0,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
    }
}
