namespace Reaparr.Application;

public static partial class MediaOverviewRankBuilder
{
    public static List<MediaOverviewOtherVideoSnapshot> AssignRanks(
        this List<MediaOverviewOtherVideoSnapshot> snapshots,
        IReadOnlyDictionary<int, MediaOverviewRankValue> values
    )
    {
        AssignRanks(snapshots, values, static x => x.PlexOtherVideoId);
        var permutation = Enumerable.Range(0, snapshots.Count).ToArray();
        AssignRank(
            snapshots,
            permutation,
            static (left, right) => Compare(left.Quality, right.Quality, left.PlexOtherVideoId, right.PlexOtherVideoId),
            static (x, rank) => x.QualityRank = rank
        );
        return snapshots;
    }
}
