namespace Reaparr.Domain;

public class PlexMusicArtistActors
{
    public PlexMusicArtistActors() { }

    [SetsRequiredMembers]
    public PlexMusicArtistActors(int plexActorId, int plexLibraryId, int plexMusicArtistId)
    {
        PlexActorId = plexActorId;
        PlexMusicArtistId = plexMusicArtistId;
        PlexLibraryId = plexLibraryId;
    }

    [Column(Order = 1)]
    public required int PlexActorId { get; init; }

    [Column(Order = 2)]
    public required int PlexMusicArtistId { get; init; }

    [Column(Order = 3)]
    public required int PlexLibraryId { get; init; }
}
