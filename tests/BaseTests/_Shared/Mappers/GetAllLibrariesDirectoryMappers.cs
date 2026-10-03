using LukeHagar.PlexAPI.SDK.Models.Components;

namespace Reaparr.BaseTests;

public static class LibrarySectionMappers
{
    public static LibrarySection ToPlexApiDTO(this PlexLibrary source) =>
        new()
        {
            AllowSync = LibrarySectionAllowSync.CreateBoolean(false),
            Art = string.Empty,
            Composite = string.Empty,
            Filters = false,
            Refreshing = false,
            Thumb = string.Empty,
            Key = source.Key,
            Type = source.Type.ToMediaTypeString(),
            Title = source.Title,
            Agent = source.Type switch
            {
                PlexMediaType.Music => "tv.plex.agents.music",
                PlexMediaType.Photos or PlexMediaType.OtherVideos => "com.plexapp.agents.none",
                PlexMediaType.TvShow => "tv.plex.agents.series",
                _ => "tv.plex.agents.movie",
            },
            Scanner = source.Type switch
            {
                PlexMediaType.Music => "Plex Music",
                PlexMediaType.Photos => "Plex Photo Scanner",
                PlexMediaType.OtherVideos => "Plex Video Files Scanner",
                PlexMediaType.TvShow => "Plex TV Series",
                _ => "Plex Movie",
            },
            Language = source.Language,
            Uuid = source.Uuid,
            UpdatedAt = source.UpdatedAt.ToUnixLong(),
            CreatedAt = source.CreatedAt.ToUnixLong(),
            ScannedAt = source.ScannedAt.ToUnixLong(),
            Content = false,
            Directory = false,
            ContentChangedAt = source.ContentChangedAt,
            Hidden = null,
            Location = [],
        };

    public static List<LibrarySection> ToPlexApiDTO(this List<PlexLibrary> plexLibraries) =>
        plexLibraries.Select(x => x.ToPlexApiDTO()).ToList();
}
