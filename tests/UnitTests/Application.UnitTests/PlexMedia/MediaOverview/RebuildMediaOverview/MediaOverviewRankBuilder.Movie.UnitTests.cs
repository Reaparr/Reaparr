
namespace Reaparr.Application.UnitTests;

public class MediaOverviewRankBuilderMovieUnitTests
{
    [Test]
    public void ShouldUseCanonicalMediaIdAsTieBreaker_WhenMovieSortValuesMatch()
    {
        // Arrange
        var source = FakeData
            .GetMediaOverviewMovieSnapshots(new Seed(1))
            .RuleFor(x => x.PlexMovieId, f => new[] { 9, 2, 5 }[f.IndexFaker])
            .RuleFor(x => x.SearchTitle, _ => "same")
            .RuleFor(x => x.Year, _ => 2020)
            .RuleFor(x => x.AddedAt, _ => new DateTime(2024, 1, 3))
            .RuleFor(x => x.UpdatedAt, _ => new DateTime(2024, 2, 3))
            .RuleFor(x => x.Duration, _ => 3)
            .RuleFor(x => x.MediaSize, _ => 3)
            .RuleFor(x => x.Quality, _ => VideoQuality.FullHD)
            .Generate(3);

        // Act
        var result = source.AssignRanks();

        // Assert
        result.OrderBy(x => x.TitleRank).Select(x => x.PlexMovieId).ShouldBe([2, 5, 9]);
        result.Select(x => x.TitleRank).OrderBy(x => x).ShouldBe([0, 1, 2]);
    }

    [Test]
    public void ShouldSortNullUpdatedAtBeforeNonNullValues_AndTieBreakNullMoviesById()
    {
        // Arrange
        var source = FakeData
            .GetMediaOverviewMovieSnapshots(new Seed(2))
            .RuleFor(x => x.PlexMovieId, f => new[] { 8, 3, 5 }[f.IndexFaker])
            .RuleFor(
                x => x.UpdatedAt,
                f => new DateTime?[] { null, null, new DateTime(2024, 2, 10) }[f.IndexFaker]
            )
            .Generate(3);

        // Act
        var result = source.AssignRanks();

        // Assert
        result.OrderBy(x => x.UpdatedAtRank).Select(x => x.PlexMovieId).ShouldBe([3, 8, 5]);
        result.Select(x => x.UpdatedAtRank).OrderBy(x => x).ShouldBe([0, 1, 2]);
    }

    [Test]
    public void ShouldAssignExpectedRanks_ForEveryMovieSort()
    {
        // Arrange
        var source = FakeData
            .GetMediaOverviewMovieSnapshots(new Seed(3))
            .RuleFor(x => x.PlexMovieId, f => new[] { 40, 10, 30, 20 }[f.IndexFaker])
            .RuleFor(x => x.SearchTitle, f => new[] { "zulu", "alpha", "mike", "bravo" }[f.IndexFaker])
            .RuleFor(x => x.Year, f => new[] { 2022, 2020, 2021, 2019 }[f.IndexFaker])
            .RuleFor(x => x.AddedAt, f => new[] { 4, 1, 3, 2 }.Select(day => new DateTime(2024, 1, day)).ElementAt(f.IndexFaker))
            .RuleFor(
                x => x.UpdatedAt,
                f => new[] { 4, 1, 3, 2 }
                    .Select(day => (DateTime?)new DateTime(2024, 2, day))
                    .ElementAt(f.IndexFaker)
            )
            .RuleFor(x => x.Duration, f => new[] { 400, 100, 300, 200 }[f.IndexFaker])
            .RuleFor(x => x.MediaSize, f => new[] { 4000L, 1000L, 3000L, 2000L }[f.IndexFaker])
            .RuleFor(x => x.Quality, f => new[] { VideoQuality.UHD_4K, VideoQuality.SD, VideoQuality.FullHD, VideoQuality.HD }[f.IndexFaker])
            .Generate(4);

        // Act
        var result = source.AssignRanks();
        var expectedRanks = Enumerable.Range(0, source.Count).ToArray();

        // Assert
        result.OrderBy(x => x.TitleRank).Select(x => x.PlexMovieId).ShouldBe([10, 20, 30, 40]);
        result.OrderBy(x => x.YearRank).Select(x => x.PlexMovieId).ShouldBe([20, 10, 30, 40]);
        result.OrderBy(x => x.AddedAtRank).Select(x => x.PlexMovieId).ShouldBe([10, 20, 30, 40]);
        result.OrderBy(x => x.UpdatedAtRank).Select(x => x.PlexMovieId).ShouldBe([10, 20, 30, 40]);
        result.OrderBy(x => x.DurationRank).Select(x => x.PlexMovieId).ShouldBe([10, 20, 30, 40]);
        result.OrderBy(x => x.MediaSizeRank).Select(x => x.PlexMovieId).ShouldBe([10, 20, 30, 40]);
        result.OrderBy(x => x.QualityRank).Select(x => x.PlexMovieId).ShouldBe([10, 20, 30, 40]);
        result.Select(x => x.TitleRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.YearRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.AddedAtRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.UpdatedAtRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.DurationRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.MediaSizeRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.QualityRank).OrderBy(x => x).ShouldBe(expectedRanks);
    }

