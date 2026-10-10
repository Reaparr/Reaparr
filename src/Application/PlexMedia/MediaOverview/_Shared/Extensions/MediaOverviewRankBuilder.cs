using NaturalSort.Extension;

namespace Reaparr.Application;

public static partial class MediaOverviewRankBuilder
{
    private static readonly IComparer<string> _titleComparer = StringComparison.OrdinalIgnoreCase.WithNaturalSort();

    private static List<TSnapshot> AssignRanks<TSnapshot>(
        List<TSnapshot> snapshots,
        IReadOnlyDictionary<int, MediaOverviewRankValue> values,
        Func<TSnapshot, int> getId
    )
        where TSnapshot : BaseMediaOverviewSnapshot
    {
        var permutation = Enumerable.Range(0, snapshots.Count).ToArray();
        AssignRank(
            snapshots,
            permutation,
            (left, right) =>
                Compare(
                    values[getId(left)].SearchTitle,
                    values[getId(right)].SearchTitle,
                    getId(left),
                    getId(right),
                    _titleComparer
                ),
            static (x, rank) => x.TitleRank = rank
        );
        AssignRank(
            snapshots,
            permutation,
            (left, right) => Compare(values[getId(left)].Year, values[getId(right)].Year, getId(left), getId(right)),
            static (x, rank) => x.YearRank = rank
        );
        AssignRank(
            snapshots,
            permutation,
            (left, right) =>
                Compare(values[getId(left)].AddedAt, values[getId(right)].AddedAt, getId(left), getId(right)),
            static (x, rank) => x.AddedAtRank = rank
        );
        AssignRank(
            snapshots,
            permutation,
            (left, right) =>
                Compare(values[getId(left)].UpdatedAt, values[getId(right)].UpdatedAt, getId(left), getId(right)),
            static (x, rank) => x.UpdatedAtRank = rank
        );
        AssignRank(
            snapshots,
            permutation,
            (left, right) =>
                Compare(values[getId(left)].Duration, values[getId(right)].Duration, getId(left), getId(right)),
            static (x, rank) => x.DurationRank = rank
        );
        AssignRank(
            snapshots,
            permutation,
            (left, right) =>
                Compare(values[getId(left)].MediaSize, values[getId(right)].MediaSize, getId(left), getId(right)),
            static (x, rank) => x.MediaSizeRank = rank
        );
        return snapshots;
    }

    private static void AssignRank<TSnapshot>(
        IReadOnlyList<TSnapshot> snapshots,
        int[] permutation,
        Comparison<TSnapshot> comparison,
        Action<TSnapshot, int> setRank
    )
    {
        Array.Sort(permutation, (left, right) => comparison(snapshots[left], snapshots[right]));
        for (var rank = 0; rank < permutation.Length; rank++)
            setRank(snapshots[permutation[rank]], rank);
    }

    private static int Compare<T>(T left, T right, int leftId, int rightId, IComparer<T>? comparer = null)
    {
        var result = (comparer ?? Comparer<T>.Default).Compare(left, right);
        return result != 0 ? result : leftId.CompareTo(rightId);
    }
}

public sealed record MediaOverviewRankValue(
    string SearchTitle,
    int? Year,
    DateTime AddedAt,
    DateTime? UpdatedAt,
    int? Duration,
    long MediaSize
);
