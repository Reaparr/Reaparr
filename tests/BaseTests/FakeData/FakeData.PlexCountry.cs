namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<PlexCountry> _plexCountry = new Faker<PlexCountry>()
        .StrictMode(true)
        .RuleFor(x => x.Id, _ => 0)
        .RuleFor(x => x.Name, f => f.Address.Country())
        .RuleFor(x => x.Key, f => f.Random.Hash(24))
        .Ignore(x => x.PlexLibraries)
        .Ignore(x => x.PlexMusicArtistCountries)
        .Ignore(x => x.PlexMusicAlbumCountries)
        .Ignore(x => x.PlexOtherVideoCountries)
        .Ignore(x => x.PlexMovieCountries)
        .Ignore(x => x.PlexTvShowCountries);

    public static Faker<PlexCountry> GetPlexCountries(Seed seed) => _plexCountry.UseSeed(seed.Next());
}
