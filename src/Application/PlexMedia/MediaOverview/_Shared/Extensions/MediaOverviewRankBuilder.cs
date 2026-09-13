using NaturalSort.Extension;

namespace Reaparr.Application;

public static class MediaOverviewRankBuilder
{
    private static readonly IComparer<string> _titleComparer = StringComparison.OrdinalIgnoreCase.WithNaturalSort();

    public static List<MediaOverviewMovieSnapshot> AssignRanks(this List<MediaOverviewMovieSnapshot> snapshots)
    {
        AssignRanks(
            snapshots,
            static x => x.PlexMovieId,
            static x => x.SearchTitle,
            static x => x.Year,
            static x => x.AddedAt,
            static x => x.UpdatedAt,
            static x => x.Duration,
            static x => x.MediaSize,
            static x => x.Quality,
            static (x, rank) => x.TitleRank = rank,
            static (x, rank) => x.YearRank = rank,
            static (x, rank) => x.AddedAtRank = rank,
            static (x, rank) => x.UpdatedAtRank = rank,
            static (x, rank) => x.DurationRank = rank,
            static (x, rank) => x.MediaSizeRank = rank,
            static (x, rank) => x.QualityRank = rank
        );
        return snapshots;
    }

    public static List<MediaOverviewTvShowSnapshot> AssignRanks(this List<MediaOverviewTvShowSnapshot> snapshots)
    {
        AssignRanks(
            snapshots,
            static x => x.PlexTvShowId,
            static x => x.SearchTitle,
            static x => x.Year,
            static x => x.AddedAt,
            static x => x.UpdatedAt,
            static x => x.Duration,
            static x => x.MediaSize,
            static x => x.Quality,
            static (x, rank) => x.TitleRank = rank,
            static (x, rank) => x.YearRank = rank,
            static (x, rank) => x.AddedAtRank = rank,
            static (x, rank) => x.UpdatedAtRank = rank,
            static (x, rank) => x.DurationRank = rank,
            static (x, rank) => x.MediaSizeRank = rank,
            static (x, rank) => x.QualityRank = rank
        );
        return snapshots;
    }

    private static void AssignRanks<TSnapshot>(
        List<TSnapshot> snapshots,
        Func<TSnapshot, int> id,
        Func<TSnapshot, string> title,
        Func<TSnapshot, int> year,
        Func<TSnapshot, DateTime> addedAt,
        Func<TSnapshot, DateTime?> updatedAt,
        Func<TSnapshot, int> duration,
        Func<TSnapshot, long> mediaSize,
        Func<TSnapshot, VideoQuality> quality,
        Action<TSnapshot, int> setTitleRank,
        Action<TSnapshot, int> setYearRank,
        Action<TSnapshot, int> setAddedAtRank,
        Action<TSnapshot, int> setUpdatedAtRank,
        Action<TSnapshot, int> setDurationRank,
        Action<TSnapshot, int> setMediaSizeRank,
        Action<TSnapshot, int> setQualityRank
    )
    {
        var permutation = Enumerable.Range(0, snapshots.Count).ToArray();
        AssignRank(
            snapshots,
            permutation,
            (left, right) => Compare(title(left), title(right), id(left), id(right), _titleComparer),
            setTitleRank
        );
        AssignRank(snapshots, permutation, (left, right) => Compare(year(left), year(right), id(left), id(right)), setYearRank);
        AssignRank(snapshots, permutation, (left, right) => Compare(addedAt(left), addedAt(right), id(left), id(right)), setAddedAtRank);
        AssignRank(snapshots, permutation, (left, right) => Compare(updatedAt(left), updatedAt(right), id(left), id(right)), setUpdatedAtRank);
        AssignRank(snapshots, permutation, (left, right) => Compare(duration(left), duration(right), id(left), id(right)), setDurationRank);
        AssignRank(snapshots, permutation, (left, right) => Compare(mediaSize(left), mediaSize(right), id(left), id(right)), setMediaSizeRank);
        AssignRank(snapshots, permutation, (left, right) => Compare(quality(left), quality(right), id(left), id(right)), setQualityRank);
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
