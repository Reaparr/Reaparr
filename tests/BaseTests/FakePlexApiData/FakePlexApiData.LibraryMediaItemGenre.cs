namespace Reaparr.BaseTests;

public partial class FakePlexApiData
{
    private static readonly Faker<LibraryMediaItemGenreDTO> _libraryMediaItemGenreDTO =
        new Faker<LibraryMediaItemGenreDTO>()
            .StrictMode(true)
            .RuleFor(x => x.PlexId, _ => GetUniqueNumber())
            .RuleFor(x => x.Name, f => f.PlexMedia().MediaGenre())
            .RuleFor(x => x.Key, (_, x) => x.Name.ToMd5Hash())
            .RuleFor(x => x.Filter, (_, x) => $"genre={x.PlexId}");

    public static Faker<LibraryMediaItemGenreDTO> GetLibraryMediaItemGenreDTO(Seed seed) =>
        _libraryMediaItemGenreDTO.UseSeed(seed.Next());
}
