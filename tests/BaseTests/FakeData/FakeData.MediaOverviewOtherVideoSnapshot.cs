namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<MediaOverviewOtherVideoSnapshot> _mediaOverviewOtherVideoSnapshot = new Faker<MediaOverviewOtherVideoSnapshot>()
        .StrictMode(true)
        .Ignore(x => x.Id)
        .RuleFor(x => x.PlexOtherVideoId, _ => GetUniqueNumber())
        .Ignore(x => x.PlexOtherVideo)
        .RuleFor(x => x.PlexLibraryId, _ => GetUniqueNumber())
        .Ignore(x => x.PlexLibrary)
        .RuleFor(x => x.QualityRank, _ => 0)
        .RuleFor(x => x.Quality, _ => VideoQuality.None)
        .RuleFor(x => x.TitleRank, _ => 0)
        .RuleFor(x => x.YearRank, _ => 0)
        .RuleFor(x => x.AddedAtRank, _ => 0)
        .RuleFor(x => x.UpdatedAtRank, _ => 0)
        .RuleFor(x => x.DurationRank, _ => 0)
        .RuleFor(x => x.MediaSizeRank, _ => 0);

    public static Faker<MediaOverviewOtherVideoSnapshot> GetMediaOverviewOtherVideoSnapshots(Seed seed) =>
        _mediaOverviewOtherVideoSnapshot.Clone().UseSeed(seed.Next());
}