    [Test]
    public void ShouldProduceIdenticalRanks_WhenMoviePreparationIsRepeated()
    {
        // Arrange
        var source = FakeData.GetMediaOverviewMovieSnapshots(new Seed(4)).Generate(3);

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
        var movies = FakeData
            .GetMediaOverviewMovieSnapshots(new Seed(5))
            .RuleFor(x => x.PlexMovieId, f => new[] { 12, 4 }[f.IndexFaker])
            .RuleFor(x => x.SearchTitle, _ => "same")
            .RuleFor(x => x.Year, f => new[] { 2020, 2019 }[f.IndexFaker])
            .RuleFor(x => x.AddedAt, f => new[] { 2, 1 }.Select(day => new DateTime(2024, 1, day)).ElementAt(f.IndexFaker))
            .RuleFor(
                x => x.UpdatedAt,
                f => new[] { (DateTime?)null, new DateTime(2024, 2, 1) }[f.IndexFaker]
            )
            .RuleFor(x => x.Duration, f => new[] { 100, 200 }[f.IndexFaker])
            .RuleFor(x => x.MediaSize, f => new[] { 200L, 100L }[f.IndexFaker])
            .RuleFor(x => x.Quality, f => new[] { VideoQuality.HD, VideoQuality.FullHD }[f.IndexFaker])
            .Generate(2);
        var tvShows = FakeData
            .GetMediaOverviewTvShowSnapshots(new Seed(6))
            .RuleFor(x => x.PlexTvShowId, f => new[] { 12, 4 }[f.IndexFaker])
            .RuleFor(x => x.SearchTitle, _ => "same")
            .RuleFor(x => x.Year, f => new[] { 2020, 2019 }[f.IndexFaker])
            .RuleFor(x => x.AddedAt, f => new[] { 2, 1 }.Select(day => new DateTime(2024, 1, day)).ElementAt(f.IndexFaker))
            .RuleFor(
                x => x.UpdatedAt,
                f => new[] { (DateTime?)null, new DateTime(2024, 2, 1) }[f.IndexFaker]
            )
            .RuleFor(x => x.Duration, f => new[] { 100, 200 }[f.IndexFaker])
            .RuleFor(x => x.MediaSize, f => new[] { 200L, 100L }[f.IndexFaker])
            .RuleFor(x => x.Quality, f => new[] { VideoQuality.HD, VideoQuality.FullHD }[f.IndexFaker])
            .Generate(2);

        // Act
        var movieResult = movies.AssignRanks();
        var tvResult = tvShows.AssignRanks();

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
}
