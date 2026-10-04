using LukeHagar.PlexAPI.SDK.Models.Components;

namespace Reaparr.BaseTests;

public partial class FakePlexApiData
{
    public static Faker<LibrarySection> GetLibrariesResponseDirectory(Seed seed, PlexMediaType type)
    {
        return new Faker<LibrarySection>()
            .StrictMode(true)
            .UseSeed(seed.Next())
            .RuleFor(x => x.AllowSync, f => LibrarySectionAllowSync.CreateBoolean(f.Random.Bool()))
            .RuleFor(x => x.Art, _ => $"/:/resources/{type.ToString().ToLowerInvariant()}-fanart.jpg")
            .RuleFor(x => x.Key, f => f.Random.Number(int.MaxValue).ToString())
            .RuleFor(x => x.Composite, (f, x) => $"/library/sections/{x.Key}/composite/{f.Random.Number(100000)}")
            .RuleFor(x => x.Filters, f => f.Random.Bool())
            .RuleFor(x => x.Refreshing, f => f.Random.Bool())
            .RuleFor(x => x.Thumb, _ => $"/:/resources/{type.ToString().ToLowerInvariant()}.png")
            .RuleFor(x => x.Type, _ => type.ToMediaTypeString())
            .RuleFor(x => x.Title, f => f.Company.CompanyName())
            .RuleFor(x => x.Agent, _ => type switch
            {
                PlexMediaType.Music => "tv.plex.agents.music",
                PlexMediaType.PhotoAlbum => "com.plexapp.agents.none",
                PlexMediaType.OtherVideos => "com.plexapp.agents.none",
                PlexMediaType.TvShow => "tv.plex.agents.series",
                _ => "tv.plex.agents.movie",
            })
            .RuleFor(x => x.Scanner, _ => type switch
            {
                PlexMediaType.Music => "Plex Music",
                PlexMediaType.PhotoAlbum => "Plex Photo Scanner",
                PlexMediaType.OtherVideos => "Plex Video Files Scanner",
                PlexMediaType.TvShow => "Plex TV Series",
                _ => "Plex Movie",
            })
            .RuleFor(x => x.Language, _ => "en-US")
            .RuleFor(x => x.Uuid, f => f.PlexApi().ClientId)
            .RuleFor(x => x.UpdatedAt, f => f.Date.Recent().ToUnixLong())
            .RuleFor(x => x.CreatedAt, f => f.Date.Past(4).ToUnixLong())
            .RuleFor(x => x.ScannedAt, f => f.Date.Recent().ToUnixLong())
            .RuleFor(x => x.Content, f => f.Random.Bool())
            .RuleFor(x => x.Directory, f => f.Random.Bool())
            .RuleFor(x => x.ContentChangedAt, f => f.Random.Long(1, 10_000_000))
            .RuleFor(x => x.Hidden, f => f.Random.Bool())
            .RuleFor(
                x => x.Location,
                f => [new LibrarySectionLocation { Id = GetUniqueNumber(), Path = f.System.DirectoryPath() }]
            );
    }
}
