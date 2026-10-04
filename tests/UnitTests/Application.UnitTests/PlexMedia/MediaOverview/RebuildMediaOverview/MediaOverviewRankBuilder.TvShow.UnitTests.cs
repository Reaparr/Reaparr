
namespace Reaparr.Application.UnitTests;

public class MediaOverviewRankBuilderTvShowUnitTests
{
    [Test]
    public void ShouldAssignExpectedRanks_ForEveryTvShowSort()
    {
        // Arrange
        var source = FakeData
            .GetMediaOverviewTvShowSnapshots(new Seed(7))
            .RuleFor(x => x.PlexTvShowId, f => new[] { 40, 10, 30, 20 }[f.IndexFaker])
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
        result.OrderBy(x => x.TitleRank).Select(x => x.PlexTvShowId).ShouldBe([10, 20, 30, 40]);
        result.OrderBy(x => x.YearRank).Select(x => x.PlexTvShowId).ShouldBe([20, 10, 30, 40]);
        result.OrderBy(x => x.AddedAtRank).Select(x => x.PlexTvShowId).ShouldBe([10, 20, 30, 40]);
        result.OrderBy(x => x.UpdatedAtRank).Select(x => x.PlexTvShowId).ShouldBe([10, 20, 30, 40]);
        result.OrderBy(x => x.DurationRank).Select(x => x.PlexTvShowId).ShouldBe([10, 20, 30, 40]);
        result.OrderBy(x => x.MediaSizeRank).Select(x => x.PlexTvShowId).ShouldBe([10, 20, 30, 40]);
        result.OrderBy(x => x.QualityRank).Select(x => x.PlexTvShowId).ShouldBe([10, 20, 30, 40]);
        result.Select(x => x.TitleRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.YearRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.AddedAtRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.UpdatedAtRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.DurationRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.MediaSizeRank).OrderBy(x => x).ShouldBe(expectedRanks);
        result.Select(x => x.QualityRank).OrderBy(x => x).ShouldBe(expectedRanks);
    }
}
