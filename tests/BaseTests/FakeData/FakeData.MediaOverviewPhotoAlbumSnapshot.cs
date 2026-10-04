namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<MediaOverviewPhotoAlbumSnapshot> _mediaOverviewPhotoAlbumSnapshot = new Faker<MediaOverviewPhotoAlbumSnapshot>()
        .StrictMode(true)
        .Ignore(x => x.Id)
        .RuleFor(x => x.PlexPhotoAlbumId, _ => GetUniqueNumber())
        .Ignore(x => x.PlexPhotoAlbum)
        .RuleFor(x => x.PlexLibraryId, _ => GetUniqueNumber())
        .Ignore(x => x.PlexLibrary)
        .RuleFor(x => x.TitleRank, _ => 0)
        .RuleFor(x => x.YearRank, _ => 0)
        .RuleFor(x => x.AddedAtRank, _ => 0)
        .RuleFor(x => x.UpdatedAtRank, _ => 0)
        .RuleFor(x => x.DurationRank, _ => 0)
        .RuleFor(x => x.MediaSizeRank, _ => 0);

    public static Faker<MediaOverviewPhotoAlbumSnapshot> GetMediaOverviewPhotoAlbumSnapshots(Seed seed) =>
        _mediaOverviewPhotoAlbumSnapshot.Clone().UseSeed(seed.Next());
}
