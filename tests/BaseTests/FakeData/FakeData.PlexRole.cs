namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<PlexActor> _plexActor = new Faker<PlexActor>()
        .StrictMode(true)
        .RuleFor(x => x.Id, _ => 0)
        .RuleFor(x => x.Name, f => f.Name.FullName())
        .RuleFor(x => x.Key, f => f.Random.Hash(24))
        .Ignore(x => x.PlexLibraries)
        .Ignore(x => x.PlexMusicArtistActors)
        .Ignore(x => x.PlexOtherVideoActors)
        .Ignore(x => x.PlexMovieActors)
        .Ignore(x => x.PlexTvShowActors);

    public static Faker<PlexActor> GetPlexActors(Seed seed, Action<FakeDataConfig>? options = null) =>
        _plexActor.UseSeed(seed.Next());
}
