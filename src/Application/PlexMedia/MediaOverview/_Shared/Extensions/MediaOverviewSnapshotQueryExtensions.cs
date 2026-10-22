namespace Reaparr.Application;

internal static class MediaOverviewSnapshotQueryExtensions
{
    public static IQueryable<TSnapshot> ApplyRankOrder<TSnapshot>(
        this IQueryable<TSnapshot> snapshots,
        (SortType Field, bool Descending) sort
    )
        where TSnapshot : BaseMediaOverviewSnapshot
    {
        var rankPropertyName = ResolveRankProperty(sort.Field);
        return sort.Descending
            ? snapshots.OrderByDescending(snapshot => EF.Property<int>(snapshot, rankPropertyName))
            : snapshots.OrderBy(snapshot => EF.Property<int>(snapshot, rankPropertyName));
    }

    public static IQueryable<MediaNavigationIndexRow> SelectNavigationRows(
        this IQueryable<MediaOverviewOtherVideoSnapshot> snapshots,
        (SortType Field, bool Descending) sort
    )
    {
        var rankPropertyName = ResolveRankProperty(sort.Field);
        return snapshots.Select(snapshot => new MediaNavigationIndexRow
        {
            SearchTitle = snapshot.NavigationTitleLabel,
            Year = snapshot.Year,
            QualityValue = (int?)snapshot.VideoQuality,
            Duration = snapshot.Duration,
            AddedAt = snapshot.AddedAt,
            UpdatedAt = snapshot.UpdatedAt,
            MediaSize = snapshot.MediaSize,
            SortRank = EF.Property<int>(snapshot, rankPropertyName),
        });
    }

    public static IQueryable<MediaNavigationIndexRow> SelectNavigationRows<TSnapshot>(
        this IQueryable<TSnapshot> snapshots,
        (SortType Field, bool Descending) sort
    )
        where TSnapshot : BaseMediaOverviewSnapshot
    {
        var rankPropertyName = ResolveRankProperty(sort.Field);
        return snapshots.Select(snapshot => new MediaNavigationIndexRow
        {
            SearchTitle = snapshot.NavigationTitleLabel,
            Year = null,
            QualityValue = null,
            Duration = snapshot.Duration,
            AddedAt = snapshot.AddedAt,
            UpdatedAt = snapshot.UpdatedAt,
            MediaSize = snapshot.MediaSize,
            SortRank = EF.Property<int>(snapshot, rankPropertyName),
        });
    }

    private static string ResolveRankProperty(SortType sortType) =>
        sortType switch
        {
            SortType.Year => nameof(MediaOverviewOtherVideoSnapshot.YearRank),
            SortType.AddedAt => nameof(BaseMediaOverviewSnapshot.AddedAtRank),
            SortType.UpdatedAt => nameof(BaseMediaOverviewSnapshot.UpdatedAtRank),
            SortType.Duration => nameof(BaseMediaOverviewSnapshot.DurationRank),
            SortType.MediaSize => nameof(BaseMediaOverviewSnapshot.MediaSizeRank),
            SortType.VideoQuality => nameof(MediaOverviewOtherVideoSnapshot.QualityRank),
            _ => nameof(BaseMediaOverviewSnapshot.TitleRank),
        };
}
