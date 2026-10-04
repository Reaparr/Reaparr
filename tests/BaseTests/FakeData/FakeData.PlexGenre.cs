namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<PlexGenre> _plexGenre = new Faker<PlexGenre>()
        .StrictMode(true)
        .RuleFor(x => x.Id, _ => 0)
        .RuleFor(x => x.Name, f => f.Name.FullName())
        .RuleFor(x => x.Key, f => f.Random.Hash(24))
        .RuleFor(x => x.Type, _ => PlexGenreType.Unknown)
        .Ignore(x => x.PlexLibraries)
        .Ignore(x => x.PlexMusicArtistGenres)
        .Ignore(x => x.PlexOtherVideoGenres)
        .Ignore(x => x.PlexMovieGenres)
        .Ignore(x => x.PlexTvShowGenres);

    public static Faker<PlexGenre> GetPlexGenres(Seed seed, Action<FakeDataConfig>? options = null) =>
        _plexGenre.UseSeed(seed.Next());
}
