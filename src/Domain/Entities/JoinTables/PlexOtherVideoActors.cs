namespace Reaparr.Domain;

public class PlexOtherVideoActors
{
    public PlexOtherVideoActors() { }

    [SetsRequiredMembers]
    public PlexOtherVideoActors(int plexActorId, int plexOtherVideoId)
    {
        PlexActorId = plexActorId;
        PlexOtherVideoId = plexOtherVideoId;
    }

    [Column(Order = 1)]
    public required int PlexActorId { get; set; }

    [Column(Order = 2)]
    public required int PlexOtherVideoId { get; set; }
}
