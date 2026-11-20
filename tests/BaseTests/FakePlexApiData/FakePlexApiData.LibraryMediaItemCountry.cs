namespace Reaparr.BaseTests;

public partial class FakePlexApiData
{
    private static readonly Faker<LibraryMediaItemCountryDTO> _libraryMediaItemCountryDTO =
        new Faker<LibraryMediaItemCountryDTO>()
            .StrictMode(true)
            .RuleFor(x => x.PlexId, _ => GetUniqueNumber())
            .RuleFor(x => x.Name, f => f.Address.Country())
            .RuleFor(x => x.Key, (_, x) => x.Name.ToMd5Hash())
            .RuleFor(x => x.Filter, (_, x) => $"country={x.PlexId}");

    public static Faker<LibraryMediaItemCountryDTO> GetLibraryMediaItemCountryDTO(Seed seed) =>
        _libraryMediaItemCountryDTO.UseSeed(seed.Next());
}
