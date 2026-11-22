namespace Reaparr.BaseTests;

public static partial class FakeData
{
    private static readonly Faker<PlexGenreVideo> _plexGenreVideo = new Faker<PlexGenreVideo>()
        .StrictMode(true)
        .RuleFor(x => x.Id, _ => 0)
        .RuleFor(x => x.Name, f => f.Name.FullName())
        .RuleFor(x => x.Key, f => f.Random.Hash(24))
        .RuleFor(x => x.Type, _ => PlexGenreType.Unknown)
        .Ignore(x => x.PlexLibraries)
        .Ignore(x => x.PlexOtherVideoGenres)
        .Ignore(x => x.PlexMovieGenres)
        .Ignore(x => x.PlexTvShowGenres);

    private static readonly Faker<PlexGenreAudio> _plexGenreAudio = new Faker<PlexGenreAudio>()
        .StrictMode(true)
        .RuleFor(x => x.Id, _ => 0)
        .RuleFor(x => x.Name, f => f.Name.FullName())
        .RuleFor(x => x.Key, f => f.Random.Hash(24))
        .Ignore(x => x.PlexLibraries)
        .Ignore(x => x.PlexMusicArtistGenres);

    public static Faker<PlexGenreVideo> GetPlexGenresVideo(Seed seed) => _plexGenreVideo.UseSeed(seed.Next());

    public static Faker<PlexGenreAudio> GetPlexGenresAudio(Seed seed) => _plexGenreAudio.UseSeed(seed.Next());
}
