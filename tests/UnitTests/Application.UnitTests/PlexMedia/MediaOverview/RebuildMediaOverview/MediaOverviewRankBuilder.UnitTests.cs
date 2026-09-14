namespace Reaparr.Application.UnitTests;

public class MediaOverviewRankBuilderUnitTests
{
    [Test]
    public void ShouldUseCanonicalMediaIdAsTieBreaker_WhenSortValuesMatch()
    {
        // Arrange
        var source = new[]
        {
            CreateMovie(9, searchTitle: "same", year: 2020, addedAt: 3, updatedAt: 3, duration: 3, mediaSize: 3, quality: VideoQuality.FullHD),
            CreateMovie(2, searchTitle: "same", year: 2020, addedAt: 3, updatedAt: 3, duration: 3, mediaSize: 3, quality: VideoQuality.FullHD),
            CreateMovie(5, searchTitle: "same", year: 2020, addedAt: 3, updatedAt: 3, duration: 3, mediaSize: 3, quality: VideoQuality.FullHD),
        };

        // Act
        var result = source.ToList().AssignRanks();

        // Assert
        result.OrderBy(x => x.TitleRank).Select(x => x.PlexMovieId).ShouldBe([2, 5, 9]);
        result.Select(x => x.TitleRank).OrderBy(x => x).ShouldBe([0, 1, 2]);
    }

    [Test]
    public void ShouldSortNullUpdatedAtBeforeNonNullValues_AndTieBreakNullsById()
    {
        // Arrange
        var source = new[]
        {
            CreateMovie(8, updatedAt: null),
            CreateMovie(3, updatedAt: null),
            CreateMovie(5, updatedAt: 10),
        };

        // Act
        var result = source.ToList().AssignRanks();

        // Assert
        result.OrderBy(x => x.UpdatedAtRank).Select(x => x.PlexMovieId).ShouldBe([3, 8, 5]);
        result.Select(x => x.UpdatedAtRank).OrderBy(x => x).ShouldBe([0, 1, 2]);
    }

    [Test]
    public void ShouldAssignDenseUniqueZeroBasedRanks_ForEverySupportedSort()
    {
        // Arrange
        var source = new[]
        {
            CreateMovie(40, searchTitle: "zulu", year: 2022, addedAt: 4, updatedAt: 4, duration: 400, mediaSize: 4000, quality: VideoQuality.UHD_4K),
            CreateMovie(10, searchTitle: "alpha", year: 2020, addedAt: 1, updatedAt: 1, duration: 100, mediaSize: 1000, quality: VideoQuality.SD),
            CreateMovie(30, searchTitle: "mike", year: 2021, addedAt: 3, updatedAt: 3, duration: 300, mediaSize: 3000, quality: VideoQuality.FullHD),
            CreateMovie(20, searchTitle: "bravo", year: 2019, addedAt: 2, updatedAt: 2, duration: 200, mediaSize: 2000, quality: VideoQuality.HD),
        };

        // Act
        var result = source.ToList().AssignRanks();
        var expectedRanks = Enumerable.Range(0, source.Length).ToArray();

        // Assert
        result.Select(x => x.TitleRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.YearRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.AddedAtRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.UpdatedAtRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.DurationRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.MediaSizeRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.QualityRank).OrderBy(x => x).ShouldBe(expectedRanks);
    }

    [Test]
    public void ShouldProduceIdenticalRanks_WhenPreparationIsRepeated()
    {
        // Arrange
        var source = new[]
        {
            CreateMovie(7, searchTitle: "title-b", year: 2022, addedAt: 2, updatedAt: null, duration: 20, mediaSize: 200, quality: VideoQuality.HD),
            CreateMovie(1, searchTitle: "title-a", year: 2022, addedAt: 1, updatedAt: 1, duration: 10, mediaSize: 100, quality: VideoQuality.FullHD),
            CreateMovie(4, searchTitle: "title-c", year: 2020, addedAt: 3, updatedAt: null, duration: 30, mediaSize: 300, quality: VideoQuality.SD),
        };

        // Act
        var first = source.ToList().AssignRanks();
        var second = source.ToList().AssignRanks();

        // Assert
        first
            .OrderBy(x => x.PlexMovieId)
            .Select(x => new { x.PlexMovieId, x.TitleRank, x.YearRank, x.AddedAtRank, x.UpdatedAtRank, x.DurationRank, x.MediaSizeRank, x.QualityRank })
            .ShouldBe(
                second
                    .OrderBy(x => x.PlexMovieId)
                    .Select(x => new { x.PlexMovieId, x.TitleRank, x.YearRank, x.AddedAtRank, x.UpdatedAtRank, x.DurationRank, x.MediaSizeRank, x.QualityRank })
            );
    }

    [Test]
    public void ShouldUseEquivalentRankBehavior_ForMoviesAndTvShowsWithOverlappingFields()
    {
        // Arrange
        var movies = new[]
        {
            CreateMovie(12, searchTitle: "same", year: 2020, addedAt: 2, updatedAt: null, duration: 100, mediaSize: 200, quality: VideoQuality.HD),
            CreateMovie(4, searchTitle: "same", year: 2019, addedAt: 1, updatedAt: 1, duration: 200, mediaSize: 100, quality: VideoQuality.FullHD),
        };
        var tvShows = movies
            .Select(x => new MediaOverviewTvShowSnapshot
            {
                PlexTvShowId = x.PlexMovieId,
                PlexLibraryId = x.PlexLibraryId,
                SearchTitle = x.SearchTitle,
                Year = x.Year,
                AddedAt = x.AddedAt,
                UpdatedAt = x.UpdatedAt,
                Duration = x.Duration,
                MediaSize = x.MediaSize,
                Quality = x.Quality,
                TitleRank = 0,
                YearRank = 0,
                AddedAtRank = 0,
                UpdatedAtRank = 0,
                DurationRank = 0,
                MediaSizeRank = 0,
                QualityRank = 0,
            })
            .ToArray();

        // Act
        var movieResult = movies.ToList().AssignRanks();
        var tvResult = tvShows.ToList().AssignRanks();

        // Assert
        movieResult
            .OrderBy(x => x.PlexMovieId)
            .Select(x => new { x.PlexMovieId, x.TitleRank, x.YearRank, x.AddedAtRank, x.UpdatedAtRank, x.DurationRank, x.MediaSizeRank, x.QualityRank })
            .ShouldBe(
                tvResult
                    .OrderBy(x => x.PlexTvShowId)
                    .Select(x => new { PlexMovieId = x.PlexTvShowId, x.TitleRank, x.YearRank, x.AddedAtRank, x.UpdatedAtRank, x.DurationRank, x.MediaSizeRank, x.QualityRank })
            );
    }

    private static MediaOverviewMovieSnapshot CreateMovie(
        int id,
        string searchTitle = "title",
        int year = 2020,
        int addedAt = 1,
        int? updatedAt = 1,
        int duration = 100,
        long mediaSize = 100,
        VideoQuality quality = VideoQuality.Unknown
    ) =>
        new()
        {
            PlexMovieId = id,
            PlexLibraryId = 1,
            SearchTitle = searchTitle,
            Year = year,
            AddedAt = new DateTime(2024, 1, addedAt),
            UpdatedAt = updatedAt.HasValue ? new DateTime(2024, 2, updatedAt.Value) : null,
            Duration = duration,
            MediaSize = mediaSize,
            Quality = quality,
            TitleRank = 0,
            YearRank = 0,
            AddedAtRank = 0,
            UpdatedAtRank = 0,
            DurationRank = 0,
            MediaSizeRank = 0,
            QualityRank = 0,
        };
}
