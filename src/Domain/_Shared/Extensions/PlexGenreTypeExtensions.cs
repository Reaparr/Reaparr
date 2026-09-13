namespace Reaparr.Domain;

public static class PlexGenreTypeExtensions
{
    private static readonly IReadOnlyDictionary<string, PlexGenreType> _genreTypes = new Dictionary<
        string,
        PlexGenreType
    >(StringComparer.OrdinalIgnoreCase)
    {
        // Action
        ["Action"] = PlexGenreType.Action,
        ["Martial Arts"] = PlexGenreType.Action,
        ["Superhero"] = PlexGenreType.Action,

        // Adventure
        ["Adventure"] = PlexGenreType.Adventure,

        // Anime
        ["Anime"] = PlexGenreType.Anime,

        // Animation
        ["Animated"] = PlexGenreType.Animation,
        ["Animatie"] = PlexGenreType.Animation,
        ["Animation"] = PlexGenreType.Animation,

        // Manga
        ["Manga"] = PlexGenreType.Manga,
        ["Bishounen"] = PlexGenreType.Manga,

        // Comedy
        ["Comedy"] = PlexGenreType.Comedy,
        ["Komedi"] = PlexGenreType.Comedy,
        ["Komedie"] = PlexGenreType.Comedy,

        // Crime
        ["Crime"] = PlexGenreType.Crime,
        ["Misdaad"] = PlexGenreType.Crime,
        ["Heist"] = PlexGenreType.Crime,

        // Documentary
        ["Documentary"] = PlexGenreType.Documentary,
        ["Documentaire"] = PlexGenreType.Documentary,

        // Biography
        ["Biography"] = PlexGenreType.Biography,
        ["Biographical"] = PlexGenreType.Biography,

        // Drama
        ["Drama"] = PlexGenreType.Drama,
        ["Angst"] = PlexGenreType.Drama,

        // Soap
        ["Soap"] = PlexGenreType.Soap,

        // Family
        ["Family"] = PlexGenreType.Family,
        ["Familie"] = PlexGenreType.Family,
        ["Kids & Family"] = PlexGenreType.Family,
        ["Kinderen en familie"] = PlexGenreType.Family,
        ["Disney"] = PlexGenreType.Family,

        // Children
        ["Children"] = PlexGenreType.Children,
        ["Children's"] = PlexGenreType.Children,
        ["Childrens"] = PlexGenreType.Children,

        // Fantasy
        ["Fantasy"] = PlexGenreType.Fantasy,
        ["Fantasie"] = PlexGenreType.Fantasy,
        ["Contemporary fantasy"] = PlexGenreType.Fantasy,
        ["Dark fantasy"] = PlexGenreType.Fantasy,
        ["Demon"] = PlexGenreType.Fantasy,
        ["Demon world"] = PlexGenreType.Fantasy,
        ["Elf"] = PlexGenreType.Fantasy,
        ["Alternative past"] = PlexGenreType.Fantasy,

        // History
        ["History"] = PlexGenreType.History,
        ["Historisch"] = PlexGenreType.History,

        // Horror
        ["Horror"] = PlexGenreType.Horror,

        // Suspense
        ["Suspense"] = PlexGenreType.Suspense,

        // Music
        ["Music"] = PlexGenreType.Music,
        ["Muziek"] = PlexGenreType.Music,
        ["Performances & Events"] = PlexGenreType.Music,
        ["Books & Spoken"] = PlexGenreType.Music,

        // Musical
        ["Musical"] = PlexGenreType.Musical,

        // Mystery
        ["Mystery"] = PlexGenreType.Mystery,
        ["Mysterie"] = PlexGenreType.Mystery,

        // News
        ["News"] = PlexGenreType.News,

        // Reality
        ["Reality"] = PlexGenreType.Reality,
        ["Competition reality"] = PlexGenreType.Reality,
        ["Housewives"] = PlexGenreType.Reality,

        // Romance
        ["Romance"] = PlexGenreType.Romance,
        ["Romantic"] = PlexGenreType.Romance,
        ["Romantiek"] = PlexGenreType.Romance,

        // Science fiction
        ["Sci-Fi"] = PlexGenreType.ScienceFiction,
        ["Science Fiction"] = PlexGenreType.ScienceFiction,
        ["Sciencefiction"] = PlexGenreType.ScienceFiction,
        ["Sci-Fi & Fantasy"] = PlexGenreType.ScienceFiction,
        ["Science Fiction & Fantasy"] = PlexGenreType.ScienceFiction,

        // Sport
        ["Sport"] = PlexGenreType.Sport,
        ["Sports"] = PlexGenreType.Sport,
        ["Sportcommentaar"] = PlexGenreType.Sport,
        ["Voetbal"] = PlexGenreType.Sport,

        // Thriller
        ["Thriller"] = PlexGenreType.Thriller,
        ["Spy"] = PlexGenreType.Thriller,
        ["Blackmail"] = PlexGenreType.Thriller,

        // War
        ["War"] = PlexGenreType.War,
        ["Oorlog"] = PlexGenreType.War,
        ["Military"] = PlexGenreType.War,
        ["Military & War"] = PlexGenreType.War,
        ["War & Politics"] = PlexGenreType.War,

        // Western
        ["Western"] = PlexGenreType.Western,

        // Adult
        ["18 restricted"] = PlexGenreType.Adult,
        ["Adult"] = PlexGenreType.Adult,
        ["Ahegao"] = PlexGenreType.Adult,
        ["Anal"] = PlexGenreType.Adult,
        ["Bdsm"] = PlexGenreType.Adult,
        ["Cg collection"] = PlexGenreType.Adult,
        ["Chikan"] = PlexGenreType.Adult,
        ["Creampie"] = PlexGenreType.Adult,
        ["Deflowering"] = PlexGenreType.Adult,
        ["Erotic game"] = PlexGenreType.Adult,
        ["Female student"] = PlexGenreType.Adult,
        ["Female teacher"] = PlexGenreType.Adult,
        ["Harem"] = PlexGenreType.Adult,
        ["High school"] = PlexGenreType.Adult,
        ["Large breasts"] = PlexGenreType.Adult,
        ["Maid"] = PlexGenreType.Adult,
        ["Nudity"] = PlexGenreType.Adult,
        ["Sex"] = PlexGenreType.Adult,
        ["Teen"] = PlexGenreType.Adult,

        // Educational
        ["Courses"] = PlexGenreType.Educational,
        ["Earth"] = PlexGenreType.Educational,
        ["Understanding Gravity"] = PlexGenreType.Educational,
        ["Black Holes, Tides, and Curved Spacetime"] = PlexGenreType.Educational,
        ["Scripture and Lesson Support"] = PlexGenreType.Educational,

        // Entertainment
        ["Entertainment"] = PlexGenreType.Entertainment,
        ["Awards Show"] = PlexGenreType.Entertainment,

        // Game shows
        ["Game Show"] = PlexGenreType.GameShow,

        // Talk shows
        ["Talk"] = PlexGenreType.TalkShow,
        ["Talk Show"] = PlexGenreType.TalkShow,

        // Independent / Indie
        ["Indie"] = PlexGenreType.Independent,

        // Religion
        ["Religion"] = PlexGenreType.Religion,
        ["Mormon channel"] = PlexGenreType.Religion,

        // Podcasts
        ["Podcast"] = PlexGenreType.Podcast,

        // Foreign
        ["Foreign"] = PlexGenreType.Foreign,
        ["Foreign Language"] = PlexGenreType.Foreign,
        ["Foreign Film"] = PlexGenreType.Foreign,
        ["Foreign Language Film"] = PlexGenreType.Foreign,
        ["asia"] = PlexGenreType.Foreign,

        // Special interest / formats that are not really genres.
        ["Food"] = PlexGenreType.Other,
        ["Holiday"] = PlexGenreType.Other,
        ["Home and Garden"] = PlexGenreType.Other,
        ["Mini-Series"] = PlexGenreType.Other,
        ["MyDVD"] = PlexGenreType.Other,
        ["Short"] = PlexGenreType.Other,
        ["Special Interest"] = PlexGenreType.Other,
        ["Travel"] = PlexGenreType.Other,
        ["Tv"] = PlexGenreType.Other,
        ["Video"] = PlexGenreType.Other,
        ["Half-Length episodes"] = PlexGenreType.Other,
        ["TV Film"] = PlexGenreType.Other,
        ["TV Movie"] = PlexGenreType.Other,
    };

    public static PlexGenreType ToPlexGenreType(this string genre)
    {
        var normalizedGenre = genre.Trim();
        if (_genreTypes.TryGetValue(normalizedGenre, out var type))
            return type;

        var types = normalizedGenre
            .Split([',', '/', ';', '&'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Map)
            .Where(x => x != PlexGenreType.Unknown)
            .Distinct()
            .Take(2)
            .ToList();

        return types.Count switch
        {
            0 => PlexGenreType.Unknown,
            1 => types[0],
            _ => PlexGenreType.Group,
        };
    }

    private static PlexGenreType Map(string genre) => _genreTypes.GetValueOrDefault(genre, PlexGenreType.Unknown);
}
