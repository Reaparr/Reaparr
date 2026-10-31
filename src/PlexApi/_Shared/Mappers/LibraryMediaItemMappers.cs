namespace Reaparr.PlexApi;

public static class LibraryMediaItemMappers
{
    public static PlexActor ToPlexActor(this LibraryMediaItemRoleDTO role) =>
        new() { Name = role.Name, Key = role.Key };

    public static List<PlexActor> ToPlexActor(this IEnumerable<LibraryMediaItemRoleDTO>? source) =>
        source?.Select(x => x.ToPlexActor()).ToList() ?? [];

    public static PlexCountry ToPlexCountry(this LibraryMediaItemCountryDTO source) =>
        new() { Name = source.Name, Key = source.Key };

    public static List<PlexCountry> ToPlexCountry(this IEnumerable<LibraryMediaItemCountryDTO>? source) =>
        source?.Select(x => x.ToPlexCountry()).ToList() ?? [];

    public static PlexGenreVideo ToPlexGenreVideo(this LibraryMediaItemGenreDTO source) =>
        new()
        {
            Name = source.Name,
            Key = source.Key,
            Type = source.Name.ToPlexGenreType(),
        };

    public static List<PlexGenreVideo> ToPlexGenreVideo(this IEnumerable<LibraryMediaItemGenreDTO>? source) =>
        source?.Select(x => x.ToPlexGenreVideo()).ToList() ?? [];

    public static PlexGenreAudio ToPlexGenreAudio(this LibraryMediaItemGenreDTO source) =>
        new() { Name = source.Name, Key = source.Key };

    public static List<PlexGenreAudio> ToPlexGenreAudio(this IEnumerable<LibraryMediaItemGenreDTO>? source) =>
        source?.Select(x => x.ToPlexGenreAudio()).ToList() ?? [];
}
