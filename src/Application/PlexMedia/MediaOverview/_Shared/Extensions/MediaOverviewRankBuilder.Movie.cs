namespace Reaparr.Application;

public static partial class MediaOverviewRankBuilder
{
    public static List<MediaOverviewMovieSnapshot> AssignRanks(this List<MediaOverviewMovieSnapshot> snapshots)
    {
        var permutation = Enumerable.Range(0, snapshots.Count).ToArray();
        AssignRank(snapshots, permutation, CompareMovieTitle, static (x, rank) => x.TitleRank = rank);
        AssignRank(
            snapshots,
            permutation,
            static (left, right) => Compare(left.Year, right.Year, left.PlexMovieId, right.PlexMovieId),
            static (x, rank) => x.YearRank = rank
        );
        AssignRank(
            snapshots,
            permutation,
            static (left, right) => Compare(left.AddedAt, right.AddedAt, left.PlexMovieId, right.PlexMovieId),
            static (x, rank) => x.AddedAtRank = rank
        );
        AssignRank(
            snapshots,
            permutation,
            static (left, right) => Compare(left.UpdatedAt, right.UpdatedAt, left.PlexMovieId, right.PlexMovieId),
            static (x, rank) => x.UpdatedAtRank = rank
        );
        AssignRank(
            snapshots,
            permutation,
            static (left, right) => Compare(left.Duration, right.Duration, left.PlexMovieId, right.PlexMovieId),
            static (x, rank) => x.DurationRank = rank
        );
        AssignRank(
            snapshots,
            permutation,
            static (left, right) => Compare(left.MediaSize, right.MediaSize, left.PlexMovieId, right.PlexMovieId),
            static (x, rank) => x.MediaSizeRank = rank
        );
        AssignRank(
            snapshots,
            permutation,
            static (left, right) => Compare(left.Quality, right.Quality, left.PlexMovieId, right.PlexMovieId),
            static (x, rank) => x.QualityRank = rank
        );
        return snapshots;
    }

    private static int CompareMovieTitle(MediaOverviewMovieSnapshot left, MediaOverviewMovieSnapshot right) =>
        Compare(left.SearchTitle, right.SearchTitle, left.PlexMovieId, right.PlexMovieId, _titleComparer);
}
