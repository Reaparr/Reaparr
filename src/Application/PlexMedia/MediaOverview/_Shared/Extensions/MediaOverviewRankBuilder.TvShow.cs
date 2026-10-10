namespace Reaparr.Application;

public static partial class MediaOverviewRankBuilder
{
    public static List<MediaOverviewTvShowSnapshot> AssignRanks(this List<MediaOverviewTvShowSnapshot> snapshots)
    {
        var permutation = Enumerable.Range(0, snapshots.Count).ToArray();
        AssignRank(snapshots, permutation, CompareTvShowTitle, static (x, rank) => x.TitleRank = rank);
        AssignRank(
            snapshots,
            permutation,
            static (left, right) => Compare(left.Year, right.Year, left.PlexTvShowId, right.PlexTvShowId),
            static (x, rank) => x.YearRank = rank
        );
        AssignRank(
            snapshots,
            permutation,
            static (left, right) => Compare(left.AddedAt, right.AddedAt, left.PlexTvShowId, right.PlexTvShowId),
            static (x, rank) => x.AddedAtRank = rank
        );
        AssignRank(
            snapshots,
            permutation,
            static (left, right) => Compare(left.UpdatedAt, right.UpdatedAt, left.PlexTvShowId, right.PlexTvShowId),
            static (x, rank) => x.UpdatedAtRank = rank
        );
        AssignRank(
            snapshots,
            permutation,
            static (left, right) => Compare(left.Duration, right.Duration, left.PlexTvShowId, right.PlexTvShowId),
            static (x, rank) => x.DurationRank = rank
        );
        AssignRank(
            snapshots,
            permutation,
            static (left, right) => Compare(left.MediaSize, right.MediaSize, left.PlexTvShowId, right.PlexTvShowId),
            static (x, rank) => x.MediaSizeRank = rank
        );
        AssignRank(
            snapshots,
            permutation,
            static (left, right) => Compare(left.Quality, right.Quality, left.PlexTvShowId, right.PlexTvShowId),
            static (x, rank) => x.QualityRank = rank
        );
        return snapshots;
    }

    private static int CompareTvShowTitle(MediaOverviewTvShowSnapshot left, MediaOverviewTvShowSnapshot right) =>
        Compare(left.SearchTitle, right.SearchTitle, left.PlexTvShowId, right.PlexTvShowId, _titleComparer);
}
