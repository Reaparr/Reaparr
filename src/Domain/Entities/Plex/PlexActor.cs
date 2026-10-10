namespace Reaparr.Domain;

public class PlexActor : BaseEntity
{
    public required string Name { get; init; }

    /// <summary>
    /// A MD5 hash of the name.
    /// </summary>
    public required string Key { get; init; }

    public ICollection<PlexLibrary> PlexLibraries { get; set; } = [];

    public ICollection<PlexMovie> PlexMovieActors { get; set; } = [];

    public ICollection<PlexTvShow> PlexTvShowActors { get; set; } = [];

    public ICollection<PlexMusicArtist> PlexMusicArtistActors { get; set; } = [];

    public ICollection<PlexOtherVideo> PlexOtherVideoActors { get; set; } = [];
}
