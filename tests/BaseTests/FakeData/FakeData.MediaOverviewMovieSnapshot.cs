namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<MediaOverviewMovieSnapshot> _mediaOverviewMovieSnapshot = new Faker<MediaOverviewMovieSnapshot>()
        .StrictMode(true)
        .Ignore(x => x.Id)
        .RuleFor(x => x.PlexMovieId, _ => GetUniqueNumber())
        .Ignore(x => x.PlexMovie)
        .RuleFor(x => x.PlexLibraryId, _ => GetUniqueNumber())
        .Ignore(x => x.PlexLibrary)
        .RuleFor(x => x.TitleRank, _ => 0)
        .RuleFor(x => x.YearRank, _ => 0)
        .RuleFor(x => x.AddedAtRank, _ => 0)
        .RuleFor(x => x.UpdatedAtRank, _ => 0)
        .RuleFor(x => x.DurationRank, _ => 0)
        .RuleFor(x => x.MediaSizeRank, _ => 0)
        .RuleFor(x => x.QualityRank, _ => 0)
        .RuleFor(x => x.SearchTitle, f => f.Lorem.Word())
        .RuleFor(x => x.Year, f => f.Random.Int(1900, 2030))
        .RuleFor(x => x.AddedAt, f => f.Date.Past(4))
        .RuleFor(x => x.UpdatedAt, f => f.Random.Bool() ? f.Date.Recent(30) : null)
        .RuleFor(x => x.Duration, f => f.Random.Int(1, 3_000_000))
        .RuleFor(x => x.MediaSize, f => f.Random.Long(1_000, 30_000_000))
        .RuleFor(x => x.Quality, f => f.Random.Enum<VideoQuality>());

    public static Faker<MediaOverviewMovieSnapshot> GetMediaOverviewMovieSnapshots(Seed seed) =>
        _mediaOverviewMovieSnapshot.Clone().UseSeed(seed.Next());
}
