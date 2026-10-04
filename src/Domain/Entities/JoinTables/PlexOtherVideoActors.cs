namespace Reaparr.Domain;

public class PlexOtherVideoActors
{
    public PlexOtherVideoActors() { }

    [SetsRequiredMembers]
    public PlexOtherVideoActors(int plexActorId, int plexLibraryId, int plexOtherVideoId)
    {
        PlexActorId = plexActorId;
        PlexOtherVideoId = plexOtherVideoId;
        PlexLibraryId = plexLibraryId;
    }

    [Column(Order = 1)]
    public required int PlexActorId { get; set; }

    [Column(Order = 2)]
    public required int PlexOtherVideoId { get; set; }

    [Column(Order = 3)]
    public required int PlexLibraryId { get; set; }
}
