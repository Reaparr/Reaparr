using NaturalSort.Extension;

namespace Reaparr.Application;

public static class MediaOverviewRankBuilder
{
    private static readonly IComparer<string> _titleComparer = StringComparison.OrdinalIgnoreCase.WithNaturalSort();

    public static List<MediaOverviewMovieSnapshot> AssignRanks(this List<MediaOverviewMovieSnapshot> snapshots)
    {
        var permutation = Enumerable.Range(0, snapshots.Count).ToArray();
        AssignRank(snapshots, permutation, CompareMovieTitle, static (x, rank) => x.TitleRank = rank);
        AssignRank(snapshots, permutation, static (left, right) => Compare(left.Year, right.Year, left.PlexMovieId, right.PlexMovieId), static (x, rank) => x.YearRank = rank);
        AssignRank(snapshots, permutation, static (left, right) => Compare(left.AddedAt, right.AddedAt, left.PlexMovieId, right.PlexMovieId), static (x, rank) => x.AddedAtRank = rank);
        AssignRank(snapshots, permutation, static (left, right) => Compare(left.UpdatedAt, right.UpdatedAt, left.PlexMovieId, right.PlexMovieId), static (x, rank) => x.UpdatedAtRank = rank);
        AssignRank(snapshots, permutation, static (left, right) => Compare(left.Duration, right.Duration, left.PlexMovieId, right.PlexMovieId), static (x, rank) => x.DurationRank = rank);
        AssignRank(snapshots, permutation, static (left, right) => Compare(left.MediaSize, right.MediaSize, left.PlexMovieId, right.PlexMovieId), static (x, rank) => x.MediaSizeRank = rank);
        AssignRank(snapshots, permutation, static (left, right) => Compare(left.Quality, right.Quality, left.PlexMovieId, right.PlexMovieId), static (x, rank) => x.QualityRank = rank);
        return snapshots;
    }

    public static List<MediaOverviewTvShowSnapshot> AssignRanks(this List<MediaOverviewTvShowSnapshot> snapshots)
    {
        var permutation = Enumerable.Range(0, snapshots.Count).ToArray();
        AssignRank(snapshots, permutation, CompareTvShowTitle, static (x, rank) => x.TitleRank = rank);
        AssignRank(snapshots, permutation, static (left, right) => Compare(left.Year, right.Year, left.PlexTvShowId, right.PlexTvShowId), static (x, rank) => x.YearRank = rank);
        AssignRank(snapshots, permutation, static (left, right) => Compare(left.AddedAt, right.AddedAt, left.PlexTvShowId, right.PlexTvShowId), static (x, rank) => x.AddedAtRank = rank);
        AssignRank(snapshots, permutation, static (left, right) => Compare(left.UpdatedAt, right.UpdatedAt, left.PlexTvShowId, right.PlexTvShowId), static (x, rank) => x.UpdatedAtRank = rank);
        AssignRank(snapshots, permutation, static (left, right) => Compare(left.Duration, right.Duration, left.PlexTvShowId, right.PlexTvShowId), static (x, rank) => x.DurationRank = rank);
        AssignRank(snapshots, permutation, static (left, right) => Compare(left.MediaSize, right.MediaSize, left.PlexTvShowId, right.PlexTvShowId), static (x, rank) => x.MediaSizeRank = rank);
        AssignRank(snapshots, permutation, static (left, right) => Compare(left.Quality, right.Quality, left.PlexTvShowId, right.PlexTvShowId), static (x, rank) => x.QualityRank = rank);
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

    private static int CompareMovieTitle(MediaOverviewMovieSnapshot left, MediaOverviewMovieSnapshot right) =>
        Compare(left.SearchTitle, right.SearchTitle, left.PlexMovieId, right.PlexMovieId, _titleComparer);

    private static int CompareTvShowTitle(MediaOverviewTvShowSnapshot left, MediaOverviewTvShowSnapshot right) =>
        Compare(left.SearchTitle, right.SearchTitle, left.PlexTvShowId, right.PlexTvShowId, _titleComparer);

    private static int Compare<T>(T left, T right, int leftId, int rightId, IComparer<T>? comparer = null)
    {
        var result = (comparer ?? Comparer<T>.Default).Compare(left, right);
        return result != 0 ? result : leftId.CompareTo(rightId);
    }
}
