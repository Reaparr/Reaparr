namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<MediaOverviewMusicArtistSnapshot> _mediaOverviewMusicArtistSnapshot = new Faker<MediaOverviewMusicArtistSnapshot>()
        .StrictMode(true)
        .Ignore(x => x.Id)
        .RuleFor(x => x.PlexArtistId, _ => GetUniqueNumber())
        .Ignore(x => x.PlexArtist)
        .RuleFor(x => x.PlexLibraryId, _ => GetUniqueNumber())
        .Ignore(x => x.PlexLibrary)
        .RuleFor(x => x.TitleRank, _ => 0)
        .RuleFor(x => x.YearRank, _ => 0)
        .RuleFor(x => x.AddedAtRank, _ => 0)
        .RuleFor(x => x.UpdatedAtRank, _ => 0)
        .RuleFor(x => x.DurationRank, _ => 0)
        .RuleFor(x => x.MediaSizeRank, _ => 0);

    public static Faker<MediaOverviewMusicArtistSnapshot> GetMediaOverviewMusicArtistSnapshots(Seed seed) =>
        _mediaOverviewMusicArtistSnapshot.Clone().UseSeed(seed.Next());
}
