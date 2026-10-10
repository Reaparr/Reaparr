namespace Reaparr.Application;

public static partial class MediaOverviewRankBuilder
{
    public static List<MediaOverviewPhotoAlbumSnapshot> AssignRanks(
        this List<MediaOverviewPhotoAlbumSnapshot> snapshots,
        IReadOnlyDictionary<int, MediaOverviewRankValue> values
    ) => AssignRanks(snapshots, values, static x => x.PlexPhotoAlbumId);
}
