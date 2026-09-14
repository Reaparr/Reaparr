namespace Reaparr.Domain;

public class PlexGenre : BaseEntity
{
    public required string Name { get; init; }

    /// <summary>
    /// A MD5 hash of the name.
    /// </summary>
    public required string Key { get; init; }

    public ICollection<PlexLibrary> PlexLibraries { get; set; } = [];

    public ICollection<PlexMovie> PlexMovieGenres { get; set; } = [];

    public ICollection<PlexTvShow> PlexTvShowGenres { get; set; } = [];

    public PlexGenreType Type { get; set; }
}
