namespace Reaparr.Application.UnitTests;

public class MediaOverviewSnapshotQueryExtensionsUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(PlexMediaType.OtherVideos, SortType.Title, false, new[] { "B", "C", "A" })]
    [Arguments(PlexMediaType.OtherVideos, SortType.Title, true, new[] { "A", "C", "B" })]
    [Arguments(PlexMediaType.OtherVideos, SortType.Year, false, new[] { "C", "A", "B" })]
    [Arguments(PlexMediaType.OtherVideos, SortType.Year, true, new[] { "B", "A", "C" })]
    [Arguments(PlexMediaType.OtherVideos, SortType.VideoQuality, false, new[] { "A", "C", "B" })]
    [Arguments(PlexMediaType.OtherVideos, SortType.VideoQuality, true, new[] { "B", "C", "A" })]
    [Arguments(PlexMediaType.OtherVideos, SortType.AddedAt, false, new[] { "B", "C", "A" })]
    [Arguments(PlexMediaType.OtherVideos, SortType.AddedAt, true, new[] { "A", "C", "B" })]
    [Arguments(PlexMediaType.OtherVideos, SortType.UpdatedAt, false, new[] { "A", "C", "B" })]
    [Arguments(PlexMediaType.OtherVideos, SortType.UpdatedAt, true, new[] { "B", "C", "A" })]
    [Arguments(PlexMediaType.OtherVideos, SortType.Duration, false, new[] { "B", "A", "C" })]
    [Arguments(PlexMediaType.OtherVideos, SortType.Duration, true, new[] { "C", "A", "B" })]
    [Arguments(PlexMediaType.OtherVideos, SortType.MediaSize, false, new[] { "C", "B", "A" })]
    [Arguments(PlexMediaType.OtherVideos, SortType.MediaSize, true, new[] { "A", "B", "C" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.Title, false, new[] { "B", "C", "A" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.Title, true, new[] { "A", "C", "B" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.AddedAt, false, new[] { "B", "C", "A" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.AddedAt, true, new[] { "A", "C", "B" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.UpdatedAt, false, new[] { "A", "C", "B" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.UpdatedAt, true, new[] { "B", "C", "A" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.Duration, false, new[] { "B", "A", "C" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.Duration, true, new[] { "C", "A", "B" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.MediaSize, false, new[] { "C", "B", "A" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.MediaSize, true, new[] { "A", "B", "C" })]
    public async Task ShouldOrderSnapshotRanksThroughSqlite(
        PlexMediaType mediaType,
        SortType sortType,
        bool descending,
        string[] expectedLabels
    )
    {
        // Arrange
        if (mediaType == PlexMediaType.MusicArtist)
            await ArrangeMusicSnapshotsAsync();
        else
            await ArrangeOtherVideoSnapshotsAsync();
        var sort = (sortType, descending);

        // Act
        var labels = mediaType == PlexMediaType.MusicArtist
            ? IDbContext.MediaOverviewMusicArtistSnapshots.ApplyRankOrder(sort).Select(x => x.NavigationTitleLabel)
            : IDbContext.MediaOverviewOtherVideoSnapshots.ApplyRankOrder(sort).Select(x => x.NavigationTitleLabel);
        var orderedLabels = await labels.ToListAsync(CancellationToken);

        // Assert
        orderedLabels.ShouldBe(expectedLabels);
    }

    [Test]
    [Arguments(SortType.Title, false, new[] { "B", "C", "A" })]
    [Arguments(SortType.Title, true, new[] { "A", "C", "B" })]
    [Arguments(SortType.Year, false, new[] { "2002", "2000", "2001" })]
    [Arguments(SortType.Year, true, new[] { "2001", "2000", "2002" })]
    [Arguments(SortType.VideoQuality, false, new[] { "480", "2160", "720" })]
    [Arguments(SortType.VideoQuality, true, new[] { "720", "2160", "480" })]
    [Arguments(SortType.AddedAt, false, new[] { "2026-02", "2026-03", "2026-01" })]
    [Arguments(SortType.AddedAt, true, new[] { "2026-01", "2026-03", "2026-02" })]
    [Arguments(SortType.UpdatedAt, false, new[] { "#", "2026-03", "2026-02" })]
    [Arguments(SortType.UpdatedAt, true, new[] { "2026-02", "2026-03", "#" })]
    [Arguments(SortType.Duration, false, new[] { "20–30 min", "10–20 min", "30–40 min" })]
    [Arguments(SortType.Duration, true, new[] { "30–40 min", "10–20 min", "20–30 min" })]
    [Arguments(SortType.MediaSize, false, new[] { "3–4 GB", "2–3 GB", "1–2 GB" })]
    [Arguments(SortType.MediaSize, true, new[] { "1–2 GB", "2–3 GB", "3–4 GB" })]
    public async Task ShouldBuildOtherVideoNavigationIndexesFromTranslatedRows(
        SortType sortType,
        bool descending,
        string[] expectedLabels
    )
    {
        // Arrange
        await ArrangeOtherVideoSnapshotsAsync();
        var snapshots = IDbContext.MediaOverviewOtherVideoSnapshots;
        var sort = (sortType, descending);
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
            sortType switch
            {
                SortType.Year => new[] { 1, 2, 0 },
                SortType.VideoQuality => new[] { 0, 2, 1 },
                SortType.AddedAt => new[] { 2, 0, 1 },
                SortType.UpdatedAt => new[] { 0, 2, 1 },
                SortType.Duration => new[] { 1, 0, 2 },
                SortType.MediaSize => new[] { 2, 1, 0 },
                _ => new[] { 2, 0, 1 },
            }
        );
        indexes.Select(x => (x.Label, x.Index)).ShouldBe(expectedLabels.Select((label, index) => (label, index)));
    }

    [Test]
    [Arguments(PlexMediaType.PhotoAlbum, SortType.Title, false, new[] { "B", "C", "A" })]
    [Arguments(PlexMediaType.PhotoAlbum, SortType.Title, true, new[] { "A", "C", "B" })]
    [Arguments(PlexMediaType.PhotoAlbum, SortType.AddedAt, false, new[] { "2026-02", "2026-03", "2026-01" })]
    [Arguments(PlexMediaType.PhotoAlbum, SortType.AddedAt, true, new[] { "2026-01", "2026-03", "2026-02" })]
    [Arguments(PlexMediaType.PhotoAlbum, SortType.UpdatedAt, false, new[] { "#", "2026-03", "2026-02" })]
    [Arguments(PlexMediaType.PhotoAlbum, SortType.UpdatedAt, true, new[] { "2026-02", "2026-03", "#" })]
    [Arguments(PlexMediaType.PhotoAlbum, SortType.Duration, false, new[] { "20–30 min", "10–20 min", "30–40 min" })]
    [Arguments(PlexMediaType.PhotoAlbum, SortType.Duration, true, new[] { "30–40 min", "10–20 min", "20–30 min" })]
    [Arguments(PlexMediaType.PhotoAlbum, SortType.MediaSize, false, new[] { "3–4 GB", "2–3 GB", "1–2 GB" })]
    [Arguments(PlexMediaType.PhotoAlbum, SortType.MediaSize, true, new[] { "1–2 GB", "2–3 GB", "3–4 GB" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.Title, false, new[] { "B", "C", "A" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.Title, true, new[] { "A", "C", "B" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.AddedAt, false, new[] { "2026-02", "2026-03", "2026-01" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.AddedAt, true, new[] { "2026-01", "2026-03", "2026-02" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.UpdatedAt, false, new[] { "#", "2026-03", "2026-02" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.UpdatedAt, true, new[] { "2026-02", "2026-03", "#" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.Duration, false, new[] { "20–30 min", "10–20 min", "30–40 min" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.Duration, true, new[] { "30–40 min", "10–20 min", "20–30 min" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.MediaSize, false, new[] { "3–4 GB", "2–3 GB", "1–2 GB" })]
    [Arguments(PlexMediaType.MusicArtist, SortType.MediaSize, true, new[] { "1–2 GB", "2–3 GB", "3–4 GB" })]
    public async Task ShouldBuildCommonNavigationIndexesWithTranslatedRanksThroughSqlite(
        PlexMediaType mediaType,
        SortType sortType,
        bool descending,
        string[] expectedLabels
    )
    {
        // Arrange
        if (mediaType == PlexMediaType.MusicArtist)
            await ArrangeMusicSnapshotsAsync();
        else
            await ArrangePhotoSnapshotsAsync();
        var sort = (sortType, descending);
        var navigationRows = mediaType == PlexMediaType.MusicArtist
            ? IDbContext.MediaOverviewMusicArtistSnapshots.SelectNavigationRows(sort)
            : IDbContext.MediaOverviewPhotoAlbumSnapshots.SelectNavigationRows(sort);

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
            sortType switch
            {
                SortType.AddedAt => new[] { 2, 0, 1 },
                SortType.UpdatedAt => new[] { 0, 2, 1 },
                SortType.Duration => new[] { 1, 0, 2 },
                SortType.MediaSize => new[] { 2, 1, 0 },
                _ => new[] { 2, 0, 1 },
            }
        );
        indexes.Select(x => (x.Label, x.Index)).ShouldBe(expectedLabels.Select((label, index) => (label, index)));
    }

    private async Task ArrangeMusicSnapshotsAsync()
    {
        await SetupDatabase(
            84207,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 3;
            }
        );
        var dbContext = IDbContext;
        var artists = await dbContext.PlexMusicArtists
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexLibraryId })
            .ToListAsync(CancellationToken);
        artists.Count.ShouldBe(3);
        var searchTitles = new[] { "alpha", "bravo", "charlie" };
        var titleRanks = new[] { 2, 0, 1 };
        var updatedRanks = new[] { 0, 2, 1 };
        var durationRanks = new[] { 1, 0, 2 };
        var sizeRanks = new[] { 2, 1, 0 };
        for (var index = 0; index < artists.Count; index++)
        {
            dbContext.MediaOverviewMusicArtistSnapshots.Add(new MediaOverviewMusicArtistSnapshot
            {
                PlexArtistId = artists[index].Id,
                PlexLibraryId = artists[index].PlexLibraryId,
                SearchTitle = searchTitles[index],
                NavigationTitleLabel = ((char)('A' + index)).ToString(),
                Duration = (index + 1) * 600,
                AddedAt = new DateTime(2026, index + 1, 1),
                UpdatedAt = index == 0 ? null : new DateTime(2026, index + 1, 20),
                MediaSize = (index + 1) * 1_000_000_000L,
                TitleRank = titleRanks[index],
                AddedAtRank = titleRanks[index],
                UpdatedAtRank = updatedRanks[index],
                DurationRank = durationRanks[index],
                MediaSizeRank = sizeRanks[index],
            });
        }
        await dbContext.SaveChangesAsync(CancellationToken);
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
