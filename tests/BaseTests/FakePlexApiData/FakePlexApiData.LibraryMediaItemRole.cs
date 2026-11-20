using Bogus.Hollywood;

namespace Reaparr.BaseTests;

public partial class FakePlexApiData
{
    private static readonly Faker<LibraryMediaItemRoleDTO> _libraryMediaItemActorDTO =
        new Faker<LibraryMediaItemRoleDTO>()
            .StrictMode(true)
            .RuleFor(x => x.PlexId, _ => GetUniqueNumber())
            .RuleFor(x => x.Name, f => f.Movies().ActorName())
            .RuleFor(x => x.Role, f => f.Movies().MovieTagline())
            .RuleFor(x => x.Filter, (_, x) => $"actor={x.PlexId}")
            .RuleFor(x => x.Thumb, f => f.Image.PicsumUrl())
            .RuleFor(x => x.Key, f => f.Random.Hash(24));

    public static Faker<LibraryMediaItemRoleDTO> GetLibraryMediaItemActorDTO(Seed seed) =>
        _libraryMediaItemActorDTO.UseSeed(seed.Next());
}
