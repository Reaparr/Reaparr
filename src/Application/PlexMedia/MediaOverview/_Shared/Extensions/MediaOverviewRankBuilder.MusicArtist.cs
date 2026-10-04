namespace Reaparr.Application;

public static partial class MediaOverviewRankBuilder
{
    public static List<MediaOverviewMusicArtistSnapshot> AssignRanks(
        this List<MediaOverviewMusicArtistSnapshot> snapshots,
        IReadOnlyDictionary<int, MediaOverviewRankValue> values
    ) => AssignRanks(snapshots, values, static x => x.PlexArtistId);
}
