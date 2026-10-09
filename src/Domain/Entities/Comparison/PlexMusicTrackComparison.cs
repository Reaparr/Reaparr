namespace Reaparr.Domain;

public class PlexMusicTrackComparison : BaseEntity
{
    public required int RemotePlexLibraryId { get; init; }
    public required int OwnedPlexLibraryId { get; init; }
    public required int RemotePlexMediaId { get; init; }
    public required int OwnedPlexMediaId { get; init; }
    public required PlexMediaComparisonMatchType MatchType { get; init; }
    public required DateTime ComparedAt { get; init; }
}
