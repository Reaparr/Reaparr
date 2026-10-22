namespace Reaparr.Application;

internal static class MediaOverviewSnapshotQueryExtensions
{
    public static IQueryable<TSnapshot> ApplyRankOrder<TSnapshot>(
        this IQueryable<TSnapshot> snapshots,
        (string Field, bool Descending) sort
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
        (string Field, bool Descending) sort
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

    public static IQueryable<MediaNavigationIndexRow> SelectNavigationRows(
        this IQueryable<MediaOverviewPhotoAlbumSnapshot> snapshots,
        (string Field, bool Descending) sort
    )
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

    private static string ResolveRankProperty(string field) =>
        field switch
        {
            nameof(PlexMediaSlimDTO.Year) => nameof(MediaOverviewOtherVideoSnapshot.YearRank),
            nameof(BasePlexMedia.AddedAt) => nameof(BaseMediaOverviewSnapshot.AddedAtRank),
            nameof(BasePlexMedia.UpdatedAt) => nameof(BaseMediaOverviewSnapshot.UpdatedAtRank),
            nameof(BasePlexMedia.Duration) => nameof(BaseMediaOverviewSnapshot.DurationRank),
            nameof(BasePlexMedia.MediaSize) => nameof(BaseMediaOverviewSnapshot.MediaSizeRank),
            nameof(BasePlexMedia.VideoQuality) => nameof(MediaOverviewOtherVideoSnapshot.QualityRank),
            _ => nameof(BaseMediaOverviewSnapshot.TitleRank),
        };
}
