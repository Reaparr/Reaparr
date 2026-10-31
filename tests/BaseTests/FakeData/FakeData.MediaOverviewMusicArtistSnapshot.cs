namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<MediaOverviewMusicArtistSnapshot> _mediaOverviewMusicArtistSnapshot =
        new Faker<MediaOverviewMusicArtistSnapshot>()
            .StrictMode(true)
            .Ignore(x => x.Id)
            .RuleFor(x => x.PlexArtistId, _ => GetUniqueNumber())
            .Ignore(x => x.PlexArtist)
            .RuleFor(x => x.PlexLibraryId, _ => GetUniqueNumber())
            .Ignore(x => x.PlexLibrary)
            .RuleFor(x => x.SearchTitle, f => f.Lorem.Word())
            .RuleFor(x => x.NavigationTitleLabel, _ => "A")
            .RuleFor(x => x.Duration, f => f.Random.Int(1, 3_000_000))
            .RuleFor(x => x.AddedAt, f => f.Date.Past(4))
            .RuleFor(x => x.UpdatedAt, f => f.Random.Bool() ? f.Date.Recent(30) : null)
            .RuleFor(x => x.MediaSize, f => f.Random.Long(1_000, 30_000_000))
            .RuleFor(x => x.TitleRank, _ => 0)
            .RuleFor(x => x.AddedAtRank, _ => 0)
            .RuleFor(x => x.UpdatedAtRank, _ => 0)
            .RuleFor(x => x.DurationRank, _ => 0)
            .RuleFor(x => x.MediaSizeRank, _ => 0);

    public static Faker<MediaOverviewMusicArtistSnapshot> GetMediaOverviewMusicArtistSnapshots(Seed seed) =>
        _mediaOverviewMusicArtistSnapshot
            .Clone() // Clone to isolate per-call random state.
            .UseSeed(seed.Next());
}
